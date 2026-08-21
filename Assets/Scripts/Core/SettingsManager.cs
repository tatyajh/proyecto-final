using UnityEngine;

/// <summary>
/// Modelo central de ajustes (spec 04 del paquete de menú): un solo punto que
/// persiste y APLICA cada preferencia, en vez de que cada control de la UI
/// escriba PlayerPrefs por su cuenta y aplique (o no) el efecto real.
///
/// Volumen: el proyecto no tiene AudioMixer todavía, pero el volumen General
/// sí es funcional desde ya vía AudioListener.volume — es global y no requiere
/// mixer. Música y Efectos se persisten y quedarán conectados al mixer cuando
/// exista audio por canales; mientras tanto el aviso de la UI lo dice honesto.
///
/// Todo se aplica al arrancar (RuntimeInitializeOnLoadMethod): sin eso, un
/// volumen o calidad guardados en la sesión anterior no surtían efecto hasta
/// abrir Ajustes de nuevo.
/// </summary>
public static class SettingsManager
{
    private const string MasterKey = "VolumeMaster";
    private const string MusicKey = "VolumeMusic";
    private const string SfxKey = "VolumeSfx";
    private const string QualityKey = "QualityLevel";

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterKey, 0.8f);
        set
        {
            PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            AudioListener.volume = Mathf.Clamp01(value);
        }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicKey, 0.8f);
        set
        {
            PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            // Pendiente de AudioMixer: no hay canal de música que controlar aún.
        }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
        set
        {
            PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            // Pendiente de AudioMixer: no hay canal de efectos que controlar aún.
        }
    }

    public static int QualityLevel
    {
        get => PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());
        set
        {
            PlayerPrefs.SetInt(QualityKey, value);
            PlayerPrefs.Save();
            QualitySettings.SetQualityLevel(value, true);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedSettings()
    {
        AudioListener.volume = MasterVolume;

        // Solo si difiere: SetQualityLevel(applyExpensiveChanges: true) no es
        // gratis y el nivel por defecto del build suele ser ya el correcto.
        int saved = PlayerPrefs.GetInt(QualityKey, -1);
        if (saved >= 0 && saved != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(saved, true);
    }
}
