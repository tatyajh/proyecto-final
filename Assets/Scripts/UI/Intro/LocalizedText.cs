using TMPro;
using UnityEngine;

/// <summary>
/// Actualiza un TMP_Text cuando cambia el idioma, sin recargar la escena.
///
/// Sin esto, GameLocalization.Choose(...) solo se evaluaba una vez, al
/// construir cada fase en Start(). El evento LanguageChanged existía y se
/// disparaba, pero nada estaba suscrito — por eso elegir español en la fase
/// de Idioma no actualizaba el texto de fases ya construidas (Modo, Personaje,
/// Ajustes). Esa era la causa real de "sigue en inglés", no el valor por
/// defecto de GameLocalization, que ya era correcto.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedText : MonoBehaviour
{
    private string spanish;
    private string english;
    private TMP_Text label;

    public static void Attach(TMP_Text text, string spanishValue, string englishValue)
    {
        if (text == null) return;

        LocalizedText localized = text.gameObject.AddComponent<LocalizedText>();
        localized.spanish = spanishValue;
        localized.english = englishValue;
        localized.label = text;
        localized.Apply();
    }

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= Apply;
    }

    private void Apply()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label != null) label.text = GameLocalization.Choose(spanish, english);
    }
}
