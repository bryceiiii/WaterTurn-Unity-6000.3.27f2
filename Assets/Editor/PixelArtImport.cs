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
        bool cave = assetPath.Contains("/Art/Cave/");
        bool water = assetPath.Contains("/Art/Water/");
        // Ground tiles are authored seamless, so they must repeat, not clamp.
        bool ground = assetPath.Contains("/Art/Ground/");

        // Cave sheets are cropped into sprites at runtime, so keep them as
        // plain readable textures.  The water frames are also upscaled and
        // given rounded corners in memory, which needs readable pixels.
        importer.textureType = sourceSheet || cave ? TextureImporterType.Default : TextureImporterType.Sprite;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = cave || ground ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.isReadable = cave || water;
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
