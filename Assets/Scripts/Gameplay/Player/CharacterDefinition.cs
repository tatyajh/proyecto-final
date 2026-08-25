using UnityEngine;
using Gameplay.Combat;

/// <summary>
/// Ficha de un personaje jugable (spec 05 del paquete de menú): nombre, ruta
/// del prefab en Resources y color provisional, editables desde el inspector
/// sin tocar código.
///
/// Las fichas viven en Resources/Characters/Definitions y CharacterCatalog
/// las carga al arrancar. Para agregar un personaje: crear una ficha nueva
/// (Create > Blighted Blossoms > Character Definition), llenarla, y listo —
/// el menú y el juego lo recogen solos. El orden del carrusel lo da sortOrder,
/// no el nombre del archivo.
/// </summary>
[CreateAssetMenu(fileName = "Character", menuName = "Blighted Blossoms/Character Definition")]
public sealed class CharacterDefinition : ScriptableObject
{
    [Tooltip("Nombre que ve el jugador en el carrusel y en la arena.")]
    public string characterName;

    [Tooltip("Ruta dentro de Resources del prefab jugable, p. ej. Characters/Solmara.")]
    public string prefabPath;

    [Tooltip("Retrato PNG con transparencia real para personajes que todavía no tienen modelo 3D.")]
    public string portraitPath;

    [Tooltip("Animator Controller opcional dentro de Resources. Se aplica al crear el personaje si el prefab no lo trae asignado.")]
    public string animatorControllerPath;

    [Tooltip("Posición en el carrusel. El índice guardado en PlayerPrefs sigue este orden.")]
    public int sortOrder;

    [Tooltip("Color de la silueta provisional mientras arte no entrega el modelo.")]
    public Color tint = Color.gray;

    [Header("Presentación en el carrusel")]
    [Min(0.1f)] public float previewScale = 1f;
    public float previewYaw = 180f;

    [Header("Ajuste del modelo jugable")]
    [Tooltip("Compensa pivotes exportados fuera de los pies sin alterar el spawn, la cámara ni los demás personajes.")]
    public Vector3 modelLocalOffset;

    [Header("Habilidades")]
    [Tooltip("Ataque básico configurable. Si queda vacío se usa la ficha oficial del catálogo.")]
    public AbilityDefinition basicAbility;
    [Tooltip("Definitiva configurable. Si queda vacía se usa la ficha oficial del catálogo.")]
    public AbilityDefinition ultimateAbility;
}
