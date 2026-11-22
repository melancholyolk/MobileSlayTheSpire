using System;
using System.Collections.Generic;
using System.Linq;

namespace Spire
{
    public readonly struct AttackRequest
    {
        public readonly int Id, Source, Target, Damage;
        public readonly string CardId;
        public AttackRequest(int id,int source,int target,int damage,string cardId)
        { Id=id; Source=source; Target=target; Damage=damage; CardId=cardId; }
    }

    public sealed partial class Run
    {
        // Headless simulations may retain immediate resolution. The live game enables this.
        public bool UsePhysicalAttacks;
        readonly Dictionary<int,AttackRequest> attacks=new Dictionary<int,AttackRequest>();
        int nextAttack;
        bool finishEnemyTurn;
        public int PendingAttacks=>attacks.Count;
        public bool TryGetAttack(int id,out AttackRequest attack)=>attacks.TryGetValue(id,out attack);
        void RequestAttack(int source,int target,int damage,string cardId=null)
        {
            var attack=new AttackRequest(++nextAttack,source,target,damage,cardId);
            attacks.Add(attack.Id,attack);
            cues.Add(new BattleCue(BattleCueKind.Attack,source,target,amount:damage,cardId:cardId,attackId:attack.Id));
        }
        bool ActorAlive(int actor)=>actor>=0?Heroes[actor].Alive:Enemies[-1-actor].Alive;
        // Only authority submits an impact. -1 is a miss; body ids are nonnegative.
        public bool ResolveAttack(int id,int bodyPart)
        {
            if(bodyPart < -1 || !attacks.TryGetValue(id,out var attack)) return false;
            // Resolve in presentation order, including multi-hit attacks.
            if(id!=attacks.Keys.Min()) return false;
            cues.Clear(); attacks.Remove(id);
            if(bodyPart>=0 && ActorAlive(attack.Source) && ActorAlive(attack.Target))
            {
                if(attack.Target>=0)
                {
                    var hero=Heroes[attack.Target];
                    hero.Hp=Math.Max(0,hero.Hp-Hit(attack.Damage,ref hero.Block));
                }
                else
                {
                    var enemy=Enemies[-1-attack.Target];
                    enemy.Hp=Math.Max(0,enemy.Hp-Hit(attack.Damage,ref enemy.Block));
                }
                cues.Add(new BattleCue(ActorAlive(attack.Target)?BattleCueKind.Hit:BattleCueKind.Death,
                    attack.Target,attack.Source,amount:attack.Damage,cardId:attack.CardId,bodyPart:bodyPart));
            }
            if(attacks.Count==0)
            {
                bool ended=CheckCombat();
                if(finishEnemyTurn && !ended) BeginTurn();
                finishEnemyTurn=false;
            }
            Revision++;
            return true;
        }
    }
}
