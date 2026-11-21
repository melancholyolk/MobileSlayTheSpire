using UnityEditor;
public sealed class ResourceCatalogPostprocessor : AssetPostprocessor
{
    static bool scheduled;
    static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
    {
        if(scheduled) return;
        bool relevant=false;
        foreach(var list in new[]{imported,deleted,moved,movedFrom})
            foreach(var path in list) if(path.StartsWith("Assets/GameResources/Characters/")) relevant=true;
        if(!relevant) return;
        scheduled=true;
        EditorApplication.delayCall+=()=> {
            scheduled=false;
            if(!EditorApplication.isPlayingOrWillChangePlaymode && System.IO.File.Exists("Assets/Resources/ResourceCatalog.asset"))
                ResourceCatalogBuilder.Build();
        };
    }
}
