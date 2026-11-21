using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
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
        readonly Dictionary<string,Slot> slots=new Dictionary<string,Slot>();
        Transform root;
        bool disposed;
        public int Count=>slots.Count;
        public void Configure(Transform parent) { root=parent!=null?parent:transform; }
        public Task<T> OpenAsync<T>(string resourcePath) where T:BaseUIView
        {
            if(disposed) return Task.FromException<T>(new ObjectDisposedException(nameof(UiManager)));
            if(string.IsNullOrWhiteSpace(resourcePath)) return Task.FromException<T>(new ArgumentException("A prefab Resources path is required"));
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
        async void Load<T>(string path,Slot slot) where T:BaseUIView
        {
            GameObject instance=null;
            try
            {
                var request=Resources.LoadAsync<GameObject>(path);
                var ready=new TaskCompletionSource<bool>();
                if(request.isDone) ready.TrySetResult(true);
                else request.completed+=_=>ready.TrySetResult(true);
                await ready.Task;
                if(disposed || slot.Closed) { slot.Completion.TrySetCanceled(); return; }
                var prefab=request.asset as GameObject;
                if(prefab==null) throw new InvalidOperationException("UI prefab not found: "+path);
                if(prefab.GetComponent<T>()==null) throw new InvalidOperationException("Prefab has no "+typeof(T).Name+": "+path);
                instance=Instantiate(prefab,root!=null?root:transform,false);
                instance.SetActive(false);
                var view=instance.GetComponent<T>(); slot.View=view; view.Initialize(view);
                if(slot.Visible) view.OnShow();
                slot.Completion.TrySetResult(view);
            }
            catch(Exception error)
            {
                if(instance!=null) Destroy(instance);
                if(slots.TryGetValue(path,out var current) && ReferenceEquals(current,slot)) slots.Remove(path);
                slot.Completion.TrySetException(error);
            }
        }
        public T Get<T>(string path) where T:BaseUIView => slots.TryGetValue(path,out var slot)?slot.View as T:null;
        public void Show(string path)
        {
            if(!slots.TryGetValue(path,out var slot)) return;
            slot.Visible=true; if(slot.View!=null) slot.View.OnShow();
        }
        public void Hide(string path)
        {
            if(!slots.TryGetValue(path,out var slot)) return;
            slot.Visible=false; if(slot.View!=null) slot.View.OnHide();
        }
        public void Close(string path)
        {
            if(!slots.TryGetValue(path,out var slot)) return;
            slots.Remove(path); slot.Closed=true; slot.Completion.TrySetCanceled();
            if(slot.View!=null) { slot.View.Release(); Destroy(slot.View.gameObject); }
        }
        void OnDestroy()
        {
            disposed=true;
            foreach(var path in new List<string>(slots.Keys)) Close(path);
        }
    }
}
