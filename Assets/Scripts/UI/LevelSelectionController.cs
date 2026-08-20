using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class LevelSelectionController : MonoBehaviour
{
    private const string PlayableArenaScene = "OnlineArena";

    [Header("Progress Config")]
    int maxUnlockedLevel = 1;

    [Header("Botones de Niveles")]
    public Button levelSelectionButton1;
    public Button levelSelectionButton2;
    public Button levelSelectionButton3;
    public Button levelSelectionButton4;
    private Button finalLevelButton;

    [Header("Referencias del Popup / Brief")]
    public GameObject briefPanel;
    public TMP_Text txtLevelTitle;
    public TMP_Text txtLevelDescription;

    private string selectedScene;
    private readonly HashSet<string> selectedCharacters = new HashSet<string>();
    private readonly Dictionary<string, Button> characterButtons = new Dictionary<string, Button>();
    private TMP_Text partySelectionStatus;
    private static readonly string[] CharacterNames =
    {
        "Heliandra", "Lunara", "Solmara", "Quietmor", "Acatheria", "Terramor"
    };
    [Tooltip("Put the exaxt name of the escene of the main menu")]
    public string mainMenu;

    void Start()
    {
        SelectMaxUnlockedLevel();
        CreateFinalLevelButton();
        UpdateButtonStates();
        UpdateLevelLabels();
        BuildPartySelector();
        HidPopUp();
    }

    void UpdateButtonStates()
    {
        if (levelSelectionButton1 != null)
            levelSelectionButton1.interactable = (maxUnlockedLevel >= 1);
        if (levelSelectionButton2 != null)
            levelSelectionButton2.interactable = (maxUnlockedLevel >= 2);
        if (levelSelectionButton3 != null)
            levelSelectionButton3.interactable = (maxUnlockedLevel >= 3);
        if (levelSelectionButton4 != null)
            levelSelectionButton4.interactable = (maxUnlockedLevel >= 4);
        if (finalLevelButton != null)
            finalLevelButton.interactable = (maxUnlockedLevel >= 5);
    }

    public void OnClickLevel1()
    {
        SelectLevel(GameLocalization.Choose("Nivel 1: El Santuario", "Level 1: The Sanctuary"), GameLocalization.Choose("Explora la arena, practica el movimiento y prueba tus habilidades.", "Explore the arena, practise movement and test your abilities."), PlayableArenaScene);
    }

    public void OnClickLevel2()
    {
        SelectLevel(GameLocalization.Choose("Nivel 2: Entrenamiento", "Level 2: Training"), GameLocalization.Choose("Prototipo jugable en la arena compartida.", "Playable prototype in the shared arena."), PlayableArenaScene);
    }

    public void OnClickLevel3()
    {
        SelectLevel(GameLocalization.Choose("Nivel 3: Combate", "Level 3: Combat"), GameLocalization.Choose("Prototipo jugable en la arena compartida.", "Playable prototype in the shared arena."), PlayableArenaScene);
    }

    public void OnClickLevel4()
    {
        SelectLevel(GameLocalization.Choose("Acto 4: Terramor", "Act 4: Terramor"), GameLocalization.Choose("Confronta a Terramor y descubre el camino hacia la Raíz Madre.", "Confront Terramor and discover the path to the Mother Root."), PlayableArenaScene);
    }

    public void OnClickFinalLevel()
    {
        SelectLevel(GameLocalization.Choose("Acto 5: La Podredumbre", "Act 5: The Rot"), GameLocalization.Choose("Enfrenta el origen de la corrupción en la Raíz Madre y decide el destino de los Árboles Primordiales.", "Face the source of corruption in the Mother Root and decide the fate of the Primordial Trees."), PlayableArenaScene);
    }


    private void SelectLevel(string title, string description, string levelSceneName)
    {
        selectedScene = levelSceneName;

        if (txtLevelTitle != null) txtLevelTitle.text = title; 
        if (txtLevelDescription != null) txtLevelDescription.text = description;

        if (briefPanel != null) briefPanel.SetActive(true); 
    }

    public void ConfirmAndStartLevel()
    {
        if (selectedCharacters.Count != 2)
        {
            if (txtLevelDescription != null)
                txtLevelDescription.text = GameLocalization.Choose("Selecciona exactamente dos personajes antes de comenzar.", "Select exactly two characters before starting.");
            return;
        }

        if (!string.IsNullOrEmpty(selectedScene)) 
        {
            PlayerPrefs.SetString("CampaignParty", string.Join("|", selectedCharacters));
            PlayerPrefs.Save();
            PlayModeContext.UseLocalStory();
            SceneManager.LoadScene(selectedScene);
        }
    }

    public void CloseBrief()
    {
        if (briefPanel != null) 
        {
            briefPanel.SetActive(false);
        }
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }

    void SelectMaxUnlockedLevel()
    {
        maxUnlockedLevel = PlayerPrefs.GetInt("NivelAlcanzado", 1);
    }

    void HidPopUp()
    {
        if (briefPanel != null) 
        {
            briefPanel.SetActive(false);
        }
    }

    private void UpdateLevelLabels()
    {
        string locked = GameLocalization.Choose("Bloqueado", "Locked");
        SetLevelLabel(levelSelectionButton1, GameLocalization.Choose("Acto 1 · El Santuario", "Act 1 · The Sanctuary"), maxUnlockedLevel >= 1, locked);
        SetLevelLabel(levelSelectionButton2, GameLocalization.Choose("Acto 2", "Act 2") + " · " + locked, maxUnlockedLevel >= 2, locked);
        SetLevelLabel(levelSelectionButton3, GameLocalization.Choose("Acto 3", "Act 3") + " · " + locked, maxUnlockedLevel >= 3, locked);
        SetLevelLabel(levelSelectionButton4, GameLocalization.Choose("Acto 4 · Terramor", "Act 4 · Terramor") + " · " + locked, maxUnlockedLevel >= 4, locked);
        SetLevelLabel(finalLevelButton, GameLocalization.Choose("Acto 5 · La Podredumbre", "Act 5 · The Rot") + " · " + locked, maxUnlockedLevel >= 5, locked);
    }

    private void CreateFinalLevelButton()
    {
        if (levelSelectionButton4 == null || finalLevelButton != null) return;
        finalLevelButton = Instantiate(levelSelectionButton4, levelSelectionButton4.transform.parent);
        finalLevelButton.name = "Acto 5 La Podredumbre";
        finalLevelButton.onClick.RemoveAllListeners();
        finalLevelButton.onClick.AddListener(OnClickFinalLevel);
    }

    private static void SetLevelLabel(Button button, string label, bool unlocked, string locked)
    {
        if (button == null) return;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = unlocked ? label.Replace(" · " + locked, string.Empty) : label;
            text.fontSize = 24f;
        }
    }

    private void BuildPartySelector()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null || canvas.transform.Find("Campaign Party Selector") != null) return;

        GameObject panel = CreateUiObject("Campaign Party Selector", canvas.transform);
        panel.transform.SetSiblingIndex(Mathf.Min(1, canvas.transform.childCount - 1));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-80f, 0f);
        panelRect.sizeDelta = new Vector2(560f, 650f);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.095f, 0.07f, 0.115f, 0.97f);

        CreateText(panel.transform, GameLocalization.Choose("Elige tu pareja", "Choose your pair"), new Vector2(0f, 265f), new Vector2(500f, 70f), 34f);
        partySelectionStatus = CreateText(panel.transform, GameLocalization.Choose("Seleccionados: 0 / 2", "Selected: 0 / 2"), new Vector2(0f, 210f), new Vector2(500f, 45f), 22f);

        for (int i = 0; i < CharacterNames.Length; i++)
        {
            string characterName = CharacterNames[i];
            int column = i % 2;
            int row = i / 2;
            Vector2 position = new Vector2(column == 0 ? -135f : 135f, 115f - row * 115f);
            Button button = CreateCharacterButton(panel.transform, characterName, position);
            characterButtons[characterName] = button;
            button.onClick.AddListener(() => ToggleCharacter(characterName));
        }

        RestorePartySelection();
        RefreshPartySelector();
    }

    private void ToggleCharacter(string characterName)
    {
        if (selectedCharacters.Contains(characterName))
            selectedCharacters.Remove(characterName);
        else if (selectedCharacters.Count < 2)
            selectedCharacters.Add(characterName);

        RefreshPartySelector();
    }

    private void RestorePartySelection()
    {
        string saved = PlayerPrefs.GetString("CampaignParty", string.Empty);
        foreach (string characterName in saved.Split('|'))
            if (!string.IsNullOrWhiteSpace(characterName) && selectedCharacters.Count < 2)
                selectedCharacters.Add(characterName);
    }

    private void RefreshPartySelector()
    {
        if (partySelectionStatus != null)
            partySelectionStatus.text = GameLocalization.Choose($"Seleccionados: {selectedCharacters.Count} / 2", $"Selected: {selectedCharacters.Count} / 2");

        foreach (KeyValuePair<string, Button> entry in characterButtons)
        {
            bool selected = selectedCharacters.Contains(entry.Key);
            Image image = entry.Value.targetGraphic as Image;
            if (image != null)
                image.color = selected
                    ? new Color(0.42f, 0.34f, 0.16f, 1f)
                    : new Color(0.10f, 0.16f, 0.14f, 1f);
        }
    }

    private static Button CreateCharacterButton(Transform parent, string label, Vector2 position)
    {
        GameObject gameObject = CreateUiObject(label + " Button", parent);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        ConfigureRect(rect, position, new Vector2(245f, 82f));
        Image image = gameObject.AddComponent<Image>();
        image.color = new Color(0.10f, 0.16f, 0.14f, 1f);
        Button button = gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        CreateText(gameObject.transform, label, Vector2.zero, new Vector2(225f, 70f), 22f);
        return button;
    }

    private static TMP_Text CreateText(Transform parent, string value, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject gameObject = CreateUiObject(value + " Text", parent);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        ConfigureRect(rect, position, size);
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

    private static void ConfigureRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
