using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using SpireResources;
namespace SpireUI
{
    // Main-thread API. One cached instance per prefab path; callers share a single in-flight request.
    public sealed class UiManager : MonoBehaviour
    {
        sealed class Slot
        {
            public Type Type;
            public bool Visible=true;
            public bool Closed;
            public BaseUIView View;
            public TaskCompletionSource<BaseUIView> Completion=new TaskCompletionSource<BaseUIView>();
        }
        readonly Dictionary<int,Slot> slots=new Dictionary<int,Slot>();
        Transform root;
        ResourceScope resources;
        bool disposed;
        public int Count=>slots.Count;
        public void Configure(Transform parent, ResourceScope scope) { root=parent!=null?parent:transform; resources=scope; }
        public Task<T> OpenAsync<T>(int resourcePath) where T:BaseUIView
        {
            if(disposed) return Task.FromException<T>(new ObjectDisposedException(nameof(UiManager)));
            if(resourcePath<=0) return Task.FromException<T>(new ArgumentException("A positive prefab resource ID is required"));
            if(slots.TryGetValue(resourcePath,out var existing))
            {
                if(existing.Type!=typeof(T)) return Task.FromException<T>(new InvalidOperationException("View type mismatch: "+resourcePath));
                existing.Visible=true;
                if(existing.View!=null) existing.View.OnShow();
                return Cast<T>(existing.Completion.Task);
            }
            var slot=new Slot { Type=typeof(T) }; slots.Add(resourcePath,slot);
            Load<T>(resourcePath,slot);
            return Cast<T>(slot.Completion.Task);
        }
        static async Task<T> Cast<T>(Task<BaseUIView> task) where T:BaseUIView => (T)await task;
        async void Load<T>(int path,Slot slot) where T:BaseUIView
        {
            GameObject instance=null;
            try
            {
                instance=await resources.InstantiateAsync(path,root!=null?root:transform,false);
                if(disposed || slot.Closed) { resources.Return(instance,false); slot.Completion.TrySetCanceled(); return; }
                if(instance.GetComponent<T>()==null) throw new InvalidOperationException("Prefab has no "+typeof(T).Name+": "+path);
                var view=instance.GetComponent<T>(); slot.View=view; view.Initialize(view);
                if(slot.Visible) view.OnShow();
                slot.Completion.TrySetResult(view);
            }
            catch(Exception error)
            {
                if(instance!=null) resources.Return(instance,false);
                if(slots.TryGetValue(path,out var current) && ReferenceEquals(current,slot)) slots.Remove(path);
                slot.Completion.TrySetException(error);
            }
        }
        public T Get<T>(int path) where T:BaseUIView => slots.TryGetValue(path,out var slot)?slot.View as T:null;
        public void Show(int path)
        {
            if(!slots.TryGetValue(path,out var slot)) return;
            slot.Visible=true; if(slot.View!=null) slot.View.OnShow();
        }
        public void Hide(int path)
        {
            if(!slots.TryGetValue(path,out var slot)) return;
            slot.Visible=false; if(slot.View!=null) slot.View.OnHide();
        }
        public void Close(int path)
        {
            if(!slots.TryGetValue(path,out var slot)) return;
            slots.Remove(path); slot.Closed=true; slot.Completion.TrySetCanceled();
            if(slot.View!=null) { slot.View.Release(); resources.Return(slot.View.gameObject,false); }
        }
        void OnDestroy()
        {
            disposed=true;
            foreach(var path in new List<int>(slots.Keys)) Close(path);
        }
    }
}
