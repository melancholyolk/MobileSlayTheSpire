using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SpireAnimation;
using UnityEngine;
namespace SpireUI
{
    [RequireComponent(typeof(CanvasGroup),typeof(BetterAnimation))]
    public abstract class BaseUIView : MonoBehaviour, IUiElement
    {
        [SerializeField] UiMotion openAnimation=new UiMotion();
        [SerializeField] UiMotion closeAnimation=new UiMotion {FromAlpha=1,ToAlpha=0};
        [SerializeField] AnimationClip openClip,closeClip;
        readonly List<IUiElement> elements=new List<IUiElement>();
        bool initialized,released; int transitionVersion;
        BetterAnimation animationDriver;
        CanvasGroup canvasGroup;
        public bool IsVisible { get; private set; }
        public Task Transition {get; private set;}=Task.CompletedTask;
        public IReadOnlyList<IUiElement> Elements=>elements;
        public void Initialize(BaseUIView owner)
        {
            if(initialized) return; initialized=true;
            canvasGroup=GetComponent<CanvasGroup>(); if(canvasGroup==null) canvasGroup=gameObject.AddComponent<CanvasGroup>();
            animationDriver=GetComponent<BetterAnimation>(); if(animationDriver==null) animationDriver=gameObject.AddComponent<BetterAnimation>();
            foreach(var component in GetComponentsInChildren<MonoBehaviour>(true))
                if(component!=this && component is IUiElement element) Register(element);
            OnInitialize();
        }
        public void Register(IUiElement element)
        {
            if(element==null || ReferenceEquals(element,this) || elements.Contains(element)) return;
            if(released) throw new ObjectDisposedException(name);
            elements.Add(element); element.Initialize(this); if(IsVisible) element.OnShow();
        }
        public void Unregister(IUiElement element)
        {
            if(!elements.Remove(element)) return;
            if(IsVisible) element.OnHide(); element.Release();
        }
        protected virtual void OnInitialize() { }
        public void OnShow()
        {
            if(released) throw new ObjectDisposedException(name);
            Initialize(this); if(IsVisible) return;
            transitionVersion++; IsVisible=true; gameObject.SetActive(true);
            canvasGroup.blocksRaycasts=canvasGroup.interactable=true;
            foreach(var element in elements.ToArray()) element.OnShow(); OnShown();
            Transition=openClip!=null?animationDriver.PlayClip(openClip):animationDriver.Play(openAnimation??new UiMotion());
        }
        public void OnHide() { _=HideAsync(); }
        public Task HideAsync()
        {
            if(!IsVisible) return Transition;
            IsVisible=false; int version=++transitionVersion;
            canvasGroup.blocksRaycasts=canvasGroup.interactable=false;
            OnHidden(); foreach(var element in elements.ToArray()) element.OnHide();
            Transition=FinishHide(version); return Transition;
        }
        async Task FinishHide(int version)
        {
            if(gameObject.activeInHierarchy)
            {
                if(closeClip!=null) await animationDriver.PlayClip(closeClip);
                else
                {
                    var motion=closeAnimation??new UiMotion {FromAlpha=1,ToAlpha=0};
                    await animationDriver.Play(new UiMotion {Duration=motion.Duration,FromAlpha=canvasGroup.alpha,ToAlpha=motion.ToAlpha,FromOffset=motion.FromOffset,ToOffset=motion.ToOffset,FromScale=motion.FromScale,ToScale=motion.ToScale,FromAngle=motion.FromAngle,ToAngle=motion.ToAngle});
                }
            }
            if(this!=null && version==transitionVersion && !IsVisible) gameObject.SetActive(false);
        }
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
        protected virtual void OnReleased() { }
        public void Release()
        {
            if(released) return;
            released=true; transitionVersion++;
            if(IsVisible) { IsVisible=false; OnHidden(); foreach(var element in elements.ToArray()) element.OnHide(); }
            if(animationDriver!=null) animationDriver.Cancel();
            OnReleased(); foreach(var element in elements.ToArray()) element.Release(); elements.Clear();
        }
        protected virtual void OnDestroy() { Release(); }
    }
}
