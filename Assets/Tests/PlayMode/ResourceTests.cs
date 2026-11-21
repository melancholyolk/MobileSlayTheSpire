using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SpireResources;
using UnityEngine;
using UnityEngine.TestTools;
public class ResourceTests
{
    static IEnumerator Wait(Task task)
    {
        float end=Time.realtimeSinceStartup+20;
        while(!task.IsCompleted && Time.realtimeSinceStartup<end) yield return null;
        Assert.IsTrue(task.IsCompleted,"Resource operation timeout");
        Assert.IsFalse(task.IsFaulted,task.Exception?.ToString());
    }
    [UnityTest] public IEnumerator SharedCharacterReferencesAndDisposeDuringLoad()
    {
        var root=new GameObject("ResourcesTest"); var rm=ResourceManager.Create(root);
        var a=rm.CreateScope(0); var b=rm.CreateScope(0);
        try
        {
            var first=a.LoadAsync<Sprite>(1001); var second=b.LoadAsync<Sprite>(1001);
            yield return Wait(Task.WhenAll(first,second));
            Assert.AreSame(first.Result,second.Result); Assert.AreEqual(1,rm.LoadedAssetCount);
            yield return Wait(a.DisposeAsync()); Assert.AreEqual(1,rm.LoadedAssetCount);
            Assert.IsNotNull(second.Result);
            yield return Wait(b.DisposeAsync()); Assert.AreEqual(0,rm.LoadedAssetCount);
            var c=rm.CreateScope(1); var load=c.LoadAsync<Sprite>(2001); var dispose=c.DisposeAsync();
            yield return Wait(dispose);
            Assert.IsTrue(load.IsCanceled || load.IsCompletedSuccessfully); Assert.AreEqual(0,rm.LoadedAssetCount);
            var wrong=rm.CreateScope(0); var rejected=wrong.LoadAsync<Sprite>(2001);
            Assert.IsTrue(rejected.IsFaulted); yield return Wait(wrong.DisposeAsync());
        }
        finally { UnityEngine.Object.Destroy(root); }
        yield return null;
    }
    [UnityTest] public IEnumerator PoolIsBoundedAndOwnerUnloadsEveryInstance()
    {
        var root=new GameObject("PoolTest"); var rm=ResourceManager.Create(root); var scope=rm.CreateScope();
        try
        {
            var objects=new List<GameObject>();
            for(int i=0;i<16;i++) { var task=scope.InstantiateAsync(3,root.transform); yield return Wait(task); objects.Add(task.Result); }
            foreach(var go in objects) scope.Return(go);
            yield return null;
            Assert.AreEqual(ResourceManager.MaxPoolPerId,scope.InstanceCount);
            var next=scope.InstantiateAsync(3,root.transform); yield return Wait(next);
            Assert.IsTrue(objects.Contains(next.Result),"Expected pooled instance reuse");
            for(int i=scope.InstanceCount;i<ResourceManager.MaxInstances;i++)
            { var task=scope.InstantiateAsync(3,root.transform,false); yield return Wait(task); }
            var overflow=scope.InstantiateAsync(3,root.transform,false); yield return null;
            Assert.IsTrue(overflow.IsFaulted); Assert.AreEqual(ResourceManager.MaxInstances,rm.InstanceCount);
            yield return Wait(scope.DisposeAsync()); yield return null;
            Assert.AreEqual(0,rm.InstanceCount); Assert.AreEqual(0,rm.LoadedAssetCount); Assert.AreEqual(0,rm.ReservedBytes);
            Assert.IsTrue(objects.TrueForAll(go=>go==null));
        }
        finally { UnityEngine.Object.Destroy(root); }
        yield return null;
    }
}
