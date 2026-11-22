using System;
using System.Linq;
using NUnit.Framework;
using SpireAnimation;
using UnityEngine;
public class BodyAssemblyTests
{
    static BodyPartDefinition Part(int id=1)=>new BodyPartDefinition {Id=id,Role=1,Ports=new[]{new BodyPort {Id=0,Type=2,Accepts=1},new BodyPort {Id=1,Type=1,Accepts=2,Position=new Vector2(0,10)},new BodyPort {Id=2,Type=1,Accepts=2,Position=new Vector2(5,0)}}};
    [Test] public void LongChainsDetachAndMergeWithoutHardcodedLimbLimits()
    {
        var body=new BodyAssembly(new[]{Part()},256);
        for(int i=0;i<200;i++) body.Add(new BodyNode {Id=i,Part=1,Parent=i-1,ParentPort=1,Port=0});
        Assert.AreEqual(200,body.Count);body.MergeGroup(50,7);Assert.AreEqual(150,body.Nodes.Count(n=>n.Group==7));
        Assert.AreEqual(new Vector2(0,10),body.LocalPosition(100));
        var detached=body.Detach(50);Assert.AreEqual(150,detached.Length);Assert.AreEqual(-1,detached[0].Parent);Assert.AreEqual(50,body.Count);
        var other=new BodyAssembly(new[]{Part()},256);foreach(var n in detached) other.Add(n);Assert.AreEqual(150,other.Count);
    }
    [Test] public void InvalidConnectionsAreAtomicAndCapacityIsEnforced()
    {
        var body=new BodyAssembly(new[]{Part()},3);body.Add(new BodyNode {Id=0,Part=1});body.Add(new BodyNode {Id=1,Part=1,Parent=0,ParentPort=1});
        Assert.Throws<ArgumentException>(()=>body.Add(new BodyNode {Id=2,Part=1,Parent=0,ParentPort=1}));
        Assert.Throws<ArgumentException>(()=>body.Add(new BodyNode {Id=2,Part=1,Parent=1,ParentPort=0,Port=1}));
        body.Add(new BodyNode {Id=2,Part=1,Parent=1,ParentPort=1});
        Assert.Throws<ArgumentException>(()=>body.Reparent(1,2,1,0));Assert.AreEqual(0,body.Node(1).Parent);
        Assert.Throws<ArgumentException>(()=>body.Add(new BodyNode {Id=3,Part=1,Parent=0,ParentPort=2}));
        var copy=body.Node(1);copy.Parent=2;Assert.AreEqual(0,body.Node(1).Parent);
    }
    [Test] public void PairRecipesRetargetAndMergedGroupsDriveRealJoints()
    {
        var root=new GameObject("Rig");root.SetActive(false);
        var a=new GameObject("A");a.transform.SetParent(root.transform);var b=new GameObject("B");b.transform.SetParent(a.transform);
        var view=root.AddComponent<BodyAssemblyView>();view.Parts=new[]{Part()};view.Initial=new[]{new BodyNode {Id=0,Part=1},new BodyNode {Id=1,Part=1,Parent=0,ParentPort=1}};view.Bones=new[]{a.transform,b.transform};
        view.Recipes=new[]{new JointPairMotion {Motion=MotionId.Attack,ParentRole=1,ChildRole=1,Duration=1,Peak=.5f,Middle=60}};
        try
        {
            view.Initialize();root.SetActive(true);view.MergeGroup(1,4);view.PlayGroup(4,MotionId.Attack,0);
            var skeleton=root.GetComponent<SkeletonView>();skeleton.Tick(.5f);
            Assert.That(Mathf.DeltaAngle(0,b.transform.localEulerAngles.z),Is.EqualTo(60).Within(1));Assert.That(Mathf.DeltaAngle(0,a.transform.localEulerAngles.z),Is.EqualTo(0).Within(1));
            for(int i=0;i<20;i++) view.PlayGroup(4,MotionId.Attack);Assert.AreEqual(10,view.OwnedClipCount);Assert.LessOrEqual(skeleton.ActivePlayableCount,3);
            root.SetActive(false);view.Release();Assert.AreEqual(0,view.OwnedClipCount);Assert.IsFalse(skeleton.GraphValid);
            root.SetActive(true);view.Initialize();Assert.AreEqual(10,view.OwnedClipCount);Assert.IsTrue(skeleton.GraphValid);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [Test] public void ProductionBinaryAssembliesValidateAllFourteenActors()
    {
        byte[] Read(string name)=>System.IO.File.ReadAllBytes("Assets/Resources/BusinessTables/"+name+".bytes");
        var c=BodyCatalog.Read(Read("BodyParts"),Read("BodyPorts"),Read("BodyNodes"),Read("BodyMotions"));
        Assert.AreEqual(14,c.Nodes.Select(n=>n.Owner).Distinct().Count());
        foreach(var group in c.Nodes.GroupBy(n=>n.Owner))
        {
            var body=new BodyAssembly(c.Parts);foreach(var row in group) body.Add(row.Node);Assert.AreEqual(11,body.Count);
        }
        Assert.AreEqual(110,c.Motions.Length);
    }
}

