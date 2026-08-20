using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Owns matchmaking only. MultiplayerMenuController owns presentation and input.
/// Legacy public methods remain so serialized scene events do not break.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    private const int QuietmorCharacterIndex = 3;

    [Header("Legacy UI (hidden by the new menu)")]
    [SerializeField] private GameObject modeSelectionPopup;
    [SerializeField] private GameObject conectionFailedMessage;

    [Header("Network")]
    [FormerlySerializedAs("_networkLauncher")]
    [SerializeField] private NetworkLauncher networkLauncher;

    [Header("Scenes")]
    [SerializeField] private int sceneIndex1v1 = 3;
    [SerializeField] private int sceneIndex2v2 = 3;
    [SerializeField] private int sceneIndex3v3 = 3;

    public static string LocalPlayerName { get; private set; }
    public bool IsConnecting { get; private set; }

    private MultiplayerMenuController menuController;

    private void Start()
    {
        LocalPlayerName = PlayerPrefs.GetString("PlayerName", "Jugador");
        HideLegacyUi();

        ResolveNetworkLauncher();

        menuController = FindFirstObjectByType<MultiplayerMenuController>();
        if (menuController == null)
            menuController = gameObject.AddComponent<MultiplayerMenuController>();
        menuController.Initialize(this);
    }

    public Task<bool> ConnectOneVersusOne() => ConnectToMode(MatchModeCatalog.Duel);

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

        // El personaje ya lo guardó MultiplayerMenuController al confirmarlo.
        // La arena lee esto para saber cuántos jugadores esperar y cómo formar equipos.
        MatchContext.Select(matchMode);

        IsConnecting = true;
        PlayModeContext.UseMultiplayer();
        OnlineMatchState.Set(
            OnlineMatchPhase.Connecting,
            GameLocalization.Choose($"Buscando partida {matchMode.Key} ({matchMode.PlayerCount} jugadores)...", $"Searching for a {matchMode.Key} match ({matchMode.PlayerCount} players)..."));

        bool success = await networkLauncher.StartGame(
            GameMode.Shared,
            matchMode,
            GetSceneIndexForMode(matchMode));

        if (!success)
        {
            OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, GameLocalization.Choose("No fue posible conectar con la partida.", "Unable to connect to the match."));
            PlayModeContext.UseLocalStory();
        }

        IsConnecting = false;
        return success;
    }

    private const string ArenaScenePath = "Assets/Scenes/Multiplayer/OnlineArena.unity";

    private int GetSceneIndexForMode(MatchModeDefinition matchMode)
    {
        int configured;
        switch (matchMode.Id)
        {
            case MatchModeId.Duo2v2: configured = sceneIndex2v2; break;
            case MatchModeId.Clash3v3: configured = sceneIndex3v3; break;
            default: configured = sceneIndex1v1; break;
        }

        // El número serializado apunta a la arena por posición en Build
        // Settings. Basta insertar una escena antes (por ejemplo, la de idioma)
        // para que apunte a otra cosa. Resolverlo por ruta lo hace inmune.
        int resolved = SceneUtility.GetBuildIndexByScenePath(ArenaScenePath);
        if (resolved >= 0)
        {
            if (resolved != configured)
                Debug.Log($"[LobbyManager] La arena está en el índice {resolved}, no en {configured}. Se usa el real.");
            return resolved;
        }

        Debug.LogWarning($"[LobbyManager] '{ArenaScenePath}' no está en Build Settings. Se usa el índice {configured}.");
        return configured;
    }

    public async Task CancelConnection()
    {
        if (networkLauncher != null)
            await networkLauncher.CancelCurrentGame();
        IsConnecting = false;
        OnlineMatchState.Reset();
        PlayModeContext.UseLocalStory();
    }

    public void ReturnToMainMenu()
    {
        OnlineMatchState.Reset();
        SceneManager.LoadScene("Main Menu");
    }

    // Compatibility with Button events already serialized in MultiplayerMenu.unity.
    public void OnClickOpenModeSelection() => menuController?.ShowModeSelection();

    public async void OnClickSelectModeAndConnect(string modeName)
    {
        await ConnectToMode(MatchModeCatalog.GetByKey(modeName));
    }

    public void HidPopup() => HideLegacyUi();

    private void HideLegacyUi()
    {
        if (modeSelectionPopup != null)
            modeSelectionPopup.SetActive(false);
        if (conectionFailedMessage != null)
            conectionFailedMessage.SetActive(false);
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
