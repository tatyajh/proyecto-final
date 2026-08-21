using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Flujo multijugador: modo -> personaje -> matchmaking.
/// Construye una sola jerarquía al arrancar y reutiliza cada elemento.
/// Los seis personajes son elegibles; los que aún no tienen modelo animado
/// entran con una cápsula provisional hasta que arte entregue su FBX.
/// </summary>
public sealed class MultiplayerMenuController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private enum ScreenState { Mode, Character, Search }

    private sealed class CharacterInfo
    {
        public readonly string Name;
        public readonly string Subtitle;
        public readonly bool HasModel;
        public readonly Color Tint;

        public CharacterInfo(string name, string subtitle, bool hasModel, Color tint)
        {
            Name = name;
            Subtitle = subtitle;
            HasModel = hasModel;
            Tint = tint;
        }
    }

    private const int QuietmorIndex = 3;

    // Se construye en Initialize, nunca como inicializador de campo:
    // GameLocalization.Choose lee PlayerPrefs y Unity lo prohíbe ahí. Al
    // lanzar, el array quedaba en null y el menú entero reventaba.
    // Construirlo tarde también recoge el idioma vigente en ese momento.
    private CharacterInfo[] characters;

    private static CharacterInfo[] BuildCharacterRoster()
    {
        string[] subtitles =
        {
            GameLocalization.Choose("Girasol Ardiente", "Burning Sunflower"),
            GameLocalization.Choose("Flor de Murciélago", "Bat Flower"),
            GameLocalization.Choose("Corona del Alba", "Crown of Dawn"),
            GameLocalization.Choose("La Campana sin Voz · Control", "The Voiceless Bell · Control"),
            GameLocalization.Choose("Reina de las Espinas", "Queen of Thorns"),
            GameLocalization.Choose("Raíz Profunda", "Deep Root")
        };

        CharacterInfo[] roster = new CharacterInfo[CharacterCatalog.Count];
        for (int i = 0; i < roster.Length; i++)
        {
            // "Tiene modelo" se comprueba contra Resources, no se escribe a mano:
            // en cuanto el prefab del personaje exista, el menú lo refleja solo.
            roster[i] = new CharacterInfo(
                CharacterCatalog.NameOf(i),
                subtitles[i],
                CharacterCatalog.HasModel(i),
                CharacterCatalog.TintOf(i));
        }

        return roster;
    }

    // Paleta única del juego. Antes eran valores a ojo que no coincidían con
    // nada; ahora salen del mismo sitio que el resto de los menús.
    private static readonly Color Plum = MenuTheme.Void;
    private static readonly Color Panel = MenuTheme.Panel;
    private static readonly Color Sunken = MenuTheme.PanelDeep;
    private static readonly Color Forest = Color.Lerp(MenuTheme.Panel, MenuTheme.Rot, 0.32f);
    private static readonly Color Gold = MenuTheme.Gilt;
    private static readonly Color GoldSoft = MenuTheme.Line;
    private static readonly Color Bone = MenuTheme.Bone;
    private static readonly Color Muted = MenuTheme.Ash;
    private static readonly Color Error = MenuTheme.BloodBright;

    private LobbyManager lobby;
    private Canvas canvas;
    private CanvasGroup modeScreen;
    private CanvasGroup characterScreen;
    private CanvasGroup searchScreen;
    private TMP_Text searchTitle;
    private TMP_Text searchSubtitle;
    private TMP_Text searchStatus;
    private TMP_Text searchDetail;
    private TMP_Text characterName;
    private TMP_Text characterSubtitle;
    private TMP_Text characterNote;
    private TMP_Text characterCounter;
    private Button retryButton;
    private RawImage previewImage;
    private RenderTexture previewTexture;
    private Transform previewRoot;
    private GameObject previewModel;
    private ScreenState state;
    private Coroutine transition;
    private int matchmakingAttempt;
    private MatchModeDefinition selectedMode;
    private int characterIndex = QuietmorIndex;
    private Vector2 pointerDown;

    public void Initialize(LobbyManager owner)
    {
        if (canvas != null) return;
        lobby = owner;
        characters = BuildCharacterRoster();
        selectedMode = MatchModeCatalog.Default;
        characterIndex = Mathf.Clamp(PlayerPrefs.GetInt("SelectedCharacterIndex", QuietmorIndex), 0, characters.Length - 1);
        BuildInterface();
        BuildPreviewStage();
        RefreshCharacter();
        ShowModeSelection();
    }

    private void Update()
    {
        if (state != ScreenState.Character) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) PreviousCharacter();
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) NextCharacter();
        if (previewModel != null)
            previewModel.transform.Rotate(0f, 11f * Time.unscaledDeltaTime, 0f, Space.World);
    }

    public void ShowModeSelection() => SetScreen(ScreenState.Mode);
    public void ShowCharacterSelection() => SetScreen(ScreenState.Character);

    public void NextCharacter()
    {
        characterIndex = (characterIndex + 1) % characters.Length;
        RefreshCharacter();
    }

    public void PreviousCharacter()
    {
        characterIndex = (characterIndex - 1 + characters.Length) % characters.Length;
        RefreshCharacter();
    }

    public void OnPointerDown(PointerEventData eventData) => pointerDown = eventData.position;

    public void OnPointerUp(PointerEventData eventData)
    {
        if (state != ScreenState.Character) return;
        float delta = eventData.position.x - pointerDown.x;
        if (Mathf.Abs(delta) < 55f) return;
        if (delta > 0f) PreviousCharacter(); else NextCharacter();
    }

    // ---------------------------------------------------------------- interfaz

    private void BuildInterface()
    {
        GameObject canvasObject = UiObject("Polished Multiplayer UI", transform);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        Image background = Image("Background", canvas.transform, Plum);
        Stretch(background.rectTransform);

        // Viñeta sutil para que el fondo no sea un plano liso, hecha con color
        // en vez de textura: el set de arte es referencia de modelado y vive
        // fuera del proyecto hasta que los personajes estén terminados.
        Image vignette = Image("Vignette", background.transform, MenuTheme.WithAlpha(MenuTheme.Rot, 0.07f));
        Center(vignette.rectTransform, new Vector2(0f, -30f), new Vector2(1000f, 640f));
        vignette.raycastTarget = false;

        Image topLine = Image("Gold top line", background.transform, GoldSoft);
        Anchor(topLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 2f));

        Button back = SecondaryButton(GameLocalization.Choose("VOLVER", "BACK"), background.transform, Vector2.zero, new Vector2(126f, 42f), 16f);
        // SecondaryButton devuelve el botón interior. Es el borde padre el que
        // debe anclarse a la pantalla; mover el hijo lo dejaba fuera del borde.
        RectTransform backBorderRect = back.transform.parent.GetComponent<RectTransform>();
        backBorderRect.anchorMin = backBorderRect.anchorMax = backBorderRect.pivot = new Vector2(0f, 1f);
        backBorderRect.anchoredPosition = new Vector2(22f, -20f);
        back.onClick.AddListener(HandleBack);

        Text("BLIGHTED BLOSSOMS", background.transform, new Vector2(0f, -24f), new Vector2(620f, 38f), 26f, Gold, FontStyles.Bold, new Vector2(0.5f, 1f));
        Text(GameLocalization.Choose("ARENA MULTIJUGADOR", "MULTIPLAYER ARENA"), background.transform, new Vector2(0f, -56f), new Vector2(620f, 22f), 13f, Muted, FontStyles.Normal, new Vector2(0.5f, 1f));

        modeScreen = Screen("Mode Screen", background.transform);
        BuildModeScreen(modeScreen.transform);
        characterScreen = Screen("Character Screen", background.transform);
        BuildCharacterScreen(characterScreen.transform);
        searchScreen = Screen("Search Screen", background.transform);
        BuildSearchScreen(searchScreen.transform);

        // Mantenerlo delante de cualquiera de las tres pantallas.
        backBorderRect.SetAsLastSibling();
    }

    private void BuildModeScreen(Transform parent)
    {
        Transform card = Card("Mode Card", parent, new Vector2(0f, -6f), new Vector2(580f, 372f));
        Text(GameLocalization.Choose("ELIGE TU MODO", "CHOOSE YOUR MODE"), card, new Vector2(0f, 140f), new Vector2(500f, 38f), 27f, Bone, FontStyles.Bold);

        // Los tres formatos comparten el flujo: solo cambia el aforo de la sala
        // y el tamaño de equipo, ambos definidos en MatchModeCatalog.
        float y = 62f;
        foreach (MatchModeDefinition mode in MatchModeCatalog.All)
        {
            MatchModeDefinition captured = mode;
            Button modeButton = SecondaryButton(string.Empty, card, new Vector2(0f, y), new Vector2(480f, 70f), 20f);
            Text(LocalizedModeName(mode), modeButton.transform, new Vector2(0f, 13f), new Vector2(450f, 28f), 20f, Bone, FontStyles.Bold);
            Text(LocalizedModeTagline(mode), modeButton.transform, new Vector2(0f, -14f), new Vector2(450f, 22f), 13f, Muted);
            modeButton.onClick.AddListener(() => SelectMode(captured));
            y -= 82f;
        }
    }

    private void BuildCharacterScreen(Transform parent)
    {
        Transform card = Card("Character Card", parent, new Vector2(0f, -6f), new Vector2(660f, 470f));
        Text(GameLocalization.Choose("ELIGE TU PERSONAJE", "CHOOSE YOUR CHARACTER"), card, new Vector2(0f, 200f), new Vector2(520f, 36f), 25f, Bone, FontStyles.Bold);
        characterCounter = Text("4 / 6", card, new Vector2(282f, 200f), new Vector2(80f, 24f), 13f, Muted);

        Image frame = Image("Preview Frame", card, GoldSoft);
        Center(frame.rectTransform, new Vector2(0f, 42f), new Vector2(266f, 256f));
        Image inner = Image("Preview Inner", frame.transform, Sunken);
        Center(inner.rectTransform, Vector2.zero, new Vector2(262f, 252f));

        // Render en vivo del modelo. Solo usa assets que ya están en el
        // proyecto: el concept art vive aparte porque es base de modelado.
        previewImage = RawImage("Character Preview", inner.transform, Vector2.zero, new Vector2(252f, 242f));
        previewImage.color = Color.white;

        // Arrastrar sobre el retrato también cambia de personaje (móvil).
        EventTrigger trigger = previewImage.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerDown, data => OnPointerDown((PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.PointerUp, data => OnPointerUp((PointerEventData)data));

        Button previous = SecondaryButton("‹", card, new Vector2(-238f, 42f), new Vector2(52f, 86f), 34f);
        previous.onClick.AddListener(PreviousCharacter);
        Button next = SecondaryButton("›", card, new Vector2(238f, 42f), new Vector2(52f, 86f), 34f);
        next.onClick.AddListener(NextCharacter);

        characterName = Text("QUIETMOR", card, new Vector2(0f, -122f), new Vector2(520f, 38f), 27f, Gold, FontStyles.Bold);
        characterSubtitle = Text(string.Empty, card, new Vector2(0f, -152f), new Vector2(520f, 24f), 14f, Bone);
        characterNote = Text(string.Empty, card, new Vector2(0f, -176f), new Vector2(520f, 22f), 12f, Muted);

        Button confirm = PrimaryButton(GameLocalization.Choose("ENTRAR AL COMBATE", "ENTER BATTLE"), card, new Vector2(0f, -208f), new Vector2(300f, 52f), 18f);
        confirm.onClick.AddListener(ConfirmCharacter);
    }

    private void RefreshCharacter()
    {
        CharacterInfo info = characters[characterIndex];
        characterName.text = info.Name.ToUpperInvariant();
        characterName.color = info.HasModel ? Gold : Bone;
        characterSubtitle.text = info.Subtitle;
        characterCounter.text = $"{characterIndex + 1} / {characters.Length}";
        characterNote.text = info.HasModel
            ? GameLocalization.Choose("Modelo listo", "Model ready")
            : GameLocalization.Choose("Cápsula provisional · modelo en camino", "Temporary capsule · model in progress");
        CreatePreview(info);
    }

    private void BuildSearchScreen(Transform parent)
    {
        Transform card = Card("Search Card", parent, new Vector2(0f, -6f), new Vector2(540f, 310f));
        searchTitle = Text(GameLocalization.Choose("BUSCANDO RIVAL", "SEARCHING FOR OPPONENT"), card, new Vector2(0f, 104f), new Vector2(480f, 38f), 26f, Bone, FontStyles.Bold);
        searchSubtitle = Text("DUELO 1V1 · QUIETMOR", card, new Vector2(0f, 74f), new Vector2(480f, 24f), 14f, Gold);
        searchStatus = Text(GameLocalization.Choose("Preparando conexión...", "Preparing connection..."), card, new Vector2(0f, 22f), new Vector2(470f, 58f), 16f, Bone);
        searchDetail = Text(string.Empty, card, new Vector2(0f, -30f), new Vector2(470f, 30f), 12f, Muted);

        retryButton = PrimaryButton(GameLocalization.Choose("REINTENTAR", "RETRY"), card, new Vector2(0f, -74f), new Vector2(260f, 46f), 17f);
        retryButton.onClick.AddListener(StartMatchmaking);
        retryButton.gameObject.SetActive(false);

        Button cancel = SecondaryButton(GameLocalization.Choose("CANCELAR", "CANCEL"), card, new Vector2(0f, -124f), new Vector2(260f, 40f), 15f);
        cancel.onClick.AddListener(CancelMatchmaking);
    }

    private static string LocalizedModeName(MatchModeDefinition mode)
    {
        switch (mode.Id)
        {
            case MatchModeId.Duo2v2: return GameLocalization.Choose("DÚO 2V2", "DUO 2V2");
            case MatchModeId.Clash3v3: return GameLocalization.Choose("CHOQUE 3V3", "CLASH 3V3");
            default: return GameLocalization.Choose("DUELO 1V1", "DUEL 1V1");
        }
    }

    private static string LocalizedModeTagline(MatchModeDefinition mode)
    {
        switch (mode.Id)
        {
            case MatchModeId.Duo2v2: return GameLocalization.Choose("Cuatro jugadores · dos por equipo", "Four players · two per team");
            case MatchModeId.Clash3v3: return GameLocalization.Choose("Seis jugadores · tres por equipo", "Six players · three per team");
            default: return GameLocalization.Choose("Dos jugadores en línea", "Two players online");
        }
    }

    // ------------------------------------------------------------------ flujo

    private void SelectMode(MatchModeDefinition mode)
    {
        selectedMode = mode ?? MatchModeCatalog.Default;
        ShowCharacterSelection();
    }

    private void HandleBack()
    {
        if (state == ScreenState.Mode)
            lobby.ReturnToMainMenu();
        else if (state == ScreenState.Character)
            ShowModeSelection();
        else
            CancelMatchmaking();
    }

    private void ConfirmCharacter()
    {
        PlayerPrefs.SetInt("SelectedCharacterIndex", characterIndex);
        PlayerPrefs.SetString("SelectedCharacter", characters[characterIndex].Name);
        PlayerPrefs.Save();
        SetScreen(ScreenState.Search);
        StartMatchmaking();
    }

    private async void StartMatchmaking()
    {
        int attempt = ++matchmakingAttempt;
        retryButton.gameObject.SetActive(false);
        searchTitle.text = selectedMode.TeamSize > 1
            ? GameLocalization.Choose("BUSCANDO EQUIPOS", "SEARCHING FOR TEAMS")
            : GameLocalization.Choose("BUSCANDO RIVAL", "SEARCHING FOR OPPONENT");
        searchSubtitle.text = $"{LocalizedModeName(selectedMode)} · {characters[characterIndex].Name.ToUpperInvariant()}";
        searchStatus.color = Bone;
        searchStatus.text = GameLocalization.Choose(
            $"Conectando con Photon...\nLa partida comienza con {selectedMode.PlayerCount} jugadores.",
            $"Connecting to Photon...\nThe match starts with {selectedMode.PlayerCount} players.");
        searchDetail.text = string.Empty;

        bool success = await lobby.ConnectToMode(selectedMode);
        if (attempt != matchmakingAttempt) return;

        if (success)
        {
            searchStatus.text = selectedMode.TeamSize > 1
                ? GameLocalization.Choose("Sala encontrada. Entrando al Santuario...\nEl combate comienza cuando se llenen los equipos.", "Room found. Entering the Sanctuary...\nCombat starts when both teams are full.")
                : GameLocalization.Choose("Rival encontrado. Entrando al Santuario...", "Opponent found. Entering the Sanctuary...");
            return;
        }

        searchStatus.color = Error;
        searchStatus.text = GameLocalization.Choose(
            "No fue posible conectar.\nRevisa tu conexión e inténtalo nuevamente.",
            "Unable to connect.\nCheck your connection and try again.");
        // El motivo crudo de Photon: sin esto un fallo de AppId, de región o de
        // cuota se ve igual que un problema de internet del jugador.
        searchDetail.text = NetworkLauncher.LastFailureReason;
        retryButton.gameObject.SetActive(true);
    }

    private async void CancelMatchmaking()
    {
        matchmakingAttempt++;
        searchStatus.color = Bone;
        searchStatus.text = GameLocalization.Choose("Cancelando búsqueda...", "Cancelling search...");
        searchDetail.text = string.Empty;
        await lobby.CancelConnection();
        ShowCharacterSelection();
    }

    private void SetScreen(ScreenState next)
    {
        state = next;
        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(TransitionTo(next));
    }

    private IEnumerator TransitionTo(ScreenState next)
    {
        CanvasGroup target = next == ScreenState.Mode ? modeScreen : next == ScreenState.Character ? characterScreen : searchScreen;
        SetGroup(modeScreen, modeScreen == target);
        SetGroup(characterScreen, characterScreen == target);
        SetGroup(searchScreen, searchScreen == target);
        target.alpha = 0f;
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.localScale = new Vector3(0.985f, 0.985f, 1f);
        const float duration = 0.18f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            target.alpha = t;
            rect.localScale = Vector3.Lerp(new Vector3(0.985f, 0.985f, 1f), Vector3.one, t);
            yield return null;
        }
        target.alpha = 1f;
        rect.localScale = Vector3.one;
    }

    private static void SetGroup(CanvasGroup group, bool active)
    {
        group.gameObject.SetActive(active);
        group.interactable = active;
        group.blocksRaycasts = active;
    }

    // ---------------------------------------------------------------- preview

    private void BuildPreviewStage()
    {
        previewTexture = new RenderTexture(384, 384, 16, RenderTextureFormat.ARGB32)
        {
            name = "Multiplayer Character Preview",
            antiAliasing = 2
        };
        previewTexture.Create();
        previewImage.texture = previewTexture;

        GameObject root = new GameObject("Character Preview Stage");
        root.transform.SetParent(transform, false);
        previewRoot = root.transform;
        previewRoot.position = new Vector3(1000f, 1000f, 1000f);

        GameObject cameraObject = new GameObject("Preview Camera");
        cameraObject.transform.SetParent(previewRoot, false);
        Camera previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.targetTexture = previewTexture;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = Sunken;
        previewCamera.fieldOfView = 30f;
        previewCamera.transform.localPosition = new Vector3(0f, 2.2f, -8.5f);
        previewCamera.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);

        GameObject lightObject = new GameObject("Preview Key Light");
        lightObject.transform.SetParent(previewRoot, false);
        Light key = lightObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.82f, 0.52f);
        key.intensity = 1.35f;
        key.transform.localRotation = Quaternion.Euler(35f, -35f, 0f);

        GameObject fillObject = new GameObject("Preview Fill Light");
        fillObject.transform.SetParent(previewRoot, false);
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.30f, 0.42f, 0.52f);
        fill.intensity = 0.65f;
        fill.transform.localRotation = Quaternion.Euler(25f, 145f, 0f);
    }

    private void CreatePreview(CharacterInfo info)
    {
        if (previewModel != null) Destroy(previewModel);

        GameObject prefab = CharacterCatalog.LoadModel(characterIndex);
        previewModel = prefab != null ? Instantiate(prefab, previewRoot) : CreateProvisionalCapsule(info.Tint);

        foreach (Collider collider in previewModel.GetComponentsInChildren<Collider>(true))
            Destroy(collider);

        FitPreview(previewModel);
    }

    /// <summary>
    /// Misma silueta que PlayerController arma en la arena, con el color del
    /// personaje: lo que se elige en el menú coincide con lo que se juega.
    /// </summary>
    private GameObject CreateProvisionalCapsule(Color tint)
    {
        GameObject root = new GameObject("Provisional Capsule");
        root.transform.SetParent(previewRoot, false);

        Material body = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = tint };
        Material accent = new Material(body) { color = Color.Lerp(tint, Bone, 0.45f) };

        Primitive(root.transform, PrimitiveType.Capsule, new Vector3(0f, 1.55f, 0f), new Vector3(0.9f, 1.5f, 0.7f), body);
        Primitive(root.transform, PrimitiveType.Sphere, new Vector3(0f, 3.25f, 0f), new Vector3(0.75f, 0.75f, 0.75f), accent);
        Primitive(root.transform, PrimitiveType.Cube, new Vector3(-0.85f, 1.6f, 0f), new Vector3(0.35f, 2.2f, 0.35f), accent);
        Primitive(root.transform, PrimitiveType.Cube, new Vector3(0.85f, 1.6f, 0f), new Vector3(0.35f, 2.2f, 0.35f), accent);
        return root;
    }

    private static void Primitive(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
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
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        model.transform.localScale *= 4.6f / Mathf.Max(bounds.size.y, 0.01f);

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        model.transform.position += (previewRoot.position + new Vector3(0f, 2.15f, 0f)) - bounds.center;
    }

    private void OnDestroy()
    {
        if (previewTexture != null)
        {
            previewTexture.Release();
            Destroy(previewTexture);
        }
    }

    // ------------------------------------------------------- interacción

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    // ---------------------------------------------------------------- helpers

    private static CanvasGroup Screen(string name, Transform parent)
    {
        GameObject gameObject = UiObject(name, parent);
        Stretch(gameObject.GetComponent<RectTransform>());
        return gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>Marco dorado de 1 px con el panel dentro: más limpio que un Outline.</summary>
    private static Transform Card(string name, Transform parent, Vector2 position, Vector2 size)
    {
        Image border = Image(name + " Border", parent, GoldSoft);
        Center(border.rectTransform, position, size + new Vector2(2f, 2f));
        Image panel = Image(name, border.transform, Panel);
        Center(panel.rectTransform, Vector2.zero, size);
        return panel.transform;
    }

    private static Image Image(string name, Transform parent, Color color)
    {
        GameObject gameObject = UiObject(name, parent);
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static RawImage RawImage(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject gameObject = UiObject(name, parent);
        RawImage image = gameObject.AddComponent<RawImage>();
        Center(image.rectTransform, position, size);
        return image;
    }

    /// <summary>Acción principal: relleno dorado con texto oscuro.</summary>
    private static Button PrimaryButton(string label, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        Button button = BuildButton(label, parent, position, size, Gold, Plum, fontSize);
        return button;
    }

    /// <summary>Acción secundaria: panel oscuro con borde y texto claro.</summary>
    private static Button SecondaryButton(string label, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        Image border = Image(label + " Border", parent, GoldSoft);
        Center(border.rectTransform, position, size + new Vector2(2f, 2f));
        return BuildButton(label, border.transform, Vector2.zero, size, Forest, Bone, fontSize);
    }

    private static Button BuildButton(string label, Transform parent, Vector2 position, Vector2 size, Color fill, Color textColor, float fontSize)
    {
        Image image = Image((string.IsNullOrEmpty(label) ? "Mode" : label) + " Button", parent, fill);
        Center(image.rectTransform, position, size);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.80f, 1f);
        colors.pressedColor = new Color(0.74f, 0.68f, 0.58f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colors.fadeDuration = 0.10f;
        button.colors = colors;

        if (!string.IsNullOrEmpty(label))
            Text(label, image.transform, Vector2.zero, size - new Vector2(18f, 8f), fontSize, textColor, FontStyles.Bold);

        return button;
    }

    private static TMP_Text Text(string value, Transform parent, Vector2 position, Vector2 size, float fontSize, Color color, FontStyles style = FontStyles.Normal, Vector2? anchor = null)
    {
        GameObject gameObject = UiObject((string.IsNullOrEmpty(value) ? "Label" : value) + " Text", parent);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        Center(rect, position, size);
        if (anchor.HasValue)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor.Value;
            rect.anchoredPosition = position;
        }
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.characterSpacing = style == FontStyles.Bold ? 1.8f : 0f;

        // Cinzel para títulos y botones, EB Garamond para el texto corrido.
        if (style == FontStyles.Bold) MenuTheme.ApplyDisplayFont(text);
        else MenuTheme.ApplyBodyFont(text);

        return text;
    }

    private static GameObject UiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
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

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
