using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Instancia un personaje jugable **en local**, sin Photon ni matchmaking.
///
/// Existe para poder probar movimiento y combate sin pasar por el menú ni
/// esperar rivales. El spawner de partidas reales sigue siendo PlayerSpawner,
/// que crea avatares en red; este no toca nada de eso.
///
/// Si no se arrastra ningún prefab, cae en CharacterCatalog y carga el modelo
/// desde Resources, así que funciona aunque el array esté vacío.
/// </summary>
public sealed class CharacterSpawner : MonoBehaviour
{
    [Header("Personajes")]
    [Tooltip("Opcional. Orden: Heliandra, Lunara, Solmara, Quietmor, Acatheria, Terramor. " +
             "Los huecos vacíos se resuelven desde Resources/Characters.")]
    [SerializeField] private GameObject[] characterPrefabs;

    [Header("Aparición")]
    [Tooltip("Dónde nace el personaje. Si se deja vacío se usa este mismo objeto.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Cámara")]
    [SerializeField] private bool focusCameraOnSpawn = true;

    private GameObject current;

    public GameObject Current => current;

    /// <summary>
    /// Crea el personaje indicado y devuelve la instancia. Reemplaza al anterior
    /// si ya había uno, para poder cambiar de personaje sin reiniciar la escena.
    /// </summary>
    public GameObject SpawnSelectedCharacter(int characterIndex)
    {
        characterIndex = CharacterCatalog.Clamp(characterIndex);

        if (current != null) Destroy(current);

        Transform anchor = spawnPoint != null ? spawnPoint : transform;
        LocalCombatantConfig config = LocalCombatantConfig.Human(characterIndex,
            CharacterCatalog.NameOf(characterIndex));
        current = SpawnLocalCombatant(config, anchor.position, anchor.rotation);
        if (current == null) return null;
        RememberSelection(characterIndex);
        FocusCamera(current.transform);

        Debug.Log($"[CharacterSpawner] {CharacterCatalog.NameOf(characterIndex)} listo en {anchor.position}.");
        return current;
    }

    /// <summary>
    /// Crea un combatiente local independiente. A diferencia de
    /// SpawnSelectedCharacter no reemplaza al humano, no cambia PlayerPrefs y
    /// no roba el seguimiento de cámara; por eso sirve para el rival de IA.
    /// </summary>
    public GameObject SpawnLocalCombatant(LocalCombatantConfig config, Vector3 position, Quaternion rotation)
    {
        config.CharacterIndex = CharacterCatalog.Clamp(config.CharacterIndex);
        PlayModeContext.UseTraining();
        OnlineMatchState.Reset();

        GameObject prefab = ResolvePrefab(config.CharacterIndex);
        GameObject instance = prefab != null && prefab.GetComponentInChildren<PlayerController>(true) != null
            ? Instantiate(prefab, position, rotation)
            : CreateLocalPlayableRig(position, rotation);
        // Mantener el ajuste final de Julian en todos los caminos de spawn,
        // incluido entrenamiento: se escala Player, no el mesh importado.
        instance.transform.localScale = Vector3.one * PlayerController.GameplayPlayerScale;
        instance.name = config.Role == LocalCombatantRole.TrainingBot
            ? $"Training Bot ({CharacterCatalog.NameOf(config.CharacterIndex)})"
            : $"Player ({CharacterCatalog.NameOf(config.CharacterIndex)})";

        PlayerController controller = instance.GetComponentInChildren<PlayerController>(true);
        if (controller == null)
        {
            Debug.LogError($"[CharacterSpawner] {instance.name} no contiene PlayerController.");
            Destroy(instance);
            return null;
        }

        controller.ConfigureLocalCombatant(config);
        PlaceOnNavMesh(instance, position);
        return instance;
    }

    /// <summary>
    /// Los prefabs de Resources/Characters son arte, no rigs jugables. El
    /// PlayerController se encarga de montar ese arte sobre esta cápsula base,
    /// manteniendo movimiento, colisión y combate iguales para los seis.
    /// </summary>
    private static GameObject CreateLocalPlayableRig(Vector3 position, Quaternion rotation)
    {
        GameObject rig = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        rig.name = "Local playable rig";
        rig.transform.SetPositionAndRotation(position, rotation);
        rig.transform.localScale = Vector3.one * PlayerController.GameplayPlayerScale;

        NavMeshAgent agent = rig.AddComponent<NavMeshAgent>();
        agent.radius = 0.67f;
        agent.height = 2f;
        // El Player completo se escala a 4.5. Un baseOffset de 1 también se
        // escalaba y levantaba el rig varios metros sobre el NavMesh.
        agent.baseOffset = 0f;
        agent.speed = 6f;

        rig.AddComponent<Gameplay.Combat.PlayerCombatController>();
        rig.AddComponent<PlayerController>();
        return rig;
    }

    /// <summary>Crea el personaje guardado en PlayerPrefs, el que eligió el jugador.</summary>
    public GameObject SpawnSavedCharacter()
    {
        return SpawnSelectedCharacter(PlayerPrefs.GetInt("SelectedCharacterIndex", 3));
    }

    private GameObject ResolvePrefab(int characterIndex)
    {
        // Lo arrastrado en el inspector manda; si no, la convención de Resources.
        if (characterPrefabs != null &&
            characterIndex < characterPrefabs.Length &&
            characterPrefabs[characterIndex] != null)
        {
            return characterPrefabs[characterIndex];
        }

        return CharacterCatalog.LoadModel(characterIndex);
    }

    /// <summary>
    /// Un NavMeshAgent que nace fuera de la malla no se mueve y no da ningún
    /// error visible: es la causa habitual de "el personaje aparece y se queda
    /// clavado". Se busca el punto navegable más cercano.
    /// </summary>
    private static void PlaceOnNavMesh(GameObject character, Vector3 desired)
    {
        NavMeshAgent agent = character.GetComponentInChildren<NavMeshAgent>();
        if (agent == null) return;

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 12f, NavMesh.AllAreas))
        {
            if (agent.isActiveAndEnabled)
            {
                if (!agent.Warp(hit.position))
                    character.transform.position = hit.position;
            }
            else
            {
                character.transform.position = hit.position;
            }
            return;
        }

        Debug.LogWarning(
            "[CharacterSpawner] No se encontró NavMesh cerca del punto de aparición. " +
            "El personaje aparecerá pero no podrá caminar hasta que se hornee la navegación.");
    }

    private static void RememberSelection(int characterIndex)
    {
        PlayerPrefs.SetInt("SelectedCharacterIndex", characterIndex);
        PlayerPrefs.SetString("SelectedCharacter", CharacterCatalog.NameOf(characterIndex));
        PlayerPrefs.Save();
    }

    private void FocusCamera(Transform target)
    {
        if (!focusCameraOnSpawn) return;

        MobaCamera camera = FindFirstObjectByType<MobaCamera>();
        if (camera != null) camera.SetTarget(target);
        else Debug.LogWarning("[CharacterSpawner] No hay MobaCamera en la escena: la cámara no seguirá al personaje.");
    }
}
