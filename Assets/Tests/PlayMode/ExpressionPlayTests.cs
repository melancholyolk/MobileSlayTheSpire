using System.Collections;
using System.Linq;
using NUnit.Framework;
using Spire;
using SpireAnimation;
using SpirePresentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.U2D.Animation;
public class ExpressionPlayTests
{
    [UnityTest] public IEnumerator BattleCuesSwitchSkinnedHeadsAndReleaseActor()
    {
        int baseline=SkinnedActorView.ActiveTargets;
        var root=new GameObject("Expression test");var app=root.AddComponent<GameApp>();
        try
        {
            float end=Time.realtimeSinceStartup+30;
            while(!app.UiReady.IsCompleted && Time.realtimeSinceStartup<end)yield return null;
            Assert.IsTrue(app.UiReady.IsCompletedSuccessfully);app.StartSolo(HeroClass.Warrior);app.Send("vote",0);
            end=Time.realtimeSinceStartup+30;
            while((!app.BattleVisuals.Ready.IsCompleted || app.BattleVisuals.PendingCount>0) && Time.realtimeSinceStartup<end)yield return null;
            Assert.AreEqual(0,app.BattleVisuals.PendingCount);
            var view=root.GetComponentInChildren<ActorExpressionView>();Assert.IsNotNull(view);
            var skin=view.Head.GetComponent<SpriteSkin>();
            var kinds=new[]{BattleCueKind.Hit,BattleCueKind.Heal,BattleCueKind.Attack,BattleCueKind.Guard};
            var expected=new[]{FacialExpression.Pain,FacialExpression.Happy,FacialExpression.Focus,FacialExpression.Focus};
            for(int i=0;i<kinds.Length;i++)
            {
                view.ResetExpression();app.BattleVisuals.Enqueue(new BattleCue(kinds[i],0,amount:4));
                end=Time.realtimeSinceStartup+5;
                while(view.Current==FacialExpression.Neutral && Time.realtimeSinceStartup<end)yield return null;
                Assert.AreEqual(expected[i],view.Current);yield return null;
                Assert.IsTrue(skin.HasCurrentDeformedVertices(),"Swapping expression must preserve active skinning");
                Assert.IsTrue(skin.GetDeformedVertexPositionData().All(v=>!float.IsNaN(v.x)));
                var rt=view.GetComponent<SkinnedActorView>();rt.ActorCamera.Render();
                var old=RenderTexture.active;RenderTexture.active=rt.Texture;var image=new Texture2D(512,512,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();RenderTexture.active=old;
                System.IO.Directory.CreateDirectory(".uloop/expression-preview");System.IO.File.WriteAllBytes(".uloop/expression-preview/"+kinds[i]+".png",image.EncodeToPNG());Object.Destroy(image);
                while((app.BattleVisuals.PendingCount>0 || view.Current!=FacialExpression.Neutral) && Time.realtimeSinceStartup<end)yield return null;
                Assert.AreEqual(FacialExpression.Neutral,view.Current);
            }
            app.ReturnToMenu();yield return null;yield return null;Assert.AreEqual(baseline,SkinnedActorView.ActiveTargets);
        }
        finally {Object.Destroy(root);}
    }
}
