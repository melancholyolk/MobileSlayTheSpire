using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpireAnimation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.U2D.IK;

public class GraftMonsterPlayTests
{
    [UnityTest] public IEnumerator GraftedBonesFollowAnimatedTargetsAndDetachWithDeathMotion()
    {
        var root=new GameObject("Graft test",typeof(RectTransform));var prototype=root.AddComponent<GraftMonsterPrototype>();
        try
        {
            prototype.Initialize();var hostClip=root.GetComponent<SkeletonView>().Motions.First(m=>m.Id==MotionId.Idle).Clip;
            var limb=prototype.Graft();var child=prototype.Graft(limb.Root+1);
            Assert.AreSame(hostClip,root.GetComponent<SkeletonView>().Motions.First(m=>m.Id==MotionId.Idle).Clip,"Grafting must not regenerate the host's animation");
            Assert.IsTrue(prototype.Bones[child.Root].IsChildOf(prototype.Bones[limb.Root+1]));
            var solver=prototype.Bones[limb.Root].parent.GetComponent<LimbSolver2D>();Assert.IsTrue(solver.isValid);
            var chain=solver.GetChain(0);Vector3 start=chain.target.localPosition;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.Greater(Vector3.Distance(start,chain.target.localPosition),.1f,"Limb animation moves its own endpoint");
            Assert.Less(Vector3.Distance(chain.effector.position,chain.target.position),.5f,"IK solves the joints to the animated endpoint");
            int hostHp=prototype.State.Hp;
            Physics2D.SyncTransforms();
            var hit=prototype.Bones[child.Root+2].GetComponent<BoxCollider2D>();
            Assert.AreEqual(child.Root+2,prototype.Attack(new AttackHitbox(hit.bounds.center,Vector2.one*2),8));
            Assert.AreEqual(hostHp,prototype.State.Hp);Assert.AreEqual(child.Template.Hp-8,child.Hp);
            var detachedRoot=prototype.Bones[limb.Root].parent;
            prototype.ReceiveHit(limb.Root+2,999);
            Assert.AreEqual(2,prototype.DetachedCount);Assert.AreEqual(0,prototype.State.Limbs.Count);
            Assert.AreEqual(MotionId.Death,detachedRoot.GetComponent<SkeletonView>().Current);
            Assert.IsTrue(detachedRoot.GetComponentsInChildren<Collider2D>().All(c=>!c.enabled));
            Assert.IsFalse(prototype.UseSkill(child.Root));Assert.AreEqual(hostHp,prototype.State.Hp);
            yield return new WaitForSecondsRealtime(1.3f);Assert.AreEqual(0,prototype.DetachedCount);Assert.IsTrue(detachedRoot==null);
            var next=prototype.Graft();Assert.IsNotNull(next);Assert.IsTrue(prototype.UseSkill(next.Root));
        }
        finally { Object.Destroy(root); }
        yield return null;
    }
}
