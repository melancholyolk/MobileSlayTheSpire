using System;
using System.Collections.Generic;
using System.Linq;

namespace Spire
{
    // 战斗与回合规则；只产生表现信号，不调用 Unity 动画或界面。
    public sealed partial class Run
    {
        void BeginCombat()
        {
            Phase = Phase.Combat;
            Turn = 0;
            Enemies.Clear();
            bool boss = Map[Current].Kind == RoomKind.Boss, elite = Map[Current].Kind == RoomKind.Elite;
            int visual = boss ? 2 : elite ? 1 : new[]
            {
                0,
                3,
                4,
                5,
                6,
                7,
                8,
                9
            }[Current % 8];
            string[] names =
            {
                "Hollow",
                "Iron Revenant",
                "Ash Warden",
                "Cultist",
                "Goblin",
                "Bandit",
                "Plague Doctor",
                "Frost Giant",
                "Sporekin",
                "Bronze Sentinel"
            };
            Enemies.Add(new Enemy { VisualId = visual, Name = names[visual], Hp = (boss ? 100 : elite ? 48 : 28) * Heroes.Count, MaxHp = (boss ? 100 : elite ? 48 : 28) * Heroes.Count, Damage = boss ? 12 : elite ? 9 : 6 });
            foreach (var h in Heroes)
            {
                h.Strength = 0;
                h.Block = 0;
                h.Draw.Clear();
                h.Draw.AddRange(h.Deck);
                Random.Shuffle(h.Draw);
                h.Hand.Clear();
                h.Discard.Clear();
                h.Exhausted.Clear();
            }

            BeginTurn();
        }

        void BeginTurn()
        {
            Turn++;
            foreach (var h in Heroes)
            {
                h.Block = 0;
                h.Energy = 3;
                h.Ended = !h.Alive;
                if (h.Alive)
                    DrawCards(h, 5);
            }
        }

        void DrawCards(Hero h, int count)
        {
            for (int i = 0; i < count && h.Hand.Count < 10; i++)
            {
                if (h.Draw.Count == 0 && h.Discard.Count > 0)
                {
                    h.Draw.AddRange(h.Discard);
                    h.Discard.Clear();
                    Random.Shuffle(h.Draw);
                    Cue(BattleCueKind.Shuffle, Heroes.IndexOf(h));
                }

                if (h.Draw.Count == 0)
                    break;
                int last = h.Draw.Count - 1;
                h.Hand.Add(h.Draw[last]);
                h.Draw.RemoveAt(last);
                Cue(BattleCueKind.Draw, Heroes.IndexOf(h), art: Cards[h.Hand.Last()].ArtId, cardId: h.Hand.Last());
            }
        }

        static int Hit(int damage, ref int block)
        {
            int absorbed = Math.Min(block, Math.Max(0, damage));
            block -= absorbed;
            return Math.Max(0, damage - absorbed);
        }

        bool Play(int p, int index)
        {
            Hero h = Heroes[p];
            if (Phase != Phase.Combat || h.Ended || index < 0 || index >= h.Hand.Count)
                return false;
            Card c = Cards[h.Hand[index]];
            if (h.Energy < c.Cost)
                return false;
            Cue(BattleCueKind.Play, p, -1, c.ArtId, cardId: c.Id, handIndex: index);
            if (c.Effect != Effect.Attack || !UsePhysicalAttacks)
                Cue(c.Effect == Effect.Attack ? BattleCueKind.Attack : c.Effect == Effect.Block ? BattleCueKind.Guard : BattleCueKind.Skill, p, -1);
            h.Energy -= c.Cost;
            h.Hand.RemoveAt(index);
            if (c.Exhaust)
                h.Exhausted.Add(c.Id);
            else
                h.Discard.Add(c.Id);
            var enemy = Enemies.FirstOrDefault(e => e.Alive);
            int enemyHpBefore = enemy?.Hp ?? 0;
            switch (c.Effect)
            {
                case Effect.Attack:
                    if (UsePhysicalAttacks)
                    {
                        for (int i = 0; i < c.Hits && enemy != null; i++)
                            RequestAttack(p, -1 - Enemies.IndexOf(enemy), c.Amount + h.Strength, c.Id);
                        break;
                    }

                    for (int i = 0; i < c.Hits && enemy != null; i++)
                        enemy.Hp = Math.Max(0, enemy.Hp - Hit(c.Amount + h.Strength, ref enemy.Block));
                    break;
                case Effect.Block:
                    h.Block += c.Amount;
                    break;
                case Effect.Strength:
                    h.Strength += c.Amount;
                    break;
                case Effect.Poison:
                    if (enemy != null)
                        enemy.Poison += c.Amount;
                    break;
                case Effect.Draw:
                    DrawCards(h, c.Amount);
                    break;
                case Effect.Heal:
                    int beforeHeal = h.Hp;
                    h.Hp = Math.Min(h.MaxHp, h.Hp + c.Amount);
                    if (h.Hp > beforeHeal)
                        Cue(BattleCueKind.Heal, p, amount: h.Hp - beforeHeal);
                    break;
                case Effect.Energy:
                    h.Energy += c.Amount;
                    break;
            }

            if (c.Effect == Effect.Attack && enemy != null && !UsePhysicalAttacks)
                Cue(enemy.Alive ? BattleCueKind.Hit : BattleCueKind.Death, -1 - Enemies.IndexOf(enemy), p, amount: enemyHpBefore - enemy.Hp);
            Cue(c.Exhaust ? BattleCueKind.Exhaust : BattleCueKind.Discard, p, art: c.ArtId, source: CardOrigin.PlayArea, cardId: c.Id);
            CheckCombat();
            return true;
        }

