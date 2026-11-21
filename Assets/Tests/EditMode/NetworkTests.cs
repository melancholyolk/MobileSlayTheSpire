using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
namespace Spire.Tests
{
    public class NetworkTests
    {
        static void Wait(CoopSession host,CoopSession client,Func<bool> done)
        {
            var until=DateTime.UtcNow.AddSeconds(5);
            while(DateTime.UtcNow<until) { host.Pump(); client.Pump(); if(done()) return; Thread.Sleep(2); }
            Assert.Fail("Network timeout: "+host.Status+" / "+client.Status);
        }
        [Test] public void RealTcpSynchronizesRejectsIllegalInputAndReconnects()
        {
            byte[] table=Resources.Load<TextAsset>("Tables/cards").bytes;
            using(var host=new CoopSession(table)) using(var client=new CoopSession(table))
            {
                host.Host(0,42); client.Join("127.0.0.1",host.Port);
                Wait(host,client,()=>host.Connected && client.Connected);
                Assert.IsTrue(host.Submit("vote",0)); Wait(host,client,()=>client.Run.Revision==1);
                client.Submit("vote",0); Wait(host,client,()=>client.Run.Phase==Phase.Combat);
                Assert.AreEqual(host.Run.Heroes[1].Hand,client.Run.Heroes[1].Hand);
                int revision=host.Run.Revision; client.Submit("play",999);
                Wait(host,client,()=>client.Status.Contains("legal")); Assert.AreEqual(revision,host.Run.Revision);
                host.Submit("end",0); Wait(host,client,()=>client.Run.Revision==revision+1);
                client.Submit("end",0); Wait(host,client,()=>client.Run.Turn==2);
                Assert.AreEqual(host.Run.Heroes[0].Hp,client.Run.Heroes[0].Hp);
                string token=client.ResumeToken; int port=host.Port; client.Dispose();
                Wait(host,client,()=>!host.Connected); Assert.IsFalse(host.Submit("end",0));
                client.Join("127.0.0.1",port,token); Wait(host,client,()=>host.Connected && client.Connected);
                Assert.AreEqual(host.Run.Revision,client.Run.Revision);
                Assert.AreEqual(host.Run.Random.State,client.Run.Random.State);
                Assert.AreEqual(host.Run.Heroes[1].Hand,client.Run.Heroes[1].Hand);
            }
        }
        [Test] public void RealTcpRunsFromMapUntilDefeatWithMatchingState()
        {
            byte[] table=Resources.Load<TextAsset>("Tables/cards").bytes;
            using(var host=new CoopSession(table)) using(var client=new CoopSession(table))
            {
                host.Host(0,9); client.Join("127.0.0.1",host.Port); Wait(host,client,()=>client.Connected);
                host.Submit("vote",0); Wait(host,client,()=>client.Run.Revision==host.Run.Revision);
                client.Submit("vote",0); Wait(host,client,()=>client.Run.Phase==Phase.Combat);
                for(int i=0;i<40 && host.Run.Phase==Phase.Combat;i++)
                {
                    int turn=host.Run.Turn;
                    if(host.Run.Heroes[0].Alive) { host.Submit("end",0); Wait(host,client,()=>client.Run.Revision==host.Run.Revision); }
                    if(host.Run.Heroes[1].Alive) client.Submit("end",0);
                    Wait(host,client,()=>host.Run.Turn>turn || host.Run.Phase==Phase.Lost);
                    Wait(host,client,()=>client.Run.Revision==host.Run.Revision);
                }
                Assert.AreEqual(Phase.Lost,host.Run.Phase); Assert.AreEqual(host.Run.Phase,client.Run.Phase);
                Assert.AreEqual(host.Run.Heroes.Select(h=>h.Hp),client.Run.Heroes.Select(h=>h.Hp));
            }
        }
    }
}
