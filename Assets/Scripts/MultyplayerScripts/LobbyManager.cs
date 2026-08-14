using UnityEngine;
using Fusion;

public class LobbyManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject modeSelectionPopup;
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private GameObject conectionFailedMessage;

    [Header("Network Reference")]
    [SerializeField] private NetworkLauncher _networkLauncher;

    [Header("Scenes Configuration")]
    [SerializeField] private int sceneIndex1v1 = 3;
    [SerializeField] private int sceneIndex2v2 = 3;
    [SerializeField] private int sceneIndex3v3 = 3;

    public static string LocalPlayerName { get; private set; }

    private void Start()
    {
        LocalPlayerName = PlayerPrefs.GetString("PlayerName", "Player");
        HidPopup();
    }

    public void OnClickOpenModeSelection()
    {
        if (modeSelectionPopup != null)
            modeSelectionPopup.SetActive(true);

        if (conectionFailedMessage != null)
            conectionFailedMessage.SetActive(false);
    }

    // Asignar a los botones del Pop-Up (1v1, 2v2, 3v3)
    public async void OnClickSelectModeAndConnect(string modeName)
    {
        SetUIInteractivity(false);
        PlayModeContext.UseMultiplayer();

        int targetSceneIndex = GetSceneIndexForMode(modeName);
        string targetRoom = $"Room_{modeName}";
        Debug.Log($"Connecting to '{modeName}'...");

        bool success = await _networkLauncher.StartGame(GameMode.Shared, targetRoom, targetSceneIndex);
        
        if (!success)
        {
            PlayModeContext.UseLocalStory();
            Debug.LogError("Connection failed or timed out. Re-enabling UI.");

            if(conectionFailedMessage != null)
            conectionFailedMessage.SetActive(true);

            SetUIInteractivity(true);
        }
    }

    public void HidPopup()
    {
        if (modeSelectionPopup != null)
            modeSelectionPopup.SetActive(false);
        if (conectionFailedMessage != null)
            conectionFailedMessage.SetActive(false);
    }

    private int GetSceneIndexForMode(string modeName)
    {
        switch (modeName)
        {
            case "1v1": return sceneIndex1v1;
            case "2v2": return sceneIndex2v2;
            case "3v3": return sceneIndex3v3;
            default: return sceneIndex1v1;
        }
    }

    private void SetUIInteractivity(bool isInteractive)
    {
        if (popupCanvasGroup != null)
        {
            popupCanvasGroup.interactable = isInteractive;
            popupCanvasGroup.blocksRaycasts = isInteractive;
        }
    }
}
