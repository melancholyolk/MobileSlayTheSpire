using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object=UnityEngine.Object;
namespace SpireResources
{
    // All APIs are main-thread only. Addressables is confined to this assembly.
    public sealed class ResourceManager : MonoBehaviour
    {
        public const int MaxAssets=256, MaxInstances=128, MaxPoolPerId=12;
        readonly HashSet<ResourceScope> scopes=new HashSet<ResourceScope>();
        readonly Dictionary<int,AssetSlot> assets=new Dictionary<int,AssetSlot>();
        ResourceCatalog catalog;
        Transform poolRoot;
        int instanceCount;
        long reservedBytes;
        public const long MaxAssetBytes=256L*1024*1024;
        public long ReservedBytes=>reservedBytes;
        public int LoadedAssetCount=>assets.Count;
        public int InstanceCount=>instanceCount;
        sealed class AssetSlot
        {
            public Type Type;
            public long Bytes;
            public int References;
            public AsyncOperationHandle Handle;
            public Task<Object> Task;
        }
        public static ResourceManager Create(GameObject owner)
        {
            var manager=owner.AddComponent<ResourceManager>();
            // The tiny bootstrap manifest is the sole Resources load in the resource backend.
            manager.catalog=Resources.Load<ResourceCatalog>("ResourceCatalog");
            if(manager.catalog==null) throw new InvalidOperationException("Build the resource catalog first.");
            manager.poolRoot=new GameObject("ResourcePool").transform;
            manager.poolRoot.SetParent(owner.transform,false); manager.poolRoot.gameObject.SetActive(false);
            return manager;
        }
        public ResourceScope CreateScope(int characterId=-1)
        {
            if(catalog==null) throw new InvalidOperationException("Resource manager is not initialized");
            var scope=new ResourceScope(this,characterId); scopes.Add(scope); return scope;
        }
        internal Task<Object> Acquire<T>(int id,int characterId) where T:Object
        {
            var entry=catalog.Find(id);
            if(entry.CharacterId>=0 && entry.CharacterId!=characterId) throw new InvalidOperationException("Resource belongs to another character: "+id);
            if(assets.TryGetValue(id,out var slot))
            {
                if(slot.Type!=typeof(T)) throw new InvalidOperationException("Resource type mismatch: "+id);
                slot.References++; return slot.Task;
            }
            if(entry.EstimatedBytes>MaxAssetBytes-reservedBytes) throw new InvalidOperationException("Asset memory budget reached");
            if(assets.Count>=MaxAssets) throw new InvalidOperationException("Asset capacity reached");
            var handle=Addressables.LoadAssetAsync<T>(entry.Guid);
            slot=new AssetSlot {Type=typeof(T),References=1,Handle=handle,Bytes=entry.EstimatedBytes};
            reservedBytes+=slot.Bytes; slot.Task=Complete(handle); assets.Add(id,slot); return slot.Task;
        }
        static async Task<Object> Complete<T>(AsyncOperationHandle<T> handle) where T:Object
        {
            var result=await handle.Task;
            if(handle.Status!=AsyncOperationStatus.Succeeded || result==null)
                throw handle.OperationException??new InvalidOperationException("Resource load failed");
            return result;
        }
        internal void Release(int id)
        {
            if(!assets.TryGetValue(id,out var slot)) return;
            if(--slot.References!=0) return;
            assets.Remove(id); reservedBytes-=slot.Bytes; if(slot.Handle.IsValid()) Addressables.Release(slot.Handle);
        }
        internal GameObject CreateInstance(GameObject prefab,Transform parent)
        {
            if(instanceCount>=MaxInstances) throw new InvalidOperationException("Instance capacity reached");
            var go=Object.Instantiate(prefab,parent,false); instanceCount++; return go;
        }
        internal void DestroyInstance(GameObject go) { if(go!=null) Object.Destroy(go); instanceCount--; }
        internal Transform PoolRoot=>poolRoot;
        internal void Forget(ResourceScope scope) { scopes.Remove(scope); }
        internal async Task NextFrame() { await Task.Yield(); await Task.Yield(); }
        static async void ReleaseAfterCompletion(AssetSlot slot)
        {
            try { await slot.Task; } catch { }
            await Task.Yield();
            if(slot.Handle.IsValid()) Addressables.Release(slot.Handle);
        }
        void OnDestroy()
        {
            foreach(var scope in scopes.ToArray()) scope.Shutdown();
            scopes.Clear();
            foreach(var slot in assets.Values) ReleaseAfterCompletion(slot);
            assets.Clear(); reservedBytes=0;
            catalog=null;
        }
    }
    public sealed class ResourceScope : IDisposable
    {
        readonly ResourceManager manager;
        readonly Dictionary<int,Task<Object>> leases=new Dictionary<int,Task<Object>>();
        readonly Dictionary<GameObject,int> instances=new Dictionary<GameObject,int>();
        readonly Dictionary<int,Stack<GameObject>> pools=new Dictionary<int,Stack<GameObject>>();
        readonly List<IDisposable> owned=new List<IDisposable>();
        public void Own(IDisposable resource)
        {
            if(disposed) { resource.Dispose(); throw new ObjectDisposedException(nameof(ResourceScope)); }
            owned.Add(resource);
        }
        void ReleaseOwned()
        {
            foreach(var resource in owned.ToArray()) { try { resource.Dispose(); } catch(Exception e) { Debug.LogException(e); } }
            owned.Clear();
        }
        bool disposed;
        Task disposal;
        public int CharacterId { get; }
        public bool IsDisposed=>disposed;
        public int AssetCount=>leases.Count;
        public int InstanceCount=>instances.Count;
        internal ResourceScope(ResourceManager manager,int characterId) { this.manager=manager; CharacterId=characterId; }
        public async Task<T> LoadAsync<T>(int id) where T:Object
        {
            if(disposed) throw new ObjectDisposedException(nameof(ResourceScope));
            if(!leases.TryGetValue(id,out var task))
            {
                task=manager.Acquire<T>(id,CharacterId); leases.Add(id,task);
            }
            Object result;
            try { result=await task; }
            catch
            {
                if(!disposed && leases.Remove(id)) manager.Release(id);
                throw;
            }
            if(disposed) throw new OperationCanceledException("Resource owner unloaded");
            if(!(result is T typed)) throw new InvalidOperationException("Resource type mismatch: "+id);
            return typed;
        }
        public async Task<GameObject> InstantiateAsync(int id,Transform parent,bool pooled=true)
        {
            var prefab=await LoadAsync<GameObject>(id);
            if(disposed) throw new OperationCanceledException();
            GameObject go=null;
            if(pooled && pools.TryGetValue(id,out var stack))
                while(stack.Count>0 && go==null) { go=stack.Pop(); if(go==null) { instances.Remove(go); manager.DestroyInstance(go); } }
            if(go==null) { go=manager.CreateInstance(prefab,parent); instances.Add(go,id); }
            else go.transform.SetParent(parent,false);
            // Caller initializes the inactive object before showing it.
            go.SetActive(false); return go;
        }
        public void Return(GameObject instance,bool pool=true)
        {
            if(instance==null || !instances.TryGetValue(instance,out var id)) return;
            if(pools.TryGetValue(id,out var existing) && existing.Contains(instance)) return;
            instance.SetActive(false);
            if(pool && !disposed)
            {
                if(!pools.TryGetValue(id,out var stack)) pools[id]=stack=new Stack<GameObject>();
                if(stack.Count<ResourceManager.MaxPoolPerId)
                {
                    instance.transform.SetParent(manager.PoolRoot,false); stack.Push(instance); return;
                }
            }
            instances.Remove(instance); manager.DestroyInstance(instance);
        }
        public Task DisposeAsync()
        {
            if(disposal!=null) return disposal;
            disposed=true; ReleaseOwned(); disposal=DisposeCore(); return disposal;
        }
        async Task DisposeCore()
        {
            foreach(var go in instances.Keys.ToArray()) manager.DestroyInstance(go);
            instances.Clear(); pools.Clear();
            // Release dependencies only after Unity's deferred instance destruction.
            await manager.NextFrame();
            await Task.WhenAll(leases.Values.Select(async task=> { try { await task; } catch { } }));
            foreach(var id in leases.Keys) manager.Release(id);
            leases.Clear(); manager.Forget(this);
        }
        public void Dispose() { _=DisposeAsync(); }
        internal void Shutdown()
        {
            disposed=true; ReleaseOwned();
            foreach(var go in instances.Keys.ToArray()) manager.DestroyInstance(go);
            instances.Clear(); pools.Clear(); leases.Clear();
        }
    }
}
