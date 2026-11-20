using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the pixel-art tiles imported from the Super Retro World pack crisp:
/// point filtering, no compression, no mipmaps, 16 pixels per unit.
/// </summary>
sealed class PixelArtImport : AssetPostprocessor
{
    const string Root = "Assets/Resources/Art/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;

        var importer = (TextureImporter)assetImporter;
        bool sourceSheet = assetPath.Contains("/Source/");

        importer.textureType = sourceSheet ? TextureImporterType.Default : TextureImporterType.Sprite;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.spritePixelsPerUnit = 16f;
        importer.spriteImportMode = SpriteImportMode.Single;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.filterMode = FilterMode.Point;
        settings.mipmapEnabled = false;
        settings.alphaIsTransparency = true;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
    }
}
