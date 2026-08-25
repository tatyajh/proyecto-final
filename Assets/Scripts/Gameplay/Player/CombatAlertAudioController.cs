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
    private Vector3 previousEnemyPosition;
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

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = AudioCatalog.SfxGroup;
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

        float healthRatio = owner.CurrentHealth / (float)PlayerController.MaxHealth;
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

        if (nearest == null || nearestDistance > 16f)
        {
            approachArmed = true;
            approachingFor = 0f;
            trackedEnemy = null;
            previousSampleAt = now;
            return;
        }

        if (nearest != trackedEnemy)
        {
            trackedEnemy = nearest;
            previousEnemyPosition = nearest.transform.position;
            previousSampleAt = now;
            approachingFor = 0f;
            return;
        }

        float deltaTime = Mathf.Max(0.001f, now - previousSampleAt);
        Vector3 velocity = (nearest.transform.position - previousEnemyPosition) / deltaTime;
        Vector3 towardOwner = owner.transform.position - nearest.transform.position;
        towardOwner.y = 0f;
        float closingSpeed = towardOwner.sqrMagnitude > 0.001f
            ? Vector3.Dot(velocity, towardOwner.normalized)
            : 0f;
        previousEnemyPosition = nearest.transform.position;
        previousSampleAt = now;

        if (approachArmed && nearestDistance <= 12f && closingSpeed >= 0.35f)
            approachingFor += deltaTime;
        else
            approachingFor = 0f;

        if (!approachArmed || approachingFor < 0.4f || now < approachAllowedAt || source.isPlaying) return;
        PlayPriority(approachClip, 0.85f, false);
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
}
