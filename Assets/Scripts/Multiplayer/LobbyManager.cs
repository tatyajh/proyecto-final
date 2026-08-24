using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Owns matchmaking only. Presentation lives in BlightedIntroFlow, which calls
/// ConnectToMode directly.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("Network")]
    [FormerlySerializedAs("_networkLauncher")]
    [SerializeField] private NetworkLauncher networkLauncher;

    public bool IsConnecting { get; private set; }

    private void Start()
    {
        ResolveNetworkLauncher();
    }

    /// <summary>
    /// Único punto de entrada del matchmaking. El formato (1v1, 2v2, 3v3) decide
    /// el aforo de la sala y el número de jugadores que la arena debe esperar.
    /// </summary>
    public async Task<bool> ConnectToMode(MatchModeDefinition matchMode)
    {
        if (IsConnecting)
            return false;

        ResolveNetworkLauncher();
        if (networkLauncher == null)
        {
            string reason = GameLocalization.Choose("No se encontró el componente de conexión Photon.", "The Photon connection component was not found.");
            NetworkLauncher.ReportConfigurationFailure(reason);
            OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, reason);
            Debug.LogError($"[LobbyManager] {reason}");
            return false;
        }

        if (matchMode == null)
            matchMode = MatchModeCatalog.Default;

        // El flujo unificado ya guardó el personaje. La arena lee este contexto
        // para saber cuántos jugadores esperar y cómo formar equipos.
        MatchContext.Select(matchMode);

        int arenaSceneIndex = SceneUtility.GetBuildIndexByScenePath(GameScenes.ArenaPath);
        if (arenaSceneIndex < 0)
        {
            string reason = GameLocalization.Choose(
                "La arena no está configurada en Build Settings.",
                "The arena is not configured in Build Settings.");
            NetworkLauncher.ReportConfigurationFailure(reason);
            OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, reason);
            Debug.LogError($"[LobbyManager] {reason} Ruta esperada: {GameScenes.ArenaPath}");
            return false;
        }

        IsConnecting = true;
        PlayModeContext.UseMultiplayer();
        OnlineMatchState.Set(
            OnlineMatchPhase.Connecting,
            GameLocalization.Choose($"Buscando partida {matchMode.Key} ({matchMode.PlayerCount} jugadores)...", $"Searching for a {matchMode.Key} match ({matchMode.PlayerCount} players)..."));

        bool success = await networkLauncher.StartGame(
            GameMode.Shared,
            matchMode,
            arenaSceneIndex);

        if (!success)
        {
            OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, GameLocalization.Choose("No fue posible conectar con la partida.", "Unable to connect to the match."));
            PlayModeContext.UseLocalStory();
        }

        IsConnecting = false;
        return success;
    }

    private void ResolveNetworkLauncher()
    {
        if (networkLauncher != null)
            return;

        networkLauncher = GetComponent<NetworkLauncher>();
        if (networkLauncher == null)
            networkLauncher = FindFirstObjectByType<NetworkLauncher>();

        // NetworkLauncher no requiere referencias serializadas, por lo que esta
        // recuperación mantiene funcional el lobby incluso si una escena antigua
        // perdió el enlace durante una migración de nombres.
        if (networkLauncher == null)
            networkLauncher = gameObject.AddComponent<NetworkLauncher>();
    }
}
