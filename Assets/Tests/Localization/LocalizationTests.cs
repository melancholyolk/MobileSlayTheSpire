using System;
using System.IO;
using NUnit.Framework;
using Spire;
using SpireLocalization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.AddressableAssets;

public class LocalizationTests
{
    byte[] Bytes=>Resources.Load<TextAsset>("Localization/catalog").bytes;
    [Test] public void GeneratedCatalogUsesIntegerIdsAndPreservesTranslations()
    {
        var catalog=Catalog.Read(Bytes);
        CollectionAssert.AreEqual(new[]{"en","zh-Hans"},catalog.Locales);
        var cards=CardTable.Read(Resources.Load<TextAsset>("Tables/cards").bytes);
        foreach(var card in cards) Assert.IsTrue(catalog.Tables["Common"].Contains(card.NameId));
        var strike=Array.Find(cards,c=>c.Id=="w_strike");
        Assert.AreEqual("铁刃打击",catalog.Entries[strike.NameId][1]);
    }
    [Test] public void CorruptionAndTruncationAreRejected()
    {
        var bytes=Bytes; bytes[0]=0;
        Assert.Throws<InvalidDataException>(()=>Catalog.Read(bytes));
        Assert.Throws<EndOfStreamException>(()=>Catalog.Read(new byte[]{83,80,76,67}));
    }
    [Test] public void UnityPluginLoadsTableByNumericEntryId()
    {
        ExcelLocalization.Initialize(); var previous=ExcelLocalization.Language;
        try
        {
            ExcelLocalization.Select("zh-Hans");
            var text=new LocalizedString("Game",(long)LocIds.UiWarrior);
            Assert.AreEqual("战士\n力量与钢铁",text.GetLocalizedString());
            var table=LocalizationSettings.StringDatabase.GetTable("Game");
            Assert.AreEqual(LocIds.UiWarrior,table.GetEntry((long)LocIds.UiWarrior).KeyId);
            Assert.AreEqual("菜单",ExcelLocalization.Text(LocIds.UiMenu));
            ExcelLocalization.Select("en"); Assert.AreEqual("Menu",ExcelLocalization.Text(LocIds.UiMenu));
            Assert.AreEqual("fallback",ExcelLocalization.Text(-1,"fallback"));
            Assert.Throws<ArgumentException>(()=>ExcelLocalization.Select("xx"));
        }
        finally { ExcelLocalization.Select(previous); }
    }
    [Test] public void IntegerLookupDoesNotAllocateOnHotPath()
    {
        ExcelLocalization.Initialize();
        for(int i=0;i<100;i++) ExcelLocalization.Text(LocIds.UiMenu);
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++) ExcelLocalization.Text(LocIds.UiMenu);
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.AreEqual(0,allocated,"Integer localization lookup must not allocate per request");
    }
    [Test] public void StoryBinaryStoresIntReferencesAndResolvesThroughCatalog()
    {
        var table=BusinessTable.Read(Resources.Load<TextAsset>("BusinessTables/StoryExample").bytes);
        int column=table.Column("Text");
        Assert.AreEqual(BusinessTable.FieldType.Int,table.Type(column));
        Assert.AreEqual(table.Int(0,column),table.Int(1,column));
        var catalog=Catalog.Read(Bytes);
        Assert.Contains(table.Int(0,column),catalog.Tables["Story"]);
        Assert.AreEqual("灰烬落下，新的旅程开始。",ExcelLocalization.Text(table.Int(0,column)));
        int button=table.Int(0,table.Column("Button"));
        Assert.Contains(button,catalog.Tables["Common"]);
        Assert.AreEqual("继续",ExcelLocalization.Text(button));
        Assert.Throws<InvalidOperationException>(()=>table.String(0,column));
    }
}
