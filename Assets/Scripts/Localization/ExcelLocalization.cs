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
        public readonly List<string> Locales=new List<string>();
        public readonly Dictionary<int,string[]> Entries=new Dictionary<int,string[]>();
        public readonly Dictionary<string,List<int>> Tables=new Dictionary<string,List<int>>();
        public static Catalog Read(byte[] data)
        {
            var catalog=new Catalog();
            using(var stream=new MemoryStream(data))
            using(var reader=new BinaryReader(stream))
            {
                if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="SPLC" || reader.ReadInt32()!=2) throw new InvalidDataException("Unsupported localization catalog");
                int languages=reader.ReadInt32(),count=reader.ReadInt32(),tables=reader.ReadInt32();
                if(languages<1 || languages>64 || count<1 || count>100000 || tables<1 || tables>10000) throw new InvalidDataException("Invalid catalog counts");
                for(int i=0;i<languages;i++)
                {
                    var code=ReadString(reader);
                    if(catalog.Locales.Contains(code)) throw new InvalidDataException("Duplicate locale");
                    catalog.Locales.Add(code);
                }
                for(int t=0;t<tables;t++)
                {
                    var name=ReadString(reader); int rows=reader.ReadInt32();
                    if(rows<0 || rows>count || catalog.Tables.ContainsKey(name)) throw new InvalidDataException("Invalid table");
                    var ids=new List<int>(); catalog.Tables.Add(name,ids);
                    for(int i=0;i<rows;i++)
                    {
                        int id=reader.ReadInt32(); var values=new string[languages];
                        if(id<=0 || catalog.Entries.ContainsKey(id)) throw new InvalidDataException("Duplicate/invalid localization ID");
                        for(int j=0;j<languages;j++) values[j]=ReadString(reader);
                        ids.Add(id); catalog.Entries.Add(id,values);
                    }
                }
                if(catalog.Entries.Count!=count || stream.Position!=stream.Length) throw new InvalidDataException("Invalid catalog payload");
            }
            return catalog;
        }
        static string ReadString(BinaryReader reader)
        {
            int length=reader.ReadInt32();
            if(length<1 || length>65536) throw new InvalidDataException("Invalid string length");
            var bytes=reader.ReadBytes(length);
            if(bytes.Length!=length) throw new EndOfStreamException();
            return new UTF8Encoding(false,true).GetString(bytes);
        }
    }

    public sealed class ExcelTableProvider : ITableProvider
    {
        readonly Catalog catalog;
        public ExcelTableProvider(Catalog catalog) { this.catalog=catalog; }
        public AsyncOperationHandle<TTable> ProvideTableAsync<TTable>(string name,Locale locale) where TTable:LocalizationTable
        {
            if(typeof(TTable)!=typeof(StringTable) || !catalog.Tables.TryGetValue(name,out var ids)) return default;
            int index=catalog.Locales.IndexOf(locale.Identifier.Code); if(index<0) index=0;
            var table=ScriptableObject.CreateInstance<StringTable>();
            table.SharedData=ScriptableObject.CreateInstance<SharedTableData>();
            table.SharedData.TableCollectionName=name; table.LocaleIdentifier=locale.Identifier;
            // The numeric overload stores the integer key directly. No ID.ToString or per-key string is created.
            foreach(int id in ids) table.AddEntry((long)id,catalog.Entries[id][index]);
            return Addressables.ResourceManager.CreateCompletedOperation(table as TTable,null);
        }
    }
    public sealed class ExcelLocales : ILocalesProvider
    {
        public List<Locale> Locales { get; }=new List<Locale>();
        public Locale GetLocale(LocaleIdentifier id) => Locales.Find(locale=>locale.Identifier==id);
        public void AddLocale(Locale locale) { if(GetLocale(locale.Identifier)==null) Locales.Add(locale); }
        public bool RemoveLocale(Locale locale) => Locales.Remove(locale);
    }
    public static class ExcelLocalization
    {
        static Catalog catalog;
        static ExcelLocalizationSettings settings;
        static int languageIndex;
        public static string Language { get { Initialize(); return LocalizationSettings.SelectedLocale.Identifier.Code; } }
        public static IReadOnlyList<string> Languages { get { Initialize(); return catalog.Locales; } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { catalog=null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if(catalog!=null && settings!=null && ReferenceEquals(LocalizationSettings.Instance,settings)
                && settings.GetAvailableLocales().Locales.Count>0 && settings.GetAvailableLocales().Locales[0]!=null) return;
            var asset=Resources.Load<TextAsset>("Localization/catalog");
            if(asset==null) throw new InvalidDataException("Run Tools/Tables/build.bat to generate localization assets");
            catalog=Catalog.Read(asset.bytes);
            settings=ScriptableObject.CreateInstance<ExcelLocalizationSettings>(); LocalizationSettings.Instance=settings;
            var locales=new ExcelLocales();
            foreach(var code in catalog.Locales) locales.AddLocale(Locale.CreateLocale(code));
            LocalizationSettings.AvailableLocales=locales;
            LocalizationSettings.PreloadBehavior=PreloadBehavior.NoPreloading;
            LocalizationSettings.StringDatabase.TableProvider=new ExcelTableProvider(catalog);
            settings.OnSelectedLocaleChanged+=LocaleChanged;
            LocalizationSettings.SelectedLocale=locales.GetLocale(new LocaleIdentifier(PlayerPrefs.GetString("Language","en"))) ?? locales.Locales[0];
            LocaleChanged(LocalizationSettings.SelectedLocale);
        }
        static void LocaleChanged(Locale locale)
        {
            languageIndex=catalog.Locales.IndexOf(locale.Identifier.Code); if(languageIndex<0) languageIndex=0;
        }
        public static void Select(string code)
        {
            Initialize(); var locale=LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
            if(locale==null) throw new ArgumentException("Unsupported locale: "+code);
            LocalizationSettings.SelectedLocale=locale; PlayerPrefs.SetString("Language",code);
        }
        // Returns a cached string. No key concatenation, boxing, params array or string formatting in the hot path.
        public static string Text(int id,string fallback="")
        {
            Initialize();
            return catalog.Entries.TryGetValue(id,out var values)?values[languageIndex]:fallback;
        }
    }
}
