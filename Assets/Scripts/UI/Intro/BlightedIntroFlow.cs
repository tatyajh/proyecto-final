using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Flujo de inicio completo en una sola escena: historia, camino, modo, nombre
/// y personaje. Las fases son CanvasGroup hermanos y se cruzan con fades, sin
/// cargar escenas entre medias — esa continuidad es la mitad del efecto.
///
/// No duplica lógica existente: el matchmaking sigue en LobbyManager, los modos
/// en MatchModeCatalog y la disponibilidad de personajes en CharacterCatalog.
/// </summary>
public sealed class BlightedIntroFlow : MonoBehaviour
{
    private enum Phase { Prologue, Path, Mode, Name, Character }

    private const float CrossFadeSeconds = 0.9f;
    private const int PreviewLayer = 30;

    /// <summary>
    /// Al volver de una partida no se repite el nombre ni el prólogo: se
    /// entra directo a elegir modo. Ver la historia entera cada vez que sales
    /// de la arena es lo que hacía que el flujo se sintiera pesado.
    /// </summary>
    public static bool ReturnDirectlyToMenu;

    [SerializeField] private LobbyManager lobbyManager;

    [Header("Depuración")]
    [Tooltip("Salta todo el menú y crea el personaje al instante, para probar " +
             "movimiento y combate sin pasar por el flujo ni esperar rivales.")]
    [SerializeField] private bool debugSkipMenu;
    [Tooltip("Escena a la que saltar en modo depuración. Vacío = quedarse aquí.")]
    [SerializeField] private string debugArenaScene = "OnlineArena";

    private Canvas canvas;
    private RectTransform progressRail;
    // Las transiciones (y sus tres defensas contra la condición de carrera)
    // viven en MenuFlowController; esta clase solo construye y reacciona.
    private readonly MenuFlowController<Phase> flow = new MenuFlowController<Phase>(CrossFadeSeconds);

    private PrologueSequence prologue;
    private SporeAtmosphere atmosphere;
    private TMP_InputField nameField;
    private TMP_Text statusLabel;
    private EtherealButton backButton;
    private CanvasGroup settingsOverlay;
    private MatchModeDefinition selectedMode = MatchModeCatalog.Default;
    private int selectedCharacter = 3;

    // Ajustes reconstruye estas opciones para reflejar qué idioma y calidad
    // están activos.
    private readonly List<EtherealButton> settingsLanguageButtons = new List<EtherealButton>();
    private readonly List<EtherealButton> settingsQualityButtons = new List<EtherealButton>();
    private readonly List<EtherealButton> modeButtons = new List<EtherealButton>();
    private readonly List<MatchModeDefinition> visibleModes = new List<MatchModeDefinition>();
    private readonly List<EtherealButton> progressButtons = new List<EtherealButton>();
    private int highestProgressIndex;
    private bool settingsVisible;

