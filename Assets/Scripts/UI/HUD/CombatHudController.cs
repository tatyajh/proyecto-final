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
    private static readonly Color Panel = MenuTheme.WithAlpha(MenuTheme.HudPanel, 0.86f);
    private static readonly Color Gold = MenuTheme.HudMossBright;
    private static readonly Color Ivory = MenuTheme.HudIvory;
    private static readonly Color Plum = MenuTheme.WithAlpha(MenuTheme.HudAshDark, 0.94f);

    private PlayerController player;
    private GameObject hudRoot;
    private TMP_Text nameLabel;
    private TMP_Text healthLabel;
    private Image healthFill;
    private TMP_Text teamLabel;
    private TMP_Text statusLabel;
    private TMP_Text arenaStatusLabel;
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
        nameLabel = CreateText(identity, "Name", string.Empty, 36, Gold, FontStyles.Bold);
        Anchor(nameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(440f, 46f));

        RectTransform healthTrack = CreatePanel(identity, "Health", new Vector2(0f, -62f),
            new Vector2(400f, 32f), TextAnchor.UpperCenter, new Color(0.04f, 0.025f, 0.035f, 0.95f));
        healthFill = CreateImage(healthTrack, "Fill", new Color(0.27f, 0.72f, 0.39f, 1f));
        healthFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        healthFill.rectTransform.anchorMax = new Vector2(1f, 1f);
        healthFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        healthFill.rectTransform.offsetMin = new Vector2(3f, 3f);
        healthFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
        healthLabel = CreateText(healthTrack, "Health value", string.Empty, 30, Ivory, FontStyles.Bold);
        Stretch(healthLabel.rectTransform);
        AddHealthFrame(healthTrack);
        nameLabel.transform.SetAsLastSibling();

        teamLabel = CreateText(root, "Team", string.Empty, 22, Ivory, FontStyles.Bold);
        Anchor(teamLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(680f, 32f));

        statusLabel = CreateText(root, "Network status", string.Empty, 22, Ivory, FontStyles.Normal);
        Anchor(statusLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -188f), new Vector2(760f, 64f));

        arenaStatusLabel = CreateText(root, "Arena status", string.Empty, 26,
            new Color(0.92f, 0.42f, 0.48f), FontStyles.Bold);
        Anchor(arenaStatusLabel.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(0f, -252f), new Vector2(820f, 42f));

        BuildConfirmation(root);
        BuildResult(root);
        BuildTrainingControls(root);
        usesTouchControls = Application.isMobilePlatform ||
                            (Input.touchSupported && SystemInfo.deviceType == DeviceType.Handheld);
        if (usesTouchControls) BuildTargetLock(root);
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
            new Vector2(430f, 122f), TextAnchor.UpperRight, Panel);
        trainingPanel = panel.gameObject;
        CreateBorder(panel);
        opponentNameLabel = CreateText(panel, "Opponent name",
            GameLocalization.Choose("RIVAL · PREPARANDO", "RIVAL · PREPARING"), 34, Gold, FontStyles.Bold);
        Anchor(opponentNameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(400f, 46f));

        RectTransform healthTrack = CreatePanel(panel, "Opponent health", new Vector2(0f, -62f),
            new Vector2(370f, 32f), TextAnchor.UpperCenter, new Color(0.04f, 0.025f, 0.035f, 0.95f));
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

    private void BuildTargetLock(RectTransform root)
    {
        lockButton = CreateButton(root, "Target lock",
            GameLocalization.Choose("FIJAR RIVAL", "LOCK TARGET"),
            new Vector2(0f, 142f), new Vector2(320f, 72f), TextAnchor.LowerCenter);
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

        basicOverlay = CreateDesktopAbilityOverlay(root, "Q", new Vector2(-266f, 104f));
        ultimateOverlay = CreateDesktopAbilityOverlay(root, "E", new Vector2(-26f, 104f));
    }

    private AbilityOverlay CreateDesktopAbilityOverlay(RectTransform root, string key, Vector2 position)
    {
        RectTransform panel = CreatePanel(root, $"{key} ability", position,
            new Vector2(220f, 190f), TextAnchor.LowerRight, Plum);
        Sprite frame = HudAbilitySprite();
        Image background = panel.GetComponent<Image>();
        if (frame != null)
        {
            background.sprite = frame;
            background.color = Color.white;
            background.type = Image.Type.Simple;
            background.preserveAspect = true;
        }

        Image fill = CreateImage(panel, "Cooldown fill", new Color(0.04f, 0.025f, 0.05f, 0.72f));
        Stretch(fill.rectTransform);
        fill.rectTransform.offsetMin = new Vector2(24f, 22f);
        fill.rectTransform.offsetMax = new Vector2(-24f, -22f);
        fill.sprite = RuntimeSprite();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = 2;
        fill.fillClockwise = false;
        fill.raycastTarget = false;

        TMP_Text label = CreateText(panel, "Ability", key, 38, Ivory, FontStyles.Bold);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(22f, 22f);
        label.rectTransform.offsetMax = new Vector2(-22f, -22f);
        label.enableAutoSizing = false;
        label.raycastTarget = false;
        return new AbilityOverlay { Root = panel.gameObject, Fill = fill, Label = label, Key = key, Duration = 1f };
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

        Image fill = CreateImage(rect, "Cooldown fill", new Color(0.04f, 0.025f, 0.05f, 0.72f));
        Stretch(fill.rectTransform);
        fill.rectTransform.offsetMin = new Vector2(18f, 18f);
        fill.rectTransform.offsetMax = new Vector2(-18f, -18f);
        fill.sprite = RuntimeSprite();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = 2;
        fill.fillClockwise = false;
        fill.raycastTarget = false;

        TMP_Text label = CreateText(rect, "Ability", key, 26, Ivory, FontStyles.Bold);
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

        nameLabel.text = training != null
            ? $"{GameLocalization.Choose("TÚ", "YOU")} · {player.PlayerDisplayName.ToUpperInvariant()}"
            : player.PlayerDisplayName.ToUpperInvariant();
        float health = Mathf.Clamp01(player.CurrentHealth / (float)PlayerController.MaxHealth);
        healthLabel.text = $"{GameLocalization.Choose("VIDA", "HEALTH")}  {Mathf.CeilToInt(health * 100f)}%";
        healthFill.rectTransform.anchorMax = new Vector2(health, 1f);
        healthFill.color = health > 0.35f ? new Color(0.27f, 0.72f, 0.39f) : new Color(0.78f, 0.22f, 0.22f);

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

        UpdateAbility(basicOverlay, player.BasicCooldownRemaining, player.BasicCooldownDuration, player.BasicAbilityName);
        UpdateAbility(ultimateOverlay, player.UltimateCooldownRemaining, player.UltimateCooldownDuration, player.UltimateAbilityName);

        confirmationWarning.text = player.ExitWarningText;
        confirmationPanel.SetActive(player.ExitConfirmationVisible);

        string result = player.MatchResultText;
        bool resultVisible = !string.IsNullOrEmpty(result);
        resultPanel.SetActive(resultVisible);
        if (resultVisible) resultPanel.transform.SetAsLastSibling();
        resultLabel.text = result;
        blindOverlay.SetActive(player.IsBlinded);

        if (resultPresentationSuppressed != resultVisible)
        {
            resultPresentationSuppressed = resultVisible;
            ArenaPowerUpManager.Instance?.SetPresentationSuppressed(resultVisible);
        }

        UpdateInputHint(resultVisible);

        if (lockButtonLabel != null)
        {
            CombatTargetingController targeting = player.Targeting;
            lockButtonLabel.text = targeting != null && targeting.HasTarget
                ? GameLocalization.Choose($"SOLTAR · {targeting.TargetName.ToUpperInvariant()}",
                    $"RELEASE · {targeting.TargetName.ToUpperInvariant()}")
                : GameLocalization.Choose("FIJAR RIVAL", "LOCK TARGET");
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
                                         opponent.PlayerDisplayName.ToUpperInvariant();
                float opponentHealth = Mathf.Clamp01(opponent.CurrentHealth / (float)PlayerController.MaxHealth);
                opponentHealthLabel.text = $"{GameLocalization.Choose("VIDA", "HEALTH")}  {Mathf.CeilToInt(opponentHealth * 100f)}%";
                opponentHealthFill.rectTransform.anchorMax = new Vector2(opponentHealth, 1f);
            }
        }
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

    private static void UpdateAbility(AbilityOverlay overlay, float remaining, float duration, string abilityName)
    {
        if (overlay == null) return;
        overlay.Duration = Mathf.Max(0.05f, duration);
        overlay.Fill.fillAmount = Mathf.Clamp01(remaining / overlay.Duration);
        string caption = overlay.Key == "Q"
            ? GameLocalization.Choose("ATAQUE", "ATTACK")
            : GameLocalization.Choose("DEFINITIVA", "ULTIMATE");
        overlay.Label.text = remaining > 0f
            ? $"{overlay.Key}\n<size=55%>{remaining:0.0} s</size>"
            : $"{overlay.Key}\n<size=50%>{caption}</size>";
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

    private static Sprite RuntimeSprite()
    {
        return Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }
}
