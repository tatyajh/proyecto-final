using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Banco de combate local reutilizable. Movement puede serializarlo en escena y
/// el botón Entrenar obtiene la misma configuración cuando aparece el jugador.
/// No inicia Photon ni altera la configuración de la cámara.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-80)]
public sealed class CombatTrainingBootstrap : MonoBehaviour
{
    [Header("Personaje local")]
    [SerializeField] private CharacterSpawner characterSpawner;
    [SerializeField] private bool spawnCharacterOnStart = true;
    [SerializeField] private bool useSavedCharacter = true;
    [SerializeField, Range(0, 5)] private int initialCharacterIndex = 3;
    [SerializeField] private bool enableDeveloperCharacterSwitch = true;

    [Header("Objetivos")]
    [SerializeField, Min(1)] private int targetHealth = 60;
    [SerializeField] private Vector3 targetScale = new Vector3(2.2f, 3.5f, 2.2f);
    [SerializeField] private Color targetColor = new Color(0.28f, 0.10f, 0.22f, 1f);

    private readonly List<GameObject> targets = new List<GameObject>(4);
    private PlayerController player;
    private Coroutine rebuildRoutine;

    public PlayerController Player => player;

    public static CombatTrainingBootstrap EnsureForLocalPlayer(PlayerController localPlayer)
    {
        if (localPlayer == null || localPlayer.IsOnlinePlayer) return null;

        CombatTrainingBootstrap training = FindFirstObjectByType<CombatTrainingBootstrap>();
        if (training == null)
        {
            GameObject host = new GameObject("Combat Training");
            training = host.AddComponent<CombatTrainingBootstrap>();
            training.spawnCharacterOnStart = false;
            training.enableDeveloperCharacterSwitch = false;
        }

        training.AttachPlayer(localPlayer);
        return training;
    }

    private void Awake()
    {
        PlayModeContext.UseTraining();
        OnlineMatchState.Reset();
        if (characterSpawner == null) characterSpawner = GetComponent<CharacterSpawner>();
    }

    private IEnumerator Start()
    {
        // Espera un frame para dejar que un rig ya presente complete Start.
        yield return null;

        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null && spawnCharacterOnStart)
        {
            if (characterSpawner == null)
                characterSpawner = gameObject.AddComponent<CharacterSpawner>();

            GameObject instance = useSavedCharacter
                ? characterSpawner.SpawnSavedCharacter()
                : characterSpawner.SpawnSelectedCharacter(initialCharacterIndex);
            if (instance != null) player = instance.GetComponent<PlayerController>();
        }

        if (player != null) AttachPlayer(player);
        else Debug.LogError("[CombatTraining] No fue posible crear un personaje local para el entrenamiento.");
    }

    public void AttachPlayer(PlayerController localPlayer)
    {
        if (localPlayer == null || localPlayer.IsOnlinePlayer) return;
        player = localPlayer;
        ScheduleTargetReset();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T)) ResetTargets();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!enableDeveloperCharacterSwitch) return;
        for (int i = 0; i < CharacterCatalog.Count; i++)
        {
            KeyCode number = (KeyCode)((int)KeyCode.Alpha1 + i);
            KeyCode keypad = (KeyCode)((int)KeyCode.Keypad1 + i);
            if (!Input.GetKeyDown(number) && !Input.GetKeyDown(keypad))
                continue;

            SpawnCharacter(i);
            break;
        }
#endif
    }

    public void ResetTargets()
    {
        ScheduleTargetReset();
    }

    private void ScheduleTargetReset()
    {
        if (!isActiveAndEnabled || player == null) return;
        if (rebuildRoutine != null) StopCoroutine(rebuildRoutine);
        rebuildRoutine = StartCoroutine(RebuildTargets());
    }

    private IEnumerator RebuildTargets()
    {
        foreach (GameObject target in targets)
        {
            if (target == null) continue;
            target.SetActive(false);
            Destroy(target);
        }
        targets.Clear();

        // Destroy se aplica al final del frame; esperar evita colliders dobles.
        yield return null;
        if (player == null) yield break;

        // Los objetivos deben quedar fuera de la línea de visión inicial. Se
        // colocan al otro lado del personaje respecto de la cámara y abiertos
        // en abanico, pero conservando radios válidos para Q y R.
        Camera view = Camera.main;
        Vector3 awayFromCamera = view != null
            ? Vector3.ProjectOnPlane(player.transform.position - view.transform.position, Vector3.up).normalized
            : Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        if (awayFromCamera.sqrMagnitude < 0.01f) awayFromCamera = Vector3.forward;
        Vector3 origin = player.transform.position;

        Vector3[] offsets =
        {
            Quaternion.AngleAxis(55f, Vector3.up) * awayFromCamera * 4f,
            Quaternion.AngleAxis(-55f, Vector3.up) * awayFromCamera * 4f,
            Quaternion.AngleAxis(55f, Vector3.up) * awayFromCamera * 7f,
            Quaternion.AngleAxis(-55f, Vector3.up) * awayFromCamera * 7f
        };

        for (int i = 0; i < offsets.Length; i++)
            targets.Add(CreateTarget(i, origin + offsets[i]));

        rebuildRoutine = null;
        Debug.Log("[CombatTraining] Cuatro objetivos listos: dos para ataque y dos para definitiva.");
    }

    private GameObject CreateTarget(int index, Vector3 desiredPosition)
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        target.name = $"Objetivo de entrenamiento {index + 1}";
        target.transform.SetParent(transform, true);
        target.transform.localScale = targetScale;

        Vector3 ground = ResolveGround(desiredPosition);
        float halfHeight = targetScale.y;
        target.transform.position = ground + Vector3.up * halfHeight;

        Renderer renderer = target.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader) { name = "Training Target (Runtime)", color = targetColor };
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", targetColor * 0.18f);
        renderer.sharedMaterial = material;

        DestructiblePracticeTarget practiceTarget = target.AddComponent<DestructiblePracticeTarget>();
        practiceTarget.Configure(targetHealth, targetColor);
        return target;
    }

    private static Vector3 ResolveGround(Vector3 desiredPosition)
    {
        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
            return navHit.position;

        if (Physics.Raycast(desiredPosition + Vector3.up * 40f, Vector3.down, out RaycastHit hit,
            100f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            return hit.point;

        return desiredPosition;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void SpawnCharacter(int characterIndex)
    {
        if (characterSpawner == null)
        {
            characterSpawner = GetComponent<CharacterSpawner>();
            if (characterSpawner == null) characterSpawner = gameObject.AddComponent<CharacterSpawner>();
        }

        GameObject instance = characterSpawner.SpawnSelectedCharacter(characterIndex);
        if (instance == null) return;

        PlayerController selected = instance.GetComponent<PlayerController>();
        if (selected != null) AttachPlayer(selected);
    }
#endif

    private void OnDestroy()
    {
        foreach (GameObject target in targets)
            if (target != null) Destroy(target);
    }
}
