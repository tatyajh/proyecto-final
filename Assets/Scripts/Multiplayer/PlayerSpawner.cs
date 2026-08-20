using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;

public class PlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network Prefabs")]
    [Tooltip("Arrastra aquí el Prefab de tu Cápsula con el NetworkObject")]
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Header("Spawn Settings")]
    [Tooltip("Centro de la arena. Cada equipo se coloca a un lado de este punto.")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(48f, 0f, -6f);

    /// <summary>Centro de la arena, leído por PlayerController al recolocarse tras el balanceo.</summary>
    public static Vector3 ArenaCenter { get; private set; } = new Vector3(48f, 0f, -6f);

    private NetworkRunner _runner;
    private bool _localSpawnInProgress;
    private readonly List<PlayerController> _playerBuffer = new List<PlayerController>();
    private Coroutine _balanceRoutine;

    private void Start()
    {
        ArenaCenter = spawnPosition;

        // 1. Buscamos el Runner global que viene vivo desde la escena del Menú
        _runner = FindFirstObjectByType<NetworkRunner>();

        if (_runner != null)
        {
            // Nos registramos para escuchar sus eventos desde este momento
            _runner.AddCallbacks(this);

            // Si el Runner YA ESTABA CORRIENDO antes de que esta escena se cargara (caso muy común),
            // forzamos el spawn de inmediato sin esperar al evento OnPlayerJoined.
            if (_runner.IsRunning && _runner.LocalPlayer.IsValid)
            {
                Debug.Log("[PlayerSpawner] El Runner ya estaba activo. Intentando Spawn en Start...");
                SpawnLocalPlayer(_runner, _runner.LocalPlayer);
            }
        }
        else
        {
            Debug.LogError("[PlayerSpawner] No se encontró ningún NetworkRunner activo en la escena.");
        }
    }

    private void OnDestroy()
    {
        if (_runner != null)
        {
            _runner.RemoveCallbacks(this);
        }
    }

    // --- CALLBACK 1: Para cuando un jugador se une MIENTRAS esta escena ya está abierta ---
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            Debug.Log($"[PlayerSpawner] OnPlayerJoined detectado para el jugador local: {player}");
            SpawnLocalPlayer(runner, player);
        }

        RefreshMatchState(runner);
    }

    // --- CALLBACK 2: Se dispara cuando la red termina de cargar la nueva escena ---
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("[PlayerSpawner] La escena ha terminado de cargarse según Photon.");
        if (runner.LocalPlayer.IsValid)
        {
            SpawnLocalPlayer(runner, runner.LocalPlayer);
        }
        RefreshMatchState(runner);
    }

    private void RefreshMatchState(NetworkRunner runner)
    {
        int required = MatchContext.RequiredPlayers;
        int playerCount = CountActivePlayers(runner);

        if (playerCount >= required)
        {
            // Con la sala llena los equipos quedan fijos y parejos antes del primer golpe.
            BalanceTeams(runner);
            OnlineMatchState.Set(
                OnlineMatchPhase.Playing,
                MatchContext.TeamSize > 1
                    ? $"{MatchContext.Mode.DisplayName} · {MatchTeams.NameOf(MatchTeams.Bloom)} vs {MatchTeams.NameOf(MatchTeams.Blight)}"
                    : string.Empty);

            if (runner.IsSharedModeMasterClient && runner.SessionInfo != null)
                runner.SessionInfo.IsOpen = false;
        }
        else
        {
            OnlineMatchState.Set(
                OnlineMatchPhase.WaitingForOpponent,
                GameLocalization.Choose(
                    $"Esperando jugadores… {playerCount}/{required} ({MatchContext.Mode.DisplayName}). Los controles se activan cuando la sala esté completa.",
                    $"Waiting for players… {playerCount}/{required} ({MatchContext.Mode.DisplayName}). Controls activate when the room is full."));
        }
    }

    /// <summary>
    /// Salida garantizada. El HUD con "Salir al menú" lo dibuja PlayerController,
    /// así que si el spawn falla o la conexión muere antes de crear el avatar
    /// el jugador se quedaba en la arena sin ninguna forma de volver.
    /// </summary>
    private void OnGUI()
    {
        if (PlayModeContext.Current != PlayMode.Multiplayer) return;
        if (_runner != null && _runner.IsRunning && _runner.LocalPlayer.IsValid &&
            _runner.GetPlayerObject(_runner.LocalPlayer) != null)
            return;

        float scale = Mathf.Clamp(Screen.width / 960f, 0.8f, 1.6f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        GUIStyle message = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        float width = Screen.width / scale;
        GUI.Box(new Rect(width * 0.5f - 250f, 90f, 500f, 96f), string.Empty);
        GUI.Label(new Rect(width * 0.5f - 240f, 96f, 480f, 40f),
            string.IsNullOrWhiteSpace(OnlineMatchState.Message)
                ? GameLocalization.Choose("Preparando la arena...", "Preparing the arena...")
                : OnlineMatchState.Message,
            message);

        if (GUI.Button(new Rect(width * 0.5f - 90f, 138f, 180f, 40f), GameLocalization.Choose("Volver al menú", "Back to menu")))
            LeaveArena();
    }

    private async void LeaveArena()
    {
        if (_runner != null && _runner.IsRunning)
            await _runner.Shutdown();

        NetworkLauncher.DestroyStaleRunners();
        OnlineMatchState.Reset();
        PlayModeContext.UseLocalStory();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MultiplayerMenu");
    }

    private static int CountActivePlayers(NetworkRunner runner)
    {
        if (runner == null) return 0;

        int count = 0;
        foreach (PlayerRef ignored in runner.ActivePlayers) count++;
        return count;
    }

    /// <summary>
    /// El master client de Shared Mode reparte los equipos en bloques por PlayerId
    /// cuando la sala se llena. El equipo provisional del spawn puede quedar
    /// desbalanceado si alguien salió y volvió a entrar durante la búsqueda.
    /// </summary>
    private void BalanceTeams(NetworkRunner runner)
    {
        if (!runner.IsSharedModeMasterClient) return;
        if (_balanceRoutine != null) return;

        _balanceRoutine = StartCoroutine(BalanceWhenAvatarsReady(runner));
    }

    /// <summary>
    /// ActivePlayers se completa antes de que los avatares remotos terminen de
    /// replicar. Repartir en ese instante encontraba menos PlayerController de
    /// los debidos y abandonaba, dejando los equipos provisionales por paridad
    /// de PlayerId, que en 2v2 y 3v3 puede salir desbalanceado.
    /// </summary>
    private System.Collections.IEnumerator BalanceWhenAvatarsReady(NetworkRunner runner)
    {
        int teamSize = MatchContext.TeamSize;
        int required = teamSize * MatchTeams.TeamCount;
        float deadline = Time.realtimeSinceStartup + 10f;

        while (Time.realtimeSinceStartup < deadline)
        {
            CollectSpawnedPlayers();
            if (_playerBuffer.Count >= required) break;
            yield return null;
        }

        if (_playerBuffer.Count < required)
        {
            Debug.LogWarning($"[PlayerSpawner] Solo {_playerBuffer.Count}/{required} avatares replicaron a tiempo. " +
                             "Los equipos se quedan con el reparto provisional.");
            _balanceRoutine = null;
            yield break;
        }

        _playerBuffer.Sort((a, b) =>
            a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId));

        System.Text.StringBuilder summary = new System.Text.StringBuilder("[PlayerSpawner] Equipos: ");
        for (int i = 0; i < _playerBuffer.Count; i++)
        {
            int team = i < teamSize ? MatchTeams.Bloom : MatchTeams.Blight;
            _playerBuffer[i].RequestTeamAssignment(team, i % teamSize);
            summary.Append($"[P{_playerBuffer[i].Object.InputAuthority.PlayerId}->{MatchTeams.NameOf(team)}] ");
        }
        Debug.Log(summary.ToString());

        _balanceRoutine = null;
    }

    private void CollectSpawnedPlayers()
    {
        _playerBuffer.Clear();
        foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (player.Object != null && player.Object.InputAuthority.IsValid)
                _playerBuffer.Add(player);
    }

    // --- MÉTODO CORE: Creación de la Entidad en Red ---
    private async void SpawnLocalPlayer(NetworkRunner runner, PlayerRef player)
    {
        // En Fusion, SOLO el Host/Server o el cliente con autorización puede ejecutar runner.Spawn.
        // Verificamos si este jugador ya tiene un avatar asignado para no duplicar.
        if (!_localSpawnInProgress && runner.GetPlayerObject(player) == null)
        {
            // Equipo provisional. BalanceTeams lo reemplaza al llenarse la sala.
            int teamSize = MatchContext.TeamSize;
            int team = MatchTeams.TeamForPlayerId(player.PlayerId);
            int slot = MatchTeams.SlotForPlayerId(player.PlayerId, teamSize);

            Vector3 assignedSpawn = spawnPosition + MatchTeams.SpawnOffset(team, slot, teamSize);
            Quaternion assignedRotation = MatchTeams.SpawnRotation(team);
            Debug.Log($"<color=green>[PlayerSpawner] EXITO: Instanciando el prefab para {player} " +
                      $"(equipo {MatchTeams.NameOf(team)}, puesto {slot}) en {assignedSpawn}</color>");

            _localSpawnInProgress = true;
            try
            {
                NetworkObject playerObject = await runner.SpawnAsync(
                    playerPrefab,
                    assignedSpawn,
                    assignedRotation,
                    player);

                if (playerObject != null && runner.GetPlayerObject(player) == null)
                    runner.SetPlayerObject(player, playerObject);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[PlayerSpawner] No fue posible crear el jugador: {exception.Message}");
            }
            finally
            {
                _localSpawnInProgress = false;
            }
        }
    }

    // --- MÉTODOS REQUERIDOS POR LA INTERFAZ ---
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        // En 2v2 y 3v3 la partida sigue mientras el equipo rival conserve gente.
        if (OnlineMatchState.Phase == OnlineMatchPhase.Playing)
        {
            if (HasEmptyTeam(runner, player))
                OnlineMatchState.Set(OnlineMatchPhase.OpponentDisconnected, GameLocalization.Choose("El equipo rival abandonó la partida.", "The opposing team left the match."));
            else
                OnlineMatchState.Set(OnlineMatchPhase.Playing, GameLocalization.Choose("Un jugador abandonó la partida.", "A player left the match."));

            return;
        }

        RefreshMatchState(runner);
    }

    private static bool HasEmptyTeam(NetworkRunner runner, PlayerRef leaving)
    {
        int bloom = 0;
        int blight = 0;
        foreach (PlayerRef active in runner.ActivePlayers)
        {
            if (active == leaving) continue;

            NetworkObject avatar = runner.GetPlayerObject(active);
            PlayerController controller = avatar != null ? avatar.GetComponent<PlayerController>() : null;
            if (controller == null) continue;

            if (controller.Team == MatchTeams.Blight) blight++;
            else bloom++;
        }

        return bloom == 0 || blight == 0;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (PlayModeContext.Current == PlayMode.Multiplayer && OnlineMatchState.Phase != OnlineMatchPhase.Finished)
            OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, GameLocalization.Choose("La conexión con la partida terminó.", "The match connection ended."));
    }
    public void OnConnectedToServer(NetworkRunner runner)
    {
        OnlineMatchState.Set(
            OnlineMatchPhase.WaitingForOpponent,
            GameLocalization.Choose(
                $"Esperando jugadores… 1/{MatchContext.RequiredPlayers} ({MatchContext.Mode.DisplayName}).",
                $"Waiting for players… 1/{MatchContext.RequiredPlayers} ({MatchContext.Mode.DisplayName})."));
    }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, GameLocalization.Choose("Se perdió la conexión con Photon.", "Connection to Photon was lost."));
    }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }

#pragma warning disable CS0618
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
#pragma warning restore CS0618
}
