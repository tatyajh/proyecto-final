using UnityEngine;
using Fusion;
using System.Threading.Tasks;

public class NetworkLauncher : MonoBehaviour
{
    private NetworkRunner _networkRunner;

    public async Task<bool> StartGame(GameMode mode, string roomName, int sceneIndex)
    {
        MakeSureAnthenaExists();
        TurnOnTheControlReceptor();
        try
        {
            await ConnectPhotonFusionToCloud(mode, roomName, sceneIndex);
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Photon Fusion Connection Failed: {ex.Message}");
            return false;
            }
    }
    
    void MakeSureAnthenaExists()
    {
        if (_networkRunner != null)
        {
            Destroy(_networkRunner);
        }
        GameObject runnerObject = new GameObject("PhotonNetworkRunner");
        _networkRunner = runnerObject.AddComponent<NetworkRunner>();
        
    }

    void TurnOnTheControlReceptor()
    {
        _networkRunner.ProvideInput = true;
    }

private async Task ConnectPhotonFusionToCloud(GameMode mode, string roomName, int sceneIndex)
    {
        await _networkRunner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = roomName,
            Scene = SceneRef.FromIndex(sceneIndex),
            SceneManager = _networkRunner.gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }
}