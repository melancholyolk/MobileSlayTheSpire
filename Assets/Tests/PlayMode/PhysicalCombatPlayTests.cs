using System.Collections;
using System.Linq;
using NUnit.Framework;
using Spire;
using SpireAnimation;
using SpirePresentation;
using UnityEngine;
using UnityEngine.TestTools;

public class PhysicalCombatPlayTests
{
    [UnityTest] public IEnumerator LiveAttacksHitBodyCollidersAndInvulnerabilityMisses()
    {
        var root=new GameObject("Physical combat test");var app=root.AddComponent<GameApp>();
        try
        {
            float end=Time.realtimeSinceStartup+30;
            while(!app.UiReady.IsCompleted && Time.realtimeSinceStartup<end) yield return null;
            Assert.IsTrue(app.UiReady.IsCompletedSuccessfully);
            app.StartSolo(HeroClass.Warrior);app.Send("vote",0);
            end=Time.realtimeSinceStartup+30;
            while((!app.BattleVisuals.Ready.IsCompleted || app.BattleVisuals.PendingCount>0) && Time.realtimeSinceStartup<end) yield return null;
            Assert.IsTrue(app.BattleVisuals.Ready.IsCompletedSuccessfully);
            Assert.AreEqual(0,app.BattleVisuals.PendingCount);
            var bodies=root.GetComponentsInChildren<CombatBody>();Assert.AreEqual(2,bodies.Length);
            foreach(var body in bodies) { Assert.Greater(body.Parts.Length,0);Assert.IsTrue(body.Parts.All(p=>p.Collider.isTrigger)); }
            var skin=root.GetComponentInChildren<SkinnedActorView>();Assert.IsNotNull(skin);
            Assert.IsTrue(skin.GetComponent<CombatBody>().Parts.Any(p=>p.ReactionTarget!=null));
            foreach(var part in skin.GetComponent<CombatBody>().Parts)
            {
                var sprite=skin.GetComponentsInChildren<SpriteRenderer>().Single(s=>s.name=="Skin_"+part.name).sprite;
                Assert.That(part.Collider.size,Is.EqualTo((Vector2)sprite.bounds.size),"Skin collider uses sprite units, not scaled UI units");
            }
            var hero=app.State.Heroes[0];
            var attack=app.State.Cards.Values.First(c=>c.Class==HeroClass.Warrior && c.Effect==Effect.Attack);
            var enemy=bodies.Single(b=>b.GetComponent<SkinnedActorView>()==null);
            bool attackStarted=false;
            app.BattleVisuals.Lifecycle+=signal=> { if(signal.Cue.AttackId>0 && signal.Phase==PresentationPhase.Started) attackStarted=true; };
            var heroBody=skin.GetComponent<CombatBody>();
            var profile=heroBody.DefaultAttack;profile.Windup=.4f;heroBody.DefaultAttack=profile;
            for(int pass=0;pass<3;pass++)
            {
                enemy.Invulnerable=pass==1;hero.Hand.Clear();hero.Hand.Add(attack.Id);hero.Energy=10;
                attackStarted=false;bool moved=false;var start=enemy.transform.position;
                int hp=app.State.Enemies[0].Hp;
                app.Send("play",0);
                Assert.AreEqual(hp,app.State.Enemies[0].Hp,"Damage waits for collision");
                end=Time.realtimeSinceStartup+30;
                while((app.State.PendingAttacks>0 || app.BattleVisuals.PendingCount>0) && Time.realtimeSinceStartup<end)
                {
                    if(pass==2 && attackStarted && !moved) { enemy.transform.position+=Vector3.right*2000;moved=true; }
                    yield return null;
                }
                Assert.AreEqual(0,app.State.PendingAttacks);Assert.AreEqual(0,app.BattleVisuals.PendingCount);
                if(pass==0) Assert.Less(app.State.Enemies[0].Hp,hp);else Assert.AreEqual(hp,app.State.Enemies[0].Hp);
                enemy.transform.position=start;
            }
            int heroHp=hero.Hp;app.Send("end",0);
            end=Time.realtimeSinceStartup+30;
            while((app.State.PendingAttacks>0 || app.BattleVisuals.PendingCount>0) && Time.realtimeSinceStartup<end) yield return null;
            Assert.AreEqual(0,app.State.PendingAttacks);Assert.Less(hero.Hp,heroHp,"Enemy hits the offscreen skinned rig");
        }
        finally { Object.Destroy(root); }
        yield return null;
    }
    [UnityTest] public IEnumerator PartReactionOffsetsOnlyItsTargetAndRestoresWithoutDrift()
    {
        var root=new GameObject("IK reaction test");var target=new GameObject("IK target");var other=new GameObject("Other IK target");
        try
        {
            target.transform.position=new Vector3(2,3,0);other.transform.position=new Vector3(5,6,0);
            var part=root.AddComponent<BodyPartHurtbox>();part.ReactionTarget=target.transform;
            part.React(Vector3.right,.25f);
            yield return null;yield return new WaitForEndOfFrame();
            Assert.Greater(target.transform.position.x,2);Assert.AreEqual(new Vector3(5,6,0),other.transform.position);
            yield return new WaitForSecondsRealtime(.3f);yield return null;
            Assert.That(Vector3.Distance(target.transform.position,new Vector3(2,3,0)),Is.LessThan(.0001f));
            part.React(Vector3.up,.25f);yield return null;yield return new WaitForEndOfFrame();
            part.enabled=false;
            Assert.That(Vector3.Distance(target.transform.position,new Vector3(2,3,0)),Is.LessThan(.0001f));
        }
        finally { Object.Destroy(root);Object.Destroy(target);Object.Destroy(other); }
        yield return null;
    }
}
