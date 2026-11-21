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
            Assert.AreEqual(5,events.Count(e=>e.Cue.Kind==BattleCueKind.Draw && e.Phase==PresentationPhase.Completed));
            app.Send("end"); yield return null; app.ReturnToMenu(); yield return null;
            Assert.AreEqual(0,app.BattleVisuals.ActorCount); Assert.AreEqual(0,app.BattleVisuals.PendingCount);
            foreach(var group in events.GroupBy(e=>e.Ticket))
                Assert.AreEqual(1,group.Count(e=>e.Phase==PresentationPhase.Completed||e.Phase==PresentationPhase.Cancelled),"Each process terminates exactly once");
        }
        finally { Object.Destroy(root); }
        yield return null;
    }
}
