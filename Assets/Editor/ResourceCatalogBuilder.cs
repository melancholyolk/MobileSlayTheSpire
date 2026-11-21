using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpireResources;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
public static class ResourceCatalogBuilder
{
    [MenuItem("Tools/Spire/Build Resource Catalog")]
    public static void Build()
    {
        var settings=AddressableAssetSettingsDefaultObject.GetSettings(true);
        var entries=new List<ResourceEntry>(); var ids=new HashSet<int>(); var valid=new HashSet<string>();
        void Add(int id,int character,string path)
        {
            if(id<=0 || !ids.Add(id)) throw new InvalidDataException("Duplicate/invalid resource ID "+id);
            string guid=AssetDatabase.AssetPathToGUID(path);
            if(string.IsNullOrEmpty(guid)) throw new FileNotFoundException(path);
            string groupName=character<0?"Spire_Shared":"Spire_Character_"+character;
            var group=settings.FindGroup(groupName)??settings.CreateGroup(groupName,false,false,false,null,typeof(BundledAssetGroupSchema),typeof(ContentUpdateGroupSchema));
            var schema=group.GetSchema<BundledAssetGroupSchema>();
            schema.BundleMode=BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.BuildPath.SetVariableByName(settings,"Local.BuildPath"); schema.LoadPath.SetVariableByName(settings,"Local.LoadPath");
            var entry=settings.CreateOrMoveEntry(guid,group); entry.address=id.ToString();
            long estimate=1048576;
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture!=null) estimate=(long)texture.width*texture.height*4;
            else if(File.Exists(path)) estimate=Math.Max(estimate,new FileInfo(path).Length*2);
            entries.Add(new ResourceEntry {Id=id,CharacterId=character,Guid=guid,EstimatedBytes=estimate}); valid.Add(guid);
        }
        Add(1,-1,"Assets/GameResources/UI/Views/GameStart/GameStartView.prefab");
        Add(2,-1,"Assets/GameResources/UI/Views/Gameplay/GameplayView.prefab");
        Add(3,-1,"Assets/GameResources/UI/Views/Card/3.prefab");
        // Tables stay in Resources for the localization plugin and existing table tooling.
        // AssetDatabase moves them into the Addressables build input outside Resources.
        Directory.CreateDirectory("Assets/GameResources/Tables");
        foreach(var item in new[]{(10,"Assets/Resources/Tables/cards.bytes"),(11,"Assets/Resources/Localization/catalog.bytes")})
        {
            var target="Assets/GameResources/Tables/"+item.Item1+".bytes";
            File.Copy(item.Item2,target,true); AssetDatabase.ImportAsset(target); Add(item.Item1,-1,target);
        }
        foreach(var path in Directory.GetFiles("Assets/GameResources/Characters","*",SearchOption.AllDirectories).OrderBy(p=>p))
        {
            if(path.EndsWith(".meta") || Directory.Exists(path)) continue;
            var normalized=path.Replace((char)92,'/'); var parts=normalized.Split('/');
            if(!int.TryParse(parts[3],out int character) || !int.TryParse(Path.GetFileNameWithoutExtension(path),out int id))
                throw new InvalidDataException("Character resources require integer filenames: "+path);
            Add(id,character,normalized);
        }
        foreach(var group in settings.groups.Where(g=>g!=null && g.Name.StartsWith("Spire_")))
            foreach(var stale in group.entries.Where(e=>!valid.Contains(e.guid)).ToArray()) settings.RemoveAssetEntry(stale.guid);
        const string output="Assets/Resources/ResourceCatalog.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<ResourceCatalog>(output);
        if(catalog==null) { catalog=ScriptableObject.CreateInstance<ResourceCatalog>(); AssetDatabase.CreateAsset(catalog,output); }
        catalog.Entries=entries.ToArray(); EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        ValidateCards(entries);
    }
    static void ValidateCards(List<ResourceEntry> entries)
    {
        var cards=Spire.CardTable.Read(File.ReadAllBytes("Assets/Resources/Tables/cards.bytes"));
        foreach(var card in cards)
            foreach(int id in new[]{card.ArtId,card.PortraitId})
                if(!entries.Any(e=>e.Id==id && e.CharacterId==(int)card.Class))
                    throw new InvalidDataException("Missing or cross-character image "+id+" on "+card.Id);
    }
    [MenuItem("Tools/Spire/Build Addressables")]
    public static void BuildContent()
    {
        Build();
        AddressableAssetSettings.BuildPlayerContent(out var result);
        if(!string.IsNullOrEmpty(result.Error)) throw new Exception(result.Error);
    }
}


// Ensure newly added numeric files and changed table data reach player builds.
public sealed class ResourceBuildGuard : UnityEditor.Build.IPreprocessBuildWithReport
{
    public int callbackOrder=>-900;
    public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report) { ResourceCatalogBuilder.BuildContent(); }
}
