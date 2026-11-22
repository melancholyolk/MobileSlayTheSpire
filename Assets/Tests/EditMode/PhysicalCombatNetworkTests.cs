using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Spire;
using UnityEngine;

public class PhysicalCombatNetworkTests
{
    static void Wait(CoopSession host,CoopSession guest,Func<bool> done)
    {
        var until=DateTime.UtcNow.AddSeconds(5);
        while(DateTime.UtcNow<until) { host.Pump();guest.Pump();if(done()) return;Thread.Sleep(2); }
        Assert.Fail(host.Status+" / "+guest.Status);
    }
    [Test] public void HostImpactAndMissReplayAcrossReconnect()
    {
        using(var host=new CoopSession(Resources.Load<TextAsset>("Tables/cards").bytes))
        using(var guest=new CoopSession(Resources.Load<TextAsset>("Tables/cards").bytes))
        {
            host.Host(0,42);guest.Join("127.0.0.1",host.Port);Wait(host,guest,()=>guest.Connected);
            host.SelectCharacter(0);guest.SelectCharacter(1);Wait(host,guest,()=>host.SelectedCharacter(1)==1);
            guest.SetReady(true);Wait(host,guest,()=>host.CanStart);Assert.IsTrue(host.StartGame());Wait(host,guest,()=>guest.Run!=null);
            host.Run.UsePhysicalAttacks=true;
            Assert.IsTrue(host.Submit("vote",0));Wait(host,guest,()=>guest.Run.Revision==1);
            guest.Submit("vote",0);Wait(host,guest,()=>guest.Run.Phase==Phase.Combat);
            host.Submit("end",0);Wait(host,guest,()=>guest.Run.Heroes[0].Ended);
            guest.Submit("end",0);Wait(host,guest,()=>guest.Run.PendingAttacks==2);
            var attacks=host.Run.Cues.Where(c=>c.AttackId>0).ToArray();
            Assert.IsFalse(guest.ResolveAttack(attacks[0].AttackId,0));Assert.IsFalse(guest.Submit("impact",attacks[0].AttackId));
            int hp=host.Run.Heroes[0].Hp;
            Assert.IsTrue(host.ResolveAttack(attacks[0].AttackId,-1));Wait(host,guest,()=>guest.Run.PendingAttacks==1);
            Assert.AreEqual(hp,guest.Run.Heroes[0].Hp);
            Assert.IsTrue(host.ResolveAttack(attacks[1].AttackId,3));Wait(host,guest,()=>guest.Run.Turn==2);
            Assert.AreEqual(host.Run.Heroes.Select(h=>h.Hp),guest.Run.Heroes.Select(h=>h.Hp));
            var token=guest.ResumeToken;int port=host.Port;guest.Dispose();Wait(host,guest,()=>!host.Connected);
            guest.Join("127.0.0.1",port,token);Wait(host,guest,()=>guest.Connected);
            Assert.AreEqual(host.Run.Revision,guest.Run.Revision);
            Assert.AreEqual(host.Run.Heroes.Select(h=>h.Hp),guest.Run.Heroes.Select(h=>h.Hp));
            Assert.AreEqual(0,guest.Run.PendingAttacks);
        }
    }
}
