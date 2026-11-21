using System.Collections;
using System.Linq;
using NUnit.Framework;
using SpireUI;
using SpirePresentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
public class UiFrameworkTests
{
    [UnityTest] public IEnumerator AsyncCacheHideCloseAndInvalidLoads()
    {
        var root=new GameObject("UiTest",typeof(RectTransform));
        var manager=root.AddComponent<UiManager>(); manager.Configure(root.transform);
        try
        {
            var first=manager.OpenAsync<GameplayView>(GameplayView.Path);
            var second=manager.OpenAsync<GameplayView>(GameplayView.Path);
            float deadline=Time.realtimeSinceStartup+20;
            while(!first.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsTrue(first.IsCompleted && !first.IsFaulted, first.Exception?.ToString());
            Assert.AreSame(first.Result,second.Result); Assert.AreEqual(1,manager.Count);
            var view=first.Result; Assert.IsTrue(view.IsVisible);
            manager.Hide(GameplayView.Path); Assert.IsFalse(view.gameObject.activeSelf);
            var reopen=manager.OpenAsync<GameplayView>(GameplayView.Path);
            Assert.AreSame(view,reopen.Result); Assert.IsTrue(view.IsVisible);
            var child=new GameObject("Dynamic",typeof(UiElement)); child.transform.SetParent(view.transform);
            var element=child.GetComponent<UiElement>(); view.Register(element);
            Assert.AreSame(view,element.Owner); Assert.Contains(element,view.Elements.ToArray());
            Object.Destroy(child); yield return null;
            Assert.IsFalse(view.Elements.Contains(element));
            var wrong=manager.OpenAsync<GameStartView>(GameplayView.Path); Assert.IsTrue(wrong.IsFaulted);
            manager.Close(GameplayView.Path); Assert.AreEqual(0,manager.Count);
            yield return null; Assert.IsTrue(view==null);
            var missing=manager.OpenAsync<GameplayView>("UI/DoesNotExist");
            deadline=Time.realtimeSinceStartup+20;
            while(!missing.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsTrue(missing.IsFaulted); Assert.AreEqual(0,manager.Count);
            var cancelled=manager.OpenAsync<GameplayView>(GameplayView.Path); manager.Close(GameplayView.Path);
            yield return null;
            Assert.IsTrue(cancelled.IsCanceled || cancelled.IsCompletedSuccessfully);
            Assert.AreEqual(0,manager.Count); Assert.AreEqual(0,root.transform.childCount);
        }
        finally { Object.Destroy(root); }
        yield return null;
    }
    [Test] public void StartPrefabOwnsAllGraphicsAndSelectables()
    {
        var prefab=Resources.Load<GameObject>(GameStartView.Path);
        Assert.IsNotNull(prefab); Assert.IsNotNull(prefab.GetComponent<GameStartView>());
        foreach(var graphic in prefab.GetComponentsInChildren<Graphic>(true))
            Assert.IsTrue(graphic.GetComponents<MonoBehaviour>().Any(c=>c is IUiElement),graphic.name);
        foreach(var selectable in prefab.GetComponentsInChildren<Selectable>(true))
            Assert.IsTrue(selectable.GetComponents<MonoBehaviour>().Any(c=>c is IUiElement),selectable.name);
        Assert.AreEqual(5,prefab.GetComponentsInChildren<Button>(true).Length);
    }
}
