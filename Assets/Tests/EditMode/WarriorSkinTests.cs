using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.IK;
using SpireAnimation;
public class WarriorSkinTests
{
    const string Path="Assets/GameResources/Characters/0/Skin/13000.prefab";
    [Test] public void ImportedSpritesContainEditableBonesAndNormalizedBlendWeights()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        Assert.IsNotNull(prefab);Assert.IsNull(prefab.GetComponent<BodyAssemblyView>());
        var skins=prefab.GetComponentsInChildren<SpriteSkin>(true);Assert.AreEqual(11,skins.Length);
        int blended=0;
        foreach(var skin in skins)
        {
            var sprite=skin.GetComponent<SpriteRenderer>().sprite;
            Assert.AreEqual(15,sprite.GetBones().Length);Assert.AreEqual(15,skin.boneTransforms.Length);
            Assert.IsNotNull(skin.rootBone);Assert.IsTrue(skin.boneTransforms.All(t=>t!=null));
            var factory=new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();factory.Init();
            var provider=factory.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)));provider.InitSpriteEditorDataProvider();
            var vertices=provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteMeshDataProvider>().GetVertices(provider.GetSpriteRects().Single().spriteID);
            Assert.Greater(vertices.Length,4);
            foreach(var vertex in vertices) { var w=vertex.boneWeight;Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.0001));if(w.weight1>0)blended++; }
        }
        Assert.Greater(blended,0);
    }
    [Test] public void TenMotionsAnimateControlsInsteadOfLimbAngles()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Path);var view=prefab.GetComponent<SkeletonView>();
        Assert.AreEqual(10,view.Motions.Length);Assert.AreEqual(4,prefab.GetComponentsInChildren<LimbSolver2D>(true).Length);
        foreach(var motion in view.Motions)
        {
            var bindings=AnimationUtility.GetCurveBindings(motion.Clip);
            Assert.IsTrue(bindings.Any(b=>b.path.Contains("Target")));
            Assert.IsFalse(bindings.Any(b=>b.path.Contains("Skeleton") && (b.path.Contains("Arm")||b.path.Contains("Shin")||b.path.Contains("Thigh"))));
            Assert.IsEmpty(AnimationUtility.GetObjectReferenceCurveBindings(motion.Clip));
        }
    }
}
