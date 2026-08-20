using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Identidad visual compartida por todos los menús: la paleta y las dos
/// tipografías del GDD. Antes cada pantalla definía sus propios colores a ojo
/// y usaba la fuente por defecto de TextMeshPro, que es lo que hacía que la UI
/// no se pareciera al resto del juego.
/// </summary>
public static class MenuTheme
{
    // Paleta tomada de las variables CSS del GDD.
    public static readonly Color Void = Hex(0x100C14);
    public static readonly Color Panel = Hex(0x1C1622);
    public static readonly Color PanelDeep = Hex(0x150F1B);
    public static readonly Color Bone = Hex(0xECE3D2);
    public static readonly Color BoneDim = Hex(0xC9BFAE);
    public static readonly Color Gilt = Hex(0xB6913F);
    public static readonly Color GiltBright = Hex(0xD9B565);
    public static readonly Color Blood = Hex(0x7C2B3B);
    public static readonly Color BloodBright = Hex(0xA8425A);
    public static readonly Color Rot = Hex(0x5E6B45);
    public static readonly Color RotBright = Hex(0x899A5E);
    public static readonly Color Ash = Hex(0x6A6274);
    public static readonly Color Line = new Color(0.714f, 0.569f, 0.247f, 0.22f);

    private static TMP_FontAsset display;
    private static TMP_FontAsset body;
    private static bool displayResolved;
    private static bool bodyResolved;

    /// <summary>Cinzel: títulos y botones. Null si la fuente no está importada.</summary>
    public static TMP_FontAsset Display
    {
        get
        {
            if (!displayResolved)
            {
                display = LoadFont("Fonts/Cinzel");
                displayResolved = true;
            }
            return display;
        }
    }

    /// <summary>EB Garamond: texto corrido.</summary>
    public static TMP_FontAsset Body
    {
        get
        {
            if (!bodyResolved)
            {
                body = LoadFont("Fonts/EBGaramond");
                bodyResolved = true;
            }
            return body;
        }
    }

    /// <summary>
    /// Convierte un .ttf de Resources en TMP_FontAsset dinámico en runtime.
    /// Evita tener que generar el asset a mano con el Font Asset Creator, y el
    /// atlas dinámico solo rasteriza los glifos que la UI realmente usa.
    /// </summary>
    private static TMP_FontAsset LoadFont(string resourcePath)
    {
        Font source = Resources.Load<Font>(resourcePath);
        if (source == null)
        {
            Debug.LogWarning($"[MenuTheme] No se encontró la fuente '{resourcePath}'. Se usará la de TextMeshPro por defecto.");
            return null;
        }

        try
        {
            // 64pt en atlas de 512 basta para UI y rasteriza bastante más
            // rápido que 90pt/1024: el primer render del menú se notaba.
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                source, 64, 6, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, true);
            if (asset != null)
                asset.name = source.name;
            return asset;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[MenuTheme] No se pudo crear el TMP_FontAsset de '{resourcePath}': {exception.Message}");
            return null;
        }
    }

    /// <summary>Aplica la tipografía sin pisarla si la fuente no está disponible.</summary>
    public static void ApplyDisplayFont(TMP_Text text)
    {
        if (text != null && Display != null) text.font = Display;
    }

    public static void ApplyBodyFont(TMP_Text text)
    {
        if (text != null && Body != null) text.font = Body;
    }

    private static Color Hex(int rgb)
    {
        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f);
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }
}
