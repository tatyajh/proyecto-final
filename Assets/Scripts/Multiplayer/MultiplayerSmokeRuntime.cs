using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Gameplay.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instrumentación dormida en builds normales. Un Development Build con
/// -bb-smoke=1v1|2v2|3v3 se conecta solo y genera movimiento/ataques desde un
/// proceso independiente, permitiendo probar 2, 4 y 6 clientes reales.
/// </summary>
public sealed class MultiplayerSmokeRuntime : MonoBehaviour
{
    public static bool Active { get; private set; }
    public static bool Invulnerable { get; private set; }
    public static string DesiredName { get; private set; }
    public static int DesiredCharacterIndex { get; private set; }

    private MatchModeDefinition mode;
    private int expectedPlayers;
    private float duration = 45f;
    private float connectDeadline;
    private float matchStartedAt = -1f;
    private float nextBasic;
    private float nextUltimate;
    private PlayerController localPlayer;
    private readonly Dictionary<int, Vector3> initialRemotePositions = new Dictionary<int, Vector3>();
    private readonly HashSet<int> movingRemotePlayers = new HashSet<int>();
    private bool sawRemoteCharacter;
    private bool sawRemoteHealthChange;
    private bool reportedReady;
    private bool networkSimulationEnabled;
    private bool useTransportSimulation;
    private bool suppressCombat;
    private OnlineMatchPhase lastPhase = (OnlineMatchPhase)(-1);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForDevelopmentSmoke()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string modeKey = Argument("-bb-smoke");
        if (string.IsNullOrWhiteSpace(modeKey) || FindFirstObjectByType<MultiplayerSmokeRuntime>() != null) return;
        GameObject host = new GameObject("Multiplayer Smoke Runtime");
        DontDestroyOnLoad(host);
        host.AddComponent<MultiplayerSmokeRuntime>();
#endif
    }

    private async void Start()
    {
        Active = true;
        Invulnerable = HasArgument("-bb-soak");
        Application.targetFrameRate = 30;
        mode = MatchModeCatalog.GetByKey(Argument("-bb-smoke"));
        expectedPlayers = mode.PlayerCount;
        if (float.TryParse(Argument("-bb-duration"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float parsedDuration))
            duration = Mathf.Max(10f, parsedDuration);

        int client = int.TryParse(Argument("-bb-client"), out int parsedClient) ? parsedClient : 0;
        DesiredName = $"Smoke-{mode.Key}-{client}";
        DesiredCharacterIndex = client % CharacterCatalog.Count;
        PlayerPrefs.SetString("PlayerName", DesiredName);
        PlayerPrefs.SetInt("SelectedCharacterIndex", DesiredCharacterIndex);
        PlayerPrefs.Save();
        useTransportSimulation = HasArgument("-bb-network-sim");
        suppressCombat = HasArgument("-bb-no-combat");
        MatchContext.Select(mode);
        PlayModeContext.UseMultiplayer();

        int arenaIndex = SceneUtility.GetBuildIndexByScenePath(GameScenes.ArenaPath);
        if (arenaIndex < 0)
        {
            Finish(2, "OnlineArena no está en el build.");
            return;
        }

        NetworkLauncher launcher = gameObject.AddComponent<NetworkLauncher>();
        connectDeadline = Time.realtimeSinceStartup + 40f;
        bool connected = await launcher.StartGame(GameMode.Shared, mode, arenaIndex);
        if (!connected) Finish(2, $"Photon no conectó: {NetworkLauncher.LastFailureReason}");
    }

    private void Update()
    {
        if (!Active) return;
        if (Time.realtimeSinceStartup > connectDeadline && localPlayer == null)
        {
            Finish(2, "Timeout esperando el avatar local.");
            return;
        }

        if (localPlayer == null)
        {
            foreach (PlayerController candidate in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (candidate.IsOnlinePlayer && candidate.HasLocalControl) localPlayer = candidate;
            return;
        }

        PlayerController[] players = Array.FindAll(
            FindObjectsByType<PlayerController>(FindObjectsSortMode.None),
            player => player.IsOnlinePlayer && player.Object != null && player.Object.InputAuthority.IsValid);
        TrackRemoteReplication(players);
        if (OnlineMatchState.Phase != lastPhase)
        {
            lastPhase = OnlineMatchState.Phase;
            Debug.Log($"[MultiplayerSmoke] STATE {mode.Key}: {lastPhase} · {OnlineMatchState.Message}");
        }
        if (!localPlayer.MatchReady)
        {
            if (Time.realtimeSinceStartup > connectDeadline)
                Finish(2, $"Timeout esperando inicio simultáneo: {players.Length}/{expectedPlayers} avatares.");
            return;
        }

        if (matchStartedAt < 0f)
        {
            matchStartedAt = Time.realtimeSinceStartup;
            nextBasic = matchStartedAt + 1f;
            nextUltimate = matchStartedAt + 3f;
            if (useTransportSimulation && !networkSimulationEnabled)
            {
                ConfigureNetworkSimulation();
                networkSimulationEnabled = true;
            }
        }

        if (!reportedReady)
        {
            reportedReady = true;
            Debug.Log($"[MultiplayerSmoke] READY {mode.Key}: {players.Length}/{expectedPlayers}, " +
                      $"equipo {localPlayer.Team}, personaje {localPlayer.PlayerDisplayName}.");
        }

        DriveCombat(players);
        float elapsed = Time.realtimeSinceStartup - matchStartedAt;
        if (!Invulnerable && !string.IsNullOrEmpty(localPlayer.MatchResultText))
        {
            bool replicationOk = movingRemotePlayers.Count >= expectedPlayers - 1 && sawRemoteCharacter;
            Finish(replicationOk ? 0 : 2,
                $"Resultado {localPlayer.MatchResultText}; remotos moviéndose {movingRemotePlayers.Count}/{expectedPlayers - 1}; " +
                $"vida remota cambió={sawRemoteHealthChange}.");
        }
        else if (elapsed >= duration)
        {
            bool success = players.Length == expectedPlayers &&
                           movingRemotePlayers.Count >= expectedPlayers - 1 && sawRemoteCharacter &&
                           (Invulnerable || suppressCombat || sawRemoteHealthChange);
            Finish(success ? 0 : 2,
                $"Soak {elapsed:0}s; jugadores {players.Length}/{expectedPlayers}; remotos moviéndose " +
                $"{movingRemotePlayers.Count}/{expectedPlayers - 1}; vida remota cambió={sawRemoteHealthChange}.");
        }
    }

    private void TrackRemoteReplication(PlayerController[] players)
    {
        foreach (PlayerController player in players)
        {
            if (!player.IsOnlinePlayer || player.HasLocalControl || player.Object == null) continue;
            int id = player.Object.InputAuthority.PlayerId;
            sawRemoteCharacter |= !string.IsNullOrWhiteSpace(player.PlayerDisplayName);
            if (!initialRemotePositions.TryGetValue(id, out Vector3 initial))
                initialRemotePositions[id] = player.transform.position;
            else if ((player.transform.position - initial).sqrMagnitude > 0.36f)
                movingRemotePlayers.Add(id);
            if (player.CurrentHealth < PlayerController.MaxHealth) sawRemoteHealthChange = true;
        }
    }

    private void DriveCombat(PlayerController[] players)
    {
        PlayerController closestEnemy = null;
        float bestDistance = float.MaxValue;
        foreach (PlayerController candidate in players)
        {
            if (candidate == localPlayer || candidate.IsDefeated || localPlayer.IsAllyOf(candidate)) continue;
            float distance = (candidate.transform.position - localPlayer.transform.position).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; closestEnemy = candidate; }
        }

        Vector3 desired = closestEnemy != null
            ? Vector3.ProjectOnPlane(closestEnemy.transform.position - localPlayer.transform.position, Vector3.up)
            : localPlayer.transform.forward;
        Vector3 direction = desired.sqrMagnitude > 0.01f ? desired.normalized : localPlayer.transform.forward;
        float distanceToEnemy = Mathf.Sqrt(bestDistance);

        Camera view = Camera.main;
        Vector3 forward = view != null ? Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = view != null ? Vector3.ProjectOnPlane(view.transform.right, Vector3.up).normalized : Vector3.right;
        Vector3 movement = distanceToEnemy > 4f ? direction : Vector3.Cross(Vector3.up, direction);
        localPlayer.SetAutomatedTestInput(new Vector2(Vector3.Dot(movement, right), Vector3.Dot(movement, forward)));

        if (suppressCombat) return;

        AimData aim = new AimData { Direction = direction, DistanceRatio = 1f, IsTap = true };
        if (Time.realtimeSinceStartup >= nextBasic)
        {
            localPlayer.TryCastAbility(AbilitySlot.Basic, aim);
            nextBasic = Time.realtimeSinceStartup + (Invulnerable ? 2.5f : 1.1f);
        }
        if (Time.realtimeSinceStartup >= nextUltimate)
        {
            localPlayer.TryCastAbility(AbilitySlot.Ultimate, aim);
            nextUltimate = Time.realtimeSinceStartup + 8.2f;
        }
    }

    private static void ConfigureNetworkSimulation()
    {
        NetworkSimulationConfiguration conditions = NetworkProjectConfig.Global.NetworkConditions;
        conditions.Enabled = true;
        conditions.DelayMin = 0.15f;
        conditions.DelayMax = 0.15f;
        conditions.AdditionalJitter = 0f;
        conditions.LossChanceMin = 0.05f;
        conditions.LossChanceMax = 0.05f;
        conditions.AdditionalLoss = 0f;
        Debug.Log("[MultiplayerSmoke] Simulación activa: 150 ms, 5% pérdida.");
    }

    private void Finish(int code, string detail)
    {
        if (!Active) return;
        Active = false;
        Debug.Log($"[MultiplayerSmoke] {(code == 0 ? "PASS" : "FAIL")} {mode?.Key}: {detail}");
        Application.Quit(code);
    }

    private static bool HasArgument(string key)
    {
        foreach (string argument in Environment.GetCommandLineArgs())
            if (string.Equals(argument, key, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string Argument(string key)
    {
        string prefix = key + "=";
        foreach (string argument in Environment.GetCommandLineArgs())
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return argument.Substring(prefix.Length);
        return string.Empty;
    }
}
