using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Spire.Tests
{
    public class TableTests
    {
        [Test] public void ProductionTableLoadsBothClassesAndAllEffects()
        {
            var cards=CardTable.Read(Resources.Load<TextAsset>("Tables/cards").bytes);
            Assert.AreEqual(16,cards.Length);
            foreach(HeroClass cls in Enum.GetValues(typeof(HeroClass))) Assert.AreEqual(10,cards.Where(c=>c.Class==cls).Sum(c=>c.StarterCopies));
            Assert.AreEqual(7,cards.Select(c=>c.Effect).Distinct().Count());
        }
        [Test] public void CorruptAndTruncatedTablesAreRejected()
        {
            var bytes=Resources.Load<TextAsset>("Tables/cards").bytes;
            Assert.Throws<EndOfStreamException>(()=>CardTable.Read(bytes.Take(bytes.Length-1).ToArray()));
            bytes[0]=0; Assert.Throws<InvalidDataException>(()=>CardTable.Read(bytes));
        }
        [TestCase(HeroClass.Warrior)] [TestCase(HeroClass.Hunter)]
        public void EveryProductionCardExecutesItsConfiguredEffect(HeroClass cls)
        {
            var cards=CardTable.Read(Resources.Load<TextAsset>("Tables/cards").bytes);
            foreach(var c in cards.Where(c=>c.Class==cls))
            {
                var r=new Run(cards,7,cls); r.Apply(0,"vote",0,out _); var h=r.Heroes[0];
                h.Hand.Clear(); h.Hand.Add(c.Id); h.Hp-=20; h.Energy=3;
                int hp=r.Enemies[0].Hp;
                Assert.IsTrue(r.Apply(0,"play",0,out var error),error);
                switch(c.Effect) {
                    case Effect.Attack: Assert.AreEqual(Math.Max(0,hp-c.Amount*c.Hits),r.Enemies[0].Hp); break;
                    case Effect.Block: Assert.AreEqual(c.Amount,h.Block); break;
                    case Effect.Poison: Assert.AreEqual(c.Amount,r.Enemies[0].Poison); break;
                    case Effect.Strength: Assert.AreEqual(c.Amount,h.Strength); break;
                    case Effect.Heal: Assert.AreEqual(h.MaxHp-20+c.Amount,h.Hp); break;
                    case Effect.Draw: Assert.AreEqual(c.Amount,h.Hand.Count); break;
                    case Effect.Energy: Assert.AreEqual(3-c.Cost+c.Amount,h.Energy); break;
                }
                Assert.That(c.Exhaust?h.Exhausted:h.Discard,Does.Contain(c.Id));
            }
        }
    }
}
