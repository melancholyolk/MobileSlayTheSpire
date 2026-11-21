using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using Spire;
using SpirePresentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
public class LobbyUiTests
{
    static IEnumerator Until(Func<bool> condition,[System.Runtime.CompilerServices.CallerLineNumber] int line=0)
    {
        float end=Time.realtimeSinceStartup+20;
        while(!condition() && Time.realtimeSinceStartup<end) yield return null;
        Assert.IsTrue(condition(),"Lobby UI timeout at line "+line);
    }
    static Button Button(GameObject root,string name)=>root.GetComponentsInChildren<Button>().Single(b=>b.name==name);
    [UnityTest] public IEnumerator SameCharacterReadyStartAndMapVote()
    {
        var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); int port=((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var a=new GameObject("HostUi"); var b=new GameObject("GuestUi");
        var host=a.AddComponent<GameApp>(); var guest=b.AddComponent<GameApp>();
        try
        {
            yield return Until(()=>host.UiReady.IsCompleted && guest.UiReady.IsCompleted);
            Assert.IsFalse(host.UiReady.IsFaulted,host.UiReady.Exception?.ToString()); Assert.IsFalse(guest.UiReady.IsFaulted,guest.UiReady.Exception?.ToString());
            host.Host(port); guest.Join(false,port);
            yield return Until(()=>host.Network.Connected && guest.Network.Connected);
            yield return Until(()=>host.UI.Get<GameStartView>(1).ChoicesReady.IsCompleted && guest.UI.Get<GameStartView>(1).ChoicesReady.IsCompleted);
            Button(a,"Character_0").onClick.Invoke(); Button(b,"Character_0").onClick.Invoke();
            yield return Until(()=>host.Network.SelectedCharacter(1)==0 && guest.Network.SelectedCharacter(0)==0);
            Assert.IsFalse(host.Network.CanStart); Assert.IsFalse(guest.Network.CanStart);
            Button(b,"Ready").onClick.Invoke(); yield return Until(()=>host.Network.CanStart);
            Button(a,"StartGame").onClick.Invoke(); yield return Until(()=>host.State!=null && guest.State!=null);
            Assert.AreEqual(Phase.Map,host.State.Phase); Assert.AreEqual(host.State.Heroes[0].Class,host.State.Heroes[1].Class);
            Button(a,"Node_0").onClick.Invoke(); Assert.AreEqual(Phase.Map,host.State.Phase);
            yield return Until(()=>guest.State.Revision==host.State.Revision);
            Button(b,"Node_0").onClick.Invoke(); yield return Until(()=>host.State.Phase==Phase.Combat && guest.State.Phase==Phase.Combat);
            yield return Until(()=>host.CardsReady.IsCompleted && guest.CardsReady.IsCompleted);
            Assert.IsFalse(host.CardsReady.IsFaulted); Assert.IsFalse(guest.CardsReady.IsFaulted);
            host.ReturnToMenu(); guest.ReturnToMenu();
            Button(a,"SinglePlayer").onClick.Invoke(); yield return Until(()=>host.UI.Get<GameStartView>(1).ChoicesReady.IsCompleted);
            Button(a,"Character_1").onClick.Invoke(); Assert.AreEqual(Phase.Map,host.State.Phase);
        }
        finally { UnityEngine.Object.Destroy(a); UnityEngine.Object.Destroy(b); }
        yield return null;
    }
}


