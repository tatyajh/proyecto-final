using UnityEditor;
using UnityEngine;

/// <summary>
/// Mantiene los recortes generados para VFX como sprites transparentes y evita
/// que Unity los importe accidentalmente como texturas opacas o repetibles.
/// </summary>
public sealed class BotanicalVfxAssetImporter : AssetPostprocessor
{
    private const string TerramorVfxRoot = "Assets/Resources/Vfx/Terramor/";
    private const string PickupVfxRoot = "Assets/Resources/Vfx/Pickups/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(TerramorVfxRoot) && !assetPath.StartsWith(PickupVfxRoot)) return;
        if (assetImporter is not TextureImporter importer) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
