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

    private void Start()
    {
        // 1. Buscamos el Runner global que viene vivo desde la escena del Menú
        _runner = FindObjectOfType<NetworkRunner>();

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
    }

    // --- CALLBACK 2: Se dispara cuando la red termina de cargar la nueva escena ---
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("[PlayerSpawner] La escena ha terminado de cargarse según Photon.");
        if (runner.LocalPlayer.IsValid)
        {
            SpawnLocalPlayer(runner, runner.LocalPlayer);
        }
    }

    // --- MÉTODO CORE: Creación de la Entidad en Red ---
    private void SpawnLocalPlayer(NetworkRunner runner, PlayerRef player)
    {
        // En Fusion, SOLO el Host/Server o el cliente con autorización puede ejecutar runner.Spawn.
        // Verificamos si este jugador ya tiene un avatar asignado para no duplicar.
        if (runner.GetPlayerObject(player) == null)
        {
            Debug.Log($"<color=green>[PlayerSpawner] EXITO: Instanciando el prefab para {player} en {spawnPosition}</color>");

            // runner.Spawn crea el objeto en todos los clientes conectados a la vez
            NetworkObject playerObject = runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);

            // Asignamos el objeto a ese cliente específico
            runner.SetPlayerObject(player, playerObject);
        }
    }

    // --- MÉTODOS REQUERIDOS POR LA INTERFAZ ---
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
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