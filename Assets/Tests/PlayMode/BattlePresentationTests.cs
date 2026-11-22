using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Spire;
using SpireAnimation;
using SpirePresentation;
using UnityEngine;
using UnityEngine.TestTools;
public class BattlePresentationTests
{
    [UnityTest] public IEnumerator CardOriginsPlayQueueAndExhaustCompleteInOrder()
    {
        var root=new GameObject("CardMotionTest"); var app=root.AddComponent<GameApp>();
        var events=new List<CardProcessSignal>();
        try
        {
            float deadline=Time.realtimeSinceStartup+30;
            while(!app.UiReady.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsTrue(app.UiReady.IsCompletedSuccessfully);
            app.StartSolo(HeroClass.Warrior); app.Send("vote",0);
            var presentation=app.BattleVisuals;
            deadline=Time.realtimeSinceStartup+30;
            while((!presentation.Ready.IsCompleted || presentation.PendingCount>0) && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsTrue(presentation.Ready.IsCompletedSuccessfully); Assert.AreEqual(0,presentation.PendingCount);
            presentation.Lifecycle+=events.Add;
            int art=app.State.Cards[app.State.Heroes[0].Hand[0]].ArtId;
            foreach(var origin in new[]{CardOrigin.DrawPile,CardOrigin.DiscardPile,CardOrigin.ExhaustPile})
                presentation.Enqueue(new BattleCue(BattleCueKind.Draw,0,artId:art,source:origin));
            foreach(var origin in new[]{CardOrigin.Hand,CardOrigin.DrawPile,CardOrigin.DiscardPile,CardOrigin.ExhaustPile,CardOrigin.Actor})
                presentation.Enqueue(new BattleCue(BattleCueKind.Play,0,artId:art,source:origin));
            var enemyPlay=new BattleCue(BattleCueKind.Play,-1,artId:art,source:CardOrigin.Actor,artOwner:0);
            Assert.AreNotEqual(presentation.SourcePosition(enemyPlay),presentation.SourcePosition(new BattleCue(BattleCueKind.Play,0,source:CardOrigin.Actor)));
            presentation.Enqueue(enemyPlay);
            presentation.Enqueue(new BattleCue(BattleCueKind.Exhaust,0,artId:art,source:CardOrigin.PlayArea));
            deadline=Time.realtimeSinceStartup+30;
            while(presentation.PendingCount>0 && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.AreEqual(0,presentation.PendingCount);
            Assert.AreEqual(10,events.Count(e=>e.Phase==PresentationPhase.Completed));
            long running=0;
            foreach(var signal in events)
            {
                Assert.AreNotEqual(PresentationPhase.Cancelled,signal.Phase);
                if(signal.Phase==PresentationPhase.Started) { Assert.AreEqual(0,running,"Display slot must be released before starting the next card"); running=signal.Ticket; }
                if(signal.Phase==PresentationPhase.Completed) { Assert.AreEqual(running,signal.Ticket); running=0; }
            }
            Assert.AreEqual(0,running);
            CollectionAssert.AreEqual(events.Where(e=>e.Phase==PresentationPhase.Queued).Select(e=>e.Ticket),events.Where(e=>e.Phase==PresentationPhase.Completed).Select(e=>e.Ticket));
        }
        finally { Object.Destroy(root); }
        yield return null;
    }
    [UnityTest] public IEnumerator ExpansionHeroesLoadThroughMenuAndReleaseBodies()
    {
        var root=new GameObject("ExpansionPresentation"); var app=root.AddComponent<GameApp>();
        try
        {
            float end=Time.realtimeSinceStartup+30;
            while(!app.UiReady.IsCompleted && Time.realtimeSinceStartup<end) yield return null;
            Assert.IsTrue(app.UiReady.IsCompletedSuccessfully,app.UiReady.Exception?.ToString());
            foreach(int owner in new[]{2,3})
            {
                root.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="SinglePlayer").onClick.Invoke();
                var choices=app.UI.Get<GameStartView>(1).ChoicesReady;
                end=Time.realtimeSinceStartup+30;
                while(!choices.IsCompleted && Time.realtimeSinceStartup<end) yield return null;
                Assert.IsTrue(choices.IsCompletedSuccessfully,choices.Exception?.ToString());
                root.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="Character_"+owner).onClick.Invoke();
                Assert.AreEqual(owner,(int)app.State.Heroes[0].Class);
                app.Send("vote",0); end=Time.realtimeSinceStartup+30;
                while((!app.BattleVisuals.Ready.IsCompleted || !app.CardsReady.IsCompleted) && Time.realtimeSinceStartup<end) yield return null;
                Assert.IsTrue(app.CardsReady.IsCompletedSuccessfully,app.CardsReady.Exception?.ToString());
                Assert.IsTrue(app.BattleVisuals.Ready.IsCompletedSuccessfully,app.BattleVisuals.Ready.Exception?.ToString());
                Assert.AreEqual(2,app.BattleVisuals.ActorCount);
                var bodies=root.GetComponentsInChildren<BodyAssemblyView>(); Assert.AreEqual(2,bodies.Length);
                foreach(var body in bodies) Assert.AreEqual(11,body.Assembly.Count);
                app.ReturnToMenu(); yield return null;
                Assert.AreEqual(0,app.BattleVisuals.ActorCount);
                foreach(var body in bodies) if(body!=null) Assert.AreEqual(0,body.OwnedClipCount);
            }
        }
        finally { Object.Destroy(root); }
        yield return null;
    }
    [UnityTest] public IEnumerator DrawProcessesCompleteAndMenuCancelsQueuedEffects()
    {
        var root=new GameObject("PresentationTest"); var app=root.AddComponent<GameApp>(); var events=new List<CardProcessSignal>();
        try
        {
            float deadline=Time.realtimeSinceStartup+30;
            while(!app.UiReady.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsTrue(app.UiReady.IsCompletedSuccessfully,app.UiReady.Exception?.ToString());
            app.BattleVisuals.Lifecycle+=events.Add; app.StartSolo(HeroClass.Warrior); app.Send("vote",0);
            deadline=Time.realtimeSinceStartup+30;
            while((!app.BattleVisuals.Ready.IsCompleted || app.BattleVisuals.PendingCount>0) && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsTrue(app.BattleVisuals.Ready.IsCompletedSuccessfully,app.BattleVisuals.Ready.Exception?.ToString());
            Assert.AreEqual(2,app.BattleVisuals.ActorCount);
            var bodies=root.GetComponentsInChildren<BodyAssemblyView>(); Assert.AreEqual(1,bodies.Length);
            Assert.AreEqual(1,root.GetComponentsInChildren<SkinnedActorView>().Length);
            foreach(var body in bodies) { Assert.AreEqual(11,body.Assembly.Count); Assert.AreEqual(10,body.OwnedClipCount); }
            Assert.AreEqual(5,events.Count(e=>e.Cue.Kind==BattleCueKind.Draw && e.Phase==PresentationPhase.Completed));
            app.Send("end"); yield return null; app.ReturnToMenu(); yield return null;
            foreach(var body in bodies) if(body!=null) Assert.AreEqual(0,body.OwnedClipCount);
            Assert.AreEqual(0,app.BattleVisuals.ActorCount); Assert.AreEqual(0,app.BattleVisuals.PendingCount);
            foreach(var group in events.GroupBy(e=>e.Ticket))
                Assert.AreEqual(1,group.Count(e=>e.Phase==PresentationPhase.Completed||e.Phase==PresentationPhase.Cancelled),"Each process terminates exactly once");
        }
        finally { Object.Destroy(root); }
        yield return null;
    }
}

