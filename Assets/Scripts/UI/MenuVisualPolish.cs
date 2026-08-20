using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuVisualPolish
{
    private static readonly Color BoneWhite = new Color(0.89f, 0.87f, 0.79f, 1f);
    private static readonly Color AgedGold = new Color(0.64f, 0.52f, 0.25f, 1f);
    private static readonly Color Forest = new Color(0.10f, 0.16f, 0.14f, 0.96f);
    private static readonly Color ForestHover = new Color(0.16f, 0.25f, 0.20f, 1f);
    private static readonly Color CorruptionPressed = new Color(0.23f, 0.15f, 0.28f, 1f);
    private static readonly Color Disabled = new Color(0.17f, 0.18f, 0.18f, 0.65f);
    private static readonly Color NightPlum = new Color(0.045f, 0.032f, 0.060f, 1f);
    private static readonly Color DeepForest = new Color(0.055f, 0.095f, 0.080f, 1f);
    private static readonly Color PanelPlum = new Color(0.095f, 0.070f, 0.115f, 0.97f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply(scene);
    }

    private static void Apply(Scene scene)
    {
        if (!scene.path.Contains("/Menus/"))
            return;

        EnsureEventSystem();
        ConfigureResponsiveCanvas(scene);
        InstallMenuBackdrop(scene);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                StyleButton(button);

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                StyleText(text);
        }

        if (scene.name == "Type Ypur Name")
            StyleNameEntry(scene);
        else if (scene.name == "Main Menu")
            StyleMainMenu(scene);
        else if (scene.name == "Story Mode Menu")
            StyleStoryMenu(scene);
    }

    /// <summary>
    /// Sin EventSystem ningún botón responde al clic. "Main Settings" y "Lobby"
    /// no lo traían, así que se entraba a Ajustes y no había forma de volver.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject host = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Debug.Log("[MenuVisualPolish] La escena no tenía EventSystem. Se creó uno para que la UI responda.");
    }

    private static void ConfigureResponsiveCanvas(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (CanvasScaler scaler in root.GetComponentsInChildren<CanvasScaler>(true))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                RectTransform canvasRect = scaler.GetComponent<RectTransform>();
                if (canvasRect != null)
                    canvasRect.localScale = Vector3.one;
            }

            // Los contenedores de layout traían escalas a mano (el del menú
            // principal estaba en 3.92), así que multiplicaban cualquier tamaño
            // que pidiera el código y los botones salían enormes. El CanvasScaler
            // ya adapta la UI a la resolución: estas escalas sobran.
            foreach (LayoutGroup group in root.GetComponentsInChildren<LayoutGroup>(true))
            {
                RectTransform groupRect = group.GetComponent<RectTransform>();
                if (groupRect != null)
                    groupRect.localScale = Vector3.one;
            }
        }
    }

    private static void StyleButton(Button button)
    {
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Forest;
        colors.highlightedColor = ForestHover;
        colors.pressedColor = CorruptionPressed;
        colors.selectedColor = ForestHover;
        colors.disabledColor = Disabled;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.14f;
        button.colors = colors;

        if (button.targetGraphic is Image image)
            image.color = Color.white;

        if (button.GetComponent<MenuButtonMotion>() == null)
            button.gameObject.AddComponent<MenuButtonMotion>();
    }

    private static void StyleText(TMP_Text text)
    {
        text.color = BoneWhite;

        if (text.GetComponentInParent<Button>() != null)
        {
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 1.5f;
            text.color = BoneWhite;
            MenuTheme.ApplyDisplayFont(text);
            return;
        }

        string objectName = text.gameObject.name.ToLowerInvariant();
        if (objectName.Contains("welcome") || objectName.Contains("title") || objectName.Contains("tittle"))
        {
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 3f;
            text.color = AgedGold;
            MenuTheme.ApplyDisplayFont(text);
            return;
        }

        // El resto del texto va en la serif de lectura, no en la fuente por
        // defecto de TextMeshPro: es lo que hacía que los menús se vieran
        // genéricos frente al resto del juego.
        MenuTheme.ApplyBodyFont(text);
    }

    private static void StyleNameEntry(Scene scene)
    {
        TMP_InputField input = null;
        Button continueButton = null;
        TMP_Text welcome = null;
        TMP_Text error = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (input == null)
                input = root.GetComponentInChildren<TMP_InputField>(true);

            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name.ToLowerInvariant().Contains("continue")) continueButton = button;

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                string name = text.name.ToLowerInvariant();
                if (text.transform.parent != null)
                    name += " " + text.transform.parent.name.ToLowerInvariant();
                if (name.Contains("welcome")) welcome = text;
                if (name.Contains("error")) error = text;
            }
        }

        if (welcome != null)
        {
            ConfigureCenteredRect(welcome.rectTransform, new Vector2(760f, 84f), new Vector2(0f, 150f));
            welcome.fontSize = 32f;
            welcome.alignment = TextAlignmentOptions.Center;
            welcome.margin = new Vector4(12f, 8f, 12f, 8f);
            welcome.text = GameLocalization.Choose("¿Cuál es tu nombre?", "What is your name?");
        }

        if (input != null)
        {
            ConfigureCenteredRect(input.GetComponent<RectTransform>(), new Vector2(620f, 84f), new Vector2(0f, 35f));
            if (input.targetGraphic is Image background)
                background.color = Forest;

            input.textComponent.color = BoneWhite;
            input.textComponent.fontSize = 30f;
            input.textComponent.alignment = TextAlignmentOptions.Center;
            input.characterLimit = 20;

            if (input.placeholder is TMP_Text placeholder)
            {
                placeholder.text = GameLocalization.Choose("Escribe tu nombre", "Enter your name");
                placeholder.color = new Color(AgedGold.r, AgedGold.g, AgedGold.b, 0.72f);
                placeholder.fontSize = 26f;
                placeholder.alignment = TextAlignmentOptions.Center;
            }

            input.Select();
            input.ActivateInputField();
        }

        if (continueButton != null)
        {
            ConfigureCenteredRect(continueButton.GetComponent<RectTransform>(), new Vector2(340f, 76f), new Vector2(0f, -90f));
            TMP_Text label = continueButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = GameLocalization.Choose("Continuar", "Continue");
                label.fontSize = 28f;
                label.alignment = TextAlignmentOptions.Center;
            }
        }

        if (error != null)
        {
            ConfigureCenteredRect(error.rectTransform, new Vector2(700f, 70f), new Vector2(0f, -175f));
            error.fontSize = 24f;
            error.alignment = TextAlignmentOptions.Center;
            error.text = GameLocalization.Choose("Escribe un nombre para continuar.", "Enter a name to continue.");
        }
    }

    private static void StyleMainMenu(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                string parentName = text.transform.parent != null
                    ? text.transform.parent.name.ToLowerInvariant()
                    : string.Empty;

                if (text.name.ToLowerInvariant().Contains("welcome") || parentName.Contains("welcome"))
                {
                    ConfigureCenteredRect(text.rectTransform, new Vector2(700f, 60f), new Vector2(0f, 170f));
                    text.fontSize = 26f;
                    text.alignment = TextAlignmentOptions.Center;
                    text.textWrappingMode = TextWrappingModes.NoWrap;
                }
            }

            // El contenedor estiraba los botones a todo su ancho, así que el
            // tamaño pedido abajo se ignoraba y quedaban gigantes.
            foreach (VerticalLayoutGroup group in root.GetComponentsInChildren<VerticalLayoutGroup>(true))
            {
                group.childForceExpandWidth = false;
                group.childForceExpandHeight = false;
                group.childControlWidth = false;
                group.childControlHeight = false;
                group.childAlignment = TextAnchor.MiddleCenter;
                group.spacing = 14f;

                // El contenedor medía 300x100 y se apoyaba en la escala para
                // caber. Con escala 1 necesita el tamaño real de los 3 botones.
                RectTransform groupRect = group.GetComponent<RectTransform>();
                if (groupRect != null)
                {
                    groupRect.sizeDelta = new Vector2(400f, 214f);
                    groupRect.anchorMin = groupRect.anchorMax = groupRect.pivot = new Vector2(0.5f, 0.5f);
                    groupRect.anchoredPosition = new Vector2(0f, -40f);
                }
            }

            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                string name = button.name.ToLowerInvariant();
                if (!name.Contains("story mode") && !name.Contains("online mode") && !name.Contains("settings button"))
                    continue;

                RectTransform rect = button.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(360f, 58f);
                LayoutElement layout = button.GetComponent<LayoutElement>();
                if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = 360f;
                layout.preferredHeight = 58f;

                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    // MainMenuController añade " · Próximamente" después de
                    // esto, así que el texto puede crecer bastante. Autosize
                    // con mínimo evita que se salga del botón.
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 13f;
                    label.fontSizeMax = 21f;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.margin = new Vector4(10f, 2f, 10f, 2f);
                    if (name.Contains("story")) label.text = GameLocalization.Choose("Modo historia", "Story mode");
                    else if (name.Contains("online")) label.text = GameLocalization.Choose("Multijugador", "Multiplayer");
                    else label.text = GameLocalization.Choose("Ajustes", "Settings");
                }
            }
        }
    }

    private static void StyleStoryMenu(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.text == "Your adventure awaits")
                {
                    text.text = GameLocalization.Choose("Tu aventura te espera", "Your adventure awaits");
                    text.fontSize = 34f;
                }
                else if (text.text == "Button")
                {
                    text.text = GameLocalization.Choose("Comenzar", "Start");
                    text.fontSize = 22f;
                }
            }

            Transform brief = FindDeepChild(root.transform, "Level Brief");
            if (brief != null)
            {
                Image panel = brief.GetComponent<Image>();
                if (panel != null) panel.color = PanelPlum;
                RectTransform rect = brief.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.sizeDelta = new Vector2(650f, 460f);
                    rect.anchoredPosition = Vector2.zero;
                }
            }
        }
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private static void InstallMenuBackdrop(Scene scene)
    {
        Canvas canvas = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas != null) break;
        }

        if (canvas == null || canvas.transform.Find("Generated Menu Backdrop") != null)
            return;

        GameObject backdrop = CreateImage(
            "Generated Menu Backdrop",
            canvas.transform,
            NightPlum,
            Vector2.zero,
            Vector2.zero,
            true);
        backdrop.transform.SetAsFirstSibling();

        GameObject forestBand = CreateImage(
            "Forest Band",
            backdrop.transform,
            DeepForest,
            new Vector2(0f, -0.33f),
            new Vector2(0f, 0.66f),
            true);
        Stretch(forestBand.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0.36f));

        CreateEdgeLine(backdrop.transform, true);
        CreateEdgeLine(backdrop.transform, false);

        if (scene.name == "Type Ypur Name")
        {
            GameObject card = CreateImage(
                "Name Entry Card",
                canvas.transform,
                PanelPlum,
                new Vector2(780f, 470f),
                Vector2.zero,
                false);
            card.transform.SetSiblingIndex(1);

            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(AgedGold.r, AgedGold.g, AgedGold.b, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }
    }

    private static GameObject CreateImage(
        string name,
        Transform parent,
        Color color,
        Vector2 size,
        Vector2 position,
        bool stretch)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        if (stretch)
            Stretch(rect, Vector2.zero, Vector2.one);
        else
            ConfigureCenteredRect(rect, size, position);

        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return gameObject;
    }

    private static void CreateEdgeLine(Transform parent, bool top)
    {
        GameObject line = CreateImage(
            top ? "Top Gold Line" : "Bottom Gold Line",
            parent,
            new Color(AgedGold.r, AgedGold.g, AgedGold.b, 0.65f),
            Vector2.zero,
            Vector2.zero,
            true);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.04f, top ? 0.94f : 0.06f);
        rect.anchorMax = new Vector2(0.96f, top ? 0.94f : 0.06f);
        rect.sizeDelta = new Vector2(0f, 2f);
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureCenteredRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }
}

public sealed class MenuButtonMotion : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private const float HoverScale = 1.035f;
    private const float PressedScale = 0.98f;
    private const float AnimationSpeed = 14f;

    private Vector3 baseScale;
    private Vector3 targetScale;

    private void Awake()
    {
        baseScale = transform.localScale;
        targetScale = baseScale;
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            AnimationSpeed * Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        transform.localScale = baseScale;
        targetScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData) => targetScale = baseScale * HoverScale;
    public void OnPointerExit(PointerEventData eventData) => targetScale = baseScale;
    public void OnPointerDown(PointerEventData eventData) => targetScale = baseScale * PressedScale;
    public void OnPointerUp(PointerEventData eventData) => targetScale = baseScale * HoverScale;
}
