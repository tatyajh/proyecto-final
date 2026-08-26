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
    private static readonly Color Panel = MenuTheme.WithAlpha(MenuTheme.HudPanel, 0.66f);
    private static readonly Color Gold = MenuTheme.GiltBright;
    private static readonly Color Ivory = MenuTheme.HudIvory;
    private static readonly Color Plum = MenuTheme.WithAlpha(MenuTheme.HudAshDark, 0.78f);

    private PlayerController player;
    private GameObject hudRoot;
    private TMP_Text nameLabel;
    private TMP_Text healthLabel;
    private Image healthFill;
    private GameObject buffRow;
    private StatusBar hasteBar;
    private StatusBar powerBar;
    private StatusBar shieldBar;
    private TMP_Text teamLabel;
    private TMP_Text statusLabel;
    private TMP_Text arenaStatusLabel;
    private RectTransform budIndicator;
    private TMP_Text budIndicatorLabel;
    private GameObject confirmationPanel;
    private TMP_Text confirmationWarning;
    private GameObject resultPanel;
    private TMP_Text resultLabel;
    private GameObject trainingPanel;
    private TMP_Text trainingLabel;
    private CombatTrainingBootstrap training;
    private TMP_Text opponentNameLabel;
    private TMP_Text opponentHealthLabel;
    private Image opponentHealthFill;
    private GameObject towerPanel;
    private TMP_Text towerTitle;
    private TMP_Text towerSubtitle;
    private TMP_Text towerProgress;
    private readonly RawImage[] towerPortraits = new RawImage[5];
    private readonly Image[] towerPortraitFrames = new Image[5];
    private Button towerPrimaryButton;
    private TMP_Text towerPrimaryLabel;
    private GameObject blindOverlay;
    private AbilityOverlay basicOverlay;
    private AbilityOverlay ultimateOverlay;
    private Button lockButton;
    private TMP_Text lockButtonLabel;
    private CanvasGroup inputHintGroup;
    private float inputHintStartedAt = -1f;
    private bool resultPresentationSuppressed;
    private bool usesTouchControls;
    private static Sprite healthFrameSprite;
    private static Sprite abilityFrameSprite;
    private static Sprite modalFrameSprite;
    private static Sprite hudButtonFrameSprite;
    private static Sprite controlHintFrameSprite;
    private static Sprite runtimeCircleSprite;

    private sealed class AbilityOverlay
    {
        public GameObject Root;
        public Image Fill;
        public TMP_Text KeyLabel;
        public TMP_Text CaptionLabel;
        public TMP_Text AbilityNameLabel;
        public TMP_Text CooldownLabel;
        public RawImage Icon;
        public float Duration;
        public string Key;
    }

    private sealed class StatusBar
    {
        public GameObject Root;
        public Image Fill;
        public TMP_Text Label;
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
            new Vector2(26f, -24f), new Vector2(400f, 90f), TextAnchor.UpperLeft);
        exit.onClick.AddListener(player.RequestExit);

        RectTransform identity = CreatePanel(root, "Player status", new Vector2(0f, -22f),
            new Vector2(520f, 146f), TextAnchor.UpperCenter, Panel);
        CreateBorder(identity);
        nameLabel = CreateText(identity, "Character", string.Empty, 40, Gold, FontStyles.Bold);
        Anchor(nameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(486f, 54f));
        ConfigureEssentialLabel(nameLabel, 40f);

        RectTransform healthTrack = CreatePanel(identity, "Health", new Vector2(0f, -78f),
            new Vector2(430f, 36f), TextAnchor.UpperCenter, new Color(0.04f, 0.025f, 0.035f, 0.95f));
        healthFill = CreateImage(healthTrack, "Fill", new Color(0.27f, 0.72f, 0.39f, 1f));
        healthFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        healthFill.rectTransform.anchorMax = new Vector2(1f, 1f);
        healthFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        healthFill.rectTransform.offsetMin = new Vector2(3f, 3f);
        healthFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
        healthLabel = CreateText(healthTrack, "Health value", string.Empty, 30, Ivory, FontStyles.Bold);
        Stretch(healthLabel.rectTransform);
        ConfigureEssentialLabel(healthLabel, 30f);
        AddHealthFrame(healthTrack);
        nameLabel.transform.SetAsLastSibling();

        BuildStatusBars(root);

        teamLabel = CreateText(root, "Team", string.Empty, 22, Ivory, FontStyles.Bold);
        Anchor(teamLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(680f, 32f));

        statusLabel = CreateText(root, "Network status", string.Empty, 22, Ivory, FontStyles.Normal);
        Anchor(statusLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -266f), new Vector2(760f, 64f));

        arenaStatusLabel = CreateText(root, "Arena status", string.Empty, 26,
            new Color(0.92f, 0.42f, 0.48f), FontStyles.Bold);
        Anchor(arenaStatusLabel.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(0f, -330f), new Vector2(820f, 42f));

        budIndicator = CreatePanel(root, "Corrupted bud pointer", Vector2.zero,
            new Vector2(330f, 68f), TextAnchor.MiddleCenter, MenuTheme.WithAlpha(MenuTheme.HudAshDark, 0.92f));
        Sprite pointerFrame = HudButtonFrameSprite();
        if (pointerFrame != null)
        {
            Image image = budIndicator.GetComponent<Image>();
            image.sprite = pointerFrame;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        budIndicatorLabel = CreateText(budIndicator, "Bud pointer label", string.Empty, 24, Gold, FontStyles.Bold);
        Stretch(budIndicatorLabel.rectTransform);
        budIndicatorLabel.rectTransform.offsetMin = new Vector2(34f, 10f);
        budIndicatorLabel.rectTransform.offsetMax = new Vector2(-34f, -10f);
        budIndicator.gameObject.SetActive(false);

        BuildConfirmation(root);
        BuildResult(root);
        BuildTrainingControls(root);
        BuildTrainingTower(root);
        usesTouchControls = Application.isMobilePlatform ||
                            (Input.touchSupported && SystemInfo.deviceType == DeviceType.Handheld);
        BuildTargetLock(root);
        BuildInputPresentation(root);
        Image blind = CreateImage(root, "Blind effect", new Color(0.015f, 0.008f, 0.02f, 0.78f));
        Stretch(blind.rectTransform);
        blind.raycastTarget = false;
        blindOverlay = blind.gameObject;
        blindOverlay.transform.SetAsFirstSibling();
        blindOverlay.SetActive(false);
        hudRoot.AddComponent<MobileSafeArea>();
    }

    private void BuildConfirmation(RectTransform root)
    {
        RectTransform panel = CreatePanel(root, "Exit confirmation", Vector2.zero,
            new Vector2(560f, 250f), TextAnchor.MiddleCenter, new Color(0.025f, 0.018f, 0.03f, 0.96f));
        confirmationPanel = panel.gameObject;
        ApplyModalFrame(panel);

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

    private void BuildStatusBars(RectTransform root)
    {
        RectTransform row = CreatePanel(root, "Active blessings", new Vector2(0f, -164f),
            new Vector2(720f, 58f), TextAnchor.UpperCenter, Color.clear);
        buffRow = row.gameObject;
        hasteBar = CreateStatusBar(row, "Haste", -240f, new Color(0.24f, 0.58f, 0.96f, 1f));
        powerBar = CreateStatusBar(row, "Power", 0f, new Color(0.96f, 0.62f, 0.16f, 1f));
        shieldBar = CreateStatusBar(row, "Shield", 240f, new Color(0.62f, 0.30f, 0.82f, 1f));
        buffRow.SetActive(false);
    }

    private StatusBar CreateStatusBar(Transform parent, string name, float x, Color color)
    {
        RectTransform track = CreatePanel(parent, name, new Vector2(x, 0f), new Vector2(218f, 50f),
            TextAnchor.MiddleCenter, new Color(0.025f, 0.02f, 0.03f, 0.94f));
        CreateBorder(track);
        Image fill = CreateImage(track, "Duration", color);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.rectTransform.offsetMin = new Vector2(4f, 4f);
        fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
        fill.color = new Color(color.r, color.g, color.b, 0.56f);
        TMP_Text label = CreateText(track, "Label", string.Empty, 22, Ivory, FontStyles.Bold);
        Stretch(label.rectTransform);
        ConfigureEssentialLabel(label, 22f);
        label.outlineWidth = 0.16f;
        label.outlineColor = new Color32(8, 6, 10, 255);
        return new StatusBar { Root = track.gameObject, Fill = fill, Label = label };
    }

    private void BuildResult(RectTransform root)
    {
        Image dimmer = CreateImage(root, "Result backdrop", new Color(0.015f, 0.02f, 0.018f, 0.58f));
        Stretch(dimmer.rectTransform);
        dimmer.raycastTarget = true;

        RectTransform panel = CreatePanel(dimmer.transform, "Match result", Vector2.zero,
            new Vector2(680f, 330f), TextAnchor.MiddleCenter, MenuTheme.WithAlpha(MenuTheme.HudPanel, 0.98f));
        resultPanel = panel.gameObject;
        ApplyModalFrame(panel);

        resultLabel = CreateText(panel, "Result", string.Empty, 50, Gold, FontStyles.Bold);
        Anchor(resultLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(560f, 68f));

        string replayCaption = PlayModeContext.Current == PlayMode.Training
            ? GameLocalization.Choose("REVANCHA", "REMATCH")
            : GameLocalization.Choose("JUGAR OTRA VEZ", "PLAY AGAIN");
        Button again = CreateButton(panel, "Play again", replayCaption,
            new Vector2(-150f, 58f), new Vector2(260f, 72f), TextAnchor.LowerCenter);
        again.onClick.AddListener(player.PlayAgain);
        Button exit = CreateButton(panel, "Exit result", GameLocalization.Choose("SALIR AL MENÚ", "EXIT TO MENU"),
            new Vector2(150f, 58f), new Vector2(260f, 72f), TextAnchor.LowerCenter);
        exit.onClick.AddListener(player.ExitToMenu);
        dimmer.gameObject.SetActive(false);
        resultPanel = dimmer.gameObject;
    }

    private void BuildTrainingControls(RectTransform root)
    {
        training = FindFirstObjectByType<CombatTrainingBootstrap>();
        if (training == null) return;

        RectTransform panel = CreatePanel(root, "Training opponent", new Vector2(-26f, -24f),
            new Vector2(500f, 146f), TextAnchor.UpperRight, Panel);
        trainingPanel = panel.gameObject;
        CreateBorder(panel);
        opponentNameLabel = CreateText(panel, "Opponent name",
            GameLocalization.Choose("RIVAL · PREPARANDO", "RIVAL · PREPARING"), 36, Gold, FontStyles.Bold);
        Anchor(opponentNameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(468f, 48f));

        RectTransform healthTrack = CreatePanel(panel, "Opponent health", new Vector2(0f, -78f),
            new Vector2(420f, 36f), TextAnchor.UpperCenter, new Color(0.04f, 0.025f, 0.035f, 0.95f));
        opponentHealthFill = CreateImage(healthTrack, "Fill", new Color(0.78f, 0.22f, 0.22f, 1f));
        opponentHealthFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        opponentHealthFill.rectTransform.anchorMax = new Vector2(1f, 1f);
        opponentHealthFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        opponentHealthFill.rectTransform.offsetMin = new Vector2(3f, 3f);
        opponentHealthFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
        opponentHealthLabel = CreateText(healthTrack, "Opponent health value", string.Empty, 29, Ivory, FontStyles.Bold);
        Stretch(opponentHealthLabel.rectTransform);
        AddHealthFrame(healthTrack);
        opponentNameLabel.transform.SetAsLastSibling();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        trainingLabel = CreateText(root, "Training hint",
            GameLocalization.Choose("1–6 cambia personaje · T nuevo duelo", "1–6 changes character · T new duel"),
            22, Ivory, FontStyles.Normal);
        Anchor(trainingLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-26f, -156f), new Vector2(430f, 34f));
#endif
    }

    private void BuildTrainingTower(RectTransform root)
    {
        if (training == null || training.Run == null) return;

        Image dimmer = CreateImage(root, "Training tower backdrop", new Color(0.01f, 0.008f, 0.015f, 0.74f));
        Stretch(dimmer.rectTransform);
        dimmer.raycastTarget = true;
        towerPanel = dimmer.gameObject;

        RectTransform panel = CreatePanel(dimmer.transform, "Blight ladder", Vector2.zero,
            new Vector2(980f, 570f), TextAnchor.MiddleCenter, MenuTheme.WithAlpha(MenuTheme.HudPanel, 0.98f));
        ApplyModalFrame(panel);

        towerTitle = CreateText(panel, "Tower title", string.Empty, 52, Gold, FontStyles.Bold);
        Anchor(towerTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(850f, 70f));
        towerSubtitle = CreateText(panel, "Tower rival", string.Empty, 34, Ivory, FontStyles.Bold);
        Anchor(towerSubtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(850f, 100f));

        RectTransform portraits = CreatePanel(panel, "Tower rivals", new Vector2(0f, 18f),
            new Vector2(780f, 150f), TextAnchor.MiddleCenter, Color.clear);
        for (int i = 0; i < towerPortraits.Length; i++)
        {
            RectTransform portraitFrame = CreatePanel(portraits, $"Rival portrait {i + 1}",
                new Vector2(-304f + i * 152f, 0f), new Vector2(126f, 138f), TextAnchor.MiddleCenter,
                new Color(0.025f, 0.018f, 0.03f, 0.94f));
            CreateBorder(portraitFrame);
            towerPortraitFrames[i] = portraitFrame.GetComponent<Image>();
            towerPortraits[i] = CreateRawImage(portraitFrame, "Portrait", Color.white);
            Stretch(towerPortraits[i].rectTransform);
            towerPortraits[i].rectTransform.offsetMin = new Vector2(7f, 7f);
            towerPortraits[i].rectTransform.offsetMax = new Vector2(-7f, -7f);
        }
        towerProgress = CreateText(panel, "Tower progress", string.Empty, 25, MenuTheme.BoneDim, FontStyles.Normal);
        Anchor(towerProgress.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -118f), new Vector2(860f, 120f));

        towerPrimaryButton = CreateButton(panel, "Tower primary", string.Empty,
            new Vector2(0f, 62f), new Vector2(380f, 78f), TextAnchor.LowerCenter);
        towerPrimaryLabel = towerPrimaryButton.GetComponentInChildren<TMP_Text>();
        towerPrimaryButton.onClick.AddListener(HandleTowerPrimary);
        Button exit = CreateButton(panel, "Tower exit", GameLocalization.Choose("SALIR AL MENÚ", "EXIT TO MENU"),
            new Vector2(0f, 150f), new Vector2(330f, 62f), TextAnchor.LowerCenter);
        exit.onClick.AddListener(player.ExitToMenu);
        towerPanel.transform.SetAsLastSibling();
    }

    private void HandleTowerPrimary()
    {
        TrainingRunController run = training != null ? training.Run : null;
        if (run == null) return;
        if (run.Phase == TrainingRunPhase.RoundLost) run.RetryCurrent();
        else if (run.Phase == TrainingRunPhase.Complete)
        {
            run.RestartRun();
            run.BeginOrContinue();
        }
        else run.BeginOrContinue();
    }

    private void BuildTargetLock(RectTransform root)
    {
        lockButton = CreateButton(root, "Target lock",
            usesTouchControls
                ? GameLocalization.Choose("FIJAR RIVAL", "LOCK TARGET")
                : GameLocalization.Choose("FIJAR RIVAL · TAB", "LOCK TARGET · TAB"),
            new Vector2(0f, 142f), new Vector2(360f, 78f), TextAnchor.LowerCenter);
        lockButton.onClick.AddListener(() => player?.Targeting?.ToggleLock());
        lockButtonLabel = lockButton.GetComponentInChildren<TMP_Text>();
    }

    private void BuildInputPresentation(RectTransform root)
    {
        if (usesTouchControls)
        {
            BindAbilityOverlays();
            return;
        }

        foreach (VirtualJoystick joystick in FindObjectsByType<VirtualJoystick>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            joystick.gameObject.SetActive(false);
        foreach (AttackJoystick joystick in FindObjectsByType<AttackJoystick>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            joystick.gameObject.SetActive(false);

        RectTransform help = CreatePanel(root, "PC controls", new Vector2(0f, 28f),
            new Vector2(660f, 72f), TextAnchor.LowerCenter, MenuTheme.WithAlpha(MenuTheme.HudPanel, 0.82f));
        Sprite hintFrame = HudControlHintSprite();
        if (hintFrame != null)
        {
            Image helpImage = help.GetComponent<Image>();
            helpImage.sprite = hintFrame;
            helpImage.type = Image.Type.Sliced;
            helpImage.color = Color.white;
        }
        else CreateBorder(help);
        TMP_Text hint = CreateText(help, "Keys",
            GameLocalization.Choose("WASD · MOVER       TAB · FIJAR RIVAL",
                "WASD · MOVE       TAB · LOCK TARGET"),
            27, Ivory, FontStyles.Bold);
        Stretch(hint.rectTransform);
        hint.rectTransform.offsetMin = new Vector2(54f, 12f);
        hint.rectTransform.offsetMax = new Vector2(-54f, -12f);
        inputHintGroup = help.gameObject.AddComponent<CanvasGroup>();

        basicOverlay = CreateDesktopAbilityOverlay(root, "Q", new Vector2(-310f, 82f));
        ultimateOverlay = CreateDesktopAbilityOverlay(root, "E", new Vector2(-18f, 82f));
    }

    private AbilityOverlay CreateDesktopAbilityOverlay(RectTransform root, string key, Vector2 position)
    {
        RectTransform panel = CreatePanel(root, $"{key} ability", position,
            new Vector2(280f, 292f), TextAnchor.LowerRight, Color.clear);
        RectTransform face = CreatePanel(panel, "Ability face", new Vector2(0f, 64f),
            new Vector2(188f, 188f), TextAnchor.MiddleCenter, Plum);
        Sprite frame = HudAbilitySprite();
        Image background = face.GetComponent<Image>();
        if (frame != null)
        {
            background.sprite = frame;
            background.color = Color.white;
            background.type = Image.Type.Simple;
            background.preserveAspect = true;
        }

        RawImage icon = CreateRawImage(face, "Ability icon", Color.white);
        Stretch(icon.rectTransform);
        icon.rectTransform.offsetMin = new Vector2(32f, 32f);
        icon.rectTransform.offsetMax = new Vector2(-32f, -32f);
        icon.raycastTarget = false;

        Image fill = CreateImage(face, "Cooldown fill", new Color(0.015f, 0.012f, 0.02f, 0.76f));
        Stretch(fill.rectTransform);
        fill.rectTransform.offsetMin = new Vector2(22f, 22f);
        fill.rectTransform.offsetMax = new Vector2(-22f, -22f);
        fill.sprite = RuntimeCircleSprite();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = 2;
        fill.fillClockwise = false;
        fill.raycastTarget = false;

        TMP_Text keyLabel = CreateText(face, "Key", key, 66, Color.white, FontStyles.Bold);
        Anchor(keyLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(150f, 92f));
        ConfigureEssentialLabel(keyLabel, 66f);
        keyLabel.outlineWidth = 0.22f;
        keyLabel.outlineColor = new Color32(8, 6, 10, 255);
        TMP_Text cooldown = CreateText(face, "Cooldown seconds", string.Empty, 30, Color.white, FontStyles.Bold);
        Anchor(cooldown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(136f, 40f));
        ConfigureEssentialLabel(cooldown, 30f);
        cooldown.outlineWidth = 0.2f;
        cooldown.outlineColor = new Color32(8, 6, 10, 255);
        TMP_Text caption = CreateText(face, "Ability type", string.Empty, 30, Gold, FontStyles.Bold);
        Anchor(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(150f, 40f));
        // En PC la tecla y el cooldown pertenecen al círculo. El nombre
        // completo debajo ya explica la habilidad y evita una tercera línea
        // compitiendo dentro del medallón.
        caption.gameObject.SetActive(false);

        RectTransform namePlate = CreatePanel(panel, "Ability name plate", new Vector2(0f, 0f),
            new Vector2(280f, 84f), TextAnchor.LowerCenter,
            MenuTheme.WithAlpha(MenuTheme.HudPanel, 0.9f));
        CreateBorder(namePlate);
        TMP_Text abilityName = CreateText(namePlate, "Ability name", string.Empty, 27, Ivory, FontStyles.Bold);
        Stretch(abilityName.rectTransform);
        abilityName.rectTransform.offsetMin = new Vector2(16f, 8f);
        abilityName.rectTransform.offsetMax = new Vector2(-16f, -8f);
        abilityName.margin = Vector4.zero;
        abilityName.enableAutoSizing = true;
        abilityName.fontSizeMin = 22f;
        abilityName.fontSizeMax = 27f;
        abilityName.textWrappingMode = TextWrappingModes.Normal;
        abilityName.overflowMode = TextOverflowModes.Overflow;
        abilityName.outlineWidth = 0.12f;
        abilityName.outlineColor = new Color32(8, 6, 10, 255);
        icon.transform.SetAsFirstSibling();
        fill.transform.SetAsLastSibling();
        keyLabel.transform.SetAsLastSibling();
        caption.transform.SetAsLastSibling();
        cooldown.transform.SetAsLastSibling();
        return new AbilityOverlay { Root = panel.gameObject, Fill = fill, KeyLabel = keyLabel,
            CaptionLabel = caption, AbilityNameLabel = abilityName, CooldownLabel = cooldown,
            Icon = icon, Key = key, Duration = 1f };
    }

    private void BindAbilityOverlays()
    {
        foreach (AttackJoystick joystick in FindObjectsByType<AttackJoystick>(FindObjectsSortMode.None))
        {
            bool ultimate = joystick.name.ToLowerInvariant().Contains("ultimate");
            AbilityOverlay overlay = CreateAbilityOverlay(joystick.transform as RectTransform,
                ultimate ? "E" : "Q", ultimate ? 15f : 3f);
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

        Sprite frame = HudAbilitySprite();
        if (frame != null)
        {
            Image decoration = CreateImage(rect, "Ability frame", Color.white);
            decoration.sprite = frame;
            decoration.type = Image.Type.Simple;
            decoration.preserveAspect = true;
            decoration.raycastTarget = false;
            Stretch(decoration.rectTransform);
        }

        RawImage icon = CreateRawImage(rect, "Ability icon", Color.white);
        Stretch(icon.rectTransform);
        icon.rectTransform.offsetMin = new Vector2(24f, 24f);
        icon.rectTransform.offsetMax = new Vector2(-24f, -24f);
        icon.raycastTarget = false;

        Image fill = CreateImage(rect, "Cooldown fill", new Color(0.04f, 0.025f, 0.05f, 0.72f));
        Stretch(fill.rectTransform);
        fill.rectTransform.offsetMin = new Vector2(18f, 18f);
        fill.rectTransform.offsetMax = new Vector2(-18f, -18f);
        fill.sprite = RuntimeCircleSprite();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = 2;
        fill.fillClockwise = false;
        fill.raycastTarget = false;

        TMP_Text keyLabel = CreateText(rect, "Key", key, 34, Ivory, FontStyles.Bold);
        Anchor(keyLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(86f, 46f));
        ConfigureEssentialLabel(keyLabel, 34f);
        TMP_Text cooldown = CreateText(rect, "Cooldown seconds", string.Empty, 21, Ivory, FontStyles.Bold);
        Anchor(cooldown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -27f), new Vector2(92f, 28f));
        ConfigureEssentialLabel(cooldown, 21f);
        TMP_Text caption = CreateText(rect, "Ability type", string.Empty, 19, Gold, FontStyles.Bold);
        Anchor(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(128f, 28f));
        caption.margin = Vector4.zero;
        icon.transform.SetAsFirstSibling();
        fill.transform.SetAsLastSibling();
        keyLabel.transform.SetAsLastSibling();
        cooldown.transform.SetAsLastSibling();
        caption.transform.SetAsLastSibling();

        return new AbilityOverlay { Root = root, Fill = fill, KeyLabel = keyLabel,
            CaptionLabel = caption, CooldownLabel = cooldown, Icon = icon, Duration = duration, Key = key };
    }

    private void Update()
    {
        if (player == null)
        {
            enabled = false;
            return;
        }

        string characterName = CharacterCatalog.NameOf(player.SelectedCharacterIndex).ToUpperInvariant();
        string playerAlias = player.PlayerDisplayName;
        nameLabel.text = training != null
            ? $"{GameLocalization.Choose("TÚ", "YOU")} · {characterName}"
            : string.IsNullOrWhiteSpace(playerAlias) ||
              string.Equals(playerAlias, "Player", System.StringComparison.OrdinalIgnoreCase)
                ? characterName
                : $"{playerAlias.ToUpperInvariant()} · {characterName}";
        // El alias queda integrado en el encabezado principal. Así nunca hay
        // una barra verde anónima ni una segunda línea frágil en resoluciones bajas.
        nameLabel.gameObject.SetActive(true);
        nameLabel.transform.SetAsLastSibling();
        float health = Mathf.Clamp01(player.CurrentHealth / (float)Mathf.Max(1, player.HealthMaximum));
        healthLabel.text = string.Empty;
        healthLabel.gameObject.SetActive(false);
        healthFill.rectTransform.anchorMax = new Vector2(health, 1f);
        healthFill.color = health > 0.35f ? new Color(0.27f, 0.72f, 0.39f) : new Color(0.78f, 0.22f, 0.22f);
        UpdateStatusBars();

        string combatStatus = player.CombatStatusText;
        teamLabel.text = string.IsNullOrWhiteSpace(combatStatus)
            ? player.TeamStatusText
            : $"{player.TeamStatusText} · {combatStatus}";
        teamLabel.color = player.TeamDisplayColor;
        statusLabel.text = player.NetworkStatusText;
        statusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusLabel.text));
        string arenaStatus = ArenaPowerUpManager.Instance != null
            ? ArenaPowerUpManager.Instance.StatusText
            : string.Empty;
        arenaStatusLabel.text = arenaStatus;
        arenaStatusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(arenaStatus));
        UpdateBudIndicator();

        UpdateAbility(basicOverlay, player.BasicCooldownRemaining, player.BasicCooldownDuration,
            player.BasicAbilityName, player.BasicAbilityIcon);
        UpdateAbility(ultimateOverlay, player.UltimateCooldownRemaining, player.UltimateCooldownDuration,
            player.UltimateAbilityName, player.UltimateAbilityIcon);

        confirmationWarning.text = player.ExitWarningText;
        confirmationPanel.SetActive(player.ExitConfirmationVisible);

        string result = player.MatchResultText;
        TrainingRunController run = training != null ? training.Run : null;
        bool towerVisible = run != null && run.OverlayVisible;
        bool resultVisible = run == null && !string.IsNullOrEmpty(result);
        resultPanel.SetActive(resultVisible);
        if (resultVisible) resultPanel.transform.SetAsLastSibling();
        resultLabel.text = result;
        blindOverlay.SetActive(player.IsBlinded);

        UpdateTower(run, towerVisible);

        bool presentationSuppressed = resultVisible || towerVisible;
        if (resultPresentationSuppressed != presentationSuppressed)
            resultPresentationSuppressed = presentationSuppressed;
        if (run != null)
            ArenaPowerUpManager.Instance?.SetSimulationPaused(towerVisible);
        else
            ArenaPowerUpManager.Instance?.SetPresentationSuppressed(resultVisible);

        UpdateInputHint(presentationSuppressed);

        if (lockButtonLabel != null)
        {
            CombatTargetingController targeting = player.Targeting;
            lockButtonLabel.text = targeting != null && targeting.HasTarget
                ? GameLocalization.Choose($"SOLTAR · {targeting.TargetName.ToUpperInvariant()}",
                    $"RELEASE · {targeting.TargetName.ToUpperInvariant()}")
                : usesTouchControls
                    ? GameLocalization.Choose("FIJAR RIVAL", "LOCK TARGET")
                    : GameLocalization.Choose("FIJAR RIVAL · TAB", "LOCK TARGET · TAB");
            Image lockImage = lockButton.targetGraphic as Image;
            if (lockImage != null)
            {
                Sprite frame = HudButtonSprite(targeting != null && targeting.HasTarget);
                if (frame != null) lockImage.sprite = frame;
                lockImage.type = frame != null ? Image.Type.Sliced : Image.Type.Simple;
                lockImage.color = frame != null
                    ? (targeting != null && targeting.HasTarget
                        ? MenuTheme.WithAlpha(MenuTheme.HudMossBright, 0.96f)
                        : Color.white)
                    : (targeting != null && targeting.HasTarget
                        ? MenuTheme.WithAlpha(MenuTheme.HudMoss, 0.96f)
                        : Plum);
            }
        }

        if (trainingPanel != null)
        {
            PlayerController opponent = training != null ? training.Opponent : null;
            bool visible = opponent != null;
            trainingPanel.SetActive(visible);
            if (visible)
            {
                opponentNameLabel.text = $"{GameLocalization.Choose("RIVAL", "RIVAL")} · " +
                    CharacterCatalog.NameOf(opponent.SelectedCharacterIndex).ToUpperInvariant();
                float opponentHealth = Mathf.Clamp01(opponent.CurrentHealth / (float)Mathf.Max(1, opponent.HealthMaximum));
                opponentHealthLabel.text = string.Empty;
                opponentHealthLabel.gameObject.SetActive(false);
                opponentHealthFill.rectTransform.anchorMax = new Vector2(opponentHealth, 1f);
            }
        }
    }

    private void UpdateStatusBars()
    {
        if (buffRow == null) return;
        float haste = player.PickupHasteRemaining;
        float power = player.PickupPowerRemaining;
        int shield = player.CurrentShield;
        UpdateStatusBar(hasteBar, haste > 0f, haste / player.PickupHasteDuration,
            GameLocalization.Choose($"CELERIDAD · {haste:0.0}s", $"HASTE · {haste:0.0}s"));
        UpdateStatusBar(powerBar, power > 0f, power / player.PickupPowerDuration,
            GameLocalization.Choose($"PODER · {power:0.0}s", $"POWER · {power:0.0}s"));
        UpdateStatusBar(shieldBar, shield > 0, shield / (float)Mathf.Max(1, player.HealthMaximum),
            GameLocalization.Choose($"BARRERA · {shield}", $"SHIELD · {shield}"));
        buffRow.SetActive(haste > 0f || power > 0f || shield > 0);
    }

    private static void UpdateStatusBar(StatusBar bar, bool visible, float ratio, string label)
    {
        if (bar?.Root == null) return;
        bar.Root.SetActive(visible);
        if (!visible) return;
        bar.Fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        bar.Label.text = label;
    }

    private void UpdateTower(TrainingRunController run, bool visible)
    {
        if (towerPanel == null) return;
        towerPanel.SetActive(visible);
        if (!visible || run == null) return;
        towerPanel.transform.SetAsLastSibling();

        int shownRival = run.Phase == TrainingRunPhase.RoundWon && run.NextOpponentIndex >= 0
            ? run.NextOpponentIndex
            : run.CurrentOpponentIndex;
        string rivalName = shownRival >= 0 ? CharacterCatalog.NameOf(shownRival).ToUpperInvariant() : string.Empty;
        int shownNumber = run.Phase == TrainingRunPhase.RoundWon
            ? Mathf.Min(run.TotalRivals, run.CurrentRivalNumber + 1)
            : run.CurrentRivalNumber;

        switch (run.Phase)
        {
            case TrainingRunPhase.Preview:
                towerTitle.text = GameLocalization.Choose("ESCALERA DE LA PODREDUMBRE", "BLIGHT LADDER");
                towerSubtitle.text = GameLocalization.Choose(
                    $"RIVAL {shownNumber} DE {run.TotalRivals} · {rivalName}",
                    $"RIVAL {shownNumber} OF {run.TotalRivals} · {rivalName}");
                towerPrimaryLabel.text = GameLocalization.Choose("COMENZAR", "BEGIN");
                break;
            case TrainingRunPhase.RoundWon:
                towerTitle.text = GameLocalization.Choose("VICTORIA", "VICTORY");
                towerSubtitle.text = GameLocalization.Choose(
                    $"SIGUIENTE: {rivalName} · RIVAL {shownNumber} DE {run.TotalRivals}",
                    $"NEXT: {rivalName} · RIVAL {shownNumber} OF {run.TotalRivals}");
                towerPrimaryLabel.text = GameLocalization.Choose("CONTINUAR", "CONTINUE");
                break;
            case TrainingRunPhase.RoundLost:
                towerTitle.text = GameLocalization.Choose("DERROTA", "DEFEAT");
                towerSubtitle.text = GameLocalization.Choose(
                    $"REINTENTA EL RIVAL {shownNumber}: {rivalName}",
                    $"RETRY RIVAL {shownNumber}: {rivalName}");
                towerPrimaryLabel.text = GameLocalization.Choose("REINTENTAR", "RETRY");
                break;
            default:
                towerTitle.text = GameLocalization.Choose("TORRE SUPERADA", "LADDER COMPLETE");
                towerSubtitle.text = GameLocalization.Choose(
                    "VENCISTE A LOS CINCO GUARDIANES", "YOU DEFEATED ALL FIVE GUARDIANS");
                towerPrimaryLabel.text = GameLocalization.Choose("REPETIR TORRE", "REPLAY LADDER");
                break;
        }

        System.Text.StringBuilder progress = new System.Text.StringBuilder();
        for (int i = 0; i < run.OpponentOrder.Count; i++)
        {
            bool defeatedCurrent = run.Phase == TrainingRunPhase.RoundWon && i == run.CurrentIndex;
            bool defeated = i < run.CurrentIndex || defeatedCurrent || run.Phase == TrainingRunPhase.Complete;
            string state = defeated
                ? GameLocalization.Choose("DERROTADO", "DEFEATED")
                : i == run.CurrentIndex ? GameLocalization.Choose("RIVAL ACTUAL", "CURRENT RIVAL") : "—";
            if (i < towerPortraits.Length)
            {
                towerPortraits[i].texture = CharacterCatalog.LoadPortrait(run.OpponentOrder[i]);
                towerPortraits[i].color = defeated
                    ? new Color(0.34f, 0.30f, 0.34f, 0.58f)
                    : Color.white;
                if (towerPortraitFrames[i] != null)
                    towerPortraitFrames[i].color = i == run.CurrentIndex && !defeated
                        ? new Color(0.34f, 0.22f, 0.08f, 0.98f)
                        : new Color(0.025f, 0.018f, 0.03f, 0.94f);
            }
            progress.Append(i + 1).Append(" · ")
                .Append(CharacterCatalog.NameOf(run.OpponentOrder[i]).ToUpperInvariant())
                .Append("   ").Append(state);
            if (i < run.OpponentOrder.Count - 1) progress.AppendLine();
        }
        towerProgress.text = progress.ToString();
    }

    private void UpdateInputHint(bool resultVisible)
    {
        if (inputHintGroup == null) return;
        bool combatStarted = training != null
            ? training.DuelActive
            : player.Object == null || (player.MatchReady && OnlineMatchState.CanPlay);
        if (combatStarted && inputHintStartedAt < 0f) inputHintStartedAt = Time.unscaledTime;

        float alpha = 0f;
        if (!resultVisible && inputHintStartedAt >= 0f)
        {
            float elapsed = Time.unscaledTime - inputHintStartedAt;
            alpha = elapsed <= 6.5f ? 1f : 1f - Mathf.Clamp01((elapsed - 6.5f) / 0.75f);
        }
        inputHintGroup.alpha = alpha;
        inputHintGroup.blocksRaycasts = false;
        inputHintGroup.interactable = false;
    }

    private void UpdateBudIndicator()
    {
        ArenaPowerUpManager manager = ArenaPowerUpManager.Instance;
        Camera camera = Camera.main;
        RectTransform canvasRect = hudRoot != null ? hudRoot.transform as RectTransform : null;
        if (manager == null || camera == null || canvasRect == null ||
            !manager.TryGetActiveBud(out Vector3 worldPosition, out int hits))
        {
            if (budIndicator != null) budIndicator.gameObject.SetActive(false);
            return;
        }

        Vector3 screen = camera.WorldToScreenPoint(worldPosition);
        if (screen.z < 0f)
        {
            screen.x = Screen.width - screen.x;
            screen.y = Screen.height - screen.y;
        }
        bool visible = screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width &&
                       screen.y >= 0f && screen.y <= Screen.height;
        screen.x = Mathf.Clamp(screen.x, 185f, Screen.width - 185f);
        screen.y = Mathf.Clamp(screen.y, 120f, Screen.height - 120f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        budIndicator.anchoredPosition = local;
        budIndicator.gameObject.SetActive(true);

        string arrow = visible ? string.Empty : screen.x <= 190f ? "<  " : screen.x >= Screen.width - 190f ? "  >" : "  ^";
        budIndicatorLabel.text = GameLocalization.Choose(
            $"{arrow}CAPULLO · {hits} GOLPES", $"{arrow}BUD · {hits} HITS");
    }

    private static void UpdateAbility(AbilityOverlay overlay, float remaining, float duration,
        string abilityName, Texture2D icon)
    {
        if (overlay == null) return;
        overlay.Duration = Mathf.Max(0.05f, duration);
        overlay.Fill.fillAmount = Mathf.Clamp01(remaining / overlay.Duration);
        string caption = overlay.Key == "Q"
            ? GameLocalization.Choose("BÁSICA", "BASIC")
            : GameLocalization.Choose("ULTI", "ULT");
        overlay.KeyLabel.text = overlay.Key;
        overlay.CaptionLabel.text = caption;
        if (overlay.AbilityNameLabel != null)
            overlay.AbilityNameLabel.text = string.IsNullOrWhiteSpace(abilityName) ? string.Empty : abilityName.ToUpperInvariant();
        if (overlay.Icon != null)
        {
            overlay.Icon.texture = icon;
            overlay.Icon.gameObject.SetActive(icon != null);
        }
        overlay.CooldownLabel.text = remaining > 0f ? $"{remaining:0.0}s" : string.Empty;
    }

    private void OnDestroy()
    {
        ArenaPowerUpManager.Instance?.SetPresentationSuppressed(false);
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
        Sprite frame = HudButtonSprite(false);
        if (frame != null)
        {
            button.targetGraphic.GetComponent<Image>().sprite = frame;
            button.targetGraphic.GetComponent<Image>().type = Image.Type.Sliced;
            button.targetGraphic.color = Color.white;
        }
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.78f);
        colors.pressedColor = new Color(0.78f, 0.66f, 0.46f);
        button.colors = colors;
        if (frame == null) CreateBorder(rect);

        TMP_Text text = CreateText(rect, "Label", caption, 26, Ivory, FontStyles.Bold);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(34f, 14f);
        text.rectTransform.offsetMax = new Vector2(-34f, -14f);
        text.raycastTarget = false;
        return button;
    }

    private static Sprite HudButtonSprite(bool selected)
    {
        return HudButtonFrameSprite();
    }

    private static Sprite HudHealthSprite()
    {
        return LoadHudSprite("UI/HUD/HealthFrame", ref healthFrameSprite);
    }

    private static Sprite HudAbilitySprite()
    {
        return LoadHudSprite("UI/HUD/AbilityFrame", ref abilityFrameSprite);
    }

    private static Sprite HudModalSprite()
    {
        return LoadHudSprite("UI/HUD/ModalFrame", ref modalFrameSprite);
    }

    private static Sprite HudButtonFrameSprite()
    {
        return LoadHudSprite("UI/HUD/HudButtonFrame", ref hudButtonFrameSprite);
    }

    private static Sprite HudControlHintSprite()
    {
        return LoadHudSprite("UI/HUD/ControlHintFrame", ref controlHintFrameSprite);
    }

    private static Sprite LoadHudSprite(string resourcePath, ref Sprite cache)
    {
        if (cache != null) return cache;
        cache = Resources.Load<Sprite>(resourcePath);
        return cache;
    }

    private static void AddHealthFrame(RectTransform healthTrack)
    {
        Sprite frame = HudHealthSprite();
        if (frame == null) return;
        Image image = CreateImage(healthTrack, "Ornate health frame", Color.white);
        image.sprite = frame;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        Stretch(image.rectTransform);
        image.rectTransform.offsetMin = new Vector2(-24f, -18f);
        image.rectTransform.offsetMax = new Vector2(24f, 18f);
        // El centro del marco contiene sombreado decorativo. Si queda como
        // último sibling tapa el nombre/porcentaje; detrás conserva las
        // espinas visibles sin ocultar la información esencial.
        image.transform.SetAsFirstSibling();
    }

    private static void ApplyModalFrame(RectTransform panel)
    {
        Image image = panel.GetComponent<Image>();
        Sprite frame = HudModalSprite();
        if (frame == null)
        {
            CreateBorder(panel);
            return;
        }
        image.sprite = frame;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
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
        // Cinzel no incluye el glifo U+2026 en el atlas generado. Unity ya
        // degradaba Ellipsis a Truncate y emitía un warning en cada rebuild.
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(20f, size * 0.78f);
        text.fontSizeMax = size;
        text.margin = new Vector4(10f, 5f, 10f, 5f);
        if ((style & FontStyles.Bold) != 0) MenuTheme.ApplyDisplayFont(text);
        else MenuTheme.ApplyBodyFont(text);
        return text;
    }

    private static void ConfigureEssentialLabel(TMP_Text text, float fontSize)
    {
        if (text == null) return;
        text.margin = Vector4.zero;
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.overflowMode = TextOverflowModes.Overflow;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static RawImage CreateRawImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RawImage image = go.GetComponent<RawImage>();
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
            TextAnchor.LowerRight => new Vector2(1f, 0f),
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

    private static Sprite RuntimeCircleSprite()
    {
        if (runtimeCircleSprite != null) return runtimeCircleSprite;
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime cooldown circle",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float alpha = Mathf.Clamp01(radius - Vector2.Distance(new Vector2(x, y), center));
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        runtimeCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), size);
        runtimeCircleSprite.name = "Runtime cooldown circle";
        return runtimeCircleSprite;
    }
}
