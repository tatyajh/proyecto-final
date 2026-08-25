using Gameplay.Combat;
using UnityEngine;

/// <summary>
/// Soft lock exclusivamente local. Decide el objetivo, dibuja la marca y
/// ofrece una corrección angular leve. Fusion solo recibe el AimData final.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatTargetingController : MonoBehaviour
{
    private const float AcquireRange = 20f;
    private const float ReleaseRange = 24f;
    private const float OcclusionGrace = 1.5f;
    private const float AssistCone = 15f;

    private PlayerController owner;
    private PlayerController lockedTarget;
    private float occludedSince = -1f;
    private GameObject marker;
    private LineRenderer markerLine;

    public PlayerController LockedTarget => lockedTarget;
    public Transform LockedTargetTransform => lockedTarget != null ? lockedTarget.transform : null;
    public bool HasTarget => lockedTarget != null;
    public string TargetName => lockedTarget != null ? lockedTarget.PlayerDisplayName : string.Empty;

    public static CombatTargetingController EnsureFor(PlayerController player)
    {
        if (player == null || !player.HasLocalControl) return null;
        CombatTargetingController controller = player.GetComponent<CombatTargetingController>();
        if (controller == null) controller = player.gameObject.AddComponent<CombatTargetingController>();
        controller.Bind(player);
        return controller;
    }

    public void Bind(PlayerController player)
    {
        owner = player;
        MobaCamera camera = FindFirstObjectByType<MobaCamera>();
        if (camera != null) camera.BindTargeting(this);
    }

    private void Awake()
    {
        if (owner == null) owner = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (owner == null || !owner.HasLocalControl) return;
        if (Input.GetKeyDown(KeyCode.Tab)) ToggleLock();

        if (lockedTarget == null) return;
        if (!IsValidEnemy(lockedTarget, ReleaseRange)) { ClearLock(); return; }

        if (HasLineOfSight(lockedTarget)) occludedSince = -1f;
        else
        {
            if (occludedSince < 0f) occludedSince = Time.unscaledTime;
            if (Time.unscaledTime - occludedSince >= OcclusionGrace) { ClearLock(); return; }
        }
        UpdateMarker();
    }

    public void ToggleLock()
    {
        if (lockedTarget != null) { ClearLock(); return; }
        PlayerController best = FindBestTarget();
        if (best != null) SetLockedTarget(best);
    }

    public Vector3 AssistDirection(Vector3 requestedDirection, AbilitySlot slot)
    {
        Vector3 requested = Vector3.ProjectOnPlane(requestedDirection, Vector3.up);
        if (requested.sqrMagnitude < 0.001f) requested = transform.forward;
        requested.Normalize();
        if (lockedTarget == null || !HasLineOfSight(lockedTarget)) return requested;

        Vector3 toTarget = Vector3.ProjectOnPlane(
            VisualCenter(lockedTarget.transform) - VisualCenter(transform), Vector3.up);
        if (toTarget.sqrMagnitude < 0.001f) return requested;
        toTarget.Normalize();
        if (Vector3.Angle(requested, toTarget) > AssistCone) return requested;

        float strength = slot == AbilitySlot.Basic ? 0.35f : 0.20f;
        return Vector3.Slerp(requested, toTarget, strength).normalized;
    }

    private PlayerController FindBestTarget()
    {
        Camera camera = Camera.main;
        PlayerController best = null;
        float bestScore = float.MaxValue;
        foreach (PlayerController candidate in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!IsValidEnemy(candidate, AcquireRange) || !HasLineOfSight(candidate)) continue;
            Vector3 viewport = camera != null
                ? camera.WorldToViewportPoint(VisualCenter(candidate.transform))
                : new Vector3(0.5f, 0.5f, 1f);
            if (viewport.z <= 0f || viewport.x < -0.1f || viewport.x > 1.1f ||
                viewport.y < -0.1f || viewport.y > 1.1f) continue;

            float centerBias = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f).sqrMagnitude * 12f;
            float score = Vector3.Distance(transform.position, candidate.transform.position) + centerBias;
            if (score >= bestScore) continue;
            bestScore = score;
            best = candidate;
        }
        return best;
    }

    private bool IsValidEnemy(PlayerController candidate, float range)
    {
        if (candidate == null || candidate == owner || candidate.IsDefeated ||
            !candidate.IsCombatParticipant || owner.IsAllyOf(candidate)) return false;
        Vector3 delta = candidate.transform.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= range * range;
    }

    private bool HasLineOfSight(PlayerController candidate)
    {
        if (candidate == null) return false;
        // El árbol usa colliders de navegación enormes que no representan lo
        // que realmente ve el jugador. Para el soft-lock, "visible" significa
        // estar dentro de la pantalla; la resolución del ataque conserva sus
        // propias reglas y alcance. Así Tab no falla por una rama decorativa.
        Camera camera = Camera.main;
        if (camera == null) return true;
        Vector3 viewport = camera.WorldToViewportPoint(VisualCenter(candidate.transform));
        return viewport.z > 0f && viewport.x >= -0.08f && viewport.x <= 1.08f &&
               viewport.y >= -0.08f && viewport.y <= 1.08f;
    }

    private void SetLockedTarget(PlayerController candidate)
    {
        lockedTarget = candidate;
        occludedSince = -1f;
        EnsureMarker();
        marker.SetActive(true);
        UpdateMarker();
        FindFirstObjectByType<MobaCamera>()?.RequestImmediateReframe();
    }

    public void ClearLock()
    {
        lockedTarget = null;
        occludedSince = -1f;
        if (marker != null) marker.SetActive(false);
        FindFirstObjectByType<MobaCamera>()?.RequestImmediateReframe();
    }

    private void EnsureMarker()
    {
        if (marker != null) return;
        marker = new GameObject("Soft Lock Target Marker");
        markerLine = marker.AddComponent<LineRenderer>();
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        markerLine.material = new Material(shader) { color = new Color(0.95f, 0.68f, 0.22f, 0.95f) };
        markerLine.startColor = new Color(0.95f, 0.68f, 0.22f, 0.95f);
        markerLine.endColor = markerLine.startColor;
        markerLine.widthMultiplier = 0.12f;
        markerLine.loop = true;
        markerLine.useWorldSpace = false;
        markerLine.positionCount = 48;
        for (int i = 0; i < markerLine.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / markerLine.positionCount;
            markerLine.SetPosition(i, new Vector3(Mathf.Cos(angle) * 1.35f, 0f, Mathf.Sin(angle) * 1.35f));
        }
    }

    private void UpdateMarker()
    {
        if (marker == null || lockedTarget == null) return;
        marker.transform.position = lockedTarget.transform.position + Vector3.up * 0.08f;
        marker.transform.rotation = Quaternion.identity;
    }

    private static Vector3 VisualCenter(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer is LineRenderer || renderer is ParticleSystemRenderer) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found ? bounds.center : root.position + Vector3.up * 2f;
    }

    private void OnDestroy()
    {
        if (marker != null) Destroy(marker);
    }
}
