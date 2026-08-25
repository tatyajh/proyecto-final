using UnityEngine;

/// <summary>
/// Aviso 2D único para el jugador local. Observa el movimiento del enemigo,
/// no el del jugador, y evita apilar el mismo drone en partidas por equipos.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyApproachAudioController : MonoBehaviour
{
    private const string ClipResource = "Audio/SFX/enemy_approach";

    [SerializeField, Min(1f)] private float warningRadius = 12f;
    [SerializeField, Min(1f)] private float rearmRadius = 16f;
    [SerializeField, Min(0f)] private float minimumClosingSpeed = 0.35f;
    [SerializeField, Min(0f)] private float sustainedApproachSeconds = 0.4f;
    [SerializeField, Min(0.02f)] private float sampleInterval = 0.1f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.85f;

    private PlayerController owner;
    private AudioClip warningClip;
    private PlayerController trackedEnemy;
    private Vector3 previousEnemyPosition;
    private float previousSampleAt;
    private float nextSampleAt;
    private float approachingFor;
    private float nextAllowedAt;
    private bool armed = true;

    public static EnemyApproachAudioController EnsureFor(PlayerController localPlayer)
    {
        if (localPlayer == null || !localPlayer.HasLocalControl) return null;
        EnemyApproachAudioController controller = localPlayer.GetComponent<EnemyApproachAudioController>();
        if (controller == null) controller = localPlayer.gameObject.AddComponent<EnemyApproachAudioController>();
        controller.owner = localPlayer;
        return controller;
    }

    private void Awake()
    {
        owner = GetComponent<PlayerController>();
        warningClip = Resources.Load<AudioClip>(ClipResource);
    }

    private void Update()
    {
        if (owner == null || !owner.HasLocalControl || owner.IsDefeated) return;
        if (owner.IsOnlinePlayer && !OnlineMatchState.CanPlay) return;
        if (Time.unscaledTime < nextSampleAt) return;

        float now = Time.unscaledTime;
        nextSampleAt = now + sampleInterval;
        PlayerController nearest = FindNearestEnemy(out float nearestDistance);

        if (nearest == null || nearestDistance > rearmRadius)
        {
            armed = true;
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
        Vector3 enemyVelocity = (nearest.transform.position - previousEnemyPosition) / deltaTime;
        Vector3 towardPlayer = owner.transform.position - nearest.transform.position;
        towardPlayer.y = 0f;
        float closingSpeed = towardPlayer.sqrMagnitude > 0.001f
            ? Vector3.Dot(enemyVelocity, towardPlayer.normalized)
            : 0f;

        previousEnemyPosition = nearest.transform.position;
        previousSampleAt = now;

        if (armed && nearestDistance <= warningRadius && closingSpeed >= minimumClosingSpeed)
            approachingFor += deltaTime;
        else
            approachingFor = 0f;

        if (!armed || approachingFor < sustainedApproachSeconds || now < nextAllowedAt) return;

        if (warningClip != null)
            AudioCatalog.PlayOneShot(warningClip, owner.transform.position, volume);
        nextAllowedAt = now + (warningClip != null ? warningClip.length : 8f);
        approachingFor = 0f;
        armed = false;
    }

    private PlayerController FindNearestEnemy(out float nearestDistance)
    {
        PlayerController nearest = null;
        nearestDistance = float.PositiveInfinity;
        foreach (PlayerController candidate in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (candidate == null || candidate == owner || !candidate.IsCombatParticipant || candidate.IsDefeated) continue;
            if (owner.IsAllyOf(candidate)) continue;

            float distance = Vector3.Distance(owner.transform.position, candidate.transform.position);
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = candidate;
        }
        return nearest;
    }
}
