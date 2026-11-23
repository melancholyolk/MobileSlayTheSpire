using System;
using System.Collections.Generic;
using System.Linq;

namespace Spire
{
    [Serializable]
    public sealed class Card
    {
        public string Id;
        public int NameId, ArtId, CharacterNameId, CharacterMaxHp = 80, PortraitId;
        public HeroClass Class;
        public int Cost, Amount, Hits = 1, StarterCopies;
        public Effect Effect;
        public bool Exhaust;
    }
}
