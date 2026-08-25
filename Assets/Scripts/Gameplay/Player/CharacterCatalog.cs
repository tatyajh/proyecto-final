using System;
using System.Linq;
using UnityEngine;
using Gameplay.Combat;

/// <summary>
/// Único lugar que sabe qué modelo corresponde a cada personaje.
///
/// Los datos viven en fichas CharacterDefinition (ScriptableObjects en
/// Resources/Characters/Definitions): agregar un personaje es crear una ficha
/// nueva desde el menú Create, sin tocar código. Esta clase queda como fachada
/// — PlayerController, CharacterSpawner y el menú siguen hablando con ella por
/// índice, igual que siempre.
///
/// Si las fichas faltaran (p. ej. un branch sin los .asset), se cae a la tabla
/// interna de respaldo con los mismos seis personajes: el juego nunca arranca
/// con un catálogo vacío.
/// </summary>
public static class CharacterCatalog
{
    private sealed class Entry
    {
        public readonly string Name;
        public readonly string PrefabPath;
        public readonly string PortraitPath;
        public readonly string AnimatorControllerPath;
        public readonly Color Tint;
        public readonly float PreviewScale;
        public readonly float PreviewYaw;
        public readonly Vector3 ModelLocalOffset;
        public readonly float ExpectedGameplayHeight;
        public readonly AbilityDefinition BasicAbility;
        public readonly AbilityDefinition UltimateAbility;

        public Entry(string name, string prefabPath, string portraitPath, string animatorControllerPath,
            Color tint, float previewScale = 1f, float previewYaw = 180f,
            Vector3 modelLocalOffset = default, float expectedGameplayHeight = 0f,
            AbilityDefinition basicAbility = null, AbilityDefinition ultimateAbility = null)
        {
            Name = name;
            PrefabPath = prefabPath;
            PortraitPath = portraitPath;
            AnimatorControllerPath = animatorControllerPath;
            Tint = tint;
            PreviewScale = previewScale;
            PreviewYaw = previewYaw;
            ModelLocalOffset = modelLocalOffset;
            ExpectedGameplayHeight = expectedGameplayHeight;
            BasicAbility = basicAbility;
            UltimateAbility = ultimateAbility;
        }
    }

    private static Entry[] entries;

    private static Entry[] Entries
    {
        get
        {
            if (entries == null) entries = LoadEntries();
            return entries;
        }
    }

    private static Entry[] LoadEntries()
    {
        CharacterDefinition[] definitions = Resources.LoadAll<CharacterDefinition>("Characters/Definitions");
        if (definitions != null && definitions.Length > 0)
        {
            return definitions
                .OrderBy(definition => definition.sortOrder)
                .Select(definition => new Entry(
                    definition.characterName,
                    definition.prefabPath,
                    definition.portraitPath,
                    definition.animatorControllerPath,
                    definition.tint,
                    definition.previewScale,
                    definition.previewYaw,
                    definition.modelLocalOffset,
                    definition.expectedGameplayHeight,
                    definition.basicAbility,
                    definition.ultimateAbility))
                .ToArray();
        }

        Debug.LogWarning("[CharacterCatalog] No hay fichas CharacterDefinition en " +
                         "Resources/Characters/Definitions. Se usa la tabla de respaldo.");
        return Fallback;
    }

    private static readonly Entry[] Fallback =
    {
        new Entry("Heliandra", "Characters/Heliandra", "UI/Portraits/HeliandraCutout", string.Empty, new Color(0.78f, 0.35f, 0.20f)),
        new Entry("Lunara", "Characters/Lunara", "UI/Portraits/LunaraCutout", string.Empty, new Color(0.34f, 0.48f, 0.76f)),
        new Entry("Solmara", "Characters/Solmara", string.Empty, "Characters/Solmara", new Color(0.83f, 0.66f, 0.20f)),
        new Entry("Quietmor", "Characters/Quietmor", string.Empty, "Characters/Quietmor", new Color(0.34f, 0.25f, 0.48f)),
        new Entry("Acatheria", "Characters/Acatheria", string.Empty, "Characters/Acatheria", new Color(0.30f, 0.64f, 0.45f)),
        new Entry("Terramor", "Characters/Terramor", "UI/Portraits/TerramorCutout", string.Empty, new Color(0.43f, 0.30f, 0.20f))
    };

    public static int Count => Entries.Length;

    public static int Clamp(int index) => Mathf.Clamp(index, 0, Entries.Length - 1);

    public static string NameOf(int index) => Entries[Clamp(index)].Name;

    public static Color TintOf(int index) => Entries[Clamp(index)].Tint;

    public static string PathOf(int index) => Entries[Clamp(index)].PrefabPath;

    public static string PortraitPathOf(int index) => Entries[Clamp(index)].PortraitPath;

    public static float PreviewScaleOf(int index) => Entries[Clamp(index)].PreviewScale;

    public static float PreviewYawOf(int index) => Entries[Clamp(index)].PreviewYaw;

    public static Vector3 ModelLocalOffsetOf(int index) => Entries[Clamp(index)].ModelLocalOffset;

    public static float ExpectedGameplayHeightOf(int index) => Entries[Clamp(index)].ExpectedGameplayHeight;

    /// <summary>Prefab del personaje, o null si arte todavía no lo entregó.</summary>
    public static GameObject LoadModel(int index) => Resources.Load<GameObject>(PathOf(index));

    public static bool HasModel(int index) => LoadModel(index) != null;

    public static Texture2D LoadPortrait(int index)
    {
        string path = PortraitPathOf(index);
        return string.IsNullOrWhiteSpace(path) ? null : Resources.Load<Texture2D>(path);
    }

    public static RuntimeAnimatorController LoadAnimatorController(int index)
    {
        string path = Entries[Clamp(index)].AnimatorControllerPath;
        return string.IsNullOrWhiteSpace(path) ? null : Resources.Load<RuntimeAnimatorController>(path);
    }

    public static AbilityDefinition AbilityOf(int index, AbilitySlot slot)
    {
        Entry entry = Entries[Clamp(index)];
        AbilityDefinition configured = slot == AbilitySlot.Ultimate
            ? entry.UltimateAbility
            : entry.BasicAbility;
        return configured != null ? configured : AbilityCatalog.GetFallback(Clamp(index), slot);
    }
}
