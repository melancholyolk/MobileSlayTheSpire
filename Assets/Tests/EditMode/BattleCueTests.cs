using System.Linq;
using NUnit.Framework;
using Spire;
namespace Spire.Tests
{
    public class BattleCueTests
    {
        [Test] public void DrawShufflePlayAndDiscardCuesFollowAcceptedCommands()
        {
            var run=new Run(CoreTests.FixtureCards(),42,HeroClass.Warrior);
            Assert.IsTrue(run.Apply(0,"vote",0,out _));
            Assert.AreEqual(BattleCueKind.Shuffle,run.Cues.First().Kind);
            Assert.AreEqual(5,run.Cues.Count(c=>c.Kind==BattleCueKind.Draw));
            Assert.IsTrue(run.Apply(0,"play",0,out _));
            Assert.AreEqual(BattleCueKind.Play,run.Cues.First().Kind);
            Assert.IsTrue(run.Cues.Any(c=>c.Kind==BattleCueKind.Discard));
            Assert.IsFalse(run.Apply(0,"play",99,out _)); Assert.IsEmpty(run.Cues);
            Assert.IsTrue(run.Apply(0,"end",0,out _));
            Assert.IsTrue(run.Cues.Any(c=>c.Kind==BattleCueKind.Attack && c.Actor<0));
            Assert.IsTrue(run.Cues.Any(c=>c.Kind==BattleCueKind.Hit && c.Actor==0));
        }
        [Test] public void CueGenerationDoesNotChangeDeterministicSimulation()
        {
            var a=new Run(CoreTests.FixtureCards(),22,HeroClass.Warrior);
            var b=new Run(CoreTests.FixtureCards(),22,HeroClass.Warrior);
            foreach(var action in new[]{"vote","play","end","end"})
            {
                a.Apply(0,action,0,out _); b.Apply(0,action,0,out _);
                Assert.AreEqual(a.Random.State,b.Random.State);
                Assert.AreEqual(a.Heroes[0].Hand,b.Heroes[0].Hand);
                Assert.AreEqual(a.Cues.Select(c=>c.Kind),b.Cues.Select(c=>c.Kind));
            }
        }
    }
}
