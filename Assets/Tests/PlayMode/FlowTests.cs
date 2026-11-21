using System.Collections;
using System.Linq;
using NUnit.Framework;
using Spire;
using SpireLocalization;
using SpirePresentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class FlowTests
{
    static IEnumerator AwaitTask(System.Threading.Tasks.Task task)
    {
        float deadline=Time.realtimeSinceStartup+20;
        while(!task.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
        Assert.IsTrue(task.IsCompleted && !task.IsFaulted,task.Exception?.ToString());
    }
    static IEnumerator AwaitUi(GameApp app)
    {
        float deadline=Time.realtimeSinceStartup+20;
        while(!app.UiReady.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
        Assert.IsTrue(app.UiReady.IsCompleted && !app.UiReady.IsFaulted, app.UiReady.Exception?.ToString());
    }
    [UnityTest] public IEnumerator MenuToMapCombatAndDefeatUsesVisibleButtons()
    {
        ExcelLocalization.Initialize(); var previousLanguage=ExcelLocalization.Language;
        ExcelLocalization.Select("en");
        var root=new GameObject("TestApp"); var app=root.AddComponent<GameApp>(); yield return AwaitUi(app);
        try
        {
            root.GetComponentsInChildren<Button>().Single(b=>b.name=="SinglePlayer").onClick.Invoke();
            yield return AwaitTask(app.UI.Get<GameStartView>(GameStartView.Path).ChoicesReady);
            root.GetComponentsInChildren<Button>().Single(b=>b.name=="Character_0").onClick.Invoke(); yield return null;
            Assert.AreEqual(Phase.Map,app.State.Phase);
            root.GetComponentsInChildren<Button>().Single(b=>b.name=="Node_0").onClick.Invoke(); yield return null;
            Assert.AreEqual(Phase.Combat,app.State.Phase); yield return AwaitTask(app.CardsReady);
            var playable=root.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("Card_") && b.interactable);
            playable.onClick.Invoke(); yield return null; Assert.Less(app.State.Heroes[0].Energy,3);
            for(int i=0;i<40 && app.State.Phase==Phase.Combat;i++)
            { root.GetComponentsInChildren<Button>().Single(b=>b.name=="EndTurn").onClick.Invoke(); yield return null; }
            Assert.AreEqual(Phase.Lost,app.State.Phase);
            Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(t=>t.text=="THE ASCENT ENDS"));
        }
        finally { Object.Destroy(root); ExcelLocalization.Select(previousLanguage); }
        yield return null;
    }
    [UnityTest] public IEnumerator LanguageButtonRefreshesMenuAndCardNamesWithoutChangingRun()
    {
        ExcelLocalization.Initialize(); var previousLanguage=ExcelLocalization.Language;
        ExcelLocalization.Select("en");
        var root=new GameObject("LocalizedApp"); var app=root.AddComponent<GameApp>(); yield return AwaitUi(app);
        try
        {
            root.GetComponentsInChildren<Button>().Single(b=>b.name=="Language").onClick.Invoke(); yield return AwaitTask(app.CardsReady);
            Assert.AreEqual("zh-Hans",ExcelLocalization.Language);
            Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(t=>t.text=="灰烬之巅"));
            app.StartSolo(HeroClass.Warrior); app.Send("vote",0); yield return AwaitTask(app.CardsReady);
            var state=app.State; int revision=state.Revision;
            var first=state.Cards[state.Heroes[0].Hand[0]];
            Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(t=>t.text.StartsWith(ExcelLocalization.Text(first.NameId))));
            root.GetComponentsInChildren<Button>().Single(b=>b.name=="Language").onClick.Invoke(); yield return AwaitTask(app.CardsReady);
            Assert.AreSame(state,app.State); Assert.AreEqual(revision,state.Revision);
            Assert.AreEqual("en",ExcelLocalization.Language);
            Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(t=>t.text.StartsWith(ExcelLocalization.Text(first.NameId))));
        }
        finally { Object.Destroy(root); ExcelLocalization.Select(previousLanguage); }
        yield return null;
    }
}
