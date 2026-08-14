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

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                StyleButton(button);

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                StyleText(text);
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
            return;
        }

        string objectName = text.gameObject.name.ToLowerInvariant();
        if (objectName.Contains("welcome") || objectName.Contains("title") || objectName.Contains("tittle"))
        {
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 3f;
            text.color = AgedGold;
        }
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
