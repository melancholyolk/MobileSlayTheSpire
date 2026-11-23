using System;
using System.Collections.Generic;
using System.Linq;

namespace Spire
{
    [Serializable]
    public sealed class Hero
    {
        public HeroClass Class;
        public int Hp, MaxHp, Block, Energy, Strength, Gold = 50;
        public bool Ended;
        public List<string> Deck = new List<string>(), Draw = new List<string>(), Hand = new List<string>(), Discard = new List<string>(), Exhausted = new List<string>();
        public bool Alive => Hp > 0;
    }
}
