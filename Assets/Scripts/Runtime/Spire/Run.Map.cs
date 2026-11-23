using System;
using System.Collections.Generic;
using System.Linq;

namespace Spire
{
    // 地图投票与房间入口；平票按节点 ID 确定，保持联机重放一致。
    public sealed partial class Run
    {
        bool Vote(int player, int node)
        {
            if (Phase != Phase.Map || !Choices.Contains(node))
                return false;
            Votes[player] = node;
            if (Heroes.Where(h => h.Alive).Count() != Votes.Count)
                return true;
            // Majority vote; deterministic tie break by node id.
            Current = Votes.GroupBy(v => v.Value).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
            Route.Add(Current);
            Votes.Clear();
            Resolved.Clear();
            switch (Map[Current].Kind)
            {
                case RoomKind.Camp:
                    Phase = Phase.Camp;
                    break;
                case RoomKind.Shop:
                    Phase = Phase.Shop;
                    break;
                case RoomKind.Event:
                    Phase = Phase.Event;
                    break;
                default:
                    BeginCombat();
                    break;
            }

            return true;
        }
    }
}
