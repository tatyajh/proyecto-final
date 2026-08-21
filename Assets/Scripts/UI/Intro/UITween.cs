using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ajustes comunes de DOTween para el flujo de menús, en un solo sitio.
///
/// Todos los tweens de UI van en tiempo no escalado: si algún día se pausa el
/// juego con Time.timeScale = 0, el menú debe seguir respondiendo.
///
/// TextMeshPro no tiene extensiones DOTween por defecto (su módulo lo genera el
/// DOTween Utility Panel, que exige abrir el Editor), así que el color de TMP se
/// anima con DOTween.To, que es la API genérica y no depende de ningún módulo.
/// </summary>
public static class UITween
{
    public const Ease StandardEase = Ease.InOutSine;
    public const Ease PopEase = Ease.OutCubic;

    /// <summary>Prepara DOTween antes del primer tween, con capacidad holgada para el flujo.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        DOTween.Init(recycleAllByDefault: false, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly)
            .SetCapacity(200, 50);
    }

    /// <summary>Fade de un CanvasGroup que además ajusta su interactividad.</summary>
    public static Tween Fade(CanvasGroup group, float to, float duration, Ease ease = StandardEase)
    {
        if (group == null) return null;

        // Mientras se desvanece no debe seguir recibiendo clics.
        if (to > group.alpha) SetInteractive(group, true);

        return group.DOFade(to, duration)
            .SetEase(ease)
            .SetUpdate(true)
            .OnComplete(() => { if (to <= 0.001f) SetInteractive(group, false); });
    }

    public static Tween Scale(Transform target, Vector3 to, float duration, Ease ease = PopEase)
    {
        if (target == null) return null;
        return target.DOScale(to, duration).SetEase(ease).SetUpdate(true);
    }

    public static Tween Tint(Graphic graphic, Color to, float duration, Ease ease = StandardEase)
    {
        if (graphic == null) return null;
        return graphic.DOColor(to, duration).SetEase(ease).SetUpdate(true);
    }

    /// <summary>Color de un TMP_Text sin depender del módulo TMP de DOTween.</summary>
    public static Tween Tint(TMP_Text text, Color to, float duration, Ease ease = StandardEase)
    {
        if (text == null) return null;
        return DOTween.To(() => text.color, value => text.color = value, to, duration)
            .SetEase(ease)
            .SetUpdate(true);
    }

    /// <summary>Alpha de un TMP_Text, para los versos del prólogo.</summary>
    public static Tween FadeText(TMP_Text text, float to, float duration, Ease ease = StandardEase)
    {
        if (text == null) return null;
        return DOTween.To(() => text.alpha, value => text.alpha = value, to, duration)
            .SetEase(ease)
            .SetUpdate(true);
    }

    /// <summary>Secuencia vacía ya configurada en tiempo no escalado.</summary>
    public static Sequence Sequence()
    {
        return DOTween.Sequence().SetUpdate(true);
    }

    public static void SetInteractive(CanvasGroup group, bool interactive)
    {
        if (group == null) return;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }

    /// <summary>Estado inicial de una pantalla oculta, sin animar.</summary>
    public static void SnapHidden(CanvasGroup group)
    {
        if (group == null) return;
        group.alpha = 0f;
        SetInteractive(group, false);
    }

    /// <summary>Corta los tweens de un objeto antes de destruirlo o reusarlo.</summary>
    public static void Kill(Component target)
    {
        if (target != null) DOTween.Kill(target, complete: false);
    }
}
