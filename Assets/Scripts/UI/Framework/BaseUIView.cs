using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpireUI
{
    public abstract class BaseUIView : MonoBehaviour, IUiElement
    {
        readonly List<IUiElement> elements=new List<IUiElement>();
        bool initialized, released;
        public bool IsVisible { get; private set; }
        public IReadOnlyList<IUiElement> Elements=>elements;
        public void Initialize(BaseUIView owner)
        {
            if(initialized) return;
            initialized=true;
            foreach(var component in GetComponentsInChildren<MonoBehaviour>(true))
                if(component!=this && component is IUiElement element) Register(element);
            OnInitialize();
        }
        public void Register(IUiElement element)
        {
            if(element==null || ReferenceEquals(element,this) || elements.Contains(element)) return;
            if(released) throw new ObjectDisposedException(name);
            elements.Add(element); element.Initialize(this);
            if(IsVisible) element.OnShow();
        }
        public void Unregister(IUiElement element)
        {
            if(!elements.Remove(element)) return;
            if(IsVisible) element.OnHide();
            element.Release();
        }
        protected virtual void OnInitialize() { }
        public void OnShow()
        {
            if(released) throw new ObjectDisposedException(name);
            Initialize(this);
            if(IsVisible) return;
            IsVisible=true; gameObject.SetActive(true);
            foreach(var element in elements) element.OnShow();
            OnShown();
        }
        public void OnHide()
        {
            if(!IsVisible) return;
            IsVisible=false; OnHidden();
            foreach(var element in elements) element.OnHide();
            gameObject.SetActive(false);
        }
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
        protected virtual void OnReleased() { }
        public void Release()
        {
            if(released) return;
            OnHide(); released=true; OnReleased();
            foreach(var element in elements.ToArray()) element.Release();
            elements.Clear();
        }
        protected virtual void OnDestroy() { Release(); }
    }
}
