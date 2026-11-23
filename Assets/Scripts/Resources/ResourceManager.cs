using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace SpireResources
{
    // All APIs are main-thread only. Addressables is confined to this assembly.
    public sealed class ResourceManager : MonoBehaviour
    {
        public const int MaxAssets = 256, MaxInstances = 128, MaxPoolPerId = 12;
        readonly HashSet<ResourceScope> scopes = new HashSet<ResourceScope>();
        readonly Dictionary<int, AssetSlot> assets = new Dictionary<int, AssetSlot>();
        ResourceCatalog catalog;
        Transform poolRoot;
        int instanceCount;
        long reservedBytes;
        public const long MaxAssetBytes = 256L * 1024 * 1024;
        public long ReservedBytes => reservedBytes;
        public int LoadedAssetCount => assets.Count;
        public int InstanceCount => instanceCount;

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
            var manager = owner.AddComponent<ResourceManager>();
            // The tiny bootstrap manifest is the sole Resources load in the resource backend.
            manager.catalog = Resources.Load<ResourceCatalog>("ResourceCatalog");
            if (manager.catalog == null)
                throw new InvalidOperationException("Build the resource catalog first.");
            manager.poolRoot = new GameObject("ResourcePool").transform;
            manager.poolRoot.SetParent(owner.transform, false);
            manager.poolRoot.gameObject.SetActive(false);
            return manager;
        }

        public ResourceScope CreateScope(int characterId = -1)
        {
            if (catalog == null)
                throw new InvalidOperationException("Resource manager is not initialized");
            var scope = new ResourceScope(this, characterId);
            scopes.Add(scope);
            return scope;
        }

        internal Task<Object> Acquire<T>(int id, int characterId)
            where T : Object
        {
            var entry = catalog.Find(id);
            if (entry.CharacterId >= 0 && entry.CharacterId != characterId)
                throw new InvalidOperationException("Resource belongs to another character: " + id);
            if (assets.TryGetValue(id, out var slot))
            {
                if (slot.Type != typeof(T))
                    throw new InvalidOperationException("Resource type mismatch: " + id);
                slot.References++;
                return slot.Task;
            }

            if (entry.EstimatedBytes > MaxAssetBytes - reservedBytes)
                throw new InvalidOperationException("Asset memory budget reached");
            if (assets.Count >= MaxAssets)
                throw new InvalidOperationException("Asset capacity reached");
            var handle = Addressables.LoadAssetAsync<T>(entry.Guid);
            slot = new AssetSlot
            {
                Type = typeof(T),
                References = 1,
                Handle = handle,
                Bytes = entry.EstimatedBytes
            };
            reservedBytes += slot.Bytes;
            slot.Task = Complete(handle);
            assets.Add(id, slot);
            return slot.Task;
        }

        static async Task<Object> Complete<T>(AsyncOperationHandle<T> handle)
            where T : Object
        {
            var result = await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded || result == null)
                throw handle.OperationException ?? new InvalidOperationException("Resource load failed");
            return result;
        }

        internal void Release(int id)
        {
            if (!assets.TryGetValue(id, out var slot))
                return;
            if (--slot.References != 0)
                return;
            assets.Remove(id);
            reservedBytes -= slot.Bytes;
            if (slot.Handle.IsValid())
                Addressables.Release(slot.Handle);
        }

        internal GameObject CreateInstance(GameObject prefab, Transform parent)
        {
            if (instanceCount >= MaxInstances)
                throw new InvalidOperationException("Instance capacity reached");
            var go = Object.Instantiate(prefab, parent, false);
            instanceCount++;
            return go;
        }

        internal void DestroyInstance(GameObject go)
        {
            if (go != null)
                Object.Destroy(go);
            instanceCount--;
        }

        internal Transform PoolRoot => poolRoot;

        internal void Forget(ResourceScope scope)
        {
            scopes.Remove(scope);
        }

        internal async Task NextFrame()
        {
            await Task.Yield();
            await Task.Yield();
        }

        static async void ReleaseAfterCompletion(AssetSlot slot)
        {
            try
            {
                await slot.Task;
            }
            catch
            {
            }

            await Task.Yield();
            if (slot.Handle.IsValid())
                Addressables.Release(slot.Handle);
        }

        void OnDestroy()
        {
            foreach (var scope in scopes.ToArray())
                scope.Shutdown();
            scopes.Clear();
            foreach (var slot in assets.Values)
                ReleaseAfterCompletion(slot);
            assets.Clear();
            reservedBytes = 0;
            catalog = null;
        }
    }
}
