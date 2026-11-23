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
    public sealed class ExcelTableProvider : ITableProvider
    {
        readonly Catalog catalog;
        public ExcelTableProvider(Catalog catalog)
        {
            this.catalog = catalog;
        }

        public AsyncOperationHandle<TTable> ProvideTableAsync<TTable>(string name, Locale locale)
            where TTable : LocalizationTable
        {
            if (typeof(TTable) != typeof(StringTable) || !catalog.Tables.TryGetValue(name, out var ids))
                return default;
            int index = catalog.Locales.IndexOf(locale.Identifier.Code);
            if (index < 0)
                index = 0;
            var table = ScriptableObject.CreateInstance<StringTable>();
            table.SharedData = ScriptableObject.CreateInstance<SharedTableData>();
            table.SharedData.TableCollectionName = name;
            table.LocaleIdentifier = locale.Identifier;
            // The numeric overload stores the integer key directly. No ID.ToString or per-key string is created.
            foreach (int id in ids)
                table.AddEntry((long)id, catalog.Entries[id][index]);
            return Addressables.ResourceManager.CreateCompletedOperation(table as TTable, null);
        }
    }

    public sealed class ExcelLocales : ILocalesProvider
    {
        public List<Locale> Locales { get; } = new List<Locale>();

        public Locale GetLocale(LocaleIdentifier id) => Locales.Find(locale => locale.Identifier == id);
        public void AddLocale(Locale locale)
        {
            if (GetLocale(locale.Identifier) == null)
                Locales.Add(locale);
        }

        public bool RemoveLocale(Locale locale) => Locales.Remove(locale);
    }

    public static class ExcelLocalization
    {
        static Catalog catalog;
        static ExcelLocalizationSettings settings;
        static int languageIndex;
        public static string Language
        {
            get
            {
                Initialize();
                return LocalizationSettings.SelectedLocale.Identifier.Code;
            }
        }

        public static IReadOnlyList<string> Languages
        {
            get
            {
                Initialize();
                return catalog.Locales;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            catalog = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (catalog != null && settings != null && ReferenceEquals(LocalizationSettings.Instance, settings) && settings.GetAvailableLocales().Locales.Count > 0 && settings.GetAvailableLocales().Locales[0] != null)
                return;
            var asset = Resources.Load<TextAsset>("Localization/catalog");
            if (asset == null)
                throw new InvalidDataException("Run Tools/Tables/build.bat to generate localization assets");
            catalog = Catalog.Read(asset.bytes);
            settings = ScriptableObject.CreateInstance<ExcelLocalizationSettings>();
            LocalizationSettings.Instance = settings;
            var locales = new ExcelLocales();
            foreach (var code in catalog.Locales)
                locales.AddLocale(Locale.CreateLocale(code));
            LocalizationSettings.AvailableLocales = locales;
            LocalizationSettings.PreloadBehavior = PreloadBehavior.NoPreloading;
            LocalizationSettings.StringDatabase.TableProvider = new ExcelTableProvider(catalog);
            settings.OnSelectedLocaleChanged += LocaleChanged;
            LocalizationSettings.SelectedLocale = locales.GetLocale(new LocaleIdentifier(PlayerPrefs.GetString("Language", "en"))) ?? locales.Locales[0];
            LocaleChanged(LocalizationSettings.SelectedLocale);
        }

        static void LocaleChanged(Locale locale)
        {
            languageIndex = catalog.Locales.IndexOf(locale.Identifier.Code);
            if (languageIndex < 0)
                languageIndex = 0;
        }

        public static void Select(string code)
        {
            Initialize();
            var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
            if (locale == null)
                throw new ArgumentException("Unsupported locale: " + code);
            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString("Language", code);
        }

        // Returns a cached string. No key concatenation, boxing, params array or string formatting in the hot path.
        public static string Text(int id, string fallback = "")
        {
            Initialize();
            return catalog.Entries.TryGetValue(id, out var values) ? values[languageIndex] : fallback;
        }
    }
}
