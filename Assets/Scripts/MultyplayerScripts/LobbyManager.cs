using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    private const int AvailableCharacterCount = 1;
    private static readonly string[] CharacterNames =
    {
        "Heliandra", "Lunara", "Solmara", "Quietmor", "Acatheria", "Terramor"
    };

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
    private readonly Dictionary<int, Button> characterButtons = new Dictionary<int, Button>();
    private GameObject characterSelectionPanel;
    private TMP_Text characterSelectionStatus;
    private int selectedCharacterIndex = -1;

    private void Start()
    {
        LocalPlayerName = PlayerPrefs.GetString("PlayerName", "Player");
        selectedCharacterIndex = PlayerPrefs.GetInt("SelectedCharacterIndex", 0);
        if (!IsCharacterAvailable(selectedCharacterIndex))
            selectedCharacterIndex = 0;
        BuildCharacterSelection();
        ConfigureModeAvailability();
        ConfigureModePopupLayout();
        HidPopup();
    }

    public void OnClickOpenModeSelection()
    {
        if (modeSelectionPopup != null)
            modeSelectionPopup.SetActive(true);
        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(true);

        if (conectionFailedMessage != null)
            conectionFailedMessage.SetActive(false);
    }

    // Asignar a los botones del Pop-Up (1v1, 2v2, 3v3)
    public async void OnClickSelectModeAndConnect(string modeName)
    {
        if (modeName != "1v1")
        {
            Debug.Log($"El modo {modeName} estará disponible después del prototipo 1v1.");
            if (conectionFailedMessage != null)
                conectionFailedMessage.SetActive(true);
            return;
        }

        if (!IsCharacterAvailable(selectedCharacterIndex))
        {
            if (characterSelectionStatus != null)
            {
                characterSelectionStatus.text = "Selecciona un personaje para entrar al duelo.";
                characterSelectionStatus.color = new Color(0.86f, 0.67f, 0.24f, 1f);
            }
            return;
        }

        PlayerPrefs.SetInt("SelectedCharacterIndex", selectedCharacterIndex);
        PlayerPrefs.SetString("SelectedCharacter", CharacterNames[selectedCharacterIndex]);
        PlayerPrefs.Save();
        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(false);

        SetUIInteractivity(false);
        PlayModeContext.UseMultiplayer();
        OnlineMatchState.Set(OnlineMatchPhase.Connecting, "Buscando partida 1v1...");

        int targetSceneIndex = GetSceneIndexForMode(modeName);
        string targetRoom = $"Room_{modeName}";
        Debug.Log($"Connecting to '{modeName}'...");

        bool success = await _networkLauncher.StartGame(GameMode.Shared, targetRoom, targetSceneIndex);
        
        if (!success)
        {
            OnlineMatchState.Set(OnlineMatchPhase.ConnectionFailed, "No fue posible conectar con la partida.");
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
        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(false);
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

    private static void ConfigureModeAvailability()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;

            string mode = label.text.Trim();
            string normalizedMode = mode.ToLowerInvariant();
            bool isDuo = normalizedMode.Contains("2v2") || normalizedMode.Contains("2 v 2") || normalizedMode.Contains("dúo") || normalizedMode.Contains("duo");
            bool isClash = normalizedMode.Contains("3v3") || normalizedMode.Contains("3 v 3") || normalizedMode.Contains("clash");
            if (!isDuo && !isClash) continue;

            button.interactable = false;
            label.text = isDuo
                ? "Dúo (2v2) · Próximamente"
                : "Clash (3v3) · Próximamente";
            label.color = new Color(0.55f, 0.53f, 0.50f, 1f);
        }
    }

    private void BuildCharacterSelection()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null || canvas.transform.Find("Multiplayer Character Selector") != null) return;

        characterSelectionPanel = CreateUiObject("Multiplayer Character Selector", canvas.transform);
        RectTransform panelRect = characterSelectionPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(285f, 0f);
        panelRect.sizeDelta = new Vector2(360f, 470f);
        Image panelImage = characterSelectionPanel.AddComponent<Image>();
        panelImage.color = new Color(0.055f, 0.035f, 0.070f, 0.97f);

        CreateText(characterSelectionPanel.transform, "Elige tu personaje", new Vector2(0f, 192f), new Vector2(330f, 52f), 27f);
        characterSelectionStatus = CreateText(characterSelectionPanel.transform, "Selecciona 1 para el duelo", new Vector2(0f, 150f), new Vector2(330f, 38f), 16f);

        for (int i = 0; i < CharacterNames.Length; i++)
        {
            int characterIndex = i;
            int column = i % 2;
            int row = i / 2;
            Vector2 position = new Vector2(column == 0 ? -85f : 85f, 82f - row * 82f);
            Button button = CreateCharacterButton(characterSelectionPanel.transform, CharacterNames[i], position);
            bool available = IsCharacterAvailable(characterIndex);
            button.interactable = available;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (!available && label != null)
            {
                label.text = CharacterNames[i] + " · Bloqueado";
                label.fontSize = 15f;
                label.color = new Color(0.48f, 0.47f, 0.45f, 1f);
            }
            if (available)
                button.onClick.AddListener(() => SelectCharacter(characterIndex));
            characterButtons[characterIndex] = button;
        }

        RefreshCharacterSelection();
        characterSelectionPanel.SetActive(false);
    }

    private void SelectCharacter(int characterIndex)
    {
        if (!IsCharacterAvailable(characterIndex)) return;

        selectedCharacterIndex = characterIndex;
        PlayerPrefs.SetInt("SelectedCharacterIndex", characterIndex);
        PlayerPrefs.SetString("SelectedCharacter", CharacterNames[characterIndex]);
        PlayerPrefs.Save();
        RefreshCharacterSelection();
    }

    private void RefreshCharacterSelection()
    {
        if (characterSelectionStatus != null)
        {
            characterSelectionStatus.text = selectedCharacterIndex >= 0 && selectedCharacterIndex < CharacterNames.Length
                ? $"Seleccionado: {CharacterNames[selectedCharacterIndex]}"
                : "Selecciona 1 para el duelo";
            characterSelectionStatus.color = new Color(0.89f, 0.87f, 0.79f, 1f);
        }

        foreach (KeyValuePair<int, Button> entry in characterButtons)
        {
            Image image = entry.Value.targetGraphic as Image;
            if (image != null)
                image.color = !IsCharacterAvailable(entry.Key)
                    ? new Color(0.10f, 0.10f, 0.11f, 0.72f)
                    : entry.Key == selectedCharacterIndex
                    ? new Color(0.49f, 0.36f, 0.12f, 1f)
                    : new Color(0.08f, 0.15f, 0.13f, 1f);
        }
    }

    private static bool IsCharacterAvailable(int characterIndex)
    {
        return characterIndex >= 0 && characterIndex < AvailableCharacterCount;
    }

    private void ConfigureModePopupLayout()
    {
        if (modeSelectionPopup == null) return;

        RectTransform popupRect = modeSelectionPopup.GetComponent<RectTransform>();
        if (popupRect != null)
        {
            popupRect.anchorMin = popupRect.anchorMax = popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.anchoredPosition = new Vector2(-215f, 0f);
            popupRect.sizeDelta = new Vector2(500f, 430f);
            popupRect.localScale = Vector3.one;
        }

        foreach (Button button in modeSelectionPopup.GetComponentsInChildren<Button>(true))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;

            string normalized = label.text.ToLowerInvariant();
            bool isModeButton = normalized.Contains("1 vs 1") || normalized.Contains("1v1") ||
                                normalized.Contains("2v2") || normalized.Contains("2 v 2") ||
                                normalized.Contains("3v3") || normalized.Contains("3 v 3") ||
                                normalized.Contains("duel") || normalized.Contains("dúo") ||
                                normalized.Contains("clash");
            if (!isModeButton) continue;

            RectTransform buttonRect = button.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(440f, 68f);
            buttonRect.localScale = Vector3.one;

            label.enableAutoSizing = false;
            label.fontSize = 25f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.Center;
            label.margin = new Vector4(12f, 4f, 12f, 4f);
        }
    }

    private static Button CreateCharacterButton(Transform parent, string label, Vector2 position)
    {
        GameObject gameObject = CreateUiObject(label + " Button", parent);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(155f, 58f);
        Image image = gameObject.AddComponent<Image>();
        Button button = gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        CreateText(gameObject.transform, label, Vector2.zero, new Vector2(145f, 50f), 16f);
        return button;
    }

    private static TMP_Text CreateText(Transform parent, string value, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject gameObject = CreateUiObject(value + " Text", parent);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = new Color(0.89f, 0.87f, 0.79f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }
}
