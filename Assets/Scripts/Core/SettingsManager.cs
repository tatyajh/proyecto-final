using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Modelo central de ajustes (spec 04 del paquete de menú): un solo punto que
/// persiste y APLICA cada preferencia, en vez de que cada control de la UI
/// escriba PlayerPrefs por su cuenta y aplique (o no) el efecto real.
///
/// Volumen: General sigue vía AudioListener.volume — es global y no requiere
/// mixer. Música y Efectos se aplican al AudioMixer en Resources/Audio, si
/// existe; si todavía no lo pusieron en el proyecto, quedan solo persistidos
/// (null-tolerante) para no romper el arranque.
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

    private static AudioMixer mixer;
    private static AudioMixer Mixer => mixer != null ? mixer : (mixer = Resources.Load<AudioMixer>("Audio/MasterMixer"));

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
            ApplyMixerVolume("MusicVolume", value);
        }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
        set
        {
            PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            ApplyMixerVolume("SfxVolume", value);
        }
    }

    /// <summary>
    /// Null-tolerante a propósito: hasta que el .mixer exista en
    /// Resources/Audio (paso manual de Editor), esto no hace nada en vez de
    /// tirar una excepción al arrancar.
    /// </summary>
    private static void ApplyMixerVolume(string exposedParam, float linear01)
    {
        if (Mixer == null) return;
        float db = linear01 > 0.0001f ? Mathf.Log10(linear01) * 20f : -80f;
        Mixer.SetFloat(exposedParam, db);
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
        ApplyMixerVolume("MusicVolume", MusicVolume);
        ApplyMixerVolume("SfxVolume", SfxVolume);

        // Solo si difiere: SetQualityLevel(applyExpensiveChanges: true) no es
        // gratis y el nivel por defecto del build suele ser ya el correcto.
        int saved = PlayerPrefs.GetInt(QualityKey, -1);
        if (saved >= 0 && saved != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(saved, true);
    }
}
