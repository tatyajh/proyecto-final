using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameLanguage { Spanish = 0, English = 1 }

/// <summary>Single language preference used by menus, matchmaking and HUD.</summary>
public static class GameLocalization
{
    private const string PreferenceKey = "GameLanguage";
    public static event Action LanguageChanged;

    public static GameLanguage Current =>
        (GameLanguage)PlayerPrefs.GetInt(PreferenceKey, (int)GameLanguage.Spanish);

    public static bool IsSpanish => Current == GameLanguage.Spanish;
    public static string Choose(string spanish, string english) => IsSpanish ? spanish : english;

    public static void Set(GameLanguage language)
    {
        if (Current == language && PlayerPrefs.HasKey(PreferenceKey)) return;
        PlayerPrefs.SetInt(PreferenceKey, (int)language);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
    }
}

/// <summary>Creates the language controls without requiring scene-specific wiring.</summary>
public static class LanguageSelectionInstaller
{
    private static readonly Color Plum = new Color(0.055f, 0.035f, 0.070f, 0.98f);
    private static readonly Color Forest = new Color(0.10f, 0.18f, 0.15f, 1f);
    private static readonly Color Gold = new Color(0.72f, 0.54f, 0.20f, 1f);
    private static readonly Color Bone = new Color(0.92f, 0.90f, 0.82f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene);

    private static void Install(Scene scene)
    {
        if (scene.name != "Main Settings" && scene.name != "Type Ypur Name") return;
        if (UnityEngine.Object.FindFirstObjectByType<LanguageSelectionView>() != null) return;

        GameObject host = new GameObject("Language Selection", typeof(RectTransform));
        host.AddComponent<LanguageSelectionView>().Build(scene.name == "Main Settings");
    }

    private sealed class LanguageSelectionView : MonoBehaviour
    {
        private TMP_Text title;
        private TMP_Text subtitle;
        private Button spanish;
        private Button english;
        private TMP_Text backLabel;

        public void Build(bool fullScreen)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Language Canvas", typeof(RectTransform));
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            transform.SetParent(canvas.transform, false);
            // MenuVisualPolish instala un fondo en el mismo canvas; sin esto el
            // panel podía quedar detrás y parecer que Ajustes estaba vacío.
            transform.SetAsLastSibling();
            RectTransform root = gameObject.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = fullScreen ? new Vector2(0.5f, 0.5f) : new Vector2(1f, 1f);
            root.pivot = fullScreen ? new Vector2(0.5f, 0.5f) : new Vector2(1f, 1f);
            root.sizeDelta = fullScreen ? new Vector2(620f, 340f) : new Vector2(280f, 58f);
            root.anchoredPosition = fullScreen ? Vector2.zero : new Vector2(-24f, -22f);
            Image panel = gameObject.AddComponent<Image>();
            panel.color = Plum;
            Outline outline = gameObject.AddComponent<Outline>();
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(1f, -1f);

            if (fullScreen)
            {
                title = MakeText("Language Title", transform, new Vector2(0f, 112f), new Vector2(560f, 45f), 30f);
                subtitle = MakeText("Language Subtitle", transform, new Vector2(0f, 65f), new Vector2(560f, 32f), 17f);
                spanish = MakeButton("Español", new Vector2(-145f, -5f), new Vector2(240f, 68f));
                english = MakeButton("English", new Vector2(145f, -5f), new Vector2(240f, 68f));
                Button back = MakeButton("Volver", new Vector2(0f, -112f), new Vector2(220f, 50f));
                backLabel = back.GetComponentInChildren<TMP_Text>(true);
                back.onClick.AddListener(() => SceneManager.LoadScene("Main Menu"));
            }
            else
            {
                spanish = MakeButton("ES", new Vector2(-64f, 0f), new Vector2(112f, 40f));
                english = MakeButton("EN", new Vector2(64f, 0f), new Vector2(112f, 40f));
            }

            spanish.onClick.AddListener(() => Select(GameLanguage.Spanish));
            english.onClick.AddListener(() => Select(GameLanguage.English));
            Refresh();
        }

        private void Select(GameLanguage language)
        {
            GameLocalization.Set(language);
            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.buildIndex);
        }

        private void Refresh()
        {
            if (title != null) title.text = GameLocalization.Choose("IDIOMA", "LANGUAGE");
            if (subtitle != null) subtitle.text = GameLocalization.Choose("Elige el idioma del juego", "Choose the game language");
            SetButtonState(spanish, GameLocalization.Current == GameLanguage.Spanish);
            SetButtonState(english, GameLocalization.Current == GameLanguage.English);
            if (backLabel != null) backLabel.text = GameLocalization.Choose("VOLVER", "BACK");
        }

        private static void SetButtonState(Button button, bool selected)
        {
            if (button.targetGraphic is Image image) image.color = selected ? Gold : Forest;
        }

        private Button MakeButton(string label, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = Forest;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            MakeText("Label", go.transform, Vector2.zero, size, 18f).text = label.ToUpperInvariant();
            return button;
        }

        private static TMP_Text MakeText(string name, Transform parent, Vector2 position, Vector2 size, float fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            text.color = Bone;
            MenuTheme.ApplyBodyFont(text);
            return text;
        }
    }
}
