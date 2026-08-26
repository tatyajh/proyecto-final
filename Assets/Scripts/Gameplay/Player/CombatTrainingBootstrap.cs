using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Orquesta el mismo duelo local al entrar desde el menú o al abrir Movement.
/// No inicia Photon: crea un humano y un bot con equipos opuestos sobre los
/// mismos PlayerController y AbilityDefinition usados por el online.
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

    [Header("Duelo contra IA")]
    [Tooltip("Zona azul validada en la arena: deja la esfera roja visible al fondo/lateral.")]
    [SerializeField] private Vector3 duelCenter = new Vector3(48f, 0f, -6f);
    [SerializeField, Min(12f)] private float duelSeparation = 18f;
    [SerializeField, Min(6f)] private float validatedSpawnRadius = 13f;

    private PlayerController player;
    private PlayerController opponent;
    private GameObject opponentObject;
    private Coroutine duelRoutine;
    private Vector3 duelForward;
    private int previousOpponent = -1;
    private TrainingRunController runController;
    private int requestedOpponent = -1;
    private int requestedDifficulty = 1;
    private float requestedHealthMultiplier = 1f;

    public PlayerController Player => player;
    public PlayerController Opponent => opponent;
    public TrainingRunController Run => runController;
    public bool DuelActive => player != null && opponent != null &&
        !player.IsDefeated && !opponent.IsDefeated;

    public static CombatTrainingBootstrap EnsureForLocalPlayer(PlayerController localPlayer)
    {
        if (localPlayer == null || localPlayer.IsOnlinePlayer || !localPlayer.HasLocalControl) return null;

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
        // El personaje y la camara se resuelven antes del primer frame. Asi
        // Movement nunca muestra durante un instante el encuadre serializado.
        if (player == null)
        {
            foreach (PlayerController candidate in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (!candidate.IsOnlinePlayer && candidate.HasLocalControl)
                {
                    player = candidate;
                    break;
                }
            }
        }

        if (player == null && spawnCharacterOnStart)
        {
            EnsureSpawner();
            GameObject instance = useSavedCharacter
                ? characterSpawner.SpawnSavedCharacter()
                : characterSpawner.SpawnSelectedCharacter(initialCharacterIndex);
            if (instance != null) player = instance.GetComponentInChildren<PlayerController>(true);
        }

        if (player != null) AttachPlayer(player);
        else Debug.LogError("[CombatTraining] No fue posible crear el personaje humano.");
        yield break;
    }

    public void AttachPlayer(PlayerController localPlayer)
    {
        if (localPlayer == null || localPlayer.IsOnlinePlayer || !localPlayer.HasLocalControl) return;
        player = localPlayer;

        duelForward = ResolveDuelForward(player.transform);
        runController = GetComponent<TrainingRunController>();
        if (runController == null) runController = gameObject.AddComponent<TrainingRunController>();
        runController.Initialize(this, player);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T)) StartNewDuel();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!enableDeveloperCharacterSwitch) return;
        for (int i = 0; i < CharacterCatalog.Count; i++)
        {
            KeyCode number = (KeyCode)((int)KeyCode.Alpha1 + i);
            KeyCode keypad = (KeyCode)((int)KeyCode.Keypad1 + i);
            if (!Input.GetKeyDown(number) && !Input.GetKeyDown(keypad)) continue;
            SpawnCharacter(i);
            break;
        }
