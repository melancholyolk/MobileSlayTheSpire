using System.Linq;
using NUnit.Framework;
using SpireAnimation;
using UnityEditor;
using UnityEngine;
[System.Serializable] public class TestRigList { public TestRig[] Items; }
[System.Serializable] public class TestRig { public string Folder; public int ResourceId,Index; }
public class RigAssetTests
{
    [Test] public void TwelveJointRigsContainSeventyNonSpriteAnimations()
    {
        var rigs=JsonUtility.FromJson<TestRigList>(System.IO.File.ReadAllText("GameData/Art/Rigs.json")).Items;
        Assert.AreEqual(12,rigs.Length); int total=0;
        foreach(var rig in rigs)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(rig.Folder+"/"+rig.ResourceId+".prefab"); Assert.IsNotNull(prefab);
            var view=prefab.GetComponent<SkeletonView>(); Assert.AreEqual(rig.Index<2?10:5,view.Motions.Length);
            Assert.AreEqual(11,prefab.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length);
            foreach(var motion in view.Motions)
            {
                Assert.Greater(motion.Clip.length,.1f);
                Assert.IsEmpty(AnimationUtility.GetObjectReferenceCurveBindings(motion.Clip),"No sprite swapping / frame sequences");
                Assert.IsTrue(AnimationUtility.GetCurveBindings(motion.Clip).Any(b=>b.path.Contains("Torso")));
                total++;
            }
        }
        Assert.AreEqual(70,total);
    }
}

