using UnityEditor;
using UnityEngine;
// Numeric actor images are always sprites; adding a table row and PNG needs no manual importer setup.
public sealed class ActorImageImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/GameResources/Characters/") && !assetPath.StartsWith("Assets/GameResources/Monsters/")) return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.isReadable=false;
        importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Compressed;
    }
}
