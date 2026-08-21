using UnityEditor;

/// <summary>
/// PlayerPrefs vive en memoria mientras el Editor está abierto; editar el
/// registro de Windows por fuera no lo actualiza hasta reiniciar el Editor
/// entero (no solo Play/Stop), y a veces ni así. Esto pasa por la propia API
/// de Unity, así que el cambio es inmediato y fiable.
/// </summary>
public static class LanguageResetTool
{
    [MenuItem("Blighted Blossoms/Idioma/Forzar Español")]
    private static void ForceSpanish() => GameLocalization.Set(GameLanguage.Spanish);

    [MenuItem("Blighted Blossoms/Idioma/Forzar English")]
    private static void ForceEnglish() => GameLocalization.Set(GameLanguage.English);
}
