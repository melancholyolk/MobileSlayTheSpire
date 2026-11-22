using System.Collections;
using System.Linq;
using NUnit.Framework;
using Spire;
using SpirePresentation;
using UnityEngine;
using UnityEngine.TestTools;
public sealed class ContinuousCardPlayTests
{
    [UnityTest] public IEnumerator PlaysQueueWithoutWaitingForPresentation()
    {
        var root=new GameObject("ContinuousCardPlayTest");
        var app=root.AddComponent<GameApp>();
        try
        {
            float deadline=Time.realtimeSinceStartup+30;
            while(!app.UiReady.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsTrue(app.UiReady.IsCompletedSuccessfully);
            app.StartSolo(HeroClass.Warrior); app.Send("vote",0);
            deadline=Time.realtimeSinceStartup+30;
            while(!app.CardsReady.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsTrue(app.CardsReady.IsCompletedSuccessfully);
            var hand=app.UI.Get<GameplayView>(2).hand.GetComponent<BattleHandLayout>();
            var hero=app.State.Heroes[0];
            var choices=hand.Cards.Where(c=>app.State.Cards[c.CardId].Cost==1).Take(3).ToArray();
            Assert.AreEqual(3,choices.Length);
            int before=hero.Hand.Count;
            hand.Play(choices[0]);
            Assert.Greater(app.BattleVisuals.PendingCount,0);
            Assert.IsTrue(hand.CanInteract,"Presentation must not gate input");
            hand.Play(choices[0]); Assert.AreEqual(before-1,hero.Hand.Count,"No duplicate submission");
            hand.Play(choices[2]); hand.Play(choices[1]);
            Assert.AreEqual(before-3,hero.Hand.Count);
            Assert.AreEqual(0,hero.Energy);
            var remaining=hand.Cards.FirstOrDefault(c=>!c.Locked);
            if(remaining!=null) hand.Play(remaining);
            Assert.AreEqual(before-3,hero.Hand.Count,"Cannot overspend energy");
            deadline=Time.realtimeSinceStartup+30;
            while((!app.CardsReady.IsCompleted || app.BattleVisuals.PendingCount>0) && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.AreEqual(0,app.BattleVisuals.PendingCount);
            Assert.IsTrue(app.CardsReady.IsCompletedSuccessfully);
            CollectionAssert.AreEqual(hero.Hand,hand.Cards.Select(c=>c.CardId));
        }
        finally { Object.Destroy(root); }
        yield return null;
    }
}