    // Carrusel de una sola carta: usa retrato con alfa o, cuando existe, el
    // modelo real girando en una RenderTexture transparente.
    private RawImage previewImage;
    private RenderTexture previewTexture;
    private Transform previewRoot;
    private GameObject previewModel;
    private TMP_Text characterName;
    private TMP_Text characterSubtitle;
    private TMP_Text characterCounter;

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= HandleLanguageChanged;
    }

    /// <summary>
    /// El único texto que no es un par estático (depende de si el personaje
    /// tiene modelo, no de una traducción fija) y por eso no puede colgar de
    /// LocalizedText. El resto de la UI ya se actualiza sola vía ese componente.
    /// </summary>
    private void HandleLanguageChanged()
    {
        RefreshCharacterSubtitle();
    }

    private void Start()
    {
        if (lobbyManager == null) lobbyManager = FindFirstObjectByType<LobbyManager>();

        selectedCharacter = CharacterCatalog.Clamp(PlayerPrefs.GetInt("SelectedCharacterIndex", 3));

        // Atajo de desarrollo: ni canvas, ni prólogo, ni matchmaking.
        if (debugSkipMenu)
        {
            SkipStraightToGameplay();
            return;
        }

        BuildCanvas();
        BuildProgressRail();
        BuildAtmosphere();
        BuildProloguePhase();
        BuildPathPhase();
        BuildModePhase();
        BuildNamePhase();
        BuildCharacterPhase();
        BuildSettingsOverlay();
        BuildBackButton();

        flow.TransitionStarted += OnTransitionStarted;
        flow.PhaseChanged += OnPhaseChanged;
        flow.PhaseShown += OnPhaseShown;

        flow.HideAll();
        UITween.SnapHidden(settingsOverlay);

        // SnapTo muestra la fase inicial al instante, sin fundido de entrada.
        flow.SnapTo(ReturnDirectlyToMenu ? Phase.Mode : Phase.Prologue);
        ReturnDirectlyToMenu = false;
    }

    /// <summary>
    /// Va directo al juego saltándose todas las fases. Si hay arena configurada
    /// la carga; si no, crea el personaje aquí mismo con CharacterSpawner.
    /// </summary>
    private void SkipStraightToGameplay()
    {
        Debug.LogWarning("[BlightedIntroFlow] debugSkipMenu activo: se omite el menú. " +
                         "Desactívalo en el inspector antes de compilar la entrega.");

        // Modo local: sin esto PlayerController se apaga creyendo estar en red.
        PlayModeContext.UseTraining();
        OnlineMatchState.Reset();

        if (!string.IsNullOrWhiteSpace(debugArenaScene) &&
            SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/Multiplayer/{debugArenaScene}.unity") >= 0)
        {
            SceneManager.LoadScene(debugArenaScene);
            return;
        }

        CharacterSpawner spawner = FindFirstObjectByType<CharacterSpawner>();
        if (spawner == null)
        {
            GameObject host = new GameObject("Debug Character Spawner");
            spawner = host.AddComponent<CharacterSpawner>();
        }
        spawner.SpawnSelectedCharacter(selectedCharacter);
    }

    private void Update()
    {
        if (flow.Current != Phase.Character) return;

        if (previewModel != null)
            previewModel.transform.Rotate(0f, 14f * Time.unscaledDeltaTime, 0f, Space.World);

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) PreviousCharacter();
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) NextCharacter();
    }

    // ------------------------------------------------------------- estructura

    private void BuildCanvas()
    {
        GameObject canvasHost = new GameObject("Blighted Intro Canvas", typeof(RectTransform));
        canvasHost.transform.SetParent(transform, false);
        canvas = canvasHost.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasHost.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        // 1 (ajustar por alto) en vez de 0.5: en una ventana más ancha que
        // 16:9 (el Game view en modo "Free Aspect" suele quedar así), un
        // valor intermedio encogía TODO el texto y empujaba los elementos de
        // esquina fuera del borde superior. Ajustando solo por alto, el
        // tamaño de letra y el margen de las esquinas se mantienen estables
        // sin importar cuán ancha quede la ventana.
        scaler.matchWidthOrHeight = 1f;
        canvasHost.AddComponent<GraphicRaycaster>();

        // Sin fondo opaco de UI: la cámara ya limpia a VoidBlack (ver
        // Camera.backgroundColor en la escena), y una imagen a pantalla
        // completa aquí delante tapaba por completo las esporas 3D detrás.

        // Marco botánico persistente: reemplaza la sensación de rectángulos
        // flotantes por el lenguaje ornamental del concepto aprobado. Se crea
        // desde Texture2D para no depender del modo de importación Sprite.
        // El marco rectangular entregado por arte es el principal. El anterior
        // queda solo como respaldo para proyectos que aún no tengan el asset.
        Texture2D frameTexture = Resources.Load<Texture2D>("UI/RectangularFrame");
        if (frameTexture != null)
        {
            Image frame = NewImage("Botanical Menu Frame", canvas.transform,
                MenuTheme.WithAlpha(MenuTheme.MarfilEnvejecido, 0.58f));
            frame.sprite = Sprite.Create(frameTexture,
                new Rect(0f, 0f, frameTexture.width, frameTexture.height),
                new Vector2(0.5f, 0.5f), 100f);
            frame.preserveAspect = false;
            frame.raycastTarget = false;
            Stretch(frame.rectTransform);
            frame.rectTransform.offsetMin = new Vector2(16f, 16f);
            frame.rectTransform.offsetMax = new Vector2(-16f, -16f);
        }

        // Anclado arriba, no al centro: con el anclaje por defecto del helper
        // quedaba a media pantalla y se solapaba con el resto de las fases.
        //
        // El wordmark real (arte de Abyss Bloom Studios) sustituye al texto
        // plano cuando está disponible; si el asset no llegó a este build,
        // el texto sigue siendo un respaldo funcional.
        Sprite titleArt = Resources.Load<Sprite>("UI/BlightedBlossomsTitle");
        if (titleArt != null)
        {
            Image headerLogo = NewImage("Game Title", canvas.transform, Color.white);
            headerLogo.sprite = titleArt;
            headerLogo.preserveAspect = true;
            headerLogo.raycastTarget = false;
            // El logotipo ocupa su propia franja. La navegación comienza por
            // debajo: nunca comparten altura, incluso en Game View estrecho.
            Center(headerLogo.rectTransform, new Vector2(0f, -20f), new Vector2(360f, 112f));
            AnchorToTop(headerLogo.rectTransform);
        }
        else
        {
            TMP_Text headerTitle = NewText(canvas.transform, "BLIGHTED BLOSSOMS", new Vector2(0f, -30f), new Vector2(700f, 44f), 30f, MenuTheme.OroMarchito, true);
            AnchorToTop(headerTitle.rectTransform);
        }
    }

    private void BuildAtmosphere()
    {
        atmosphere = SporeAtmosphere.Create(transform, Camera.main);
    }

    /// <summary>Índice superior sin marco, con cinco columnas de igual ancho.</summary>
    private void BuildProgressRail()
    {
        GameObject rail = new GameObject("Flow Progress", typeof(RectTransform));
        rail.transform.SetParent(canvas.transform, false);
        progressRail = rail.GetComponent<RectTransform>();
        Center(progressRail, new Vector2(0f, 300f), new Vector2(1600f, 94f));

        const float spacing = 310f;
        const float columnWidth = 300f;
        const float fontSize = 36f;
        float firstX = -spacing * 2f;

        string[] spanish = { "HISTORIA", "MODO DE JUEGO", "NOMBRE", "PERSONAJE", "AJUSTES" };
        string[] english = { "STORY", "GAME MODE", "NAME", "CHARACTER", "SETTINGS" };
        for (int i = 0; i < spanish.Length; i++)
        {
            int captured = i;
            EtherealButton step = EtherealButton.CreateLocalized(rail.transform, spanish[i], english[i], fontSize,
                new Vector2(firstX + i * spacing, 0f), new Vector2(columnWidth, 72f), MenuTheme.BoneDim,
                () => NavigateFromProgress(captured));
            progressButtons.Add(step);
        }
    }

    private void RefreshProgressRail(Phase phase)
    {
        int active = settingsVisible ? 4 : ProgressIndex(phase);
        if (!settingsVisible) highestProgressIndex = Mathf.Max(highestProgressIndex, active);

        for (int i = 0; i < progressButtons.Count; i++)
        {
            EtherealButton step = progressButtons[i];
            bool settings = i == 4;
            bool available = settings || i <= highestProgressIndex;
            step.SetRestColor(i < active && !settings
                ? MenuTheme.MarfilEnvejecido
                : MenuTheme.WithAlpha(MenuTheme.BoneDim, available ? 0.82f : 0.44f));
            step.SetInteractable(available);
            step.SetSelected(i == active);
        }
    }

    private static int ProgressIndex(Phase phase)
    {
        if (phase == Phase.Prologue) return 0;
        if (phase == Phase.Path || phase == Phase.Mode) return 1;
        if (phase == Phase.Name) return 2;
        return 3;
    }

    private void NavigateFromProgress(int index)
    {
        if (index == 4)
        {
            ShowSettingsOverlay();
            return;
        }
        if (index > highestProgressIndex) return;
        if (settingsVisible) HideSettingsOverlay();

        switch (index)
        {
            case 0: GoTo(Phase.Prologue); break;
            case 1: GoTo(Phase.Path); break;
            case 2: GoTo(Phase.Name); break;
            case 3: GoTo(Phase.Character); break;
        }
    }

    private CanvasGroup NewPhase(Phase phase, string name)
    {
        GameObject host = new GameObject(name, typeof(RectTransform));
        host.transform.SetParent(canvas.transform, false);
        Stretch(host.GetComponent<RectTransform>());
        CanvasGroup group = host.AddComponent<CanvasGroup>();
        flow.Register(phase, group);
        return group;
    }

    // ------------------------------------------------------ fase 1: camino

    private void BuildPathPhase()
    {
        CanvasGroup group = NewPhase(Phase.Path, "1 - Path");

        NewText(group.transform, "ELIGE TU CAMINO", "CHOOSE YOUR PATH",
            new Vector2(0f, 82f), new Vector2(800f, 64f), 44f, MenuTheme.MarfilEnvejecido, true);

        EtherealButton campaign = EtherealButton.CreateLocalized(group.transform,
            "CAMPAÑA · PRÓXIMAMENTE", "CAMPAIGN · COMING SOON", 32f,
            new Vector2(-240f, -35f), new Vector2(430f, 96f), MenuTheme.BoneDim, () => { }, true);
        campaign.SetInteractable(false);
        EtherealButton.CreateLocalized(group.transform, "MULTIJUGADOR", "MULTIPLAYER", 34f,
            new Vector2(240f, -35f), new Vector2(430f, 96f), MenuTheme.MarfilEnvejecido,
            () => GoTo(Phase.Mode), true);

        // Logo del estudio en la primera pantalla, como en el boceto del
        // paquete de specs. Solo aquí: repetirlo en cada fase lo devaluaría.
        Sprite studioLogo = Resources.Load<Sprite>("UI/AbyssBloomLogo");
        if (studioLogo != null)
        {
            Image logo = NewImage("Abyss Bloom Logo", group.transform, Color.white);
            logo.sprite = studioLogo;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            Center(logo.rectTransform, new Vector2(0f, -250f), new Vector2(240f, 190f));
        }
    }

    /// <summary>El idioma activo se marca en dorado; el resto en blanco espectral.</summary>
    private void RefreshAllLanguageButtons()
    {
        HighlightLanguageButtons(settingsLanguageButtons);
    }

    private static void HighlightLanguageButtons(List<EtherealButton> buttons)
    {
        if (buttons.Count < 2) return;
        bool spanish = GameLocalization.IsSpanish;
        buttons[0].SetRestColor(spanish ? MenuTheme.OroMarchito : MenuTheme.SpectralWhite);
        buttons[1].SetRestColor(spanish ? MenuTheme.SpectralWhite : MenuTheme.OroMarchito);
    }

    // ---------------------------------------------------------- fase 1: nombre

    private void BuildNamePhase()
    {
        CanvasGroup group = NewPhase(Phase.Name, "3 - Name");

        // El campo empieza siempre vacío: prerrellenarlo con el nombre de una
        // sesión anterior hacía que el juego pareciera "recordar en secreto"
        // en vez de preguntar. Se sigue guardando en PlayerPrefs (para la
        // insignia de la esquina), pero cada partida vuelve a pedirlo.
        NewText(group.transform, "Ingresa tu nombre", "Enter your name",
            new Vector2(0f, 90f), new Vector2(900f, 64f), 36f, MenuTheme.MarfilEnvejecido, false);

        // Input sin fondo: solo el texto y una línea que respira al enfocarse.
        // Pero SÍ necesita un Graphic con raycastTarget: sin uno, el área del
        // campo no es clicable — nada capta el puntero, así que un clic
        // dentro del campo (o en cualquier parte vacía de la pantalla, algo
        // que el jugador hace de forma natural) deselecciona el input vía
        // EventSystem sin ninguna forma de reenfocarlo con el mouse. Eso es
        // lo que hacía que "no dejara escribir": funcionaba una vez al
        // entrar a la fase (ActivateInputField automático) y se perdía en
        // cuanto se tocaba la pantalla.
        GameObject fieldHost = new GameObject("Name Field", typeof(RectTransform));
        fieldHost.transform.SetParent(group.transform, false);
        RectTransform fieldRect = fieldHost.GetComponent<RectTransform>();
        Center(fieldRect, Vector2.zero, new Vector2(640f, 76f));

        Image fieldHitArea = fieldHost.AddComponent<Image>();
        fieldHitArea.color = new Color(1f, 1f, 1f, 0.001f);
        fieldHitArea.raycastTarget = true;

        TextMeshProUGUI fieldText = NewText(fieldHost.transform, string.Empty, Vector2.zero,
            new Vector2(620f, 66f), 44f, MenuTheme.SpectralWhite, false);
        fieldText.alignment = TextAlignmentOptions.Center;

        nameField = fieldHost.AddComponent<TMP_InputField>();
        nameField.targetGraphic = fieldHitArea;
        nameField.textComponent = fieldText;
        nameField.textViewport = fieldRect;
        nameField.characterLimit = 20;
        nameField.lineType = TMP_InputField.LineType.SingleLine;

        Color underlineRest = new Color(MenuTheme.PaleBlue.r, MenuTheme.PaleBlue.g, MenuTheme.PaleBlue.b, 0.35f);
        Image underline = NewImage("Underline", group.transform, underlineRest);
        Center(underline.rectTransform, new Vector2(0f, -44f), new Vector2(620f, 1.5f));
        underline.raycastTarget = false;

        nameField.onSelect.AddListener(_ => UITween.Tint(underline, MenuTheme.GiltBright, 0.25f));
        nameField.onDeselect.AddListener(_ => UITween.Tint(underline, underlineRest, 0.4f));
        nameField.onSubmit.AddListener(_ => ConfirmName());

        EtherealButton.CreateLocalized(group.transform, "Continuar", "Continue",
            30f, new Vector2(0f, -140f), new Vector2(340f, 58f), MenuTheme.MarfilEnvejecido, ConfirmName, true);
    }

    private void ConfirmName()
    {
        string chosen = nameField != null ? nameField.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(chosen)) return;

        PlayerPrefs.SetString("PlayerName", chosen);
        PlayerPrefs.Save();

        GoTo(Phase.Character);
    }

    // -------------------------------------------------------- fase 2: prólogo

    private void BuildProloguePhase()
    {
        CanvasGroup group = NewPhase(Phase.Prologue, "0 - Prologue");
        prologue = gameObject.AddComponent<PrologueSequence>();
        prologue.Build(group.transform, group, () => GoTo(Phase.Path));
    }

    // ----------------------------------------------------------- fase 3: modo

    private void BuildModePhase()
    {
        CanvasGroup group = NewPhase(Phase.Mode, "2 - Mode");

        NewText(group.transform, "MODO DE JUEGO", "GAME MODE",
            new Vector2(0f, 120f), new Vector2(820f, 68f), 44f, MenuTheme.MarfilEnvejecido, true);
        NewText(group.transform, "Elige el formato de combate", "Choose the battle format",
            new Vector2(0f, 62f), new Vector2(820f, 46f), 30f, MenuTheme.BoneDim, false);

        // Los tres modos salen del catálogo: añadir uno no toca esta pantalla.
        // Con fondo visible (withBackground): tarjetas, no texto suelto — sin
        // esa caja no había ninguna pista de que fueran interactivas.
        float x = -330f;
        foreach (MatchModeDefinition mode in MatchModeCatalog.All)
        {
            MatchModeDefinition captured = mode;
            (string titleEs, string titleEn) = ModeTitlePair(mode);
            EtherealButton modeButton = EtherealButton.CreateLocalized(group.transform, titleEs, titleEn, 58f,
                new Vector2(x, -65f), new Vector2(275f, 190f), MenuTheme.MarfilEnvejecido,
                () => SelectMode(captured), true);
            modeButtons.Add(modeButton);
            visibleModes.Add(captured);

            (string taglineEs, string taglineEn) = ModeTaglinePair(mode);
            NewText(group.transform, taglineEs, taglineEn, new Vector2(x, -135f),
                new Vector2(270f, 60f), 26f, MenuTheme.BoneDim, false);
            x += 330f;
        }

        EtherealButton.CreateLocalized(group.transform, "CONTINUAR", "CONTINUE", 32f,
            new Vector2(210f, -275f), new Vector2(320f, 66f), MenuTheme.MarfilEnvejecido,
            () => GoTo(Phase.Name), true);
        EtherealButton.CreateLocalized(group.transform, "VOLVER", "BACK", 30f,
            new Vector2(-210f, -275f), new Vector2(300f, 66f), MenuTheme.BoneDim,
            () => GoTo(Phase.Path), true);

        SelectMode(selectedMode);
    }

    private void SelectMode(MatchModeDefinition mode)
    {
        selectedMode = mode;
        for (int i = 0; i < modeButtons.Count; i++)
            modeButtons[i].SetSelected(i < visibleModes.Count && visibleModes[i].Id == mode.Id);
    }

    // Los títulos cortos de modo y sus taglines viven aquí (no en
    // MatchModeDefinition) para poder colgarlos de LocalizedText sin tocar
    // MatchMode.cs, fuera del alcance de este cambio.
    private static (string, string) ModeTitlePair(MatchModeDefinition mode)
    {
        switch (mode.Id)
        {
            case MatchModeId.Duel1v1: return ("1 VS 1", "1 VS 1");
            case MatchModeId.Duo2v2: return ("2 VS 2", "2 VS 2");
            default: return ("3 VS 3", "3 VS 3");
        }
    }

    private static (string, string) ModeTaglinePair(MatchModeDefinition mode)
    {
        switch (mode.Id)
        {
            case MatchModeId.Duel1v1: return ("Uno contra uno", "One versus one");
            case MatchModeId.Duo2v2: return ("Dos por equipo", "Two per team");
            default: return ("Tres por equipo", "Three per team");
        }
    }

    // -------------------------------------------------------- fase 4: personaje

    private void BuildCharacterPhase()
    {
        CanvasGroup group = NewPhase(Phase.Character, "4 - Character");

        NewText(group.transform, "SELECCIÓN DE PERSONAJE", "CHARACTER SELECT",
            new Vector2(0f, 210f), new Vector2(960f, 70f), 44f, MenuTheme.MarfilEnvejecido, true);

        Vector2 frameCenter = new Vector2(0f, -40f);
        Vector2 frameSize = new Vector2(520f, 390f);
        previewImage = RawImage("Character Preview", group.transform, frameCenter, new Vector2(390f, 360f));
        previewImage.color = Color.white;
        CreateBorderRect(group.transform, frameCenter, frameSize, 3f, MenuTheme.OroMarchito);

        float arrowX = frameSize.x * 0.5f + 95f;
        EtherealButton previous = EtherealButton.Create(group.transform, "◀", 58f,
            new Vector2(-arrowX, frameCenter.y), new Vector2(110f, 140f), MenuTheme.GiltBright, PreviousCharacter);
        EtherealButton next = EtherealButton.Create(group.transform, "▶", 58f,
            new Vector2(arrowX, frameCenter.y), new Vector2(110f, 140f), MenuTheme.GiltBright, NextCharacter);
        MenuTheme.ApplyBodyFont(previous.Label);
        MenuTheme.ApplyBodyFont(next.Label);

        float belowFrame = frameCenter.y - frameSize.y * 0.5f;
        characterName = NewText(group.transform, string.Empty, new Vector2(0f, belowFrame - 35f),
            new Vector2(760f, 58f), 42f, MenuTheme.MarfilEnvejecido, true);
        characterCounter = NewText(group.transform, string.Empty, new Vector2(0f, belowFrame - 76f),
            new Vector2(220f, 38f), 26f, MenuTheme.OroMarchito, false);
        characterSubtitle = NewText(group.transform, string.Empty, new Vector2(0f, belowFrame - 112f),
            new Vector2(820f, 40f), 26f, MenuTheme.BoneDim, false);

        BuildPreviewStage();
        RefreshCharacterPreview();

        float actionsY = belowFrame - 170f;
        EtherealButton.CreateLocalized(group.transform, "JUGAR ONLINE", "PLAY ONLINE",
            30f, new Vector2(215f, actionsY), new Vector2(390f, 70f), MenuTheme.MarfilEnvejecido, ConfirmCharacter, true);
        EtherealButton.CreateLocalized(group.transform, "ENTRENAR", "TRAIN",
            30f, new Vector2(-215f, actionsY), new Vector2(390f, 70f), MenuTheme.OroMarchito, TestCharacterLocally, true);

        NewText(group.transform, "Prueba habilidades sin esperar rivales", "Try abilities without waiting for rivals",
            new Vector2(-215f, actionsY - 50f), new Vector2(410f, 38f), 26f, MenuTheme.BoneDim, false);
        NewText(group.transform, "Busca una sala del formato elegido", "Finds a room for the chosen format",
            new Vector2(215f, actionsY - 50f), new Vector2(410f, 38f), 26f, MenuTheme.BoneDim, false);

        // El fallo de conexión se muestra aquí: es la única fase desde la que
        // se dispara el matchmaking (ConfirmCharacter).
        statusLabel = NewText(group.transform, string.Empty, new Vector2(0f, actionsY - 86f),
            new Vector2(940f, 46f), 26f, MenuTheme.BloodBright, false);
    }

    private static void CreateBorderRect(Transform parent, Vector2 center, Vector2 size, float thickness, Color color)
    {
        GameObject host = new GameObject("Gold Preview Border", typeof(RectTransform));
        host.transform.SetParent(parent, false);
        Center(host.GetComponent<RectTransform>(), center, size);

        Image top = NewImage("Top", host.transform, color);
        Center(top.rectTransform, new Vector2(0f, size.y * 0.5f - thickness * 0.5f), new Vector2(size.x, thickness));
        Image bottom = NewImage("Bottom", host.transform, color);
        Center(bottom.rectTransform, new Vector2(0f, -size.y * 0.5f + thickness * 0.5f), new Vector2(size.x, thickness));
        Image left = NewImage("Left", host.transform, color);
        Center(left.rectTransform, new Vector2(-size.x * 0.5f + thickness * 0.5f, 0f), new Vector2(thickness, size.y));
        Image right = NewImage("Right", host.transform, color);
        Center(right.rectTransform, new Vector2(size.x * 0.5f - thickness * 0.5f, 0f), new Vector2(thickness, size.y));
        foreach (Image edge in host.GetComponentsInChildren<Image>()) edge.raycastTarget = false;
    }

    private void ShowStatus(string message)
    {
        if (statusLabel == null) return;

        statusLabel.text = message;

        // Un mensaje nuevo reinicia el contador: sin matar el anterior, el
        // primero borraría el texto del segundo a mitad de camino.
        UITween.Kill(statusLabel);
        UITween.Sequence()
            .SetTarget(statusLabel)
            .AppendInterval(3f)
            .AppendCallback(() => { if (statusLabel != null) statusLabel.text = string.Empty; });
    }

    public void PreviousCharacter()
    {
        if (flow.Current != Phase.Character) return;
        selectedCharacter = (selectedCharacter - 1 + CharacterCatalog.Count) % CharacterCatalog.Count;
        RefreshCharacterPreview();
    }

    public void NextCharacter()
    {
        if (flow.Current != Phase.Character) return;
        selectedCharacter = (selectedCharacter + 1) % CharacterCatalog.Count;
        RefreshCharacterPreview();
    }

    private void RefreshCharacterPreview()
    {
        PlayerPrefs.SetInt("SelectedCharacterIndex", selectedCharacter);
        PlayerPrefs.SetString("SelectedCharacter", CharacterCatalog.NameOf(selectedCharacter));
        PlayerPrefs.Save();

        characterName.text = CharacterCatalog.NameOf(selectedCharacter).ToUpperInvariant();
        characterCounter.text = (selectedCharacter + 1) + " / " + CharacterCatalog.Count;
        RefreshCharacterSubtitle();

        SwapCharacterModel();
    }

    /// <summary>
    /// Depende de si el personaje tiene modelo, no de un par fijo, así que no
    /// encaja en LocalizedText: se recalcula sola en cada cambio de idioma o
    /// de personaje.
    /// </summary>
    private void RefreshCharacterSubtitle()
    {
        if (characterSubtitle == null) return;

        characterSubtitle.text = GameLocalization.Choose(
            "Usa las flechas para explorar", "Use the arrows to browse");
    }

    /// <summary>
    /// Antes era un corte seco (Destroy/Instantiate sin transición). Un
    /// fundido corto alrededor del cambio evita que se sienta como un salto.
    /// </summary>
    private void SwapCharacterModel()
    {
        if (previewImage == null) { CreateCharacterPreview(); return; }

        UITween.Kill(previewImage);
        UITween.Sequence()
            .SetTarget(previewImage)
            .Append(UITween.Tint(previewImage, new Color(1f, 1f, 1f, 0f), 0.14f))
            .AppendCallback(CreateCharacterPreview)
            .Append(UITween.Tint(previewImage, Color.white, 0.16f));
    }

    private void BuildPreviewStage()
    {
        previewTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
        {
            name = "Character Preview",
            antiAliasing = 2
        };
        previewTexture.Create();
        previewImage.texture = previewTexture;

        GameObject root = new GameObject("Character Preview Stage");
        root.transform.SetParent(transform, false);
        previewRoot = root.transform;
        previewRoot.gameObject.layer = PreviewLayer;
        previewRoot.position = new Vector3(1000f, 1000f, 1000f);

        // Las luces direccionales de la escena también alcanzaban el escenario
        // remoto de preview y se sumaban a las tres de abajo. El resultado era
        // una textura quemada casi blanca aunque el material sí tuviera albedo.
        int previewMask = 1 << PreviewLayer;
        foreach (Light sceneLight in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (sceneLight != null && !sceneLight.transform.IsChildOf(previewRoot))
                sceneLight.cullingMask &= ~previewMask;

        GameObject cameraObject = new GameObject("Preview Camera");
        cameraObject.transform.SetParent(previewRoot, false);
        Camera previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.targetTexture = previewTexture;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = Color.clear;
        previewCamera.cullingMask = previewMask;
        previewCamera.fieldOfView = 30f;
        previewCamera.transform.localPosition = new Vector3(0f, 3f, -8.5f);
        previewCamera.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);

        // Las luces de color fuerte (ámbar + azul) se veían atmosféricas pero
        // teñían tanto la malla que la textura real (Albedo/Normal/Roughness)
        // quedaba casi ilegible — el modelo se leía como "un color liso", no
        // como algo con detalle pintado. Casi blancas y más intensas: el
        // material se ve tal cual es, con solo un matiz de ambiente.
        GameObject lightObject = new GameObject("Preview Key Light");
        lightObject.transform.SetParent(previewRoot, false);
        Light key = lightObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.97f, 0.92f);
        key.intensity = 1.05f;
        key.cullingMask = previewMask;
        key.transform.localRotation = Quaternion.Euler(35f, -35f, 0f);

        GameObject fillObject = new GameObject("Preview Fill Light");
        fillObject.transform.SetParent(previewRoot, false);
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.85f, 0.89f, 0.97f);
        fill.intensity = 0.35f;
        fill.cullingMask = previewMask;
        fill.transform.localRotation = Quaternion.Euler(25f, 145f, 0f);

        // Tercera luz frontal, casi de cámara: sin ella, la cara del
        // personaje que mira directo al retrato quedaba en la sombra entre
        // las otras dos y perdía todo el detalle de la textura.
        GameObject rimObject = new GameObject("Preview Rim Light");
        rimObject.transform.SetParent(previewRoot, false);
        Light rim = rimObject.AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.color = Color.white;
        rim.intensity = 0.25f;
        rim.cullingMask = previewMask;
        rim.transform.localRotation = Quaternion.Euler(10f, 5f, 0f);
    }

    private void CreateCharacterPreview()
    {
        if (previewModel != null) Destroy(previewModel);

        Texture2D portrait = CharacterCatalog.LoadPortrait(selectedCharacter);
        if (portrait != null)
        {
            previewModel = null;
            previewImage.texture = portrait;
            FitPreviewTextureRect(portrait, new Vector2(410f, 360f));
            return;
        }

        previewImage.texture = previewTexture;
        FitPreviewTextureRect(previewTexture, new Vector2(400f, 360f));

        GameObject prefab = CharacterCatalog.LoadModel(selectedCharacter);
        previewModel = prefab != null
            ? Instantiate(prefab, previewRoot)
            : CreateProvisionalCapsule(CharacterCatalog.TintOf(selectedCharacter));

        Animator animator = previewModel.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.runtimeAnimatorController == null)
            animator.runtimeAnimatorController = CharacterCatalog.LoadAnimatorController(selectedCharacter);

        foreach (Collider collider in previewModel.GetComponentsInChildren<Collider>(true))
            Destroy(collider);

        SetLayerRecursively(previewModel, PreviewLayer);
        FitPreview(previewModel);
    }

    private void FitPreviewTextureRect(Texture texture, Vector2 maximum)
    {
        if (previewImage == null || texture == null || texture.height <= 0) return;
        float aspect = texture.width / (float)texture.height;
        Vector2 size = new Vector2(maximum.y * aspect, maximum.y);
        if (size.x > maximum.x)
            size = new Vector2(maximum.x, maximum.x / Mathf.Max(aspect, 0.01f));
        previewImage.rectTransform.sizeDelta = size;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null) return;
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    /// <summary>
    /// Misma silueta que PlayerController arma en la arena, con el color del
    /// personaje: lo que se elige en el menu coincide con lo que se juega.
    /// </summary>
    private GameObject CreateProvisionalCapsule(Color tint)
    {
        GameObject root = new GameObject("Provisional Capsule");
        root.transform.SetParent(previewRoot, false);

        Material body = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = tint };
        Material accent = new Material(body) { color = Color.Lerp(tint, MenuTheme.Bone, 0.45f) };

        PreviewPrimitive(root.transform, PrimitiveType.Capsule, new Vector3(0f, 1.55f, 0f), new Vector3(0.9f, 1.5f, 0.7f), body);
        PreviewPrimitive(root.transform, PrimitiveType.Sphere, new Vector3(0f, 3.25f, 0f), new Vector3(0.75f, 0.75f, 0.75f), accent);
        PreviewPrimitive(root.transform, PrimitiveType.Cube, new Vector3(-0.85f, 1.6f, 0f), new Vector3(0.35f, 2.2f, 0.35f), accent);
        PreviewPrimitive(root.transform, PrimitiveType.Cube, new Vector3(0.85f, 1.6f, 0f), new Vector3(0.35f, 2.2f, 0.35f), accent);
        return root;
    }

    private static void PreviewPrimitive(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
    }

    private void FitPreview(GameObject model)
    {
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, CharacterCatalog.PreviewYawOf(selectedCharacter), 0f);
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        // La cámara del retrato ve ~4.56 unidades de alto a esta distancia
        // (2 · 8.5 · tan(15°) con el FOV de 30°). Encajar la altura del
        // Ocupa casi todo el retrato para que materiales y silueta se lean.
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        model.transform.localScale *= 4.1f * CharacterCatalog.PreviewScaleOf(selectedCharacter) /
                                      Mathf.Max(bounds.size.y, 0.01f);

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        model.transform.position += (previewRoot.position + new Vector3(0f, 2.15f, 0f)) - bounds.center;
    }

    private async void ConfirmCharacter()
    {
        if (lobbyManager == null)
        {
            ShowStatus(GameLocalization.Choose("No se encontro el servicio multijugador.", "Multiplayer service not found."));
            return;
        }

        ShowStatus(GameLocalization.Choose(
            "Buscando " + selectedMode.PlayerCount + " jugadores...",
            "Searching for " + selectedMode.PlayerCount + " players..."));

        bool connected = await lobbyManager.ConnectToMode(selectedMode);
        if (!connected) ShowStatus(NetworkLauncher.LastFailureReason);
    }

    private void TestCharacterLocally()
    {
        int characterToTest = selectedCharacter;
        PlayerPrefs.SetInt("SelectedCharacterIndex", characterToTest);
        PlayerPrefs.Save();
        PlayModeContext.UseTraining();
        OnlineMatchState.Reset();

        void SpawnAfterLoad(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= SpawnAfterLoad;
            // OnlineArena ya contiene el rig jugable completo. Al cargarla en
            // contexto local, ese rig toma la selección guardada y habilita
            // movimiento/poderes sin Photon. El fallback cubre una escena de
            // pruebas que no tenga rig preinstalado.
            if (FindFirstObjectByType<PlayerController>() == null)
            {
                GameObject host = new GameObject("Local Character Test");
                CharacterSpawner spawner = host.AddComponent<CharacterSpawner>();
                spawner.SpawnSelectedCharacter(characterToTest);
            }
        }

        SceneManager.sceneLoaded += SpawnAfterLoad;
        SceneManager.LoadScene(GameScenes.Arena);
    }

    // ---------------------------------------------------------- ajustes (overlay)

    /// <summary>
    /// Ajustes es un overlay, no una fase: se muestra/oculta con fade sin
    /// tocar "current" en ningún momento, así no hace falta recordar desde
    /// qué fase se abrió ni a cuál volver.
    /// </summary>
    private void BuildSettingsOverlay()
    {
        GameObject host = new GameObject("S - Settings Overlay", typeof(RectTransform));
        host.transform.SetParent(canvas.transform, false);
        Stretch(host.GetComponent<RectTransform>());
        CanvasGroup group = host.AddComponent<CanvasGroup>();
        settingsOverlay = group;

        // Fondo que bloquea clics a lo que hay detrás; pulsarlo cierra el
        // overlay, igual que en cualquier modal.
        Image backdrop = NewImage("Backdrop", group.transform, new Color(0.04f, 0.035f, 0.03f, 0.78f));
        Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;
        Button backdropButton = backdrop.gameObject.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.onClick.AddListener(HideSettingsOverlay);

        NewText(group.transform, "AJUSTES", "SETTINGS",
            new Vector2(0f, 225f), new Vector2(700f, 64f), 44f, MenuTheme.GiltBright, true);

        // --- idioma ---
        NewText(group.transform, "Idioma", "Language",
            new Vector2(-420f, 140f), new Vector2(300f, 42f), 26f, MenuTheme.BoneDim, false);
        EtherealButton spanish = EtherealButton.Create(group.transform, "ES", 30f,
            new Vector2(-460f, 85f), new Vector2(120f, 52f), MenuTheme.SpectralWhite,
            () => { GameLocalization.Set(GameLanguage.Spanish); RefreshAllLanguageButtons(); }, true);
        EtherealButton english = EtherealButton.Create(group.transform, "EN", 30f,
            new Vector2(-320f, 85f), new Vector2(120f, 52f), MenuTheme.SpectralWhite,
            () => { GameLocalization.Set(GameLanguage.English); RefreshAllLanguageButtons(); }, true);
        settingsLanguageButtons.Add(spanish);
        settingsLanguageButtons.Add(english);

        // --- volumen ---
        NewText(group.transform, "Volumen", "Volume",
            new Vector2(0f, 140f), new Vector2(400f, 42f), 26f, MenuTheme.BoneDim, false);
        BuildVolumeSlider(group.transform, "General", "Master", "VolumeMaster", new Vector2(0f, 85f));
        BuildVolumeSlider(group.transform, "Música", "Music", "VolumeMusic", new Vector2(0f, 30f));
        BuildVolumeSlider(group.transform, "Efectos", "SFX", "VolumeSfx", new Vector2(0f, -25f));
        NewText(group.transform,
            "Música y efectos aplicarán cuando el juego tenga audio por canales.",
                "Music and SFX will apply once the game has per-channel audio.",
            new Vector2(0f, -72f), new Vector2(560f, 42f), 20f, MenuTheme.BoneDim, false);

        // --- calidad gráfica ---
        NewText(group.transform, "Calidad gráfica", "Graphics quality",
            new Vector2(420f, 140f), new Vector2(340f, 42f), 26f, MenuTheme.BoneDim, false);
        BuildQualityButton(group.transform, "Baja", "Low", 1, new Vector2(420f, 85f));
        BuildQualityButton(group.transform, "Media", "Medium", 2, new Vector2(420f, 30f));
        BuildQualityButton(group.transform, "Alta", "High", 4, new Vector2(420f, -25f));
        RefreshQualityButtons();

        // --- controles ---
        NewText(group.transform, "Controles", "Controls",
            new Vector2(0f, -145f), new Vector2(700f, 42f), 26f, MenuTheme.BoneDim, false);
        NewText(group.transform,
                "WASD o clic — moverse      Q — ataque      E — definitiva",
                "WASD or click — move      Q — attack      E — ultimate",
            new Vector2(0f, -190f), new Vector2(1000f, 50f), 26f, MenuTheme.Bone, false);

        EtherealButton.CreateLocalized(group.transform, "Volver", "Back",
            30f, new Vector2(0f, -285f), new Vector2(320f, 58f), MenuTheme.MarfilEnvejecido,
            HideSettingsOverlay, true);

        RefreshAllLanguageButtons();
    }

    private void ShowSettingsOverlay()
    {
        if (settingsOverlay == null) return;
        settingsVisible = true;
        settingsOverlay.transform.SetAsLastSibling();
        if (progressRail != null) progressRail.SetAsLastSibling();
        UITween.Kill(settingsOverlay);
        UITween.Fade(settingsOverlay, 1f, 0.35f);
        RefreshProgressRail(flow.Current);
    }

    private void HideSettingsOverlay()
    {
        if (settingsOverlay == null) return;
        settingsVisible = false;
        UITween.Kill(settingsOverlay);
        UITween.Fade(settingsOverlay, 0f, 0.35f);
        RefreshProgressRail(flow.Current);
    }

    private void BuildVolumeSlider(Transform parent, string labelEs, string labelEn, string prefsKey, Vector2 position)
    {
        NewText(parent, labelEs, labelEn, position + new Vector2(-210f, 0f), new Vector2(160f, 38f), 22f, MenuTheme.Bone, false);

        GameObject sliderHost = new GameObject(labelEs + " Slider", typeof(RectTransform));
        sliderHost.transform.SetParent(parent, false);
        Center(sliderHost.GetComponent<RectTransform>(), position + new Vector2(60f, 0f), new Vector2(300f, 24f));

        Image background = sliderHost.AddComponent<Image>();
        background.color = new Color(MenuTheme.PanelTranslucent.r, MenuTheme.PanelTranslucent.g, MenuTheme.PanelTranslucent.b, 0.8f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderHost.transform, false);
        Stretch(fillArea.GetComponent<RectTransform>());

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer));
        fillObject.transform.SetParent(fillArea.transform, false);
        Image fillImage = fillObject.AddComponent<Image>();
        fillImage.color = MenuTheme.OroMarchito;
        Stretch(fillObject.GetComponent<RectTransform>());

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderHost.transform, false);
        Stretch(handleArea.GetComponent<RectTransform>());

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer));
        handleObject.transform.SetParent(handleArea.transform, false);
        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = MenuTheme.SpectralWhite;
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(14f, 24f);

        Slider slider = sliderHost.AddComponent<Slider>();
        slider.fillRect = fillObject.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        // Todo pasa por SettingsManager: él persiste Y aplica (el volumen
        // General ya controla AudioListener.volume de verdad, no solo el
        // PlayerPrefs). La UI deja de decidir qué efecto tiene cada ajuste.
        slider.value = GetVolume(prefsKey);
        slider.onValueChanged.AddListener(value => SetVolume(prefsKey, value));
    }

    private static float GetVolume(string prefsKey)
    {
        switch (prefsKey)
        {
            case "VolumeMusic": return SettingsManager.MusicVolume;
            case "VolumeSfx": return SettingsManager.SfxVolume;
            default: return SettingsManager.MasterVolume;
        }
    }

    private static void SetVolume(string prefsKey, float value)
    {
        switch (prefsKey)
        {
            case "VolumeMusic": SettingsManager.MusicVolume = value; break;
            case "VolumeSfx": SettingsManager.SfxVolume = value; break;
            default: SettingsManager.MasterVolume = value; break;
        }
    }

    private void BuildQualityButton(Transform parent, string labelEs, string labelEn, int qualityIndex, Vector2 position)
    {
        EtherealButton button = EtherealButton.CreateLocalized(parent, labelEs, labelEn, 26f, position, new Vector2(250f, 48f), MenuTheme.SpectralWhite,
            () => { SettingsManager.QualityLevel = qualityIndex; RefreshQualityButtons(); }, true);
        settingsQualityButtons.Add(button);
        button.gameObject.name = $"Quality {qualityIndex}";
    }

    private void RefreshQualityButtons()
    {
        int current = SettingsManager.QualityLevel;
        // El índice de calidad queda codificado en el nombre del botón: más
        // simple que llevar una lista paralela solo para esta comparación.
        foreach (EtherealButton button in settingsQualityButtons)
        {
            bool active = button.gameObject.name == $"Quality {current}";
            button.SetRestColor(active ? MenuTheme.OroMarchito : MenuTheme.SpectralWhite);
        }
    }

    // ------------------------------------------------------------ transiciones

    private void BuildBackButton()
    {
        // Anclado a la esquina real (no al centro + un offset grande): con
        // un offset fijo desde el centro, cualquier ventana más ancha que
        // 16:9 podía dejarlo fuera del borde visible. Anclado a la esquina,
        // el margen es siempre el mismo sin importar el aspecto. Con fondo
        // visible: sin él, un icono suelto de un solo carácter se perdía
        // contra el fondo estrellado.
        backButton = EtherealButton.Create(canvas.transform, "<", 32f,
            new Vector2(80f, -80f), new Vector2(74f, 74f), MenuTheme.SpectralWhite, GoBack, true);
        AnchorTopLeft(backButton.GetComponent<RectTransform>());
        backButton.gameObject.SetActive(false);
    }

    private void GoBack()
    {
        switch (flow.Current)
        {
            case Phase.Mode: GoTo(Phase.Prologue); break;
            case Phase.Name: GoTo(Phase.Mode); break;
            case Phase.Character: GoTo(Phase.Name); break;
        }
    }

    private void GoTo(Phase next) => flow.GoTo(next);

    private void OnTransitionStarted(Phase next)
    {
        // El botón de volver y el de ajustes se ocultan durante el cruce:
        // verlos saltar entre fases delata la transición.
        if (backButton != null) backButton.gameObject.SetActive(false);
    }

    private void OnPhaseChanged(Phase next)
    {
        // El prólogo pide penumbra; el resto del flujo, algo más de vida.
        if (atmosphere != null)
            atmosphere.SetIntensity(next == Phase.Prologue ? 0.35f : 0.65f);
        RefreshProgressRail(next);
    }

    private void OnPhaseShown(Phase next)
    {
        RefreshProgressRail(next);
        if (backButton != null)
            backButton.gameObject.SetActive(next == Phase.Name || next == Phase.Mode || next == Phase.Character);

        if (next == Phase.Name && nameField != null)
        {
            nameField.Select();
            nameField.ActivateInputField();
        }

        if (next == Phase.Prologue) prologue.Play();
    }

    private void OnDestroy()
    {
        // Tweens vivos tras destruir la escena seguirían tocando objetos muertos.
        flow.Kill();
        UITween.Kill(statusLabel);

        if (previewTexture != null)
        {
            previewTexture.Release();
            Destroy(previewTexture);
        }
    }

    // ---------------------------------------------------------------- helpers

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject host = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        host.transform.SetParent(parent, false);
        Image image = host.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static RawImage RawImage(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject host = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        host.transform.SetParent(parent, false);
        RawImage image = host.AddComponent<RawImage>();
        Center(image.rectTransform, position, size);
        return image;
    }

    private static TextMeshProUGUI NewText(Transform parent, string value, Vector2 position, Vector2 size,
        float fontSize, Color color, bool display = true)
    {
        GameObject host = new GameObject($"{(string.IsNullOrEmpty(value) ? "Label" : value)} Text",
            typeof(RectTransform), typeof(CanvasRenderer));
        host.transform.SetParent(parent, false);
        Center(host.GetComponent<RectTransform>(), position, size);

        TextMeshProUGUI text = host.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize * 1.15f;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        if (display) MenuTheme.ApplyDisplayFont(text);
        else MenuTheme.ApplyBodyFont(text);

        return text;
    }

    /// <summary>
    /// Variante con par (español, inglés): adjunta LocalizedText, así el
    /// texto se actualiza solo si el idioma cambia en Ajustes, sin reconstruir
    /// la fase ni recargar la escena.
    /// </summary>
    private static TextMeshProUGUI NewText(Transform parent, string spanish, string english, Vector2 position,
        Vector2 size, float fontSize, Color color, bool display = true)
    {
        TextMeshProUGUI text = NewText(parent, GameLocalization.Choose(spanish, english), position, size, fontSize, color, display);
        LocalizedText.Attach(text, spanish, english);
        return text;
    }

    private static void AnchorToTop(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
    }

    private static void AnchorTopLeft(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
    }

    private static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
