using System;
using System.Collections.Generic;
using System.Linq;

namespace Spire
{
    public enum HeroClass { Warrior, Hunter }
    public enum Phase { Map, Combat, Reward, Camp, Shop, Event, Won, Lost }
    public enum RoomKind { Battle, Elite, Camp, Shop, Event, Boss }
    public enum Effect { Attack, Block, Strength, Poison, Draw, Heal, Energy }

    [Serializable] public sealed class Card
    {
        public string Id;
        public int NameId, ArtId, CharacterNameId, CharacterMaxHp=80, PortraitId;
        public HeroClass Class;
        public int Cost, Amount, Hits = 1, StarterCopies;
        public Effect Effect;
        public bool Exhaust;
    }

    // Explicit PRNG state is portable across Mono, IL2CPP and network peers.
    public sealed class RandomSource
    {
        public uint State { get; private set; }
        public RandomSource(uint seed) { State = seed == 0 ? 1u : seed; }
        public int Next(int limit)
        {
            if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
            uint x = State; x ^= x << 13; x ^= x >> 17; x ^= x << 5; State = x;
            return (int)(x % (uint)limit);
        }
        public void Shuffle<T>(List<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--) { int j = Next(i + 1); T t = items[i]; items[i] = items[j]; items[j] = t; }
        }
    }

    [Serializable] public sealed class Hero
    {
        public HeroClass Class;
        public int Hp, MaxHp, Block, Energy, Strength, Gold = 50;
        public bool Ended;
        public List<string> Deck = new List<string>(), Draw = new List<string>(), Hand = new List<string>(), Discard = new List<string>(), Exhausted = new List<string>();
        public bool Alive => Hp > 0;
    }
    [Serializable] public sealed class Enemy
    {
        public string Name;
        public int Hp, MaxHp, Block, Poison, Damage;
        public int VisualId;
        public bool Alive => Hp > 0;
    }
    [Serializable] public sealed class MapNode
    {
        public int Id, Floor, Lane;
        public RoomKind Kind;
        public List<int> Next = new List<int>();
    }

    // All state changes pass through validated commands. No Unity dependency.
    public sealed class Run
    {
        public readonly List<int> Route=new List<int>();
        public readonly List<MapNode> Map = new List<MapNode>();
        public readonly List<Hero> Heroes = new List<Hero>();
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly Dictionary<int, int> Votes = new Dictionary<int, int>();
        public readonly List<List<string>> Rewards = new List<List<string>>();
        public readonly HashSet<int> Resolved = new HashSet<int>();
        public readonly Dictionary<string, Card> Cards;
        public readonly RandomSource Random;
        public Phase Phase { get; private set; } = Phase.Map;
        public int Current { get; private set; } = -1;
        public int Turn { get; private set; }
        public int Revision { get; private set; }
        public const int Floors = 8;
        readonly List<BattleCue> cues=new List<BattleCue>();
        public IReadOnlyList<BattleCue> Cues=>cues;
        void Cue(BattleCueKind kind,int actor,int target=0,int art=0,int amount=0,CardOrigin source=CardOrigin.Automatic) => cues.Add(new BattleCue(kind,actor,target,art,amount,source));

        public Run(IEnumerable<Card> cards, uint seed, params HeroClass[] classes)
        {
            Cards = cards.ToDictionary(c => c.Id);
            if (classes.Length < 1 || classes.Length > 4) throw new ArgumentException("Party size must be 1..4");
            Random = new RandomSource(seed);
            foreach (var cls in classes)
            {
                var h = new Hero { Class = cls, MaxHp = Cards.Values.First(c=>c.Class==cls).CharacterMaxHp };
                h.Hp = h.MaxHp;
                foreach (var card in Cards.Values.Where(c => c.Class == cls).OrderBy(c => c.Id, StringComparer.Ordinal))
                    for (int n = 0; n < card.StarterCopies; n++) h.Deck.Add(card.Id);
                if (h.Deck.Count < 5) throw new ArgumentException("Each class needs at least five starter cards");
                Heroes.Add(h);
                Rewards.Add(new List<string>());
            }
            for (int f = 0; f < Floors; f++)
                for (int l = 0; l < (f == Floors - 1 ? 1 : 3); l++)
                {
                    var kind = f == Floors - 1 ? RoomKind.Boss : f == 0 ? RoomKind.Battle : f == Floors - 2 ? RoomKind.Camp : (RoomKind)Random.Next(5);
                    Map.Add(new MapNode { Id = Map.Count, Floor = f, Lane = l, Kind = kind });
                }
            foreach (var node in Map)
                node.Next.AddRange(Map.Where(n => n.Floor == node.Floor + 1 && (n.Kind == RoomKind.Boss || Math.Abs(n.Lane - node.Lane) <= 1)).Select(n => n.Id));
        }

        public IEnumerable<int> Choices => Current < 0 ? Map.Where(n => n.Floor == 0).Select(n => n.Id) : Map[Current].Next;
        public bool Apply(int player, string action, int argument, out string error)
        {
            error = null; cues.Clear();
            if (player < 0 || player >= Heroes.Count || !Heroes[player].Alive) { error = "Invalid or defeated player"; return false; }
            bool ok = false;
            switch (action)
            {
                case "vote": ok = Vote(player, argument); break;
                case "play": ok = Play(player, argument); break;
                case "end": ok = End(player); break;
                case "reward": ok = Reward(player, argument); break;
                case "room": ok = ResolveRoom(player, argument); break;
            }
            if (ok) Revision++; else error = "Action is not legal in the current state";
            return ok;
        }

        bool Vote(int player, int node)
        {
            if (Phase != Phase.Map || !Choices.Contains(node)) return false;
            Votes[player] = node;
            if (Heroes.Where(h => h.Alive).Count() != Votes.Count) return true;
            // Majority vote; deterministic tie break by node id.
            Current = Votes.GroupBy(v => v.Value).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
            Route.Add(Current);
            Votes.Clear(); Resolved.Clear();
            switch (Map[Current].Kind)
            {
                case RoomKind.Camp: Phase = Phase.Camp; break;
                case RoomKind.Shop: Phase = Phase.Shop; break;
                case RoomKind.Event: Phase = Phase.Event; break;
                default: BeginCombat(); break;
            }
            return true;
        }

        void BeginCombat()
        {
            Phase = Phase.Combat; Turn = 0; Enemies.Clear();
            bool boss = Map[Current].Kind == RoomKind.Boss, elite = Map[Current].Kind == RoomKind.Elite;
            int visual=boss?2:elite?1:new[]{0,3,4,5,6,7,8,9}[Current%8];
            string[] names={"Hollow","Iron Revenant","Ash Warden","Cultist","Goblin","Bandit","Plague Doctor","Frost Giant","Sporekin","Bronze Sentinel"};
            Enemies.Add(new Enemy { VisualId=visual, Name = names[visual], Hp = (boss ? 100 : elite ? 48 : 28) * Heroes.Count, MaxHp = (boss ? 100 : elite ? 48 : 28) * Heroes.Count, Damage = boss ? 12 : elite ? 9 : 6 });
            foreach (var h in Heroes)
            {
                h.Strength = 0; h.Block = 0; h.Draw.Clear(); h.Draw.AddRange(h.Deck); Random.Shuffle(h.Draw);
                h.Hand.Clear(); h.Discard.Clear(); h.Exhausted.Clear();
                Cue(BattleCueKind.Shuffle,Heroes.IndexOf(h));
            }
            BeginTurn();
        }
        void BeginTurn()
        {
            Turn++;
            foreach (var h in Heroes) { h.Block = 0; h.Energy = 3; h.Ended = !h.Alive; if (h.Alive) DrawCards(h, 5); }
        }
        void DrawCards(Hero h, int count)
        {
            for (int i = 0; i < count && h.Hand.Count < 10; i++)
            {
                if (h.Draw.Count == 0 && h.Discard.Count>0) { h.Draw.AddRange(h.Discard); h.Discard.Clear(); Random.Shuffle(h.Draw); Cue(BattleCueKind.Shuffle,Heroes.IndexOf(h)); }
                if (h.Draw.Count == 0) break;
                int last = h.Draw.Count - 1; h.Hand.Add(h.Draw[last]); h.Draw.RemoveAt(last); Cue(BattleCueKind.Draw,Heroes.IndexOf(h),art:Cards[h.Hand.Last()].ArtId);
            }
        }
        static int Hit(int damage, ref int block) { int absorbed = Math.Min(block, Math.Max(0, damage)); block -= absorbed; return Math.Max(0, damage - absorbed); }
        bool Play(int p, int index)
        {
            Hero h = Heroes[p];
            if (Phase != Phase.Combat || h.Ended || index < 0 || index >= h.Hand.Count) return false;
            Card c = Cards[h.Hand[index]];
            if (h.Energy < c.Cost) return false;
            Cue(BattleCueKind.Play,p,-1,c.ArtId);
            Cue(c.Effect==Effect.Attack?BattleCueKind.Attack:c.Effect==Effect.Block?BattleCueKind.Guard:BattleCueKind.Skill,p,-1);
            h.Energy -= c.Cost; h.Hand.RemoveAt(index);
            if (c.Exhaust) h.Exhausted.Add(c.Id); else h.Discard.Add(c.Id);
            var enemy = Enemies.FirstOrDefault(e => e.Alive);
            switch (c.Effect)
            {
                case Effect.Attack:
                    for (int i = 0; i < c.Hits && enemy != null; i++) enemy.Hp = Math.Max(0, enemy.Hp - Hit(c.Amount + h.Strength, ref enemy.Block)); break;
                case Effect.Block: h.Block += c.Amount; break;
                case Effect.Strength: h.Strength += c.Amount; break;
                case Effect.Poison: if (enemy != null) enemy.Poison += c.Amount; break;
                case Effect.Draw: DrawCards(h, c.Amount); break;
                case Effect.Heal: h.Hp = Math.Min(h.MaxHp, h.Hp + c.Amount); break;
                case Effect.Energy: h.Energy += c.Amount; break;
            }
            if(c.Effect==Effect.Attack && enemy!=null) Cue(enemy.Alive?BattleCueKind.Hit:BattleCueKind.Death,-1,p,amount:c.Amount*c.Hits);
            Cue(c.Exhaust?BattleCueKind.Exhaust:BattleCueKind.Discard,p,art:c.ArtId,source:CardOrigin.PlayArea);
            CheckCombat(); return true;
        }
        bool End(int p)
        {
            if (Phase != Phase.Combat || Heroes[p].Ended) return false;
            Heroes[p].Ended = true;
            if (Heroes.Any(h => h.Alive && !h.Ended)) return true;
            foreach (var h in Heroes) { foreach(var id in h.Hand) Cue(BattleCueKind.Discard,Heroes.IndexOf(h),art:Cards[id].ArtId); h.Discard.AddRange(h.Hand); h.Hand.Clear(); }
            foreach (var e in Enemies.Where(e => e.Alive)) { e.Hp = Math.Max(0, e.Hp - e.Poison); e.Poison = Math.Max(0, e.Poison - 1); }
            if (CheckCombat()) return true;
            foreach (var e in Enemies.Where(e => e.Alive))
                {
                    Cue(BattleCueKind.Attack,-1-Enemies.IndexOf(e));
                    foreach (var h in Heroes.Where(h => h.Alive))
                    { h.Hp = Math.Max(0, h.Hp - Hit(e.Damage + (Turn % 3 == 0 ? 3 : 0), ref h.Block)); Cue(h.Alive?BattleCueKind.Hit:BattleCueKind.Death,Heroes.IndexOf(h),-1,amount:e.Damage); }
                }
            if (!CheckCombat()) BeginTurn();
            return true;
        }
        bool CheckCombat()
        {
            if (Heroes.All(h => !h.Alive)) { Phase = Phase.Lost; return true; }
            if (Enemies.Any(e => e.Alive)) return false;
            if (Map[Current].Kind == RoomKind.Boss) { Phase = Phase.Won; return true; }
            foreach(var h in Heroes.Where(h=>h.Alive)) Cue(BattleCueKind.Victory,Heroes.IndexOf(h));
            Phase = Phase.Reward; Resolved.Clear();
            for (int i = 0; i < Heroes.Count; i++)
            {
                Rewards[i].Clear(); if (!Heroes[i].Alive) continue;
                Heroes[i].Gold += 25;
                var pool = Cards.Values.Where(c => c.Class == Heroes[i].Class).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).ToList();
                Random.Shuffle(pool); Rewards[i].AddRange(pool.Take(3));
            }
            return true;
        }
        bool Reward(int p, int index)
        {
            if (Phase != Phase.Reward || Resolved.Contains(p) || index < -1 || index >= Rewards[p].Count) return false;
            if (index >= 0) Heroes[p].Deck.Add(Rewards[p][index]);
            Resolve(p); return true;
        }
        bool ResolveRoom(int p, int choice)
        {
            if (Resolved.Contains(p) || choice < 0 || choice > 1) return false;
            var h = Heroes[p];
            switch (Phase)
            {
                case Phase.Camp: if (choice == 0) h.Hp = Math.Min(h.MaxHp, h.Hp + h.MaxHp / 3); else h.MaxHp += 5; break;
                case Phase.Event: if (choice == 0) { h.Hp = Math.Max(1, h.Hp - 6); h.Gold += 35; } break;
                case Phase.Shop:
                    if (choice == 0) { if (h.Gold < 30) return false; h.Gold -= 30; h.Hp = Math.Min(h.MaxHp, h.Hp + 25); } break;
                default: return false;
            }
            Resolve(p); return true;
        }
        void Resolve(int p) { Resolved.Add(p); if (Heroes.Where(h => h.Alive).Count() == Resolved.Count) { Phase = Phase.Map; Resolved.Clear(); } }
    }
}
