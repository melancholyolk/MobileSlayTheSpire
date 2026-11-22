using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Spire.Tests
{
    public class ProductionRunTests
    {
        public static void Choose(Run r,int player,out string action,out int arg)
        {
            var h=r.Heroes[player]; action="end"; arg=0;
            switch(r.Phase)
            {
                case Phase.Map:
                    action="vote"; arg=r.Choices.OrderBy(id=>r.Map[id].Kind==RoomKind.Camp?0:r.Map[id].Kind==RoomKind.Shop?1:r.Map[id].Kind==RoomKind.Event?2:r.Map[id].Kind==RoomKind.Elite?4:3).First(); break;
                case Phase.Reward:
                    action="reward"; arg=-1;
                    for(int i=0;i<r.Rewards[player].Count;i++)
                    { var c=r.Cards[r.Rewards[player][i]]; if(c.Effect==Effect.Poison || c.Effect==Effect.Strength || (c.Effect==Effect.Attack && c.Amount*c.Hits>=12)) {arg=i;break;} }
                    break;
                case Phase.Camp: action="room"; break;
                case Phase.Shop: action="room"; arg=h.Gold>=30?0:1; break;
                case Phase.Event: action="room"; arg=1; break;
                case Phase.Combat:
                    int best=-1; float score=-1;
                    for(int i=0;i<h.Hand.Count;i++)
                    {
                        var c=r.Cards[h.Hand[i]]; if(c.Cost>h.Energy) continue;
                        float value=c.Effect==Effect.Attack?(c.Amount+h.Strength)*c.Hits/(float)(c.Cost+1):c.Effect==Effect.Poison?c.Amount*2f:c.Effect==Effect.Strength?15:c.Effect==Effect.Block?1:10;
                        if(value>score) {score=value;best=i;}
                    }
                    if(best>=0) {action="play";arg=best;} break;
            }
        }
        [TestCase(HeroClass.Warrior)] [TestCase(HeroClass.Hunter)] [TestCase((HeroClass)2)] [TestCase((HeroClass)3)]
        public void ProductionDeckCanWinEightFloorRun(HeroClass cls)
        {
            var r=new Run(CardTable.Read(Resources.Load<TextAsset>("Tables/cards").bytes),42,cls);
            for(int i=0;i<2000 && r.Phase!=Phase.Won && r.Phase!=Phase.Lost;i++)
            { Choose(r,0,out var action,out var arg); Assert.IsTrue(r.Apply(0,action,arg,out var error),error); }
            Assert.AreEqual(Phase.Won,r.Phase,$"{cls}: HP {r.Heroes[0].Hp}, floor {r.Map[r.Current].Floor}, turn {r.Turn}");
        }
        [Test] public void PoisonBypassesBlockAndDecaysAtEnemyTurn()
        {
            var r=new Run(CardTable.Read(Resources.Load<TextAsset>("Tables/cards").bytes),42,HeroClass.Hunter);
            r.Apply(0,"vote",0,out _); r.Enemies[0].Poison=6; r.Enemies[0].Block=100;
            r.Apply(0,"end",0,out _);
            Assert.AreEqual(22,r.Enemies[0].Hp); Assert.AreEqual(5,r.Enemies[0].Poison); Assert.AreEqual(100,r.Enemies[0].Block);
        }
    }
}
