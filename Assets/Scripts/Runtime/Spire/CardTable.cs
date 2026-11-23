using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Spire
{
    public static class CardTable
    {
        static string ReadText(BinaryReader r)
        {
            int n = r.ReadInt32();
            if (n < 1 || n > 1024)
                throw new InvalidDataException("Invalid text length");
            var bytes = r.ReadBytes(n);
            if (bytes.Length != n)
                throw new EndOfStreamException();
            return new UTF8Encoding(false, true).GetString(bytes);
        }

        public static Card[] Read(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            using (var r = new BinaryReader(stream))
            {
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "SPCR" || r.ReadInt32() != 3)
                    throw new InvalidDataException("Unsupported card table");
                int count = r.ReadInt32();
                if (count < 1 || count > 10000)
                    throw new InvalidDataException("Invalid card count");
                var cards = new List<Card>();
                var ids = new HashSet<string>();
                var starters = new Dictionary<HeroClass, int>();
                for (int i = 0; i < count; i++)
                {
                    var c = new Card
                    {
                        Id = ReadText(r),
                        NameId = r.ReadInt32(),
                        Class = (HeroClass)r.ReadInt32(),
                        Cost = r.ReadInt32(),
                        Effect = (Effect)r.ReadInt32(),
                        Amount = r.ReadInt32(),
                        Hits = r.ReadInt32()
                    };
                    int exhaust = r.ReadInt32();
                    c.Exhaust = exhaust == 1;
                    c.StarterCopies = r.ReadInt32();
                    c.ArtId = r.ReadInt32();
                    c.CharacterNameId = r.ReadInt32();
                    c.CharacterMaxHp = r.ReadInt32();
                    c.PortraitId = r.ReadInt32();
                    if (c.NameId <= 0 || !ids.Add(c.Id) || (int)c.Class < 0 || c.ArtId <= 0 || c.CharacterNameId <= 0 || c.CharacterMaxHp <= 0 || c.PortraitId <= 0 || !Enum.IsDefined(typeof(Effect), c.Effect) || c.Cost < 0 || c.Cost > 10 || c.Amount < 0 || c.Amount > 999 || c.Hits < 1 || c.Hits > 10 || exhaust < 0 || exhaust > 1 || c.StarterCopies < 0 || c.StarterCopies > 20)
                        throw new InvalidDataException("Invalid card: " + c.Id);
                    if (!starters.ContainsKey(c.Class))
                        starters[c.Class] = 0;
                    starters[c.Class] += c.StarterCopies;
                    cards.Add(c);
                }

                if (stream.Position != stream.Length || System.Linq.Enumerable.Any(starters.Values, n => n < 5))
                    throw new InvalidDataException("Invalid table payload or starter decks");
                return cards.ToArray();
            }
        }
    }
}
