using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Punto único para disparar sonidos sueltos (UI, impactos de habilidad).
/// Mismo patrón que CharacterCatalog: lookup por Resources, null-tolerante
/// para que el juego funcione aunque todavía no se haya importado el clip
/// ni exista el AudioMixer.
/// </summary>
public static class AudioCatalog
{
    private static AudioClip uiClick;
    private static bool uiClickLoaded;
    private static AudioMixerGroup sfxGroup;
    private static bool sfxGroupLoaded;

    public static AudioMixerGroup SfxGroup => GetSfxGroup();

    public static void PlayUiClick()
    {
        if (!uiClickLoaded)
        {
            uiClick = Resources.Load<AudioClip>("Audio/SFX/ui_click");
            uiClickLoaded = true;
        }

        if (uiClick != null) PlayOneShot(uiClick, Vector3.zero);
    }

    public static void PlayOneShot(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null) return;

        AudioMixerGroup group = GetSfxGroup();

        var go = new GameObject("OneShotAudio");
        go.transform.position = position;
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = group;
        // Sin mixer todavía, respeta el slider de SFX a mano para no sonar
        // siempre a volumen fijo mientras el .mixer no exista en el proyecto.
        source.volume = group != null ? volumeScale : volumeScale * SettingsManager.SfxVolume;
        source.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }

    private static AudioMixerGroup GetSfxGroup()
    {
        if (sfxGroupLoaded) return sfxGroup;
        sfxGroupLoaded = true;

        AudioMixer mixer = Resources.Load<AudioMixer>("Audio/MasterMixer");
        if (mixer == null) return null;

        AudioMixerGroup[] groups = mixer.FindMatchingGroups("SFX");
        sfxGroup = groups.Length > 0 ? groups[0] : null;
        return sfxGroup;
    }
}
