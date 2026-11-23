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
    /// <summary>
    /// 管理同一所有者的资源租约、实例池及附属对象。
    /// Dispose 启动异步清理；需要等待实例销毁及资源释放时使用 DisposeAsync。
    /// </summary>
    public sealed class ResourceScope : IDisposable
    {
        readonly ResourceManager manager;
        readonly Dictionary<int, Task<Object>> leases = new Dictionary<int, Task<Object>>();
        readonly Dictionary<GameObject, int> instances = new Dictionary<GameObject, int>();
        readonly Dictionary<int, Stack<GameObject>> pools = new Dictionary<int, Stack<GameObject>>();
        readonly List<IDisposable> owned = new List<IDisposable>();
        public void Own(IDisposable resource)
        {
            if (disposed)
            {
                resource.Dispose();
                throw new ObjectDisposedException(nameof(ResourceScope));
            }

            owned.Add(resource);
        }

        void ReleaseOwned()
        {
            foreach (var resource in owned.ToArray())
            {
                try
                {
                    resource.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            owned.Clear();
        }

        bool disposed;
        Task disposal;
        public int CharacterId { get; }
        public bool IsDisposed => disposed;
        public int AssetCount => leases.Count;
        public int InstanceCount => instances.Count;

        internal ResourceScope(ResourceManager manager, int characterId)
        {
            this.manager = manager;
            CharacterId = characterId;
        }

        public async Task<T> LoadAsync<T>(int id)
            where T : Object
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ResourceScope));
            if (!leases.TryGetValue(id, out var task))
            {
                task = manager.Acquire<T>(id, CharacterId);
                leases.Add(id, task);
            }

            Object result;
            try
            {
                result = await task;
            }
            catch
            {
                if (!disposed && leases.Remove(id))
                    manager.Release(id);
                throw;
            }

            if (disposed)
                throw new OperationCanceledException("Resource owner unloaded");
            if (!(result is T typed))
                throw new InvalidOperationException("Resource type mismatch: " + id);
            return typed;
        }

        public async Task<GameObject> InstantiateAsync(int id, Transform parent, bool pooled = true)
        {
            var prefab = await LoadAsync<GameObject>(id);
            if (disposed)
                throw new OperationCanceledException();
            GameObject go = null;
            if (pooled && pools.TryGetValue(id, out var stack))
                while (stack.Count > 0 && go == null)
                {
                    go = stack.Pop();
                    if (go == null)
                    {
                        instances.Remove(go);
                        manager.DestroyInstance(go);
                    }
                }

            if (go == null)
            {
                go = manager.CreateInstance(prefab, parent);
                instances.Add(go, id);
            }
            else
                go.transform.SetParent(parent, false);
            // Caller initializes the inactive object before showing it.
            go.SetActive(false);
            return go;
        }

        public void Return(GameObject instance, bool pool = true)
        {
            if (instance == null || !instances.TryGetValue(instance, out var id))
                return;
            if (pools.TryGetValue(id, out var existing) && existing.Contains(instance))
                return;
            instance.SetActive(false);
            if (pool && !disposed)
            {
                if (!pools.TryGetValue(id, out var stack))
                    pools[id] = stack = new Stack<GameObject>();
                if (stack.Count < ResourceManager.MaxPoolPerId)
                {
                    instance.transform.SetParent(manager.PoolRoot, false);
                    stack.Push(instance);
                    return;
                }
            }

            instances.Remove(instance);
            manager.DestroyInstance(instance);
        }

        public Task DisposeAsync()
        {
            if (disposal != null)
                return disposal;
            disposed = true;
            ReleaseOwned();
            disposal = DisposeCore();
            return disposal;
        }

        async Task DisposeCore()
        {
            foreach (var go in instances.Keys.ToArray())
                manager.DestroyInstance(go);
            instances.Clear();
            pools.Clear();
            // Release dependencies only after Unity's deferred instance destruction.
            await manager.NextFrame();
            await Task.WhenAll(leases.Values.Select(async task =>
            {
                try
                {
                    await task;
                }
                catch
                {
                }
            }));
            foreach (var id in leases.Keys)
                manager.Release(id);
            leases.Clear();
            manager.Forget(this);
        }

        public void Dispose()
        {
            _ = DisposeAsync();
        }

        internal void Shutdown()
        {
            disposed = true;
            ReleaseOwned();
            foreach (var go in instances.Keys.ToArray())
                manager.DestroyInstance(go);
            instances.Clear();
            pools.Clear();
            leases.Clear();
        }
    }
}
