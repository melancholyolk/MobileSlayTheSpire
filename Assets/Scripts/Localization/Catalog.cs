using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SpireLocalization
{
    public sealed class Catalog
    {
        public readonly List<string> Locales = new List<string>();
        public readonly Dictionary<int, string[]> Entries = new Dictionary<int, string[]>();
        public readonly Dictionary<string, List<int>> Tables = new Dictionary<string, List<int>>();
        public static Catalog Read(byte[] data)
        {
            var catalog = new Catalog();
            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "SPLC" || reader.ReadInt32() != 2)
                    throw new InvalidDataException("Unsupported localization catalog");
                int languages = reader.ReadInt32(), count = reader.ReadInt32(), tables = reader.ReadInt32();
                if (languages < 1 || languages > 64 || count < 1 || count > 100000 || tables < 1 || tables > 10000)
                    throw new InvalidDataException("Invalid catalog counts");
                for (int i = 0; i < languages; i++)
                {
                    var code = ReadString(reader);
                    if (catalog.Locales.Contains(code))
                        throw new InvalidDataException("Duplicate locale");
                    catalog.Locales.Add(code);
                }

                for (int t = 0; t < tables; t++)
                {
                    var name = ReadString(reader);
                    int rows = reader.ReadInt32();
                    if (rows < 0 || rows > count || catalog.Tables.ContainsKey(name))
                        throw new InvalidDataException("Invalid table");
                    var ids = new List<int>();
                    catalog.Tables.Add(name, ids);
                    for (int i = 0; i < rows; i++)
                    {
                        int id = reader.ReadInt32();
                        var values = new string[languages];
                        if (id <= 0 || catalog.Entries.ContainsKey(id))
                            throw new InvalidDataException("Duplicate/invalid localization ID");
                        for (int j = 0; j < languages; j++)
                            values[j] = ReadString(reader);
                        ids.Add(id);
                        catalog.Entries.Add(id, values);
                    }
                }

                if (catalog.Entries.Count != count || stream.Position != stream.Length)
                    throw new InvalidDataException("Invalid catalog payload");
            }

            return catalog;
        }

        static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 1 || length > 65536)
                throw new InvalidDataException("Invalid string length");
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException();
            return new UTF8Encoding(false, true).GetString(bytes);
        }
    }
}
