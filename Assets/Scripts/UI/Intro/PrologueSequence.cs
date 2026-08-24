using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Prólogo cinematográfico: cuatro versos que entran y salen sobre el vacío,
/// encadenados en una única Sequence de DOTween.
///
/// Los tiempos están calculados para lectura pausada, no para pasar rápido: dos
/// segundos de entrada, una permanencia proporcional a la longitud del verso y
/// dos de salida. Se puede saltar en cualquier momento.
/// </summary>
public sealed class PrologueSequence : MonoBehaviour
{
    private const float FadeSeconds = 2f;
    private const float HoldBaseSeconds = 2.6f;
    // Un verso largo necesita más tiempo en pantalla que uno corto.
    private const float HoldPerCharacter = 0.028f;
    private const float GapSeconds = 0.5f;
    private const float OpeningSilenceSeconds = 1.2f;

    private TextMeshProUGUI verseLabel;
    private Sequence playback;
    private System.Action onFinished;
    private bool finished;

    private static string[] Verses => new[]
    {
        GameLocalization.Choose(
            "Hace siglos, los Cuatro Árboles Primordiales sostenían el equilibrio del mundo. Su savia sanaba la tierra, pero los reinos hicieron de ella una causa de guerra.",
            "Centuries ago, the Four Primordial Trees upheld the balance of the world. Their sap healed the land, but the kingdoms turned it into a cause for war."),
        GameLocalization.Choose(
            "Las guerras quebraron los sellos. La Podredumbre, un hambre antigua y sin rostro, se filtró por la red de raíces y convirtió el juramento de los guardianes en una puerta.",
            "The wars shattered the seals. The Blight, an ancient faceless hunger, seeped through the root network and turned the guardians' oath into a doorway."),
        GameLocalization.Choose(
            "Solmara, Lunara, Acatheria y Terramor quedaron encadenados a los Árboles que juraron proteger. Heliandra y Quietmor fueron marcadas, pero nunca sometidas.",
            "Solmara, Lunara, Acatheria, and Terramor were bound to the Trees they swore to protect. Heliandra and Quietmor were marked, but never subdued."),
        GameLocalization.Choose(
            "Sólo ellas pueden reunir las cuatro Chispas Primordiales, purificar a los guardianes y abrir el camino hacia la Raíz Madre, donde aguarda La Podredumbre.",
            "Only they can gather the four Primordial Sparks, purify the guardians, and open the way to the Mother Root, where the Blight awaits.")
    };

    public void Build(Transform parent, CanvasGroup phaseGroup, System.Action onPrologueFinished)
    {
        onFinished = onPrologueFinished;

        GameObject verseHost = new GameObject("Verse", typeof(RectTransform));
        verseHost.transform.SetParent(parent, false);
        RectTransform rect = verseHost.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1120f, 280f);

        verseLabel = verseHost.AddComponent<TextMeshProUGUI>();
        verseLabel.fontSize = 38f;
        verseLabel.alignment = TextAlignmentOptions.Center;
        verseLabel.color = MenuTheme.SpectralWhite;
        verseLabel.characterSpacing = 0.5f;
        verseLabel.lineSpacing = 10f;
        verseLabel.raycastTarget = false;
        MenuTheme.ApplyDisplayFont(verseLabel);

        EtherealButton.CreateLocalized(
            parent, "Saltar prólogo", "Skip prologue",
            30f, new Vector2(0f, -320f), new Vector2(420f, 68f),
            MenuTheme.MarfilEnvejecido, Skip, true);
    }

    public void Play()
    {
        finished = false;
        verseLabel.alpha = 0f;

        playback = UITween.Sequence();
        // Un respiro en negro antes del primer verso: entrar de golpe rompe el tono.
        playback.AppendInterval(OpeningSilenceSeconds);

        foreach (string verse in Verses)
        {
            string captured = verse;
            float hold = HoldBaseSeconds + captured.Length * HoldPerCharacter;

            playback.AppendCallback(() => verseLabel.text = captured);
            playback.Append(UITween.FadeText(verseLabel, 1f, FadeSeconds));
            playback.AppendInterval(hold);
            playback.Append(UITween.FadeText(verseLabel, 0f, FadeSeconds));
            playback.AppendInterval(GapSeconds);
        }

        playback.OnComplete(Finish);
    }

    public void Skip()
    {
        if (finished) return;

        if (playback != null && playback.IsActive())
        {
            playback.Kill();
            playback = null;
        }
        Finish();
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;
        onFinished?.Invoke();
    }

    private void OnDestroy()
    {
        // Una Sequence viva tras destruir el objeto seguiría tocando el label.
        if (playback != null && playback.IsActive()) playback.Kill();
        UITween.Kill(verseLabel);
    }
}
