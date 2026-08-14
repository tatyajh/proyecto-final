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
    [SerializeField] private Vector3 spawnPosition = new Vector3(0, 1, 0);

    private NetworkRunner _runner;
    private bool _localSpawnInProgress;

    private void Start()
    {
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
        int playerCount = 0;
        foreach (PlayerRef ignored in runner.ActivePlayers) playerCount++;

        if (playerCount >= 2)
        {
            OnlineMatchState.Set(OnlineMatchPhase.Playing, string.Empty);
            if (runner.IsSharedModeMasterClient)
                runner.SessionInfo.IsOpen = false;
        }
        else
        {
            OnlineMatchState.Set(OnlineMatchPhase.WaitingForOpponent, "Buscando oponente...");
        }
    }

    // --- MÉTODO CORE: Creación de la Entidad en Red ---
    private async void SpawnLocalPlayer(NetworkRunner runner, PlayerRef player)
    {
        // En Fusion, SOLO el Host/Server o el cliente con autorización puede ejecutar runner.Spawn.
        // Verificamos si este jugador ya tiene un avatar asignado para no duplicar.
        if (!_localSpawnInProgress && runner.GetPlayerObject(player) == null)
        {
            Vector3 assignedSpawn = spawnPosition + Vector3.right * (player.PlayerId % 2 == 0 ? -4f : 4f);
            Quaternion assignedRotation = player.PlayerId % 2 == 0
                ? Quaternion.LookRotation(Vector3.right)
                : Quaternion.LookRotation(Vector3.left);
            Debug.Log($"<color=green>[PlayerSpawner] EXITO: Instanciando el prefab para {player} en {assignedSpawn}</color>");

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
        OnlineMatchState.Set(OnlineMatchPhase.OpponentDisconnected, "El oponente se desconectó.");
    }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (PlayModeContext.Current == PlayMode.Multiplayer && OnlineMatchState.Phase != OnlineMatchPhase.Finished)
            OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, "La conexión con la partida terminó.");
    }
    public void OnConnectedToServer(NetworkRunner runner)
    {
        OnlineMatchState.Set(OnlineMatchPhase.WaitingForOpponent, "Buscando oponente...");
    }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, "Se perdió la conexión con Photon.");
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
