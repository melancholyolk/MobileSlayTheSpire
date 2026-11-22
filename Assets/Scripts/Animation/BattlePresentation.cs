using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Spire;
using SpireResources;
using UnityEngine;
using UnityEngine.UI;
namespace SpireAnimation
{
    [Serializable] public sealed class ActorVisual { public int Owner,Prefab; }
    [Serializable] public sealed class ActorVisualCatalog { public ActorVisual[] Actors; }
    public readonly struct CardProcessSignal
    {
        public readonly long Ticket; public readonly BattleCue Cue; public readonly PresentationPhase Phase;
        public CardProcessSignal(long ticket,BattleCue cue,PresentationPhase phase) { Ticket=ticket; Cue=cue; Phase=phase; }
    }
    public sealed class BattlePresentation : MonoBehaviour
    {
        sealed class Actor { public ResourceScope Scope; public SkeletonView View; }
        sealed class Job { public long Id; public BattleCue Cue; }
        public event Action<CardProcessSignal> Lifecycle;
        public const int MaxQueuedProcesses=128;
        readonly Dictionary<int,Actor> actors=new Dictionary<int,Actor>();
        readonly Queue<Job> pending=new Queue<Job>();
        ResourceManager resources; ResourceScope shared; ActorVisualCatalog catalog; BodyCatalog bodyCatalog;
        RectTransform stage; Run observed; int room=-1,revision=-1,generation; long ticket;
        Job active; GameObject ghost; ResourceScope ghostOwner; Coroutine routine;
        Task ready=Task.CompletedTask;
        public Task Ready=>ready;
        public int PendingCount=>pending.Count+(active==null?0:1);
        public int ActorCount=>actors.Count;
        public void Configure(ResourceManager manager,RectTransform root) { resources=manager; stage=root; shared=manager.CreateScope(); }
        RectTransform[] heroAnchors,enemyAnchors;
        RectTransform handAnchor,drawAnchor,discardAnchor,exhaustAnchor,playAnchor;
        public void ConfigureLayout(RectTransform[] heroes,RectTransform[] enemies,RectTransform hand,RectTransform draw,RectTransform discard,RectTransform exhaust,RectTransform play)
        { heroAnchors=heroes; enemyAnchors=enemies; handAnchor=hand; drawAnchor=draw; discardAnchor=discard; exhaustAnchor=exhaust; playAnchor=play; }
        Vector2 EffectPosition(RectTransform anchor) => anchor==null?Vector2.zero:(Vector2)stage.InverseTransformPoint(anchor.TransformPoint(anchor.rect.center));
        public void Observe(Run run,int localPlayer)
        {
            if(run==null) { Clear(); return; }
            if(run!=observed || room!=run.Current)
            {
                Clear(); observed=run; room=run.Current;
                if(run.Enemies.Count>0 && (run.Phase==Phase.Combat||run.Phase==Phase.Reward||run.Phase==Phase.Lost||run.Phase==Phase.Won)) ready=LoadActors(run,++generation);
            }
            if(run.Phase==Phase.Map || run.Phase==Phase.Camp || run.Phase==Phase.Shop || run.Phase==Phase.Event) { stage.gameObject.SetActive(false); return; }
            stage.gameObject.SetActive(true);
            if(revision==run.Revision) return; revision=run.Revision;
            foreach(var cue in run.Cues)
            {
                // Card movements of remote hands are represented too, without exposing private decision logic.
                var job=new Job {Id=++ticket,Cue=cue}; Signal(job,PresentationPhase.Queued);
                if(pending.Count>=MaxQueuedProcesses) Signal(job,PresentationPhase.Cancelled); else pending.Enqueue(job);
            }
            if(routine==null) routine=StartCoroutine(Process());
        }
        async Task LoadActors(Run run,int version)
        {
            if(catalog==null) catalog=JsonUtility.FromJson<ActorVisualCatalog>((await shared.LoadAsync<TextAsset>(21)).text);
            if(bodyCatalog==null)
            {
                var parts=await shared.LoadAsync<TextAsset>(23);var ports=await shared.LoadAsync<TextAsset>(24);
                var nodes=await shared.LoadAsync<TextAsset>(25);var motions=await shared.LoadAsync<TextAsset>(26);
                bodyCatalog=BodyCatalog.Read(parts.bytes,ports.bytes,nodes.bytes,motions.bytes);
            }
            if(version!=generation) return;
            for(int i=0;i<run.Heroes.Count+run.Enemies.Count;i++)
            {
                bool hero=i<run.Heroes.Count; int seat=hero?i:-1-(i-run.Heroes.Count);
                int owner=hero?(int)run.Heroes[i].Class:1000+run.Enemies[-1-seat].VisualId;
                var config=catalog.Actors.FirstOrDefault(a=>a.Owner==owner); if(config==null) continue;
                var scope=resources.CreateScope(owner); var actor=new Actor {Scope=scope};
                actors[seat]=actor;
                try
                {
                    var anchors=hero?heroAnchors:enemyAnchors;
                    int slot=hero?seat:-1-seat;
                    var parent=anchors!=null && slot<anchors.Length?anchors[slot]:stage;
                    var go=await scope.InstantiateAsync(config.Prefab,parent,false);
                    if(version!=generation || scope.IsDisposed) { scope.Dispose(); return; }
                    if(go.GetComponent<SkinnedActorView>()==null)
                    {
                        var body=go.GetComponent<BodyAssemblyView>()??go.AddComponent<BodyAssemblyView>();
                        await body.ConfigureAsync(bodyCatalog,owner,scope);
                    }
                    if(version!=generation || scope.IsDisposed) { scope.Dispose(); return; }
                    var rt=(RectTransform)go.transform; rt.anchorMin=rt.anchorMax=parent.pivot; rt.anchoredPosition=Vector2.zero;
                    rt.localScale=Vector3.one; actor.View=go.GetComponent<SkeletonView>(); go.SetActive(true);
                    if(hero) actor.View.Play(MotionId.Spawn);
                }
                catch(OperationCanceledException) { return; }
                catch(ObjectDisposedException) { return; }
            }
        }
        void Signal(Job job,PresentationPhase phase)
        {
            if(Lifecycle==null) return; var signal=new CardProcessSignal(job.Id,job.Cue,phase);
            foreach(Action<CardProcessSignal> callback in Lifecycle.GetInvocationList()) try { callback(signal); } catch(Exception e) { Debug.LogException(e); }
        }
        IEnumerator Process()
        {
            while(!ready.IsCompleted) yield return null;
            if(ready.IsFaulted) { Debug.LogException(ready.Exception); Clear(); yield break; }
            while(pending.Count>0)
            {
                active=pending.Dequeue(); var job=active; Signal(job,PresentationPhase.Started);
                var cue=job.Cue; actors.TryGetValue(cue.Actor,out var actor);
                bool card=cue.Kind==BattleCueKind.Draw || cue.Kind==BattleCueKind.Shuffle || cue.Kind==BattleCueKind.Play || cue.Kind==BattleCueKind.Discard || cue.Kind==BattleCueKind.Exhaust;
                float duration=card?.11f:.24f;
                if(!card && actor?.View!=null)
                {
                    var motion=cue.Kind==BattleCueKind.Attack?MotionId.Attack:cue.Kind==BattleCueKind.Death?MotionId.Death:cue.Kind==BattleCueKind.Hit?MotionId.Hit:cue.Kind==BattleCueKind.Guard?MotionId.Guard:cue.Kind==BattleCueKind.Victory?MotionId.Victory:MotionId.Skill;
                    if(actor.View.Motions.Any(m=>m.Id==motion)) actor.View.Play(motion,.08f);
                    duration=motion==MotionId.Death?.8f:.36f;
                }
                Task<PresentationPhase> animation=null;
                if(card && actor!=null)
                {
                    var load=actor.Scope.InstantiateAsync(22,stage); while(!load.IsCompleted) yield return null;
                    if(load.IsFaulted || load.IsCanceled) { Signal(job,PresentationPhase.Cancelled); active=null; continue; }
                    ghost=load.Result; ghostOwner=actor.Scope; var rect=(RectTransform)ghost.transform;
                    rect.anchorMin=rect.anchorMax=stage.pivot; rect.anchoredPosition=EffectPosition(handAnchor); rect.localScale=Vector3.one;
                    var image=ghost.GetComponent<Image>(); image.sprite=null; image.color=cue.Kind==BattleCueKind.Shuffle?new Color(.55f,.38f,.20f):Color.white;
                    if(cue.ArtId>0)
                    {
                        var sprite=actor.Scope.LoadAsync<Sprite>(cue.ArtId); while(!sprite.IsCompleted) yield return null;
                        if(sprite.IsCompletedSuccessfully) image.sprite=sprite.Result;
                    }
                    ghost.SetActive(true);
                    var source=cue.Kind==BattleCueKind.Draw?drawAnchor:cue.Kind==BattleCueKind.Shuffle?discardAnchor:handAnchor;
                    var target=cue.Kind==BattleCueKind.Play?playAnchor:cue.Kind==BattleCueKind.Draw?handAnchor:cue.Kind==BattleCueKind.Shuffle?drawAnchor:cue.Kind==BattleCueKind.Exhaust?exhaustAnchor:discardAnchor;
                    Vector2 from=EffectPosition(source)-rect.anchoredPosition;
                    Vector2 to=EffectPosition(target)-rect.anchoredPosition;
                    animation=ghost.GetComponent<BetterAnimation>().Play(new UiMotion {Duration=duration,FromAlpha=.5f,ToAlpha=cue.Kind==BattleCueKind.Draw?1:0,FromOffset=from,ToOffset=to,FromScale=.7f,ToScale=cue.Kind==BattleCueKind.Exhaust?.05f:.85f,FromAngle=-12,ToAngle=cue.Kind==BattleCueKind.Shuffle?180:10});
                }
                float elapsed=0; bool impact=false;
                while(elapsed<duration || (animation!=null && !animation.IsCompleted))
                {
                    elapsed+=Time.unscaledDeltaTime;
                    if(!impact && elapsed>=duration*.45f) { impact=true; Signal(job,PresentationPhase.Impact); }
                    yield return null;
                }
                ReleaseGhost(); Signal(job,PresentationPhase.Completed); active=null;
            }
            routine=null;
        }
        void ReleaseGhost() { if(ghost!=null) { ghost.GetComponent<BetterAnimation>().Cancel(); ghostOwner?.Return(ghost); } ghost=null; ghostOwner=null; }
        public void Clear()
        {
            if(stage!=null) stage.gameObject.SetActive(false);
            generation++; if(routine!=null) StopCoroutine(routine); routine=null;
            if(active!=null) Signal(active,PresentationPhase.Cancelled); active=null;
            while(pending.Count>0) Signal(pending.Dequeue(),PresentationPhase.Cancelled);
            ReleaseGhost(); foreach(var actor in actors.Values) actor.Scope.Dispose(); actors.Clear();
            observed=null; room=revision=-1; ready=Task.CompletedTask;
        }
        void OnDestroy() { Clear(); shared?.Dispose(); Lifecycle=null; }
    }
}
