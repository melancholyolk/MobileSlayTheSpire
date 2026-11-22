using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Spire;
using SpireAnimation;
using SpireResources;
using UnityEngine;
using UnityEngine.UI;
namespace SpirePresentation
{
    // Persistent hand instances; deliberately not an ILayoutController or LayoutGroup.
    public sealed class BattleHandLayout : MonoBehaviour
    {
        [SerializeField] Vector2 cardSize=new Vector2(156,180);
        [SerializeField] float spacing=8;
        readonly List<HandCardInteraction> cards=new List<HandCardInteraction>();
        readonly List<HandCardInteraction> owned=new List<HandCardInteraction>();
        GameApp app; GameplayView view; ResourceScope scope; int generation;
        HandCardInteraction displayed,hovered,dragged;
        Task refreshTask=Task.CompletedTask;
        public IReadOnlyList<HandCardInteraction> Cards=>cards;
        public bool CanInteract=>app!=null && app.State!=null && app.State.Phase==Phase.Combat && app.BattleVisuals.PendingCount==0 && app.State.Heroes[app.Player].Alive && !app.State.Heroes[app.Player].Ended && (app.Network==null || app.Network.Connected);
        public void Configure(GameApp owner,GameplayView gameplay,ResourceScope resources)
        { app=owner; view=gameplay; scope=resources; }
        public void Clear()
        {
            generation++; refreshTask=Task.CompletedTask; dragged=hovered=displayed=null; cards.Clear();
            foreach(var card in owned.ToArray()) Release(card);
        }
        void Release(HandCardInteraction card)
        {
            owned.Remove(card); if(card==null) return;
            card.Motion.Cancel(); view.Unregister(card.GetComponent<CardUIView>()); scope.Return(card.gameObject,false);
        }
        Vector2 Point(RectTransform anchor) => ((RectTransform)transform).InverseTransformPoint(anchor.TransformPoint(anchor.rect.center));
        Vector2 Source(BattleCue cue)
        {
            switch(cue.Source)
            {
                case CardOrigin.DiscardPile: return Point(view.discardAnchor);
                case CardOrigin.ExhaustPile: return Point(view.exhaustAnchor);
                case CardOrigin.PlayArea: return Point(view.playAnchor);
                case CardOrigin.Hand: return Vector2.zero;
                case CardOrigin.Actor:
                    var anchors=cue.Actor<0?view.enemyAnchors:view.heroAnchors; int index=cue.Actor<0?-1-cue.Actor:cue.Actor;
                    return index<anchors.Length?Point(anchors[index]):Vector2.zero;
                default: return Point(view.drawAnchor);
            }
        }
        async Task<HandCardInteraction> Create(string id,Vector2 position,int version)
        {
            var resourceOwner=scope;
            var go=await resourceOwner.InstantiateAsync(ResourceIds.CardView,transform,false);
            if(version!=generation) { resourceOwner.Return(go,false); throw new OperationCanceledException(); }
            var card=go.GetComponent<HandCardInteraction>()??go.AddComponent<HandCardInteraction>();
            card.Configure(this,id); owned.Add(card);
            var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=cardSize; rect.anchoredPosition=position; rect.localScale=Vector3.one*.65f; rect.localRotation=Quaternion.identity;
            var ui=go.GetComponent<CardUIView>(); view.Register(ui);
            await ui.Bind(app.State.Cards[id],resourceOwner,()=>{},true);
            if(version!=generation) throw new OperationCanceledException();
            go.SetActive(true); card.Group.alpha=1;
            return card;
        }
        public async Task<bool> Present(BattleCue cue)
        {
            if(cue.Actor!=app.Player || string.IsNullOrEmpty(cue.CardId)) return false;
            int version=generation;
            HandCardInteraction card;
            if(cue.Kind==BattleCueKind.Draw)
            {
                card=await Create(cue.CardId,Source(cue),version);
                cards.Add(card); Arrange(.30f); await card.Transition;
            }
            else if(cue.Kind==BattleCueKind.Play || cue.Kind==BattleCueKind.Discard || cue.Kind==BattleCueKind.Exhaust)
            {
                bool fromHand=cue.Source==CardOrigin.Automatic || cue.Source==CardOrigin.Hand;
                card=cue.Source==CardOrigin.PlayArea?displayed:null;
                if(fromHand)
                {
                    int index=cue.HandIndex;
                    card=index>=0 && index<cards.Count?cards[index]:cards.FirstOrDefault(c=>c.CardId==cue.CardId);
                    if(card!=null) cards.Remove(card);
                }
                if(card==null) card=await Create(cue.CardId,Source(cue),version);
                if(hovered==card) hovered=null; if(dragged==card) dragged=null;
                card.Locked=true; card.Group.blocksRaycasts=false; card.transform.SetAsLastSibling(); Arrange(.18f);
                if(cue.Kind==BattleCueKind.Play)
                {
                    displayed=card;
                    await Move(card,Point(view.playAnchor),0,1.35f,.28f);
                    await Task.Delay(400);
                    if(version!=generation) throw new OperationCanceledException();
                    // Keep this exact instance for its following discard/exhaust event.
                }
                else
                {
                    bool exhaust=cue.Kind==BattleCueKind.Exhaust;
                    await Move(card,Point(exhaust?view.exhaustAnchor:view.discardAnchor),exhaust?100:15,exhaust?.03f:.5f,exhaust?.42f:.30f,0);
                    if(version!=generation) throw new OperationCanceledException();
                    if(displayed==card) displayed=null; Release(card);
                }
            }
            else return false;
            if(version!=generation) throw new OperationCanceledException();
            return true;
        }
        public Task Refresh()
        {
            if(!refreshTask.IsCompleted) return refreshTask;
            return refreshTask=RefreshAsync();
        }
        async Task RefreshAsync()
        {
            int version=generation;
            while(app.BattleVisuals.PendingCount>0) { await Task.Yield(); if(version!=generation) return; }
            // Initial/reconnected snapshots have no draw history. Reconcile only after all cues.
            var ids=app.State?.Phase==Phase.Combat?app.State.Heroes[app.Player].Hand.ToArray():Array.Empty<string>();
            if(!cards.Select(c=>c.CardId).SequenceEqual(ids))
            {
                var available=new List<HandCardInteraction>(cards); cards.Clear();
                foreach(var id in ids)
                {
                    var card=available.FirstOrDefault(c=>c.CardId==id);
                    if(card!=null) available.Remove(card); else card=await Create(id,Point(view.drawAnchor),version);
                    cards.Add(card);
                }
                foreach(var card in available) Release(card);
                Arrange(.25f);
                await Task.WhenAll(cards.Select(c=>c.Transition));
            }
        }
        Vector2 Slot(int index)
        {
            float width=((RectTransform)transform).rect.width;
            float step=cards.Count<2?0:Mathf.Min(cardSize.x+spacing,Mathf.Max(0,width-cardSize.x)/(cards.Count-1));
            float t=cards.Count<2?0:(index-(cards.Count-1)*.5f)/Mathf.Max(1,(cards.Count-1)*.5f);
            return new Vector2((index-(cards.Count-1)*.5f)*step,8-24*t*t);
        }
        float Angle(int index)=>cards.Count<2?0:-(index-(cards.Count-1)*.5f)*Mathf.Min(5,24f/(cards.Count-1));
        Task Move(HandCardInteraction card,Vector2 to,float angle,float scale,float duration,float alpha=1)
        {
            var rt=(RectTransform)card.transform; var current=rt.anchoredPosition;
            var size=rt.localScale.x; float rotation=Mathf.DeltaAngle(0,rt.localEulerAngles.z);
            card.Transition=card.Motion.Play(new UiMotion {Duration=duration,FromAlpha=card.Group.alpha,ToAlpha=alpha,ToOffset=to-current,FromScale=1,ToScale=scale/Mathf.Max(.001f,size),FromAngle=rotation,ToAngle=angle});
            return card.Transition;
        }
        public void Arrange(float duration=.16f)
        {
            for(int i=0;i<cards.Count;i++)
            {
                var card=cards[i]; card.gameObject.name="Card_"+i;
                if(card==dragged) continue;
                bool focus=card==hovered;
                var position=Slot(i); if(focus) position.y+=65;
                else if(hovered!=null) position.x+=i<cards.IndexOf(hovered)?-20:20;
                card.transform.SetSiblingIndex(i);
                _=Move(card,position,focus?0:Angle(i),focus?1.35f:1,duration);
            }
            if(hovered!=null) hovered.transform.SetAsLastSibling();
            if(dragged!=null) dragged.transform.SetAsLastSibling();
            if(displayed!=null) displayed.transform.SetAsLastSibling();
        }
        public void Hover(HandCardInteraction card,bool enter)
        { if(!CanInteract || dragged!=null) return; if(enter) hovered=card; else if(hovered==card) hovered=null; Arrange(); }
        public bool BeginDrag(HandCardInteraction card)
        {
            if(!CanInteract || !cards.Contains(card)) return false;
            dragged=card; hovered=null; card.Motion.Cancel(); card.transform.SetAsLastSibling(); card.Group.blocksRaycasts=false;
            card.transform.localRotation=Quaternion.identity; card.transform.localScale=Vector3.one*1.15f; return true;
        }
        public void Drag(HandCardInteraction card,Vector2 screen,Camera camera)
        {
            if(dragged!=card) return;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,screen,camera,out var local)) ((RectTransform)card.transform).anchoredPosition=local;
        }
        public void EndDrag(HandCardInteraction card)
        {
            if(dragged!=card) return;
            dragged=null; card.Group.blocksRaycasts=true;
            float y=((RectTransform)card.transform).anchoredPosition.y;
            if(y>((RectTransform)transform).rect.height*.5f+35) Play(card); else Arrange(.22f);
        }
        public void Play(HandCardInteraction card)
        {
            int index=cards.IndexOf(card);
            if(!CanInteract || index<0 || app.State.Heroes[app.Player].Energy<app.State.Cards[card.CardId].Cost) { Arrange(.22f); return; }
            hovered=null; app.Send("play",index);
            if(app.BattleVisuals.PendingCount==0) Arrange(.22f);
        }
        void OnRectTransformDimensionsChange() { if(app!=null && dragged==null) Arrange(); }
        void OnDestroy() { Clear(); }
    }
}
