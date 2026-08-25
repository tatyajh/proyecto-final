using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Botón ornamental del menú. El foco usa contraste, marco y una escala corta;
/// no depende de bloom ni de colores HDR.
///
/// El fondo (panel + borde) es opcional: los controles donde la interactividad
/// no era obvia a simple vista (Ajustes, tarjetas de Modo) lo llevan; las
/// opciones grandes de mayor jerarquía se quedan en texto puro.
///
/// </summary>
public sealed class EtherealButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler
{
    private const float HoverScale = 1.06f;
    private const float TransitionSeconds = 0.28f;
    private static Sprite roundedSprite;
    private static Sprite normalFrameSprite;
    private static Sprite selectedFrameSprite;

    private Image background;
    private Outline outline;
    private Color backgroundRest;
    private TextMeshProUGUI label;
    private Color restColor;
    private Color hoverColor;
    private bool interactable = true;
    private bool selected;

    public System.Action OnActivated;

    public TextMeshProUGUI Label => label;
    public static Sprite RoundedBackground => GetRoundedSprite();

    /// <summary>Crea la opción con un texto fijo (sin traducción en vivo).</summary>
    public static EtherealButton Create(
        Transform parent, string text, float fontSize, Vector2 position, Vector2 size,
        Color rest, System.Action onActivated, bool withBackground = false)
    {
        EtherealButton button = Build(parent, text, fontSize, position, size, rest, onActivated, withBackground);
        return button;
    }

    /// <summary>
    /// Crea la opción con un par (español, inglés): se adjunta LocalizedText a
    /// la etiqueta, así el texto cambia solo si el idioma cambia en Ajustes,
    /// sin necesidad de reconstruir la fase ni recargar la escena.
    /// </summary>
    public static EtherealButton CreateLocalized(
        Transform parent, string spanish, string english, float fontSize, Vector2 position, Vector2 size,
        Color rest, System.Action onActivated, bool withBackground = false)
    {
        EtherealButton button = Build(parent, GameLocalization.Choose(spanish, english),
            fontSize, position, size, rest, onActivated, withBackground);
        LocalizedText.Attach(button.label, spanish, english);
        return button;
    }

    private static EtherealButton Build(
        Transform parent, string text, float fontSize, Vector2 position, Vector2 size,
        Color rest, System.Action onActivated, bool withBackground)
    {
        GameObject host = new GameObject($"{text} Option", typeof(RectTransform), typeof(CanvasRenderer));
        host.transform.SetParent(parent, false);

        RectTransform rect = host.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        // El fondo es el objetivo real del clic: cubre todo el rect del botón,
        // no solo el área de los glifos. Sin esto, un botón sin caja visible
        // solo respondía al clic si caía exactamente sobre una letra.
        Image background = host.AddComponent<Image>();
        if (withBackground)
            background.sprite = GetFrameSprite(false) ?? GetRoundedSprite();
        background.color = withBackground
            ? new Color(1f, 1f, 1f, 0.72f)
            : new Color(0f, 0f, 0f, 0f);
        background.raycastTarget = true;

        if (withBackground)
        {
            Outline outline = host.AddComponent<Outline>();
            outline.effectColor = MenuTheme.IntroLine;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        GameObject labelHost = new GameObject("Label", typeof(RectTransform));
        labelHost.transform.SetParent(host.transform, false);
        RectTransform labelRect = labelHost.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelHost.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize * 1.15f;
        label.color = rest;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        // Tracking corto: el espaciado anterior hacía que las palabras
        // pequeñas se vieran rotas y reducía mucho su legibilidad.
        label.characterSpacing = 0.5f;
        label.raycastTarget = false;
        // Cinzel: la voz ceremonial del juego.
        MenuTheme.ApplyDisplayFont(label);

        EtherealButton button = host.AddComponent<EtherealButton>();
        button.Initialize(background, label, rest, onActivated);
        button.outline = host.GetComponent<Outline>();
        return button;
    }

    private static Sprite GetRoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;

        const int size = 64;
        const float radius = 11f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Rounded UI Mask",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = Mathf.Clamp(x, radius, size - 1f - radius);
            float cy = Mathf.Clamp(y, radius, size - 1f - radius);
            float alpha = Mathf.Clamp01(radius + 0.75f - Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        roundedSprite.name = "Rounded UI Sprite";
        return roundedSprite;
    }

    private static Sprite GetFrameSprite(bool selectedFrame)
    {
        Sprite cached = selectedFrame ? selectedFrameSprite : normalFrameSprite;
        if (cached != null) return cached;

        Texture2D texture = Resources.Load<Texture2D>(selectedFrame ? "UI/ButtonSelected" : "UI/ButtonNormal");
        if (texture == null) return null;
        cached = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f);
        if (selectedFrame) selectedFrameSprite = cached;
        else normalFrameSprite = cached;
        return cached;
    }

    private void Initialize(Image backgroundImage, TextMeshProUGUI textLabel, Color rest, System.Action onActivated)
    {
        background = backgroundImage;
        backgroundRest = background.color;
        label = textLabel;
        restColor = rest;
        // El foco se apoya en escala, marco y contraste, no en un halo HDR.
        hoverColor = MenuTheme.GiltBright;
        OnActivated = onActivated;
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        if (label == null) return;

        label.color = value ? restColor : new Color(restColor.r, restColor.g, restColor.b, 0.58f);
        if (background != null)
            background.color = value ? backgroundRest : new Color(backgroundRest.r, backgroundRest.g, backgroundRest.b, 0.42f);
    }

    /// <summary>Cambia el color de reposo sin perder el estado de hover.</summary>
    public void SetRestColor(Color color)
    {
        restColor = color;
        if (interactable && label != null) label.color = color;
    }

    /// <summary>Estado persistente de selección, separado del hover.</summary>
    public void SetSelected(bool value)
    {
        selected = value;
        if (background != null)
        {
            Sprite frame = GetFrameSprite(value);
            if (frame != null) background.sprite = frame;
            background.color = value
                ? new Color(1f, 1f, 1f, 0.9f)
                : backgroundRest;
        }
        if (outline != null)
        {
            outline.effectColor = value ? MenuTheme.GiltBright : MenuTheme.IntroLine;
            outline.effectDistance = value ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
        }
        if (label != null && interactable)
            label.color = value ? MenuTheme.GiltBright : restColor;
    }

    public void OnPointerEnter(PointerEventData eventData) => Highlight(true);
    public void OnPointerExit(PointerEventData eventData) => Highlight(false);
    public void OnSelect(BaseEventData eventData) => Highlight(true);
    public void OnDeselect(BaseEventData eventData) => Highlight(false);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable) return;
        AudioCatalog.PlayUiClick();
        OnActivated?.Invoke();
    }

    private void Highlight(bool on)
    {
        if (!interactable || !isActiveAndEnabled) return;

        // Matar los tweens previos evita que un hover rápido deje el botón a
        // medio camino entre los dos estados.
        UITween.Kill(label);
        UITween.Kill(transform);

        UITween.Tint(label, on ? hoverColor : (selected ? MenuTheme.GiltBright : restColor), TransitionSeconds);
        UITween.Scale(transform, Vector3.one * (on ? HoverScale : 1f), TransitionSeconds);
    }

    private void OnDisable()
    {
        // Al ocultar la fase, el estado visual debe volver a reposo o el botón
        // reaparece agrandado la próxima vez que se muestre.
        UITween.Kill(label);
        UITween.Kill(transform);
        transform.localScale = Vector3.one;
        if (label != null && interactable) label.color = selected ? MenuTheme.GiltBright : restColor;
    }
}