#endif
    }

    public void StartNewDuel()
    {
        if (!isActiveAndEnabled || player == null) return;
        if (runController != null)
        {
            runController.RetryOrStartCurrent();
            return;
        }
        requestedOpponent = ChooseOpponent(player.SelectedCharacterIndex);
        requestedDifficulty = 1;
        requestedHealthMultiplier = 1f;
        StartRequestedDuel();
    }

    public void BeginTowerDuel(int opponentIndex, int difficultyLevel, float healthMultiplier)
    {
        if (!isActiveAndEnabled || player == null) return;
        requestedOpponent = CharacterCatalog.Clamp(opponentIndex);
        requestedDifficulty = Mathf.Clamp(difficultyLevel, 1, 5);
        requestedHealthMultiplier = Mathf.Max(1f, healthMultiplier);
        StartRequestedDuel();
    }

    private void StartRequestedDuel()
    {
        if (duelRoutine != null) StopCoroutine(duelRoutine);
        duelRoutine = StartCoroutine(RebuildDuel());
    }

    public void PauseDuel()
    {
        player?.SetLocalControlsEnabled(false);
        opponent?.SetLocalControlsEnabled(false);
        ArenaPowerUpManager.Instance?.SetSimulationPaused(true);
    }

    public void ClearOpponent()
    {
        if (opponentObject != null)
        {
            opponentObject.SetActive(false);
            Destroy(opponentObject);
        }
        opponentObject = null;
        opponent = null;
    }

    private IEnumerator RebuildDuel()
    {
        if (player == null) yield break;
        EnsureSpawner();

        Vector3 center = ResolveGround(duelCenter);
        float halfSeparation = duelSeparation * 0.5f;
        Vector3 humanPosition = ResolveDuelPosition(center, -duelForward, halfSeparation);
        Vector3 opponentPosition = ResolveDuelPosition(center, duelForward, halfSeparation);
        if (Vector3.Distance(humanPosition, opponentPosition) < duelSeparation * 0.72f)
            opponentPosition = ResolveOpponentSpawn(humanPosition, duelForward, duelSeparation);
        Vector3 facing = opponentPosition - humanPosition;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.01f) facing = duelForward;

        // Antes del primer yield: cámara y humano nacen en la zona abierta
        // marcada para el combate. El eje de cámara deja al humano abajo, al
        // rival arriba y la esfera roja desplazada hacia un costado.
        player.ResetLocalCombatState(humanPosition, Quaternion.LookRotation(facing));
        FindFirstObjectByType<MobaCamera>()?.SetTarget(player.transform);

        if (opponentObject != null)
        {
            opponentObject.SetActive(false);
            Destroy(opponentObject);
        }
        opponentObject = null;
        opponent = null;
        yield return null;

        int opponentIndex = requestedOpponent >= 0
            ? CharacterCatalog.Clamp(requestedOpponent)
            : ChooseOpponent(player.SelectedCharacterIndex);
        opponentObject = characterSpawner.SpawnLocalCombatant(LocalCombatantConfig.Bot(opponentIndex),
            opponentPosition, Quaternion.LookRotation(-facing));
        if (opponentObject == null)
        {
            duelRoutine = null;
            yield break;
        }

        opponent = opponentObject.GetComponentInChildren<PlayerController>(true);
        if (opponent == null)
        {
            Debug.LogError("[CombatTraining] El rival no contiene PlayerController.");
            duelRoutine = null;
            yield break;
        }

        opponent.ResetLocalCombatState(opponentPosition, Quaternion.LookRotation(-facing),
            requestedHealthMultiplier);
        TrainingBotController ai = opponentObject.GetComponent<TrainingBotController>();
        if (ai == null) ai = opponentObject.AddComponent<TrainingBotController>();
        ai.ConfigureDifficulty(requestedDifficulty);
        ai.Configure(opponent, player);

        ArenaPowerUpManager powerUps = ArenaPowerUpManager.EnsureFor(player);
        if (powerUps != null)
        {
            powerUps.ConfigureTraining(player, opponent, center);
            powerUps.SetSimulationPaused(false);
        }

        previousOpponent = opponentIndex;
        runController?.BindOpponent(opponent);
        duelRoutine = null;
        float actualSeparation = Vector3.Distance(player.transform.position, opponent.transform.position);
        Debug.Log($"[CombatTraining] Duelo listo: {player.PlayerDisplayName} contra " +
                  $"{opponent.PlayerDisplayName}, separados {actualSeparation:0.0} unidades. " +
                  $"Zona validada: humano {FormatPosition(player.transform.position)}, " +
                  $"rival {FormatPosition(opponent.transform.position)}.");

        float humanRadius = PlanarDistance(center, player.transform.position);
        float rivalRadius = PlanarDistance(center, opponent.transform.position);
        if (humanRadius > validatedSpawnRadius || rivalRadius > validatedSpawnRadius)
            Debug.LogWarning($"[CombatTraining] Un spawn salió de la zona azul " +
                             $"(humano {humanRadius:0.0}, rival {rivalRadius:0.0}, " +
                             $"máximo {validatedSpawnRadius:0.0}).");
    }

    private int ChooseOpponent(int playerIndex)
    {
        List<int> choices = new List<int>(CharacterCatalog.Count);
        for (int i = 0; i < CharacterCatalog.Count; i++)
            if (i != playerIndex && i != previousOpponent) choices.Add(i);

        if (choices.Count == 0)
            for (int i = 0; i < CharacterCatalog.Count; i++)
                if (i != playerIndex) choices.Add(i);

        return choices.Count > 0 ? choices[Random.Range(0, choices.Count)] : playerIndex;
    }

    private void EnsureSpawner()
    {
        if (characterSpawner != null) return;
        characterSpawner = GetComponent<CharacterSpawner>();
        if (characterSpawner == null) characterSpawner = gameObject.AddComponent<CharacterSpawner>();
    }

    private static Vector3 ResolveDuelForward(Transform human)
    {
        Camera view = Camera.main;
        Vector3 forward = view != null
            ? Vector3.ProjectOnPlane(view.transform.forward, Vector3.up)
            : Vector3.ProjectOnPlane(human.forward, Vector3.up);
        return forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
    }

    private static Vector3 ResolveOpponentSpawn(Vector3 origin, Vector3 forward, float distance)
    {
        float[] angles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 180f };
        float minimumSeparation = Mathf.Max(12f, distance * 0.72f);
        Vector3 farthestCandidate = origin + forward * distance;
        float farthestDistance = 0f;

        foreach (float angle in angles)
        {
            Vector3 desired = origin + Quaternion.AngleAxis(angle, Vector3.up) * forward * distance;
            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                continue;

            Vector3 planar = hit.position - origin;
            planar.y = 0f;
            float actualSeparation = planar.magnitude;
            if (actualSeparation > farthestDistance)
            {
                farthestDistance = actualSeparation;
                farthestCandidate = hit.position;
            }

            if (actualSeparation >= minimumSeparation)
                return hit.position;
        }

        // En sectores estrechos del NavMesh se usa el punto navegable más
        // lejano encontrado, nunca el primer punto cercano al jugador.
        return farthestDistance > 0f
            ? farthestCandidate
            : ResolveGround(origin + forward * distance);
    }

    private static Vector3 ResolveDuelPosition(Vector3 center, Vector3 direction, float distance)
    {
        direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
        float[] angles = { 0f, 18f, -18f, 35f, -35f };
        foreach (float angle in angles)
        {
            Vector3 ray = Quaternion.AngleAxis(angle, Vector3.up) * direction;
            Vector3 desired = center + ray * distance;
            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                return hit.position;
        }
        return ResolveGround(center + direction * distance);
    }

    private static Vector3 ResolveGround(Vector3 desiredPosition)
    {
        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit navHit, 12f, NavMesh.AllAreas))
            return navHit.position;
        if (Physics.Raycast(desiredPosition + Vector3.up * 40f, Vector3.down, out RaycastHit hit,
            100f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            return hit.point;
        return desiredPosition;
    }

    private static float PlanarDistance(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        return delta.magnitude;
    }

    private static string FormatPosition(Vector3 position) =>
        $"({position.x:0.0}, {position.y:0.0}, {position.z:0.0})";

    private void OnDrawGizmos()
    {
        Vector3 center = duelCenter;
        Vector3 forward = ResolveDuelForward(transform);
        float halfSeparation = duelSeparation * 0.5f;

        // La circunferencia azul marca la zona abierta acordada para el duelo.
        // Las esferas verde/dorada muestran los dos puntos previstos antes de
        // que el NavMesh haga el pequeño ajuste vertical al entrar en Play.
        Gizmos.color = new Color(0.12f, 0.58f, 1f, 0.82f);
        Gizmos.DrawWireSphere(center, validatedSpawnRadius);
        Vector3 human = center - forward * halfSeparation;
        Vector3 rival = center + forward * halfSeparation;
        Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.9f);
        Gizmos.DrawWireSphere(human, 1.25f);
        Gizmos.color = new Color(1f, 0.38f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(rival, 1.25f);
        Gizmos.DrawLine(human, rival);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void SpawnCharacter(int characterIndex)
    {
        EnsureSpawner();
        if (player != null) Destroy(player.gameObject);
        GameObject instance = characterSpawner.SpawnSelectedCharacter(characterIndex);
        if (instance == null) return;

        player = instance.GetComponentInChildren<PlayerController>(true);
        AttachPlayer(player);
    }
#endif

    private void OnDestroy()
    {
        if (opponentObject != null) Destroy(opponentObject);
    }
}
