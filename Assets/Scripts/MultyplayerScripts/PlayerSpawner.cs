using UnityEngine;
using Fusion;

public class PlayerSpawner : FusionCallbacks
{
    [Header("Network Prefabs")]
    [Tooltip("Arrastra aquí el Prefab de tu Cápsula con el NetworkObject")]
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(0, 1, 0);

    private NetworkRunner _runner;

    private void Start()
    {
        // Buscamos el NetworkRunner que creó el NetworkLauncher
        _runner = FindObjectOfType<NetworkRunner>();

        if (_runner != null)
        {
            // Nos registramos para escuchar los eventos de Fusion
            _runner.AddCallbacks(this);

            // Si la sala ya inició y nosotros ya estamos dentro, creamos nuestro jugador
            if (_runner.IsRunning && _runner.LocalPlayer.IsValid)
            {
                SpawnLocalPlayer(_runner, _runner.LocalPlayer);
            }
        }
    }

    // Este evento se dispara automáticamente cuando un jugador entra
    public override void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            SpawnLocalPlayer(runner, player);
        }
    }

    private void SpawnLocalPlayer(NetworkRunner runner, PlayerRef player)
    {
        // Verificamos que no tengamos ya un jugador creado para evitar duplicados
        if (runner.GetPlayerObject(player) == null)
        {
            Debug.Log($"[Spawner] Spawning player prefab for local player: {player}");

            // Instanciamos el Prefab en la red
            NetworkObject playerObject = runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);

            // Asignamos la propiedad de este objeto al jugador local
            runner.SetPlayerObject(player, playerObject);
        }
    }

    private void OnDestroy()
    {
        if (_runner != null)
        {
            _runner.RemoveCallbacks(this);
        }
    }
}