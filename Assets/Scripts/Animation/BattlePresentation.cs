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
        sealed class Actor { public ResourceScope Scope; public SkeletonView View; public CombatBody Body; public ActorExpressionView Expression; }
        sealed class Job { public long Id; public BattleCue Cue; }
        public event Action<CardProcessSignal> Lifecycle;
        // The presentation layer may consume local card events using persistent hand instances.
        public Func<BattleCue,Task<bool>> CardPresenter;
        public Func<bool> IsCombatAuthority;
        public Action<int,int> AttackResolved;
        public const int MaxQueuedProcesses=128; // Initial capacity, never discard a queued card play.
        readonly Dictionary<int,Actor> actors=new Dictionary<int,Actor>();
        readonly Queue<Job> pending=new Queue<Job>(MaxQueuedProcesses);
        readonly Dictionary<Job,Task<bool>> discarding=new Dictionary<Job,Task<bool>>();
        int localSeat;
        readonly Dictionary<int,ResourceScope> cardScopes=new Dictionary<int,ResourceScope>();
        ResourceManager resources; ResourceScope shared; ActorVisualCatalog catalog; BodyCatalog bodyCatalog;
        RectTransform stage; Run observed; int room=-1,revision=-1,generation; long ticket;
        Job active; GameObject ghost; ResourceScope ghostOwner; Coroutine routine;
        Task ready=Task.CompletedTask;
        public Task Ready=>ready;
        public int PendingCount=>pending.Count+discarding.Count+(active==null?0:1);
        public int ActorCount=>actors.Count;
        public void Configure(ResourceManager manager,RectTransform root) { resources=manager; stage=root; shared=manager.CreateScope(); }
        RectTransform[] heroAnchors,enemyAnchors;
        RectTransform handAnchor,drawAnchor,discardAnchor,exhaustAnchor,playAnchor;
        public void ConfigureLayout(RectTransform[] heroes,RectTransform[] enemies,RectTransform hand,RectTransform draw,RectTransform discard,RectTransform exhaust,RectTransform play)
        { heroAnchors=heroes; enemyAnchors=enemies; handAnchor=hand; drawAnchor=draw; discardAnchor=discard; exhaustAnchor=exhaust; playAnchor=play; }
        Vector2 EffectPosition(RectTransform anchor) => anchor==null?Vector2.zero:(Vector2)stage.InverseTransformPoint(anchor.TransformPoint(anchor.rect.center));
        public void Observe(Run run,int localPlayer)
        {
            localSeat=localPlayer;
            if(run==null) { Clear(); return; }
            run.UsePhysicalAttacks=true;
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
                if(cue.BodyPart>=0 && actors.TryGetValue(cue.Actor,out var victim) && victim.Body!=null)
                {
                    actors.TryGetValue(cue.Target,out var source);
                    victim.Body.React(cue.BodyPart,source?.Body!=null?source.Body.Profile(cue.CardId):AttackProfile.Default,cue.Target>=0?1:-1);
                }
                // Card movements of remote hands are represented too, without exposing private decision logic.
                Queue(cue);
            }
            if(routine==null) routine=StartCoroutine(Process());
        }
        void Queue(BattleCue cue)
        {
            var job=new Job {Id=++ticket,Cue=cue}; pending.Enqueue(job); Signal(job,PresentationPhase.Queued);
        }
        // Effect systems use this for retrieval/generated plays without inventing a gameplay action.
        public void Enqueue(BattleCue cue)
        {
            if(resources==null || stage==null) throw new InvalidOperationException("Configure presentation first");
            stage.gameObject.SetActive(true); Queue(cue);
            if(routine==null) routine=StartCoroutine(Process());
        }
        public Vector2 SourcePosition(BattleCue cue)
        {
            var source=cue.Source;
            if(source==CardOrigin.Automatic)
                source=cue.Kind==BattleCueKind.Draw?CardOrigin.DrawPile:cue.Kind==BattleCueKind.Shuffle?CardOrigin.DiscardPile:CardOrigin.Hand;
            RectTransform anchor=handAnchor;
            switch(source)
            {
                case CardOrigin.DrawPile: anchor=drawAnchor; break;
                case CardOrigin.DiscardPile: anchor=discardAnchor; break;
                case CardOrigin.ExhaustPile: anchor=exhaustAnchor; break;
                case CardOrigin.PlayArea: anchor=playAnchor; break;
                case CardOrigin.Actor:
                    var anchors=cue.Actor>=0?heroAnchors:enemyAnchors; int index=cue.Actor>=0?cue.Actor:-1-cue.Actor;
                    anchor=anchors!=null && index<anchors.Length?anchors[index]:null; break;
            }
            return EffectPosition(anchor);
        }
        ResourceScope CardScope(BattleCue cue,Actor actor)
        {
            if(!cue.ArtOwner.HasValue && actor!=null) return actor.Scope;
            int owner=cue.ArtOwner??-1;
            if(!cardScopes.TryGetValue(owner,out var scope)) cardScopes[owner]=scope=resources.CreateScope(owner);
            return scope;
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
                    actor.Body=go.GetComponent<CombatBody>()??go.AddComponent<CombatBody>();
                    actor.Body.Configure(bodyCatalog,owner);
                    actor.Expression=go.GetComponent<ActorExpressionView>();
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
            // StartCoroutine may advance synchronously; publish its handle before completion.
            yield return null;
            while(!ready.IsCompleted) yield return null;
            if(ready.IsFaulted) { Debug.LogException(ready.Exception); Clear(); yield break; }
            while(pending.Count>0)
            {
                if(IsHandDiscard(pending.Peek().Cue) && CardPresenter!=null)
                { yield return DiscardHandBatch(); continue; }
                active=pending.Dequeue(); var job=active; Signal(job,PresentationPhase.Started);
                var cue=job.Cue; actors.TryGetValue(cue.Actor,out var actor);
                actor?.Expression?.Apply(cue);
                if(cue.Kind==BattleCueKind.Attack && cue.AttackId>0)
                {
                    yield return PhysicalAttack(job,actor);
                    Signal(job,PresentationPhase.Completed); active=null; continue;
                }
                if(cue.BodyPart>=0 && actor?.Body!=null)
                {
                    if(cue.Kind==BattleCueKind.Hit)
                    {
                        Signal(job,PresentationPhase.Impact); Signal(job,PresentationPhase.Completed); active=null; continue;
                    }
                }
                bool card=cue.Kind==BattleCueKind.Draw || cue.Kind==BattleCueKind.Shuffle || cue.Kind==BattleCueKind.Play || cue.Kind==BattleCueKind.Discard || cue.Kind==BattleCueKind.Exhaust;
                if(card && CardPresenter!=null)
                {
                    var presentation=CardPresenter(cue);
                    while(!presentation.IsCompleted) yield return null;
                    if(presentation.IsFaulted) { Debug.LogException(presentation.Exception); Signal(job,PresentationPhase.Cancelled); active=null; continue; }
                    if(presentation.IsCanceled) { Signal(job,PresentationPhase.Cancelled); active=null; continue; }
                    if(presentation.Result) { Signal(job,PresentationPhase.Impact); Signal(job,PresentationPhase.Completed); active=null; continue; }
                }
                float duration=card?(cue.Kind==BattleCueKind.Exhaust?.42f:cue.Kind==BattleCueKind.Play?.28f:.30f):.24f;
                if(!card && actor?.View!=null)
                {
                    var motion=cue.Kind==BattleCueKind.Attack?MotionId.Attack:cue.Kind==BattleCueKind.Death?MotionId.Death:cue.Kind==BattleCueKind.Hit?MotionId.Hit:cue.Kind==BattleCueKind.Guard?MotionId.Guard:cue.Kind==BattleCueKind.Victory?MotionId.Victory:MotionId.Skill;
                    if(actor.View.Motions.Any(m=>m.Id==motion)) actor.View.Play(motion,.08f);
                    duration=motion==MotionId.Death?.8f:.36f;
                }
                Task<PresentationPhase> animation=null;
                if(card)
                {
                    var scope=CardScope(cue,actor);
                    var load=scope.InstantiateAsync(22,stage,false); while(!load.IsCompleted) yield return null;
                    if(load.IsFaulted || load.IsCanceled) { Signal(job,PresentationPhase.Cancelled); active=null; continue; }
                    ghost=load.Result; ghostOwner=scope; var rect=(RectTransform)ghost.transform;
                    rect.anchorMin=rect.anchorMax=stage.pivot; rect.anchoredPosition=Vector2.zero; rect.localScale=Vector3.one;
                    rect.localRotation=Quaternion.identity; rect.SetAsLastSibling();
                    var image=ghost.GetComponent<Image>(); image.sprite=null; image.color=cue.Kind==BattleCueKind.Shuffle?new Color(.55f,.38f,.20f):Color.white;
                    if(cue.ArtId>0)
                    {
                        var sprite=scope.LoadAsync<Sprite>(cue.ArtId); while(!sprite.IsCompleted) yield return null;
                        if(sprite.IsCompletedSuccessfully) image.sprite=sprite.Result;
                    }
                    ghost.SetActive(true);
                    var target=cue.Kind==BattleCueKind.Play?playAnchor:cue.Kind==BattleCueKind.Draw?handAnchor:cue.Kind==BattleCueKind.Shuffle?drawAnchor:cue.Kind==BattleCueKind.Exhaust?exhaustAnchor:discardAnchor;
                    bool play=cue.Kind==BattleCueKind.Play, exhaust=cue.Kind==BattleCueKind.Exhaust;
                    animation=ghost.GetComponent<BetterAnimation>().Play(new UiMotion {Duration=duration,FromAlpha=1,ToAlpha=play||cue.Kind==BattleCueKind.Draw?1:0,FromOffset=SourcePosition(cue),ToOffset=EffectPosition(target),FromScale=cue.Source==CardOrigin.PlayArea?1.8f:.85f,ToScale=exhaust?.05f:play?1.8f:1,FromAngle=0,ToAngle=exhaust?100:cue.Kind==BattleCueKind.Shuffle?180:0});
                }
                float elapsed=0; bool impact=false;
                while(elapsed<duration || (animation!=null && !animation.IsCompleted))
                {
                    elapsed+=Time.unscaledDeltaTime;
                    if(!impact && elapsed>=duration*.45f) { impact=true; Signal(job,PresentationPhase.Impact); }
                    yield return null;
                }
                if(cue.Kind==BattleCueKind.Play && ghost!=null)
                {
                    // Own the display slot until arrival, readable hold, and exit all finish.
                    float hold=0; while(hold<.40f) { hold+=Time.unscaledDeltaTime; yield return null; }
                    var exit=ghost.GetComponent<BetterAnimation>().Play(new UiMotion {Duration=.16f,FromAlpha=1,ToAlpha=0,FromScale=1,ToScale=.92f});
                    while(!exit.IsCompleted) yield return null;
                }
                ReleaseGhost(); Signal(job,PresentationPhase.Completed); active=null;
            }
            routine=null;
        }
        void ReleaseGhost() { if(ghost!=null) { ghost.GetComponent<BetterAnimation>().Cancel(); ghostOwner?.Return(ghost); } ghost=null; ghostOwner=null; }
        bool IsHandDiscard(BattleCue cue) => cue.Actor==localSeat && cue.Kind==BattleCueKind.Discard && !string.IsNullOrEmpty(cue.CardId) && (cue.Source==CardOrigin.Hand || cue.Source==CardOrigin.Automatic);
        IEnumerator DiscardHandBatch()
        {
            float nextStart=0,elapsed=0;
            do
            {
                if(pending.Count>0 && IsHandDiscard(pending.Peek().Cue) && elapsed>=nextStart)
                {
                    var job=pending.Dequeue(); Signal(job,PresentationPhase.Started);
                    discarding.Add(job,CardPresenter(job.Cue));
                    nextStart=elapsed+.1f;
                }
                foreach(var pair in discarding.ToArray())
                {
                    if(!pair.Value.IsCompleted) continue;
                    discarding.Remove(pair.Key);
                    if(pair.Value.IsFaulted) Debug.LogException(pair.Value.Exception);
                    bool success=pair.Value.IsCompletedSuccessfully && pair.Value.Result;
                    if(success) Signal(pair.Key,PresentationPhase.Impact);
                    Signal(pair.Key,success?PresentationPhase.Completed:PresentationPhase.Cancelled);
                }
                elapsed+=Time.unscaledDeltaTime;
                yield return null;
            } while(discarding.Count>0 || (pending.Count>0 && IsHandDiscard(pending.Peek().Cue)));
        }
        IEnumerator PhysicalAttack(Job job,Actor actor)
        {
            var cue=job.Cue;
            actors.TryGetValue(cue.Target,out var target);
            var profile=actor?.Body!=null?actor.Body.Profile(cue.CardId):AttackProfile.Default;
            float direction=cue.Actor>=0?1:-1;
            var aim=target?.Body!=null?target.Body.AimPoint:Vector2.zero;
            var scale=stage.lossyScale;
            // Freeze aim before windup. Neither dodge motion nor active frames home on the target.
            var box=new AttackHitbox(aim+(Vector2)stage.TransformVector(new Vector3(profile.Offset.x*direction,profile.Offset.y,0)),
                new Vector2(profile.Size.x*Mathf.Abs(scale.x),profile.Size.y*Mathf.Abs(scale.y)),stage.eulerAngles.z+profile.Angle*direction);
            if(actor?.View!=null && actor.View.Motions.Any(m=>m.Id==MotionId.Attack)) actor.View.Play(MotionId.Attack,.08f);
            float elapsed=0;
            while(elapsed<Mathf.Max(0,profile.Windup)) { elapsed+=Time.unscaledDeltaTime; yield return null; }
            int part=-1; elapsed=0;
            do
            {
                // Query in late update after IK but before rendering/Gizmos. EndOfFrame
                // would hide the current-frame Gizmo and can stall in an unfocused Game tab.
                if(actor?.Body!=null && target?.Body!=null) target.Body.ScheduleQuery(box);
                yield return null;
                if(actor?.Body!=null && target?.Body!=null) part=target.Body.QueryResult;
                elapsed+=Time.unscaledDeltaTime;
            } while(part<0 && elapsed<Mathf.Max(.001f,profile.ActiveTime));
            Signal(job,PresentationPhase.Impact);
            if(IsCombatAuthority==null || IsCombatAuthority())
            {
                if(AttackResolved!=null) AttackResolved(cue.AttackId,part);
                else if(observed!=null && observed.ResolveAttack(cue.AttackId,part)) Observe(observed,0);
            }
            // Guests wait for the authoritative result, never mutate HP from local physics.
            while(observed!=null && observed.TryGetAttack(cue.AttackId,out _)) yield return null;
            elapsed=0;
            while(elapsed<Mathf.Max(0,profile.Recovery)) { elapsed+=Time.unscaledDeltaTime; yield return null; }
        }
        public void Clear()
        {
            if(stage!=null) stage.gameObject.SetActive(false);
            generation++; if(routine!=null) StopCoroutine(routine); routine=null;
            if(active!=null) Signal(active,PresentationPhase.Cancelled); active=null;
            foreach(var job in discarding.Keys) Signal(job,PresentationPhase.Cancelled); discarding.Clear();
            while(pending.Count>0) Signal(pending.Dequeue(),PresentationPhase.Cancelled);
            ReleaseGhost(); foreach(var actor in actors.Values) actor.Scope.Dispose(); actors.Clear();
            foreach(var scope in cardScopes.Values) scope.Dispose(); cardScopes.Clear();
            observed=null; room=revision=-1; ready=Task.CompletedTask;
        }
        void OnDestroy() { Clear(); shared?.Dispose(); Lifecycle=null; }
    }
}
