using UnityEngine;
using Fusion;
using Photon.Realtime;
using System.Collections.Generic;
using System.Threading.Tasks;

public class NetworkLauncher : MonoBehaviour
{
    private NetworkRunner _networkRunner;

    public async Task<bool> StartGame(GameMode mode, string roomName, int sceneIndex)
    {
        MakeSureAnthenaExists();
        TurnOnTheControlReceptor();
        float startedAt = Time.realtimeSinceStartup;

        try
        {
            StartGameResult result = await ConnectPhotonFusionToCloud(mode, roomName, sceneIndex);
            float elapsed = Time.realtimeSinceStartup - startedAt;
            Debug.Log($"[NetworkLauncher] Matchmaking 1v1 terminó en {elapsed:F2} s. Resultado: {result.ShutdownReason}");

            if (result.Ok)
                return true;

            Debug.LogWarning($"Photon matchmaking failed: {result.ShutdownReason}");
        }
        catch (System.Exception ex)
        {
            float elapsed = Time.realtimeSinceStartup - startedAt;
            Debug.LogWarning($"Photon matchmaking failed after {elapsed:F2} s: {ex.Message}");
        }

        DiscardRunner();
        OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, "No fue posible encontrar una partida 1v1.");
        return false;
    }
    
    void MakeSureAnthenaExists()
    {
        if (_networkRunner != null)
        {
            Destroy(_networkRunner);
        }
        GameObject runnerObject = new GameObject("PhotonNetworkRunner");
        DontDestroyOnLoad(runnerObject);
        _networkRunner = runnerObject.AddComponent<NetworkRunner>();
        
    }

    void TurnOnTheControlReceptor()
    {
        _networkRunner.ProvideInput = true;
    }

    private void DiscardRunner()
    {
        if (_networkRunner != null)
            Destroy(_networkRunner.gameObject);
        _networkRunner = null;
    }

    private async Task<StartGameResult> ConnectPhotonFusionToCloud(GameMode mode, string roomName, int sceneIndex)
    {
        NetworkSceneManagerDefault sceneManager =
            _networkRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        NetworkObjectProviderDefault objectProvider =
            _networkRunner.gameObject.AddComponent<NetworkObjectProviderDefault>();

        StartGameResult result = await _networkRunner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            // Sin nombre fijo, Fusion llena primero una sala 1v1 compatible y
            // crea una nueva únicamente cuando no existe ninguna disponible.
            SessionName = null,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                { "mode", roomName }
            },
            MatchmakingMode = MatchmakingMode.FillRoom,
            EnableClientSessionCreation = true,
            PlayerCount = 2,
            IsOpen = true,
            IsVisible = true,
            UseCachedRegions = true,
            Scene = SceneRef.FromIndex(sceneIndex),
            SceneManager = sceneManager,
            ObjectProvider = objectProvider
        });

        return result;
    }
}
