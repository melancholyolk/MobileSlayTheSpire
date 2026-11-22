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

