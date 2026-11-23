using System.Linq;
using NUnit.Framework;
using SpireAnimation;

public class GraftMonsterTests
{
    static GraftTemplate Template(GraftSkill skill=GraftSkill.Slash,int type=1)=>new GraftTemplate {Name=skill.ToString(),Skill=skill,SocketType=type,Hp=24,Power=9};
    [Test] public void LibrarySelectsCompatibleSocketAndSupportsRecursiveAttachment()
    {
        var state=new GraftMonsterState(new[]{Template(type:2),Template()});
        var first=state.Graft();Assert.IsNotNull(first);Assert.AreEqual(1,first.Template.SocketType);
        for(int i=0;i<20;i++) { var child=state.Graft(first.Root+1);Assert.IsNotNull(child);Assert.AreEqual(first.Root+1,child.ParentNode);first=child; }
        Assert.AreEqual(21,state.Limbs.Count);Assert.AreEqual(65,state.Assembly.Count);
        Assert.IsNull(new GraftMonsterState(new[]{Template(type:2)}).Graft());
    }
    [Test] public void LimbTakesDamageWithoutOverflowAndDeadParentRemovesDescendantSkills()
    {
        var state=new GraftMonsterState(new[]{Template()});var parent=state.Graft();var child=state.Graft(parent.Root+1);
        state.Damage(child.Root+2,5);Assert.AreEqual(19,child.Hp);Assert.AreEqual(24,parent.Hp);Assert.AreEqual(120,state.Hp);
        var dead=state.Damage(parent.Root,1000);Assert.AreEqual(2,dead.Length);Assert.AreEqual(120,state.Hp);
        Assert.IsFalse(state.UseSkill(child.Root));Assert.AreEqual(2,state.Assembly.Count);
        var replacement=state.Graft();Assert.AreEqual(parent.ParentNode,replacement.ParentNode);Assert.AreEqual(parent.ParentPort,replacement.ParentPort);
        Assert.Greater(replacement.Root,child.Root);
    }
    [Test] public void EachLimbSkillHasIndependentStateAndHostDeathEndsActions()
    {
        var state=new GraftMonsterState(new[]{Template(),Template(GraftSkill.Guard),Template(GraftSkill.Mend)});
        var slash=state.Graft();var guard=state.Graft();var mend=state.Graft();
        Assert.IsTrue(state.UseSkill(slash.Root));Assert.AreEqual(151,state.OpponentHp);
        state.UseSkill(guard.Root);state.Damage(guard.Root,12);Assert.AreEqual(21,guard.Hp);Assert.AreEqual(24,slash.Hp);
        state.Damage(0,10);state.UseSkill(mend.Root);Assert.AreEqual(119,state.Hp);
        Assert.AreEqual(3,state.Damage(0,1000).Length);Assert.IsNull(state.Graft());Assert.IsFalse(state.UseSkill(slash.Root));
    }
    [Test] public void BudgetFailureLeavesTopologyUnchanged()
    {
        var state=new GraftMonsterState(new[]{Template()},5);Assert.IsNotNull(state.Graft());Assert.IsNull(state.Graft());Assert.AreEqual(5,state.Assembly.Count);
    }
}
