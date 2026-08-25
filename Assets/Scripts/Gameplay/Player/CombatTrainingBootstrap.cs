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
    [SerializeField, Min(12f)] private float duelSeparation = 18f;

    private PlayerController player;
    private PlayerController opponent;
    private GameObject opponentObject;
    private Coroutine duelRoutine;
    private Vector3 humanSpawnPosition;
    private Vector3 duelForward;
    private bool spawnCaptured;
    private int previousOpponent = -1;

    public PlayerController Player => player;
    public PlayerController Opponent => opponent;
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
        yield return null;

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
    }

    public void AttachPlayer(PlayerController localPlayer)
    {
        if (localPlayer == null || localPlayer.IsOnlinePlayer || !localPlayer.HasLocalControl) return;
        player = localPlayer;

        if (!spawnCaptured)
        {
            humanSpawnPosition = ResolveGround(player.transform.position);
            duelForward = ResolveDuelForward(player.transform);
            spawnCaptured = true;
        }

        StartNewDuel();
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
        if (duelRoutine != null) StopCoroutine(duelRoutine);
        duelRoutine = StartCoroutine(RebuildDuel());
    }

    private IEnumerator RebuildDuel()
    {
        if (opponentObject != null)
        {
            opponentObject.SetActive(false);
            Destroy(opponentObject);
        }
        opponentObject = null;
        opponent = null;
        yield return null;

        if (player == null) yield break;
        EnsureSpawner();

        Vector3 humanPosition = ResolveGround(humanSpawnPosition);
        Vector3 opponentPosition = ResolveOpponentSpawn(humanPosition, duelForward, duelSeparation);
        Vector3 facing = opponentPosition - humanPosition;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.01f) facing = duelForward;

        player.ResetLocalCombatState(humanPosition, Quaternion.LookRotation(facing));

        int opponentIndex = ChooseOpponent(player.SelectedCharacterIndex);
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

        opponent.ResetLocalCombatState(opponentPosition, Quaternion.LookRotation(-facing));
        TrainingBotController ai = opponentObject.GetComponent<TrainingBotController>();
        if (ai == null) ai = opponentObject.AddComponent<TrainingBotController>();
        ai.Configure(opponent, player);

        ArenaPowerUpManager powerUps = ArenaPowerUpManager.EnsureFor(player);
        if (powerUps != null)
            powerUps.ConfigureTraining(player, opponent,
                Vector3.Lerp(player.transform.position, opponent.transform.position, 0.5f));

        previousOpponent = opponentIndex;
        duelRoutine = null;
        float actualSeparation = Vector3.Distance(player.transform.position, opponent.transform.position);
        Debug.Log($"[CombatTraining] Duelo listo: {player.PlayerDisplayName} contra " +
                  $"{opponent.PlayerDisplayName}, separados {actualSeparation:0.0} unidades.");
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

    private static Vector3 ResolveGround(Vector3 desiredPosition)
    {
        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit navHit, 12f, NavMesh.AllAreas))
            return navHit.position;
        if (Physics.Raycast(desiredPosition + Vector3.up * 40f, Vector3.down, out RaycastHit hit,
            100f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            return hit.point;
        return desiredPosition;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void SpawnCharacter(int characterIndex)
    {
        EnsureSpawner();
        if (player != null) Destroy(player.gameObject);
        GameObject instance = characterSpawner.SpawnSelectedCharacter(characterIndex);
        if (instance == null) return;

        player = instance.GetComponentInChildren<PlayerController>(true);
        spawnCaptured = false;
        AttachPlayer(player);
    }
#endif

    private void OnDestroy()
    {
        if (opponentObject != null) Destroy(opponentObject);
    }
}
