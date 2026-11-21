using System.Linq;
using NUnit.Framework;
namespace Spire.Tests
{
    public class CoreTests
    {
        public static Card[] FixtureCards() => new[] {
            new Card { Id="strike", NameId=1, Class=HeroClass.Warrior, Cost=1, Amount=8, Effect=Effect.Attack, StarterCopies=5 },
            new Card { Id="guard", NameId=1, Class=HeroClass.Warrior, Cost=1, Amount=7, Effect=Effect.Block, StarterCopies=5 },
            new Card { Id="shot", NameId=1, Class=HeroClass.Hunter, Cost=1, Amount=8, Effect=Effect.Attack, StarterCopies=5 },
            new Card { Id="dodge", NameId=1, Class=HeroClass.Hunter, Cost=1, Amount=7, Effect=Effect.Block, StarterCopies=5 }};
        static void Do(Run r,int p,string action,int arg=0) { Assert.That(r.Apply(p,action,arg,out var error),Is.True,error); }
        [Test] public void SeedProducesIdenticalMapAndHands()
        {
            var a=new Run(FixtureCards(),42,HeroClass.Warrior); var b=new Run(FixtureCards(),42,HeroClass.Warrior);
            Assert.That(a.Map.Select(n=>n.Kind),Is.EqualTo(b.Map.Select(n=>n.Kind)));
            Do(a,0,"vote"); Do(b,0,"vote"); Assert.That(a.Heroes[0].Hand,Is.EqualTo(b.Heroes[0].Hand));
            Assert.That(a.Map.Where(n=>n.Floor<Run.Floors-1).All(n=>n.Next.Count>0));
        }
        [Test] public void IllegalCommandsDoNotMutateState()
        {
            var r=new Run(FixtureCards(),1,HeroClass.Warrior);
            Assert.IsFalse(r.Apply(0,"play",0,out _)); Assert.IsFalse(r.Apply(0,"vote",20,out _)); Assert.AreEqual(0,r.Revision);
            Do(r,0,"vote"); Assert.IsFalse(r.Apply(0,"play",99,out _)); Assert.AreEqual(3,r.Heroes[0].Energy);
        }
        [Test] public void CooperativeRoundWaitsForEveryLivingPlayer()
        {
            var r=new Run(FixtureCards(),1,HeroClass.Warrior,HeroClass.Hunter);
            Do(r,0,"vote"); Assert.AreEqual(Phase.Map,r.Phase); Do(r,1,"vote");
            Do(r,0,"end"); Assert.AreEqual(1,r.Turn); Assert.IsFalse(r.Apply(0,"end",0,out _));
            Do(r,1,"end"); Assert.AreEqual(2,r.Turn); Assert.AreEqual(74,r.Heroes[0].Hp);
        }
        [Test] public void DefeatEndsRunAndRejectsFurtherActions()
        {
            var r=new Run(FixtureCards(),1,HeroClass.Warrior); Do(r,0,"vote");
            for(int i=0;i<30 && r.Phase==Phase.Combat;i++) Do(r,0,"end");
            Assert.AreEqual(Phase.Lost,r.Phase); Assert.IsFalse(r.Apply(0,"vote",1,out _));
        }
        [Test] public void CompleteRunTraversesRoomsRewardsAndBoss()
        {
            var cards=FixtureCards(); foreach(var c in cards) { c.Amount=200; c.Effect=Effect.Attack; }
            var r=new Run(cards,123,HeroClass.Warrior,HeroClass.Hunter);
            for(int step=0;step<200 && r.Phase!=Phase.Won;step++)
                for(int p=0;p<r.Heroes.Count;p++)
                {
                    switch(r.Phase) {
                        case Phase.Map: Do(r,p,"vote",r.Choices.First()); break;
                        case Phase.Combat: Do(r,p,"play"); break;
                        case Phase.Reward: if(!r.Resolved.Contains(p)) Do(r,p,"reward",-1); break;
                        case Phase.Camp: case Phase.Shop: case Phase.Event: if(!r.Resolved.Contains(p)) Do(r,p,"room",1); break;
                    }
                }
            Assert.AreEqual(Phase.Won,r.Phase); Assert.AreEqual(Run.Floors-1,r.Map[r.Current].Floor);
        }
    }
}
