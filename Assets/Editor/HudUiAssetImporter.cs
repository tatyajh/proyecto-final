using UnityEditor;
using UnityEngine;

/// <summary>
/// Mantiene los marcos del HUD listos para Unity UI. Los bordes viven en el
/// importer (no en Sprite.Create), de modo que Image.Type.Sliced conserva las
/// tapas y solo estira la zona central.
/// </summary>
public sealed class HudUiAssetImporter : AssetPostprocessor
{
    private const string HudRoot = "Assets/Resources/UI/HUD/";

    [InitializeOnLoadMethod]
    private static void ScheduleHudSpriteImport()
    {
        EditorApplication.delayCall += EnsureHudSpritesImported;
    }

    private static void EnsureHudSpritesImported()
    {
        string[] sliced = { "HealthFrame", "ControlHintFrame", "HudButtonFrame", "ModalFrame" };
        foreach (string name in sliced)
        {
            string path = HudRoot + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite ||
                importer.spriteBorder == Vector4.zero)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        const string ability = HudRoot + "AbilityFrame.png";
        TextureImporter abilityImporter = AssetImporter.GetAtPath(ability) as TextureImporter;
        if (abilityImporter == null || abilityImporter.textureType != TextureImporterType.Sprite)
            AssetDatabase.ImportAsset(ability, ImportAssetOptions.ForceUpdate);
    }

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(HudRoot, System.StringComparison.Ordinal)) return;
        if (assetPath.EndsWith("LoadingSanctuary.png", System.StringComparison.Ordinal)) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        switch (file)
        {
            case "HealthFrame":
                importer.spritePixelsPerUnit = 600f;
                importer.spriteBorder = new Vector4(420f, 160f, 420f, 160f);
                break;
            case "ControlHintFrame":
                importer.spritePixelsPerUnit = 600f;
                importer.spriteBorder = new Vector4(380f, 155f, 380f, 155f);
                break;
            case "HudButtonFrame":
                importer.spritePixelsPerUnit = 600f;
                importer.spriteBorder = new Vector4(410f, 175f, 410f, 175f);
                break;
            case "ModalFrame":
                importer.spritePixelsPerUnit = 300f;
                importer.spriteBorder = new Vector4(245f, 165f, 245f, 165f);
                break;
            default:
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = Vector4.zero;
                break;
        }
    }
}
