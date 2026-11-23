using System;
using System.Collections.Generic;
using System.Linq;

namespace Spire
{
    /// <summary>
    /// 一局游戏的确定性规则与状态。外部操作通过 Apply 或 ResolveAttack 验证后执行。
    /// 地图、战斗、房间和物理命中按 partial 文件分工；此类型不依赖 Unity。
    /// </summary>
    public sealed partial class Run
    {
        public readonly List<int> Route = new List<int>();
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
        readonly List<BattleCue> cues = new List<BattleCue>();
        public IReadOnlyList<BattleCue> Cues => cues;

        void Cue(BattleCueKind kind, int actor, int target = 0, int art = 0, int amount = 0, CardOrigin source = CardOrigin.Automatic, string cardId = null, int handIndex = -1) => cues.Add(new BattleCue(kind, actor, target, art, amount, source, cardId: cardId, handIndex: handIndex));
        public Run(IEnumerable<Card> cards, uint seed, params HeroClass[] classes)
        {
            Cards = cards.ToDictionary(c => c.Id);
            if (classes.Length < 1 || classes.Length > 4)
                throw new ArgumentException("Party size must be 1..4");
            Random = new RandomSource(seed);
            foreach (var cls in classes)
            {
                var h = new Hero
                {
                    Class = cls,
                    MaxHp = Cards.Values.First(c => c.Class == cls).CharacterMaxHp
                };
                h.Hp = h.MaxHp;
                foreach (var card in Cards.Values.Where(c => c.Class == cls).OrderBy(c => c.Id, StringComparer.Ordinal))
                    for (int n = 0; n < card.StarterCopies; n++)
                        h.Deck.Add(card.Id);
                if (h.Deck.Count < 5)
                    throw new ArgumentException("Each class needs at least five starter cards");
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
            if (PendingAttacks > 0)
            {
                error = "Wait for the attack to resolve";
                return false;
            }

            error = null;
            cues.Clear();
            if (player < 0 || player >= Heroes.Count || !Heroes[player].Alive)
            {
                error = "Invalid or defeated player";
                return false;
            }

            bool ok = false;
            switch (action)
            {
                case "vote":
                    ok = Vote(player, argument);
                    break;
                case "play":
                    ok = Play(player, argument);
                    break;
                case "end":
                    ok = End(player);
                    break;
                case "reward":
                    ok = Reward(player, argument);
                    break;
                case "room":
                    ok = ResolveRoom(player, argument);
                    break;
            }

            if (ok)
                Revision++;
            else
                error = "Action is not legal in the current state";
            return ok;
        }
    }
}
