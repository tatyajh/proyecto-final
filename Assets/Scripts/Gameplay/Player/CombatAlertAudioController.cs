using UnityEngine;

/// <summary>
/// Canal único de avisos del combatiente local. Las señales de resultado
/// interrumpen vida crítica y proximidad, evitando tres AudioSource superpuestos.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatAlertAudioController : MonoBehaviour
{
    private const float CriticalThreshold = 0.25f;
    private const float CriticalRearmThreshold = 0.40f;
    private const float ApproachWarningRadius = 15f;
    private const float ApproachRearmRadius = 19f;
    private const float HardApproachRadius = 11f;
    private const float MinimumClosingSpeed = 0.12f;
    private const float SustainedApproachSeconds = 0.20f;
    private const float AlertMusicLevel = 0.28f;

    private PlayerController owner;
    private AudioSource source;
    private AudioClip approachClip;
    private AudioClip lowHealthClip;
    private AudioClip defeatClip;
    private AudioClip victoryClip;
    private bool resultPlayed;
    private bool lowHealthArmed = true;
    private bool approachArmed = true;
    private PlayerController trackedEnemy;
    private float previousEnemyDistance;
    private bool hasPreviousEnemyDistance;
    private float previousSampleAt;
    private float nextSampleAt;
    private float approachingFor;
    private float approachAllowedAt;

    public static CombatAlertAudioController EnsureFor(PlayerController localPlayer)
    {
        if (localPlayer == null || !localPlayer.HasLocalControl) return null;
        CombatAlertAudioController controller = localPlayer.GetComponent<CombatAlertAudioController>();
        if (controller == null) controller = localPlayer.gameObject.AddComponent<CombatAlertAudioController>();
        controller.owner = localPlayer;
        return controller;
    }

    private void Awake()
    {
        owner ??= GetComponent<PlayerController>();
        approachClip = Resources.Load<AudioClip>("Audio/SFX/enemy_approach");
        lowHealthClip = Resources.Load<AudioClip>("Audio/SFX/low_health");
        defeatClip = Resources.Load<AudioClip>("Audio/SFX/game_over");
        victoryClip = Resources.Load<AudioClip>("Audio/SFX/victory");
        Preload(approachClip, "enemy_approach");
        Preload(lowHealthClip, "low_health");
        Preload(defeatClip, "game_over");
        Preload(victoryClip, "victory");

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.priority = 16;
        source.ignoreListenerPause = true;
        source.outputAudioMixerGroup = AudioCatalog.SfxGroup;
    }

    /// <summary>
    /// Rehabilita las alertas al comenzar una ronda, revancha o nuevo rival.
    /// La torre reutiliza al jugador entre combates, por lo que Awake no vuelve
    /// a ejecutarse y el estado de resultado debe limpiarse explícitamente.
    /// </summary>
    public void ResetForRound()
    {
        resultPlayed = false;
        lowHealthArmed = true;
        approachArmed = true;
        trackedEnemy = null;
        hasPreviousEnemyDistance = false;
        previousEnemyDistance = float.PositiveInfinity;
        previousSampleAt = Time.unscaledTime;
        nextSampleAt = 0f;
        approachingFor = 0f;
        approachAllowedAt = 0f;
        if (source != null) source.Stop();
        MusicPlayer.Instance?.SetCombatAlertDuck(false);
    }

    private void Update()
    {
        if (owner == null || !owner.HasLocalControl) return;

        if (!resultPlayed && (owner.HasWon || owner.HasLost))
        {
            resultPlayed = true;
            PlayPriority(owner.HasWon ? victoryClip : defeatClip, 1f, true);
            return;
        }
        if (resultPlayed) return;

        float healthRatio = owner.CurrentHealth / (float)Mathf.Max(1, owner.HealthMaximum);
        if (healthRatio > CriticalRearmThreshold) lowHealthArmed = true;
        if (lowHealthArmed && healthRatio > 0f && healthRatio <= CriticalThreshold)
        {
            lowHealthArmed = false;
            PlayPriority(lowHealthClip, 0.92f, true);
            return;
        }

        if (owner.IsOnlinePlayer && !OnlineMatchState.CanPlay) return;
        UpdateApproachWarning();
    }

    private void UpdateApproachWarning()
    {
        float now = Time.unscaledTime;
        if (now < nextSampleAt) return;
        nextSampleAt = now + 0.1f;
        PlayerController nearest = FindNearestEnemy(out float nearestDistance);

        if (nearest == null || nearestDistance > ApproachRearmRadius)
        {
            approachArmed = true;
            approachingFor = 0f;
            trackedEnemy = null;
            hasPreviousEnemyDistance = false;
            previousSampleAt = now;
            return;
        }

        if (nearest != trackedEnemy)
        {
            trackedEnemy = nearest;
            previousEnemyDistance = nearestDistance;
            hasPreviousEnemyDistance = true;
            previousSampleAt = now;
            approachingFor = 0f;
        }

        float deltaTime = Mathf.Max(0.001f, now - previousSampleAt);
        float closingSpeed = hasPreviousEnemyDistance
            ? (previousEnemyDistance - nearestDistance) / deltaTime
            : 0f;
        previousEnemyDistance = nearestDistance;
        hasPreviousEnemyDistance = true;
        previousSampleAt = now;

        if (approachArmed && nearestDistance <= ApproachWarningRadius &&
            (closingSpeed >= MinimumClosingSpeed || nearestDistance <= HardApproachRadius))
            approachingFor += deltaTime;
        else
            approachingFor = 0f;

        if (!approachArmed || approachingFor < SustainedApproachSeconds ||
            now < approachAllowedAt || source.isPlaying) return;
        PlayPriority(approachClip, 1f, false);
        approachAllowedAt = now + (approachClip != null ? approachClip.length : 8f);
        approachingFor = 0f;
        approachArmed = false;
    }

    private void PlayPriority(AudioClip clip, float volume, bool interrupt)
    {
        if (clip == null) return;
        if (interrupt) source.Stop();
        else if (source.isPlaying) return;
        source.clip = clip;
        source.volume = source.outputAudioMixerGroup != null ? volume : volume * SettingsManager.SfxVolume;
        source.Play();
        MusicPlayer.Instance?.SetCombatAlertDuck(true, AlertMusicLevel);
        CancelInvoke(nameof(ReleaseMusicDuck));
        Invoke(nameof(ReleaseMusicDuck), Mathf.Max(0.25f, clip.length + 0.15f));
    }

    private void ReleaseMusicDuck() => MusicPlayer.Instance?.SetCombatAlertDuck(false);

    private void OnDisable()
    {
        CancelInvoke(nameof(ReleaseMusicDuck));
        MusicPlayer.Instance?.SetCombatAlertDuck(false);
    }

    private static void Preload(AudioClip clip, string resourceName)
    {
        if (clip == null)
        {
            Debug.LogError($"[CombatAudio] Falta Resources/Audio/SFX/{resourceName}.");
            return;
        }
        if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
            Debug.LogWarning($"[CombatAudio] No fue posible precargar '{resourceName}'.");
    }

    private PlayerController FindNearestEnemy(out float nearestDistance)
    {
        PlayerController nearest = null;
        nearestDistance = float.PositiveInfinity;
        foreach (PlayerController candidate in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (candidate == null || candidate == owner || !candidate.IsCombatParticipant ||
                candidate.IsDefeated || owner.IsAllyOf(candidate)) continue;
            float distance = Vector3.Distance(owner.transform.position, candidate.transform.position);
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = candidate;
        }
        return nearest;
    }

#if UNITY_EDITOR
    public bool DebugIsPlaying => source != null && source.isPlaying;
    public string DebugCurrentAlert => source == null || source.clip == null
        ? string.Empty
        : source.clip == approachClip ? "enemy_approach"
        : source.clip == lowHealthClip ? "low_health"
        : source.clip == defeatClip ? "game_over"
        : source.clip == victoryClip ? "victory"
        : source.clip.name;
    public bool DebugResultPlayed => resultPlayed;
    public void DebugPlayApproachAlert() => PlayPriority(approachClip, 1f, false);
#endif
}
