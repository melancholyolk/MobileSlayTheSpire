using System.Collections.Generic;
using NUnit.Framework;
using SpireAnimation;
using UnityEngine;
public class SkeletonTests
{
    [Test] public void PlayablesDriveJointsAndInterruptionHasOneTerminalCallback()
    {
        var root=new GameObject("Rig"); var bone=new GameObject("Joint"); bone.transform.SetParent(root.transform);
        var clip=new AnimationClip(); clip.SetCurve("Joint",typeof(Transform),"localEulerAnglesRaw.z",AnimationCurve.Linear(0,0,1,60));
        var idle=new AnimationClip(); idle.SetCurve("Joint",typeof(Transform),"localEulerAnglesRaw.z",AnimationCurve.Constant(0,1,0));
        var rig=root.AddComponent<SkeletonView>(); var events=new List<MotionSignal>(); rig.Lifecycle+=events.Add;
        rig.Motions=new[]{new MotionEntry {Id=MotionId.Idle,Clip=idle,Loop=true},new MotionEntry {Id=MotionId.Attack,Clip=clip},new MotionEntry {Id=MotionId.Death,Clip=clip,Hold=true}};
        try
        {
            int attack=rig.Play(MotionId.Attack,0); rig.Tick(.5f);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0,bone.transform.localEulerAngles.z)),20);
            rig.Play(MotionId.Death); rig.Tick(1.1f); Assert.AreEqual(MotionId.Death,rig.Current);
            Assert.AreEqual(1,events.FindAll(e=>e.Ticket==attack && e.Phase==PresentationPhase.Cancelled).Count);
            for(int i=0;i<100;i++) rig.Play(MotionId.Attack);
            Assert.LessOrEqual(rig.ActivePlayableCount,3,"Mixer and at most two clips");
            rig.Stop(); Assert.IsFalse(rig.GraphValid);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(clip); Object.DestroyImmediate(idle); }
    }
}
