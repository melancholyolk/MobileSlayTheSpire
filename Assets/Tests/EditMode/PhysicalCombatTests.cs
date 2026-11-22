using System.Linq;
using NUnit.Framework;
using Spire;
using SpireAnimation;
using UnityEngine;

namespace Spire.Tests
{
    public class PhysicalCombatTests
    {
        static Run Battle(int hits=1)
        {
            var cards=CoreTests.FixtureCards(); cards[0].Hits=hits;
            var run=new Run(cards,1,HeroClass.Warrior) {UsePhysicalAttacks=true};
            Assert.IsTrue(run.Apply(0,"vote",0,out _));
            run.Heroes[0].Hand.Clear();run.Heroes[0].Hand.Add("strike");
            return run;
        }
        [Test] public void MissPreservesHealthAndBlockAndRejectsDuplicateImpact()
        {
            var run=Battle();run.Enemies[0].Block=5;int hp=run.Enemies[0].Hp;
            Assert.IsTrue(run.Apply(0,"play",0,out _));
            int id=run.Cues.Single(c=>c.AttackId>0).AttackId;
            Assert.AreEqual(hp,run.Enemies[0].Hp);
            Assert.IsFalse(run.Apply(0,"end",0,out _));
            Assert.IsTrue(run.ResolveAttack(id,-1));
            Assert.AreEqual(hp,run.Enemies[0].Hp);Assert.AreEqual(5,run.Enemies[0].Block);
            Assert.IsFalse(run.Cues.Any(c=>c.Kind==BattleCueKind.Hit));
            Assert.IsFalse(run.ResolveAttack(id,0));
        }
        [Test] public void MultiHitSettlesOncePerStrikeAndWaitsBeforeVictory()
        {
            var run=Battle(3);run.Enemies[0].Hp=9;
            Assert.IsTrue(run.Apply(0,"play",0,out _));
            var ids=run.Cues.Where(c=>c.AttackId>0).Select(c=>c.AttackId).ToArray();
            Assert.IsFalse(run.ResolveAttack(ids[1],0));
            Assert.IsTrue(run.ResolveAttack(ids[0],3));Assert.AreEqual(1,run.Enemies[0].Hp);
            Assert.AreEqual(3,run.Cues.Single().BodyPart);
            Assert.IsTrue(run.ResolveAttack(ids[1],4));Assert.AreEqual(Phase.Combat,run.Phase);
            Assert.IsTrue(run.ResolveAttack(ids[2],4));Assert.AreEqual(Phase.Reward,run.Phase);
            Assert.AreEqual(0,run.PendingAttacks);
        }
        [Test] public void EnemyMissWaitsForImpactBeforeStartingNextTurn()
        {
            var run=Battle();int hp=run.Heroes[0].Hp;
            Assert.IsTrue(run.Apply(0,"end",0,out _));Assert.AreEqual(1,run.Turn);
            Assert.IsTrue(run.ResolveAttack(run.Cues.Single(c=>c.AttackId>0).AttackId,-1));
            Assert.AreEqual(2,run.Turn);Assert.AreEqual(hp,run.Heroes[0].Hp);
        }
        [Test] public void RealCollidersSupportRotationDodgeInvulnerabilityAndOwnerFilter()
        {
            var root=new GameObject("Hurtbox test");var other=new GameObject("Other actor");
            try
            {
                var body=root.AddComponent<CombatBody>();var part=root.AddComponent<BodyPartHurtbox>();
                part.Owner=body;part.NodeId=7;part.Collider.size=new Vector2(1,1);part.Collider.isTrigger=true;
                var foreign=other.AddComponent<BodyPartHurtbox>();foreign.Owner=other.AddComponent<CombatBody>();foreign.NodeId=0;
                var box=new AttackHitbox(Vector2.zero,new Vector2(2,1),45);
                Assert.AreEqual(7,body.Query(box));
                root.transform.position=new Vector3(100,0,0);Assert.AreEqual(-1,body.Query(box));
                root.transform.position=Vector3.zero;body.Invulnerable=true;Assert.AreEqual(-1,body.Query(box));
                body.Invulnerable=false;part.Collider.enabled=false;Assert.AreEqual(-1,body.Query(box));
                Assert.AreEqual(1,root.GetComponents<Collider2D>().Length,"No attack collider is created");
            }
            finally { Object.DestroyImmediate(root);Object.DestroyImmediate(other); }
        }
    }
}
