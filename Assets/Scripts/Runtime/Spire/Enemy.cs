using System;
using System.Collections.Generic;
using System.Linq;

namespace Spire
{
    [Serializable]
    public sealed class Enemy
    {
        public string Name;
        public int Hp, MaxHp, Block, Poison, Damage;
        public int VisualId;
        public bool Alive => Hp > 0;
    }
}
