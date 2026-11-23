using System;
using System.Collections.Generic;
using System.Linq;

namespace Spire
{
    // Explicit PRNG state is portable across Mono, IL2CPP and network peers.
    public sealed class RandomSource
    {
        public uint State { get; private set; }

        public RandomSource(uint seed)
        {
            State = seed == 0 ? 1u : seed;
        }

        public int Next(int limit)
        {
            if (limit <= 0)
                throw new ArgumentOutOfRangeException(nameof(limit));
            uint x = State;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State = x;
            return (int)(x % (uint)limit);
        }

        public void Shuffle<T>(List<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T t = items[i];
                items[i] = items[j];
                items[j] = t;
            }
        }
    }
}
