using UnityEngine;
using Fusion;
using Photon.Realtime;
using System.Collections.Generic;
using System.Threading.Tasks;

public class NetworkLauncher : MonoBehaviour
{
    private const string RunnerObjectName = "PhotonNetworkRunner";
    // Crear sala + preparar el relay en WebGL puede pasar de 15 s en redes lentas.
    private const int ConnectionTimeoutMilliseconds = 25000;
    private NetworkRunner _networkRunner;

    /// <summary>Motivo exacto del último fallo, para mostrarlo en pantalla en vez de un genérico.</summary>
    public static string LastFailureReason { get; private set; } = string.Empty;

    public static void ReportConfigurationFailure(string reason)
    {
        LastFailureReason = reason ?? string.Empty;
    }

    public async Task CancelCurrentGame()
    {
        if (_networkRunner != null)
        {
            try
            {
                await _networkRunner.Shutdown();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[NetworkLauncher] No se pudo cerrar limpiamente la conexión: {exception.Message}");
            }
        }
        DiscardRunner();
    }

    public async Task<bool> StartGame(GameMode mode, MatchModeDefinition matchMode, int sceneIndex,
        string requestedSessionName = null)
    {
        if (matchMode == null)
            matchMode = MatchModeCatalog.Default;

        MakeSureAnthenaExists();
        TurnOnTheControlReceptor();
        LastFailureReason = string.Empty;
        float startedAt = Time.realtimeSinceStartup;

        try
        {
            Task<StartGameResult> connectionTask = ConnectPhotonFusionToCloud(
                mode, matchMode, sceneIndex, requestedSessionName);
            Task completedTask = await Task.WhenAny(connectionTask, Task.Delay(ConnectionTimeoutMilliseconds));
            if (completedTask != connectionTask)
            {
                Debug.LogWarning($"[NetworkLauncher] Photon no respondió en {ConnectionTimeoutMilliseconds / 1000} s. Se canceló el intento.");
                if (_networkRunner != null)
                    await _networkRunner.Shutdown();
                DiscardRunner();
                LastFailureReason = GameLocalization.Choose(
                    $"Photon no respondió en {ConnectionTimeoutMilliseconds / 1000} segundos.",
                    $"Photon did not respond within {ConnectionTimeoutMilliseconds / 1000} seconds.");
                OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, GameLocalization.Choose("La conexión tardó demasiado. Intenta nuevamente.", "The connection took too long. Try again."));
                return false;
            }

            StartGameResult result = await connectionTask;
            float elapsed = Time.realtimeSinceStartup - startedAt;
            Debug.Log($"[NetworkLauncher] Matchmaking {matchMode.Key} terminó en {elapsed:F2} s. Resultado: {result.ShutdownReason}");

            if (result.Ok)
                return true;

            string technicalReason = $"{result.ShutdownReason}: {result.ErrorMessage}";
            LastFailureReason = GameLocalization.Choose(
                $"Photon rechazó la conexión ({result.ShutdownReason}).",
                $"Photon rejected the connection ({result.ShutdownReason}).");
            Debug.LogWarning($"[NetworkLauncher] Photon matchmaking failed: {technicalReason}");
        }
        catch (System.Exception ex)
        {
            float elapsed = Time.realtimeSinceStartup - startedAt;
            LastFailureReason = DescribeFailure(ex);
            Debug.LogWarning($"[NetworkLauncher] Photon matchmaking failed after {elapsed:F2} s: {ex}");
        }

