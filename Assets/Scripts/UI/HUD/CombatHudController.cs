using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIScripts;

/// <summary>
/// HUD de combate compartido por partidas online y entrenamiento local.
/// Se construye en runtime para que cualquier personaje/prefab use la misma
/// jerarquía y para evitar duplicar controles entre escenas.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatHudController : MonoBehaviour
{
    private static readonly Color Panel = new Color(0.025f, 0.018f, 0.03f, 0.78f);
    private static readonly Color Gold = new Color(0.72f, 0.57f, 0.27f, 1f);
    private static readonly Color Ivory = new Color(0.94f, 0.91f, 0.82f, 1f);
    private static readonly Color Plum = new Color(0.20f, 0.09f, 0.17f, 0.92f);

    private PlayerController player;
    private GameObject hudRoot;
    private TMP_Text nameLabel;
    private TMP_Text healthLabel;
    private Image healthFill;
    private TMP_Text teamLabel;
    private TMP_Text statusLabel;
    private GameObject confirmationPanel;
    private TMP_Text confirmationWarning;
    private GameObject resultPanel;
    private TMP_Text resultLabel;
    private GameObject trainingPanel;
    private TMP_Text trainingLabel;
    private AbilityOverlay basicOverlay;
    private AbilityOverlay ultimateOverlay;

    private sealed class AbilityOverlay
    {
        public GameObject Root;
        public Image Fill;
        public TMP_Text Label;
        public float Duration;
        public string Key;
    }

    public static CombatHudController EnsureFor(PlayerController owner)
    {
        if (owner == null || !owner.HasLocalControl) return null;

        CombatHudController hud = owner.GetComponent<CombatHudController>();
        if (hud == null) hud = owner.gameObject.AddComponent<CombatHudController>();
        hud.Bind(owner);
        return hud;
    }

    public void Bind(PlayerController owner)
    {
        player = owner;
        if (hudRoot == null && isActiveAndEnabled) Build();
    }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    private void Start()
    {
        if (player != null && player.HasLocalControl && hudRoot == null) Build();
    }

    private void Build()
    {
        hudRoot = new GameObject("Combat HUD", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = hudRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        CanvasScaler scaler = hudRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        RectTransform root = hudRoot.GetComponent<RectTransform>();
        Stretch(root);

        Button exit = CreateButton(root, "Exit", GameLocalization.Choose("SALIR AL MENÚ", "EXIT TO MENU"),
            new Vector2(26f, -24f), new Vector2(290f, 72f), TextAnchor.UpperLeft);
        exit.onClick.AddListener(player.RequestExit);

        RectTransform identity = CreatePanel(root, "Player status", new Vector2(0f, -22f),
            new Vector2(470f, 122f), TextAnchor.UpperCenter, Panel);
        nameLabel = CreateText(identity, "Name", string.Empty, 32, Ivory, FontStyles.Bold);
        Anchor(nameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(430f, 40f));

        RectTransform healthTrack = CreatePanel(identity, "Health", new Vector2(0f, -62f),
            new Vector2(400f, 32f), TextAnchor.UpperCenter, new Color(0.04f, 0.025f, 0.035f, 0.95f));
        healthFill = CreateImage(healthTrack, "Fill", new Color(0.27f, 0.72f, 0.39f, 1f));
        healthFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        healthFill.rectTransform.anchorMax = new Vector2(1f, 1f);
        healthFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        healthFill.rectTransform.offsetMin = new Vector2(3f, 3f);
        healthFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
        healthLabel = CreateText(healthTrack, "Health value", string.Empty, 24, Ivory, FontStyles.Bold);
        Stretch(healthLabel.rectTransform);

        teamLabel = CreateText(root, "Team", string.Empty, 22, Ivory, FontStyles.Bold);
        Anchor(teamLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(680f, 32f));

        statusLabel = CreateText(root, "Network status", string.Empty, 22, Ivory, FontStyles.Normal);
        Anchor(statusLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -188f), new Vector2(760f, 64f));

        BuildConfirmation(root);
        BuildResult(root);
        BuildTrainingControls(root);
        BindAbilityOverlays();
        hudRoot.AddComponent<MobileSafeArea>();
    }

    private void BuildConfirmation(RectTransform root)
    {
        RectTransform panel = CreatePanel(root, "Exit confirmation", Vector2.zero,
            new Vector2(560f, 250f), TextAnchor.MiddleCenter, new Color(0.025f, 0.018f, 0.03f, 0.96f));
        confirmationPanel = panel.gameObject;
        CreateBorder(panel);

        TMP_Text title = CreateText(panel, "Title",
            GameLocalization.Choose("¿ABANDONAR LA PARTIDA?", "LEAVE THE MATCH?"), 34, Gold, FontStyles.Bold);
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(510f, 42f));

        confirmationWarning = CreateText(panel, "Warning", string.Empty, 24, Ivory, FontStyles.Normal);
        Anchor(confirmationWarning.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(500f, 68f));

        Button keep = CreateButton(panel, "Keep playing",
            GameLocalization.Choose("SEGUIR JUGANDO", "KEEP PLAYING"), new Vector2(-135f, 26f),
            new Vector2(240f, 54f), TextAnchor.LowerCenter);
        keep.onClick.AddListener(player.CancelExit);

        Button leave = CreateButton(panel, "Leave", GameLocalization.Choose("ABANDONAR", "LEAVE"),
            new Vector2(135f, 26f), new Vector2(240f, 54f), TextAnchor.LowerCenter);
        leave.onClick.AddListener(player.ConfirmExit);
        confirmationPanel.SetActive(false);
    }

    private void BuildResult(RectTransform root)
    {
        RectTransform panel = CreatePanel(root, "Match result", new Vector2(0f, -225f),
            new Vector2(520f, 180f), TextAnchor.UpperCenter, new Color(0.025f, 0.018f, 0.03f, 0.94f));
        resultPanel = panel.gameObject;
        CreateBorder(panel);

        resultLabel = CreateText(panel, "Result", string.Empty, 42, Gold, FontStyles.Bold);
        Anchor(resultLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(480f, 48f));

        Button again = CreateButton(panel, "Play again", GameLocalization.Choose("JUGAR OTRA VEZ", "PLAY AGAIN"),
            new Vector2(-125f, 22f), new Vector2(220f, 52f), TextAnchor.LowerCenter);
        again.onClick.AddListener(player.PlayAgain);
        Button exit = CreateButton(panel, "Exit result", GameLocalization.Choose("SALIR AL MENÚ", "EXIT TO MENU"),
            new Vector2(125f, 22f), new Vector2(220f, 52f), TextAnchor.LowerCenter);
        exit.onClick.AddListener(player.ExitToMenu);
        resultPanel.SetActive(false);
    }

    private void BuildTrainingControls(RectTransform root)
    {
        CombatTrainingBootstrap training = FindFirstObjectByType<CombatTrainingBootstrap>();
        if (training == null) return;

        RectTransform panel = CreatePanel(root, "Training controls", new Vector2(-24f, -24f),
            new Vector2(450f, 136f), TextAnchor.UpperRight, Panel);
        trainingPanel = panel.gameObject;
        trainingLabel = CreateText(panel, "Training hint",
            GameLocalization.Choose("ENTRENAMIENTO · R reinicia objetivos", "TRAINING · R resets targets"),
            26, Ivory, FontStyles.Bold);
        Anchor(trainingLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(420f, 42f));

        Button reset = CreateButton(panel, "Reset targets", GameLocalization.Choose("REINICIAR OBJETIVOS", "RESET TARGETS"),
            new Vector2(0f, 16f), new Vector2(370f, 58f), TextAnchor.LowerCenter);
        reset.onClick.AddListener(training.ResetTargets);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        trainingLabel.text = GameLocalization.Choose(
            "ENTRENAMIENTO · 1–6 personaje · R objetivos",
            "TRAINING · 1–6 character · R targets");
#endif
    }

    private void BindAbilityOverlays()
    {
        foreach (AttackJoystick joystick in FindObjectsByType<AttackJoystick>(FindObjectsSortMode.None))
        {
            bool ultimate = joystick.name.ToLowerInvariant().Contains("ultimate");
            AbilityOverlay overlay = CreateAbilityOverlay(joystick.transform as RectTransform,
                ultimate ? "E" : "Q", ultimate ? 8f : 1f);
            if (ultimate) ultimateOverlay = overlay;
            else basicOverlay = overlay;
        }
    }

    private AbilityOverlay CreateAbilityOverlay(RectTransform parent, string key, float duration)
    {
        if (parent == null) return null;

        GameObject root = new GameObject($"{key} Cooldown", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        Stretch(rect);
        root.transform.SetAsLastSibling();

        Image fill = CreateImage(rect, "Cooldown fill", new Color(0.04f, 0.025f, 0.05f, 0.72f));
        Stretch(fill.rectTransform);
        fill.sprite = RuntimeSprite();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = 2;
        fill.fillClockwise = false;
        fill.raycastTarget = false;

        TMP_Text label = CreateText(rect, "Key", key, 40, Ivory, FontStyles.Bold);
        Stretch(label.rectTransform);
        label.raycastTarget = false;

        return new AbilityOverlay { Root = root, Fill = fill, Label = label, Duration = duration, Key = key };
    }

    private void Update()
    {
        if (player == null)
        {
            enabled = false;
            return;
        }

        nameLabel.text = player.PlayerDisplayName;
        healthLabel.text = $"{GameLocalization.Choose("VIDA", "HEALTH")}  {player.CurrentHealth} / {PlayerController.MaxHealth}";
        float health = Mathf.Clamp01(player.CurrentHealth / (float)PlayerController.MaxHealth);
        healthFill.rectTransform.anchorMax = new Vector2(health, 1f);
        healthFill.color = health > 0.35f ? new Color(0.27f, 0.72f, 0.39f) : new Color(0.78f, 0.22f, 0.22f);

        teamLabel.text = player.TeamStatusText;
        teamLabel.color = player.TeamDisplayColor;
        statusLabel.text = player.NetworkStatusText;
        statusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusLabel.text));

        UpdateAbility(basicOverlay, player.BasicCooldownRemaining);
        UpdateAbility(ultimateOverlay, player.UltimateCooldownRemaining);

        confirmationWarning.text = player.ExitWarningText;
        confirmationPanel.SetActive(player.ExitConfirmationVisible);

        string result = player.MatchResultText;
        resultPanel.SetActive(!string.IsNullOrEmpty(result));
        resultLabel.text = result;
    }

    private static void UpdateAbility(AbilityOverlay overlay, float remaining)
    {
        if (overlay == null) return;
        overlay.Fill.fillAmount = Mathf.Clamp01(remaining / overlay.Duration);
        overlay.Label.text = remaining > 0f ? $"{overlay.Key}\n{remaining:0.0}" : overlay.Key;
    }

    private void OnDestroy()
    {
        if (hudRoot != null) Destroy(hudRoot);
        if (basicOverlay?.Root != null) Destroy(basicOverlay.Root);
        if (ultimateOverlay?.Root != null) Destroy(ultimateOverlay.Root);
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 position,
        Vector2 size, TextAnchor anchor, Color color)
    {
        Image image = CreateImage(parent, name, color);
        SetAnchor(image.rectTransform, anchor, position, size);
        return image.rectTransform;
    }

    private Button CreateButton(Transform parent, string name, string caption, Vector2 position,
        Vector2 size, TextAnchor anchor)
    {
        RectTransform rect = CreatePanel(parent, name, position, size, anchor, Plum);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.86f, 0.58f);
        colors.pressedColor = new Color(0.75f, 0.58f, 0.30f);
        button.colors = colors;
        CreateBorder(rect);

        TMP_Text text = CreateText(rect, "Label", caption, 26, Ivory, FontStyles.Bold);
        Stretch(text.rectTransform);
        text.raycastTarget = false;
        return button;
    }

    private static void CreateBorder(RectTransform parent)
    {
        Outline outline = parent.gameObject.AddComponent<Outline>();
        outline.effectColor = Gold;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, int size, Color color, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(20f, size * 0.78f);
        text.fontSizeMax = size;
        if ((style & FontStyles.Bold) != 0) MenuTheme.ApplyDisplayFont(text);
        else MenuTheme.ApplyBodyFont(text);
        return text;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void SetAnchor(RectTransform rect, TextAnchor anchor, Vector2 position, Vector2 size)
    {
        Vector2 point = anchor switch
        {
            TextAnchor.UpperLeft => new Vector2(0f, 1f),
            TextAnchor.UpperCenter => new Vector2(0.5f, 1f),
            TextAnchor.UpperRight => new Vector2(1f, 1f),
            TextAnchor.MiddleCenter => new Vector2(0.5f, 0.5f),
            TextAnchor.LowerCenter => new Vector2(0.5f, 0f),
            _ => new Vector2(0.5f, 0.5f)
        };
        Anchor(rect, point, position, size);
    }

    private static void Anchor(RectTransform rect, Vector2 point, Vector2 position, Vector2 size)
    {
        rect.anchorMin = point;
        rect.anchorMax = point;
        rect.pivot = point;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite RuntimeSprite()
    {
        return Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }
}