        bool End(int p)
        {
            if (Phase != Phase.Combat || Heroes[p].Ended)
                return false;
            Heroes[p].Ended = true;
            if (Heroes.Any(h => h.Alive && !h.Ended))
                return true;
            foreach (var h in Heroes)
            {
                foreach (var id in h.Hand)
                    Cue(BattleCueKind.Discard, Heroes.IndexOf(h), art: Cards[id].ArtId, cardId: id, handIndex: 0);
                h.Discard.AddRange(h.Hand);
                h.Hand.Clear();
            }

            foreach (var e in Enemies.Where(e => e.Alive))
            {
                e.Hp = Math.Max(0, e.Hp - e.Poison);
                e.Poison = Math.Max(0, e.Poison - 1);
            }

            if (CheckCombat())
                return true;
            foreach (var e in Enemies.Where(e => e.Alive))
            {
                if (UsePhysicalAttacks)
                {
                    foreach (var h in Heroes.Where(h => h.Alive))
                        RequestAttack(-1 - Enemies.IndexOf(e), Heroes.IndexOf(h), e.Damage + (Turn % 3 == 0 ? 3 : 0));
                    continue;
                }

                Cue(BattleCueKind.Attack, -1 - Enemies.IndexOf(e));
                foreach (var h in Heroes.Where(h => h.Alive))
                {
                    int before = h.Hp;
                    h.Hp = Math.Max(0, h.Hp - Hit(e.Damage + (Turn % 3 == 0 ? 3 : 0), ref h.Block));
                    Cue(h.Alive ? BattleCueKind.Hit : BattleCueKind.Death, Heroes.IndexOf(h), -1 - Enemies.IndexOf(e), amount: before - h.Hp);
                }
            }

            if (PendingAttacks > 0)
                finishEnemyTurn = true;
            else if (!CheckCombat())
                BeginTurn();
            return true;
        }

        bool CheckCombat()
        {
            if (PendingAttacks > 0)
                return false;
            if (Heroes.All(h => !h.Alive))
            {
                Phase = Phase.Lost;
                return true;
            }

            if (Enemies.Any(e => e.Alive))
                return false;
            if (Map[Current].Kind == RoomKind.Boss)
            {
                Phase = Phase.Won;
                return true;
            }

            foreach (var h in Heroes.Where(h => h.Alive))
                Cue(BattleCueKind.Victory, Heroes.IndexOf(h));
            Phase = Phase.Reward;
            Resolved.Clear();
            for (int i = 0; i < Heroes.Count; i++)
            {
                Rewards[i].Clear();
                if (!Heroes[i].Alive)
                    continue;
                Heroes[i].Gold += 25;
                var pool = Cards.Values.Where(c => c.Class == Heroes[i].Class).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).ToList();
                Random.Shuffle(pool);
                Rewards[i].AddRange(pool.Take(3));
            }

            return true;
        }
    }
}