        DiscardRunner();
        OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, GameLocalization.Choose($"No fue posible encontrar una partida {matchMode.Key}.", $"Unable to find a {matchMode.Key} match."));
        return false;
    }

    /// <summary>
    /// Traduce el fallo de Photon a algo accionable. "ExceptionOnConnect" es
    /// que el socket no llegó a abrirse: sin red, DNS caído o firewall. No es
    /// lo mismo que Photon rechazando la sala, y el mensaje genérico anterior
    /// no dejaba distinguirlos.
    /// </summary>
    private static string DescribeFailure(System.Exception exception)
    {
        string raw = exception?.ToString() ?? string.Empty;

        if (raw.Contains("ExceptionOnConnect") || raw.Contains("DnsExceptionOnConnect"))
            return GameLocalization.Choose(
                "No se pudo abrir la conexión. Revisa tu internet y reintenta.",
                "Could not open the connection. Check your internet and retry.");

        if (raw.Contains("Timeout") || raw.Contains("timed out"))
            return GameLocalization.Choose(
                "El servidor no respondió a tiempo. Reintenta.",
                "The server did not respond in time. Retry.");

        return GameLocalization.Choose(
            $"Error de conexión ({exception?.GetType().Name}).",
            $"Connection error ({exception?.GetType().Name}).");
    }

    /// <summary>
    /// Destruye runners de partidas anteriores. Un "PhotonNetworkRunner" que
    /// sobrevive por DontDestroyOnLoad conserva su NetworkSceneManagerDefault;
    /// cuando Fusion carga la escena de la siguiente partida, ese manager
    /// huérfano corre con Runner ya en null y NetworkSceneManagerDefault
    /// .IsMultiplePeer lanza NullReferenceException.
    /// Filtra por nombre para no tocar jamás un runner que administre Fusion.
    /// </summary>
    public static void DestroyStaleRunners(NetworkRunner keep = null)
    {
        foreach (NetworkRunner runner in FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None))
        {
            if (runner == null || runner == keep) continue;
            if (runner.gameObject.name != RunnerObjectName) continue;
            if (!runner.gameObject.scene.IsValid()) continue;

            // Destroy es diferido hasta el fin del frame: parar las corrutinas
            // evita que el manager huérfano siga procesando la carga de escena.
            foreach (MonoBehaviour behaviour in runner.GetComponents<MonoBehaviour>())
                behaviour.StopAllCoroutines();

            Destroy(runner.gameObject);
        }
    }

    void MakeSureAnthenaExists()
    {
        // El código original hacía Destroy(_networkRunner), que destruye el
        // componente y deja el GameObject huérfano vivo por DontDestroyOnLoad.
        DiscardRunner();
        DestroyStaleRunners();

        GameObject runnerObject = new GameObject(RunnerObjectName);
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

    private async Task<StartGameResult> ConnectPhotonFusionToCloud(GameMode mode,
        MatchModeDefinition matchMode, int sceneIndex, string requestedSessionName)
    {
        NetworkSceneManagerDefault sceneManager =
            _networkRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        NetworkObjectProviderDefault objectProvider =
            _networkRunner.gameObject.AddComponent<NetworkObjectProviderDefault>();

        StartGameResult result = await _networkRunner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            // Sin nombre fijo, Fusion llena primero una sala compatible y
            // crea una nueva únicamente cuando no existe ninguna disponible.
            // En producción permanece null y FillRoom hace matchmaking normal.
            // El arnés puede fijar un nombre único para que muchos procesos
            // lanzados en el mismo milisegundo no creen salas paralelas.
            SessionName = string.IsNullOrWhiteSpace(requestedSessionName)
                ? null
                : requestedSessionName,
            // Fusion usa esta propiedad como filtro del matchmaking aleatorio,
            // así que un jugador de 3v3 nunca entra a una sala abierta de 1v1.
            // Se mantiene el formato "Room_x" y una sola clave string: es el
            // valor con el que el 1v1 ya venía conectando bien.
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                { "mode", $"Room_{matchMode.Key}" }
            },
            MatchmakingMode = MatchmakingMode.FillRoom,
            EnableClientSessionCreation = true,
            PlayerCount = matchMode.PlayerCount,
            IsOpen = true,
            IsVisible = true,
            // La caché solo debe usarse después de una conexión válida previa.
            // Forzarla en el primer arranque deja el Relay Client sin preparar.
            UseCachedRegions = false,
            Scene = SceneRef.FromIndex(sceneIndex),
            SceneManager = sceneManager,
            ObjectProvider = objectProvider
        });

        return result;
    }
}
