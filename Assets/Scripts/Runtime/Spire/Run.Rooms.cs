using System;
using System.Collections.Generic;
using System.Linq;

namespace Spire
{
    // 奖励、营地、商店和事件结算；存活玩家全部完成后回到地图。
    public sealed partial class Run
    {
        bool Reward(int p, int index)
        {
            if (Phase != Phase.Reward || Resolved.Contains(p) || index < -1 || index >= Rewards[p].Count)
                return false;
            if (index >= 0)
                Heroes[p].Deck.Add(Rewards[p][index]);
            Resolve(p);
            return true;
        }

        bool ResolveRoom(int p, int choice)
        {
            if (Resolved.Contains(p) || choice < 0 || choice > 1)
                return false;
            var h = Heroes[p];
            switch (Phase)
            {
                case Phase.Camp:
                    if (choice == 0)
                        h.Hp = Math.Min(h.MaxHp, h.Hp + h.MaxHp / 3);
                    else
                        h.MaxHp += 5;
                    break;
                case Phase.Event:
                    if (choice == 0)
                    {
                        h.Hp = Math.Max(1, h.Hp - 6);
                        h.Gold += 35;
                    }

                    break;
                case Phase.Shop:
                    if (choice == 0)
                    {
                        if (h.Gold < 30)
                            return false;
                        h.Gold -= 30;
                        h.Hp = Math.Min(h.MaxHp, h.Hp + 25);
                    }

                    break;
                default:
                    return false;
            }

            Resolve(p);
            return true;
        }

        void Resolve(int p)
        {
            Resolved.Add(p);
            if (Heroes.Where(h => h.Alive).Count() == Resolved.Count)
            {
                Phase = Phase.Map;
                Resolved.Clear();
            }
        }
    }
}
