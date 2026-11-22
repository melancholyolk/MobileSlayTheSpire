using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using SpireAnimation;
public static class WarriorExpressionBuilder
{
    public const string Folder="Assets/GameResources/Characters/0/Expressions";
    [MenuItem("Tools/Spire/Warrior Skin/Build Expressions")]
    public static void Build()
    {
        if(!File.Exists(Folder+"/13101.png"))return;
        AssetDatabase.Refresh();
        var factory=new SpriteDataProviderFactories();factory.Init();
        var source=factory.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(WarriorSkinBuilder.Folder+"/13022.png"));source.InitSpriteEditorDataProvider();
        var sourceId=source.GetSpriteRects().Single().spriteID;
        var sourceMesh=source.GetDataProvider<ISpriteMeshDataProvider>();
        var sprites=new Sprite[3];
        for(int i=0;i<3;i++)
        {
            string path=Folder+"/"+(13101+i)+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var id=provider.GetSpriteRects().Single().spriteID;
            provider.GetDataProvider<ISpriteBoneDataProvider>().SetBones(id,source.GetDataProvider<ISpriteBoneDataProvider>().GetBones(sourceId));
            var mesh=provider.GetDataProvider<ISpriteMeshDataProvider>();
            mesh.SetVertices(id,sourceMesh.GetVertices(sourceId));mesh.SetIndices(id,sourceMesh.GetIndices(sourceId));mesh.SetEdges(id,sourceMesh.GetEdges(sourceId));
            provider.Apply();importer.SaveAndReimport();sprites[i]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        string prefabPath=WarriorSkinBuilder.Folder+"/13000.prefab";
        var root=PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var view=root.GetComponent<ActorExpressionView>()??root.AddComponent<ActorExpressionView>();
            view.Head=root.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Skin_Head");
            view.Neutral=AssetDatabase.LoadAssetAtPath<Sprite>(WarriorSkinBuilder.Folder+"/13022.png");
            view.Pain=sprites[0];view.Happy=sprites[1];view.Focus=sprites[2];view.ResetExpression();
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();ResourceCatalogBuilder.Build();
    }
}
