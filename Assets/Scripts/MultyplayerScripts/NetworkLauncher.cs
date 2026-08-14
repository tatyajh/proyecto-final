using UnityEngine;
using Fusion;
using System.Threading.Tasks;

public class NetworkLauncher : MonoBehaviour
{
    private NetworkRunner _networkRunner;

    public async Task<bool> StartGame(GameMode mode, string roomName, int sceneIndex)
    {
        const int maxRoomAttempts = 5;
        for (int attempt = 1; attempt <= maxRoomAttempts; attempt++)
        {
            MakeSureAnthenaExists();
            TurnOnTheControlReceptor();
            string candidateRoom = $"{roomName}_{attempt}";

            try
            {
                StartGameResult result = await ConnectPhotonFusionToCloud(mode, candidateRoom, sceneIndex);
                if (result.Ok)
                    return true;

                Debug.LogWarning($"Photon room '{candidateRoom}' unavailable: {result.ShutdownReason}");
                DiscardRunner();

                if (result.ShutdownReason != ShutdownReason.GameIsFull)
                    break;
            }
            catch (System.Exception ex)
            {
                bool roomIsFull = ex.Message.Contains("GameIsFull") || ex.Message.Contains("Game full");
                Debug.LogWarning($"Photon room '{candidateRoom}' failed: {ex.Message}");
                DiscardRunner();
                if (!roomIsFull) break;
            }

            await Task.Yield();
        }

        OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, "No fue posible encontrar una sala 1v1 disponible.");
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
        StartGameResult result = await _networkRunner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = roomName,
            PlayerCount = 2,
            Scene = SceneRef.FromIndex(sceneIndex),
            SceneManager = _networkRunner.gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        return result;
    }
}
