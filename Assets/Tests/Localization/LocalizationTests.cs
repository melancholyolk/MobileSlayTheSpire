using System;
using System.IO;
using NUnit.Framework;
using SpireLocalization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.AddressableAssets;

public class LocalizationTests
{
    byte[] Bytes => Resources.Load<TextAsset>("Localization/catalog").bytes;
    [Test] public void GeneratedBinaryContainsBothLanguages()
    {
        var catalog=Catalog.Read(Bytes);
        CollectionAssert.AreEqual(new[]{"en","zh-Hans"},catalog.Locales);
        Assert.AreEqual("铁刃打击",catalog.Entries["card.w_strike.name"][1]);
    }
    [Test] public void CorruptionAndTruncationAreRejected()
    {
        var bytes=Bytes; bytes[0]=0;
        Assert.Throws<InvalidDataException>(()=>Catalog.Read(bytes));
        Assert.Throws<EndOfStreamException>(()=>Catalog.Read(new byte[]{83,80,76,67}));
    }
    [Test] public void ProviderProducesRealUnityStringTables()
    {
        var provider=new ExcelTableProvider(Catalog.Read(Bytes)); var locale=Locale.CreateLocale("zh-Hans");
        var handle=provider.ProvideTableAsync<StringTable>("Game",locale);
        Assert.IsTrue(handle.IsDone);
        Assert.AreEqual("战士\n力量与钢铁",handle.Result.GetEntry("ui.warrior").LocalizedValue);
        var table=handle.Result; var shared=table.SharedData;
        Addressables.Release(handle); UnityEngine.Object.DestroyImmediate(table); UnityEngine.Object.DestroyImmediate(shared); UnityEngine.Object.DestroyImmediate(locale);
    }
    [Test] public void UnityDatabaseSwitchesLanguageAndFallsBackForUnknownKey()
    {
        ExcelLocalization.Initialize(); var previous=ExcelLocalization.Language;
        try
        {
            ExcelLocalization.Select("en"); Assert.AreEqual("Menu",ExcelLocalization.Text("ui.menu"));
            ExcelLocalization.Select("zh-Hans"); Assert.AreEqual("菜单",ExcelLocalization.Text("ui.menu"));
            Assert.AreEqual("fallback",ExcelLocalization.Text("missing","fallback"));
            Assert.Throws<ArgumentException>(()=>ExcelLocalization.Select("xx"));
        }
        finally { ExcelLocalization.Select(previous); }
    }
}
