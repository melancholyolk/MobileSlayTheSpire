using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.IK;
using Spire;
using SpireAnimation;
using SpirePresentation;
public class WarriorSkinPlayTests
{
    static IEnumerator Await(System.Threading.Tasks.Task task)
    {
        float end=Time.realtimeSinceStartup+30;
        while(!task.IsCompleted && Time.realtimeSinceStartup<end)yield return null;
        Assert.IsTrue(task.IsCompletedSuccessfully,task.Exception?.ToString());
    }
    [UnityTest] public IEnumerator SkinDeformsWithIkControlsAndReleasesRenderTargets()
    {
        int baseline=SkinnedActorView.ActiveTargets;
        var root=new GameObject("WarriorSkinTest");var app=root.AddComponent<GameApp>();
        try
        {
            yield return Await(app.UiReady);app.StartSolo(HeroClass.Warrior);app.Send("vote",0);
            yield return Await(app.BattleVisuals.Ready);yield return Await(app.CardsReady);
            float end=Time.realtimeSinceStartup+10;while(app.BattleVisuals.PendingCount>0 && Time.realtimeSinceStartup<end)yield return null;
            var skinView=root.GetComponentInChildren<SkinnedActorView>();Assert.IsNotNull(skinView);
            var skins=skinView.GetComponentsInChildren<SpriteSkin>();Assert.AreEqual(11,skins.Length);
            var player=skinView.GetComponent<SkeletonView>();player.enabled=false;
            var manager=skinView.GetComponentInChildren<IKManager2D>();manager.enabled=false;
            var arm=skins.Single(s=>s.name=="Skin_ForearmR");
            Vector3[] before=null;float moved=0;int visible=0;
            foreach(MotionId id in Enum.GetValues(typeof(MotionId)))
            {
                player.Play(id,0);player.Tick(player.Motions.Single(m=>m.Id==id).Clip.length*.45f);manager.UpdateManager();
                yield return null;yield return new WaitForEndOfFrame();
                Assert.IsTrue(skins.All(s=>s.HasCurrentDeformedVertices()),"All SpriteSkin buffers must be valid");
                var vertices=arm.GetDeformedVertexPositionData().ToArray();
                Assert.IsTrue(vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)));
                if(id==MotionId.Idle)before=vertices;
                if(id==MotionId.Attack) moved=vertices.Zip(before,(a,b)=>(a-b).magnitude).Max();
                foreach(var solver in manager.solvers)
                {
                    var chain=solver.GetChain(0);var start=chain.transforms[0].position;
                    float reach=Vector3.Distance(start,chain.transforms[1].position)+Vector3.Distance(chain.transforms[1].position,chain.effector.position);
                    if(Vector3.Distance(start,chain.target.position)<reach-.01f)
                        Assert.Less(Vector3.Distance(chain.effector.position,chain.target.position),.04f,"Reachable IK target: "+solver.name);
                }
                var old=RenderTexture.active;RenderTexture.active=skinView.Texture;var image=new Texture2D(512,512,TextureFormat.RGBA32,false);
                try
                {
                    image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();
                    visible=image.GetPixels32().Count(p=>p.a>30);
                    Assert.Greater(visible,1000,"Skin must actually render");Assert.Less(visible,512*512/2,"Transparent background required");
                    Directory.CreateDirectory(".uloop/skin-preview");File.WriteAllBytes(".uloop/skin-preview/"+id+".png",image.EncodeToPNG());
                }
                finally {RenderTexture.active=old;UnityEngine.Object.Destroy(image);}
            }
            Assert.Greater(moved,.03f,"Attack must deform mesh vertices, not just move a flat image");
            var texture=skinView.Texture;app.ReturnToMenu();yield return null;yield return null;
            Assert.AreEqual(baseline,SkinnedActorView.ActiveTargets);Assert.IsTrue(texture==null);
        }
        finally {UnityEngine.Object.Destroy(root);}
        yield return null;
    }
}
