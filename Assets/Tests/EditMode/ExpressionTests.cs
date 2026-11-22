using System.Linq;
using NUnit.Framework;
using Spire;
using SpireAnimation;
using UnityEditor;
using UnityEngine;
public class ExpressionTests
{
    [Test] public void ReactionsResetAndDeathCannotBeOverridden()
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameResources/Characters/0/Skin/13000.prefab"));
        try
        {
            var view=go.GetComponent<ActorExpressionView>();Assert.IsNotNull(view);
            foreach(var kind in new[]{BattleCueKind.Attack,BattleCueKind.Guard})
            {view.ResetExpression();view.Apply(new BattleCue(kind,0));Assert.AreSame(view.Focus,view.Head.sprite);view.Tick(1);Assert.AreSame(view.Neutral,view.Head.sprite);}
            view.Apply(new BattleCue(BattleCueKind.Hit,0,amount:0));Assert.AreEqual(FacialExpression.Neutral,view.Current);
            view.Apply(new BattleCue(BattleCueKind.Hit,0,amount:3));Assert.AreSame(view.Pain,view.Head.sprite);
            view.Apply(new BattleCue(BattleCueKind.Attack,0));Assert.AreEqual(FacialExpression.Pain,view.Current);
            view.Tick(1);view.Apply(new BattleCue(BattleCueKind.Heal,0,amount:2));Assert.AreSame(view.Happy,view.Head.sprite);
            view.Tick(1);Assert.AreSame(view.Neutral,view.Head.sprite);
            view.Apply(new BattleCue(BattleCueKind.Death,0));view.Tick(10);view.Apply(new BattleCue(BattleCueKind.Heal,0,amount:5));Assert.AreSame(view.Pain,view.Head.sprite);
            view.ResetExpression();Assert.AreSame(view.Neutral,view.Head.sprite);
        }
        finally {Object.DestroyImmediate(go);}
    }
    [Test] public void HealingReportsActualGainAndFullHealthDoesNotReact()
    {
        var cards=Spire.Tests.CoreTests.FixtureCards().Concat(new[]{new Card{Id="heal",Class=HeroClass.Warrior,Effect=Effect.Heal,Amount=10,Cost=0}}).ToArray();
        var run=new Run(cards,42,HeroClass.Warrior);Assert.IsTrue(run.Apply(0,"vote",0,out _));var hero=run.Heroes[0];
        hero.Hp=hero.MaxHp-3;hero.Hand.Clear();hero.Hand.Add("heal");Assert.IsTrue(run.Apply(0,"play",0,out _));Assert.AreEqual(3,run.Cues.Single(c=>c.Kind==BattleCueKind.Heal).Amount);
        hero.Hand.Add("heal");Assert.IsTrue(run.Apply(0,"play",0,out _));Assert.IsFalse(run.Cues.Any(c=>c.Kind==BattleCueKind.Heal));
    }
    [TestCase(false)] [TestCase(true)] public void FullBlockReportsZeroHpLoss(bool physical)
    {
        var run=new Run(Spire.Tests.CoreTests.FixtureCards(),42,HeroClass.Warrior){UsePhysicalAttacks=physical};run.Apply(0,"vote",0,out _);
        var hero=run.Heroes[0];hero.Block=10000;int hp=hero.Hp;run.Apply(0,"end",0,out _);
        if(physical){var attack=run.Cues.First(c=>c.Kind==BattleCueKind.Attack);Assert.IsTrue(run.ResolveAttack(attack.AttackId,0));}
        Assert.AreEqual(hp,hero.Hp);Assert.AreEqual(0,run.Cues.Single(c=>c.Kind==BattleCueKind.Hit && c.Actor==0).Amount);
    }
}
