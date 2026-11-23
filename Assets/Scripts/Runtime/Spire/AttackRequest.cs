using System;
using System.Collections.Generic;
using System.Linq;

namespace Spire
{
    public readonly struct AttackRequest
    {
        public readonly int Id, Source, Target, Damage;
        public readonly string CardId;
        public AttackRequest(int id, int source, int target, int damage, string cardId)
        {
            Id = id;
            Source = source;
            Target = target;
            Damage = damage;
            CardId = cardId;
        }
    }
}
