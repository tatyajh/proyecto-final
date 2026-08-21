using UnityEngine;

/// <summary>
/// Único lugar que sabe qué modelo corresponde a cada personaje.
///
/// Para integrar un personaje nuevo NO hace falta tocar código: basta con
/// dejar su prefab en `Assets/Resources/Characters/` con el nombre que aparece
/// aquí abajo. Si el prefab existe, el juego y el menú lo usan; si no existe,
/// ambos caen en la silueta provisional de primitivas.
/// </summary>
public static class CharacterCatalog
{
    public static readonly string[] Names =
    {
        "Heliandra", "Lunara", "Solmara", "Quietmor", "Acatheria", "Terramor"
    };

    /// <summary>
    /// Ruta dentro de Resources por índice. Quietmor conserva "CampanaPrototype"
    /// porque así se llama el prefab que ya genera CharacterPrototypeImporter;
    /// el resto sigue la convención Characters/&lt;Nombre&gt;.
    /// </summary>
    private static readonly string[] PrefabPaths =
    {
        "Characters/Heliandra",
        "Characters/Lunara",
        "Characters/Solmara",
        "Characters/CampanaPrototype",
        "Characters/Acatheria",
        "Characters/Terramor"
    };

    /// <summary>Color de la silueta provisional mientras no llega el modelo.</summary>
    public static readonly Color[] Tints =
    {
        new Color(0.78f, 0.35f, 0.20f),
        new Color(0.34f, 0.48f, 0.76f),
        new Color(0.83f, 0.66f, 0.20f),
        new Color(0.34f, 0.25f, 0.48f),
        new Color(0.30f, 0.64f, 0.45f),
        new Color(0.43f, 0.30f, 0.20f)
    };

    public static int Count => Names.Length;

    public static int Clamp(int index) => Mathf.Clamp(index, 0, Names.Length - 1);

    public static string NameOf(int index) => Names[Clamp(index)];

    public static Color TintOf(int index) => Tints[Clamp(index)];

    public static string PathOf(int index) => PrefabPaths[Clamp(index)];

    /// <summary>Prefab del personaje, o null si arte todavía no lo entregó.</summary>
    public static GameObject LoadModel(int index) => Resources.Load<GameObject>(PathOf(index));

    public static bool HasModel(int index) => LoadModel(index) != null;
}
