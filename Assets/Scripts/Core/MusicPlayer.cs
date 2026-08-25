using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton persistente (DontDestroyOnLoad) que reproduce la música de
/// fondo con crossfade entre dos AudioSource. Se auto-inicializa al arrancar
/// sin necesidad de ponerlo en ninguna escena, igual que otros bootstraps
/// del proyecto (ver NetworkLauncher).
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    private const string IntroMusicResource = "Audio/Music/Botanical_Decay";
    private const string ArenaMusicResource = "Audio/Music/TreeOfAbundance";

    public static MusicPlayer Instance { get; private set; }

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;
    private Coroutine fadeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("MusicPlayer");
        Object.DontDestroyOnLoad(go);
        Instance = go.AddComponent<MusicPlayer>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        AudioMixerGroup musicGroup = LoadMusicGroup();

        sourceA = gameObject.AddComponent<AudioSource>();
        sourceB = gameObject.AddComponent<AudioSource>();
        foreach (AudioSource source in new[] { sourceA, sourceB })
        {
            source.loop = true;
            source.playOnAwake = false;
            source.outputAudioMixerGroup = musicGroup;
        }
        activeSource = sourceA;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => PlayForScene(scene);

    public void PlayForScene(Scene scene)
    {
        string resource = scene.name switch
        {
            GameScenes.Intro => IntroMusicResource,
            GameScenes.Arena => ArenaMusicResource,
            "Movement" => ArenaMusicResource,
            _ => null
        };

        if (string.IsNullOrEmpty(resource))
        {
            Stop(0.8f);
            return;
        }

        AudioClip clip = Resources.Load<AudioClip>(resource);
        if (clip != null) PlayMusic(clip, 1.5f);
        else Debug.LogWarning($"[MusicPlayer] No se encontró Resources/{resource}.");
    }

    private static AudioMixerGroup LoadMusicGroup()
    {
        AudioMixer mixer = Resources.Load<AudioMixer>("Audio/MasterMixer");
        if (mixer == null) return null;

        AudioMixerGroup[] groups = mixer.FindMatchingGroups("Music");
        return groups.Length > 0 ? groups[0] : null;
    }

    public void PlayMusic(AudioClip clip, float fadeSeconds = 1.5f)
    {
        if (clip == null) return;
        if (activeSource.clip == clip)
        {
            if (!activeSource.isPlaying)
            {
                activeSource.volume = 1f;
                activeSource.Play();
            }
            return;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(CrossfadeTo(clip, fadeSeconds));
    }

    public void Stop(float fadeSeconds = 1f)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutAndStop(fadeSeconds));
    }

    private IEnumerator CrossfadeTo(AudioClip clip, float fadeSeconds)
    {
        AudioSource incoming = activeSource == sourceA ? sourceB : sourceA;
        AudioSource outgoing = activeSource;

        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.Play();

        float t = 0f;
        float outgoingStartVolume = outgoing.volume;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            float ratio = fadeSeconds > 0f ? Mathf.Clamp01(t / fadeSeconds) : 1f;
            incoming.volume = ratio;
            outgoing.volume = outgoingStartVolume * (1f - ratio);
            yield return null;
        }

        incoming.volume = 1f;
        outgoing.Stop();
        activeSource = incoming;
        fadeRoutine = null;
    }

    private IEnumerator FadeOutAndStop(float fadeSeconds)
    {
        float startVolume = activeSource.volume;
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            activeSource.volume = startVolume * (1f - Mathf.Clamp01(t / fadeSeconds));
            yield return null;
        }

        activeSource.Stop();
        fadeRoutine = null;
    }
}
