using System.Collections;
using System.Linq;
using NUnit.Framework;
using Spire;
using SpirePresentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class FlowTests
{
    [UnityTest] public IEnumerator MenuToMapCombatAndDefeatUsesVisibleButtons()
    {
        var root=new GameObject("TestApp"); var app=root.AddComponent<GameApp>(); yield return null;
        try
        {
            root.GetComponentsInChildren<Button>().Single(b=>b.name=="Warrior").onClick.Invoke(); yield return null;
            Assert.AreEqual(Phase.Map,app.State.Phase);
            root.GetComponentsInChildren<Button>().Single(b=>b.name=="Node_0").onClick.Invoke(); yield return null;
            Assert.AreEqual(Phase.Combat,app.State.Phase);
            var playable=root.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("Card_") && b.interactable);
            playable.onClick.Invoke(); yield return null; Assert.Less(app.State.Heroes[0].Energy,3);
            for(int i=0;i<40 && app.State.Phase==Phase.Combat;i++)
            { root.GetComponentsInChildren<Button>().Single(b=>b.name=="EndTurn").onClick.Invoke(); yield return null; }
            Assert.AreEqual(Phase.Lost,app.State.Phase);
            Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(t=>t.text=="THE ASCENT ENDS"));
        }
        finally { Object.Destroy(root); }
        yield return null;
    }
}
