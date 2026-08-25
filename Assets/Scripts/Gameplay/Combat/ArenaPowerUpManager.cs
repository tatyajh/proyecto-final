using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum ArenaPickupType
{
    Vitality = 0,
    Haste = 1,
    Power = 2
}

/// <summary>
/// Presenta los cuatro altares tanto en entrenamiento como en red. En Shared
/// Mode lee y reclama el estado replicado por el PlayerController coordinador;
/// localmente conserva exactamente las mismas reglas y posiciones.
/// </summary>
[DisallowMultipleComponent]
public sealed class ArenaPowerUpManager : MonoBehaviour
{
    private static readonly Color[] PickupColors =
    {
        new Color(0.30f, 0.90f, 0.48f, 1f),
        new Color(0.34f, 0.68f, 1f, 1f),
        new Color(0.94f, 0.58f, 0.18f, 1f)
    };

    private sealed class PedestalVisual
    {
        public GameObject Root;
        public Renderer Bloom;
        public LineRenderer Ring;
    }

    public static ArenaPowerUpManager Instance { get; private set; }

    private readonly PedestalVisual[] visuals = new PedestalVisual[4];
    private readonly Dictionary<PlayerController, float> corruptionDamageAt = new Dictionary<PlayerController, float>();
    private PlayerController owner;
    private PlayerController trainingOpponent;
    private PlayerController coordinator;
    private Vector3 arenaCenter;
    private int localMask;
    private int localTypes;
    private int localGeneration;
    private float nextLocalSpawnAt;
    private float localRoundStartedAt;
    private int requestedGeneration = -1;
    private int requestedPedestal = -1;
    private bool visualsBuilt;
    private bool presentationSuppressed;
    private LineRenderer corruptionRing;

    public bool CorruptionActive { get; private set; }
    public float CorruptionProgress { get; private set; }
    public float SafeRadius => Mathf.Lerp(20f, 6f, CorruptionProgress);
    public string StatusText => CorruptionActive
        ? GameLocalization.Choose($"CORRUPCIÓN · RADIO {SafeRadius:0}", $"CORRUPTION · RADIUS {SafeRadius:0}")
        : string.Empty;

    public static ArenaPowerUpManager EnsureFor(PlayerController localPlayer)
    {
        if (localPlayer == null || !localPlayer.HasLocalControl) return null;
        ArenaPowerUpManager manager = FindFirstObjectByType<ArenaPowerUpManager>();
        if (manager == null)
        {
            GameObject host = new GameObject("Arena Power Ups");
            manager = host.AddComponent<ArenaPowerUpManager>();
        }
        manager.owner = localPlayer;
        manager.arenaCenter = PlayerSpawner.ArenaCenter;
        return manager;
    }

    private void Awake()
    {
        Instance = this;
        localRoundStartedAt = Time.unscaledTime;
        nextLocalSpawnAt = localRoundStartedAt + 15f;
    }

    public void ConfigureTraining(PlayerController human, PlayerController opponent, Vector3 center)
    {
        owner = human;
        trainingOpponent = opponent;
        arenaCenter = ResolveGround(center);
        localMask = 0;
        localTypes = 0;
        localGeneration = 0;
        localRoundStartedAt = Time.unscaledTime;
        nextLocalSpawnAt = localRoundStartedAt + 15f;
        corruptionDamageAt.Clear();
        CorruptionActive = false;
        CorruptionProgress = 0f;
        requestedGeneration = -1;
        requestedPedestal = -1;
        BuildVisuals();
        RefreshVisualTransforms();
    }

    private void Update()
    {
        if (owner == null || !owner.HasLocalControl) return;
        BuildVisuals();

        if (owner.IsOnlinePlayer) UpdateOnline();
        else UpdateTraining();

        RefreshVisualState();
        UpdateCorruptionRing();
    }

    private void UpdateOnline()
    {
        arenaCenter = PlayerSpawner.ArenaCenter;
        if (coordinator == null || !coordinator.IsNetworkMatchParticipant)
        {
            foreach (PlayerController candidate in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (!candidate.IsNetworkMatchParticipant || !candidate.ArenaSystemsInitialized) continue;
                coordinator = candidate;
                break;
            }
        }
        if (coordinator == null) return;

        CorruptionActive = coordinator.ArenaCorruptionActive;
        CorruptionProgress = coordinator.ArenaCorruptionProgress;
        TryClaimOnline(owner, coordinator.ArenaPickupMask, coordinator.ArenaPickupGeneration);
        ApplyCorruptionIfNeeded(owner);
    }

    private void UpdateTraining()
    {
        float elapsed = Time.unscaledTime - localRoundStartedAt;
        if (CountBits(localMask) < 1 && Time.unscaledTime >= nextLocalSpawnAt)
            SpawnLocalPickup();

        if (!CorruptionActive && elapsed >= 180f) CorruptionActive = true;
        if (CorruptionActive)
            CorruptionProgress = Mathf.Clamp01((elapsed - 180f) / 45f);

        TryClaimLocal(owner);
        TryClaimLocal(trainingOpponent);
        ApplyCorruptionIfNeeded(owner);
        ApplyCorruptionIfNeeded(trainingOpponent);
    }

    private void SpawnLocalPickup()
    {
        localGeneration++;
        int pedestal = localGeneration % 4;
        int type = (localGeneration + pedestal) % 3;
        localMask = 1 << pedestal;
        localTypes = (localTypes & ~(0x3 << (pedestal * 2))) | (type << (pedestal * 2));
        nextLocalSpawnAt = float.PositiveInfinity;
    }

    private void TryClaimLocal(PlayerController participant)
    {
        if (participant == null || participant.IsDefeated) return;
        for (int i = 0; i < 4; i++)
        {
            if ((localMask & (1 << i)) == 0) continue;
            if (Vector3.Distance(participant.transform.position, PedestalPosition(arenaCenter, i)) > 2.4f) continue;
            participant.GrantArenaPickup(LocalTypeAt(i));
            localMask &= ~(1 << i);
            nextLocalSpawnAt = Time.unscaledTime + 20f;
            break;
        }
    }

    private void TryClaimOnline(PlayerController participant, int mask, int generation)
    {
        if (participant == null || participant.IsDefeated || coordinator == null) return;
        if (generation != requestedGeneration) { requestedGeneration = -1; requestedPedestal = -1; }
        for (int i = 0; i < 4; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            if (Vector3.Distance(participant.transform.position, PedestalPosition(arenaCenter, i)) > 2.4f) continue;
            if (requestedGeneration == generation && requestedPedestal == i) return;
            requestedGeneration = generation;
            requestedPedestal = i;
            coordinator.RequestArenaPickupClaim(i, generation, participant.Object.InputAuthority.PlayerId);
            return;
        }
    }

    private void ApplyCorruptionIfNeeded(PlayerController participant)
    {
        if (!CorruptionActive || participant == null || participant.IsDefeated) return;
        if (corruptionDamageAt.TryGetValue(participant, out float nextAt) && Time.unscaledTime < nextAt) return;
        Vector3 planar = participant.transform.position - arenaCenter;
        planar.y = 0f;
        if (planar.magnitude <= SafeRadius) return;

        int damage = Mathf.CeilToInt(PlayerController.MaxHealth * Mathf.Lerp(0.02f, 0.08f, CorruptionProgress));
        participant.ApplyCorruptionDamage(damage);
        corruptionDamageAt[participant] = Time.unscaledTime + 1f;
    }

    public bool TryGetBestPickup(PlayerController seeker, out Vector3 position)
    {
        position = Vector3.zero;
        if (seeker == null) return false;
        int mask = CurrentMask;
        float bestScore = float.MaxValue;
        bool found = false;
        for (int i = 0; i < 4; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            ArenaPickupType type = CurrentTypeAt(i);
            float distance = Vector3.Distance(seeker.transform.position, PedestalPosition(arenaCenter, i));
            float priority = type == ArenaPickupType.Vitality &&
                             seeker.CurrentHealth <= PlayerController.MaxHealth * 0.55f ? -12f : 0f;
            float score = distance + priority;
            if (score >= bestScore) continue;
            bestScore = score;
            position = PedestalPosition(arenaCenter, i);
            found = true;
        }
        return found;
    }

    private int CurrentMask => owner != null && owner.IsOnlinePlayer && coordinator != null
        ? coordinator.ArenaPickupMask
        : localMask;

    private ArenaPickupType CurrentTypeAt(int pedestal)
    {
        return owner != null && owner.IsOnlinePlayer && coordinator != null
            ? coordinator.NetworkPickupTypeAt(pedestal)
            : LocalTypeAt(pedestal);
    }

    private ArenaPickupType LocalTypeAt(int pedestal)
    {
        return (ArenaPickupType)((localTypes >> (pedestal * 2)) & 0x3);
    }

    private void BuildVisuals()
    {
        if (visualsBuilt) return;
        visualsBuilt = true;
        for (int i = 0; i < visuals.Length; i++)
        {
            GameObject root = new GameObject($"Power-up altar {i + 1}");
            root.transform.SetParent(transform, false);

            GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObject.name = "Pedestal";
            baseObject.transform.SetParent(root.transform, false);
            baseObject.transform.localScale = new Vector3(1.25f, 0.12f, 1.25f);
            Collider collider = baseObject.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            SetMaterial(baseObject.GetComponent<Renderer>(), new Color(0.12f, 0.07f, 0.11f, 1f));

            GameObject bloom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bloom.name = "Primordial bloom";
            bloom.transform.SetParent(root.transform, false);
            bloom.transform.localPosition = Vector3.up * 1.05f;
            bloom.transform.localScale = Vector3.one * 0.72f;
            collider = bloom.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            LineRenderer ring = root.AddComponent<LineRenderer>();
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.widthMultiplier = 0.08f;
            ring.positionCount = 48;
            for (int p = 0; p < ring.positionCount; p++)
            {
                float angle = p * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(p, new Vector3(Mathf.Cos(angle) * 1.25f, 0.14f, Mathf.Sin(angle) * 1.25f));
            }
            visuals[i] = new PedestalVisual { Root = root, Bloom = bloom.GetComponent<Renderer>(), Ring = ring };
        }
        RefreshVisualTransforms();
    }

    private void RefreshVisualTransforms()
    {
        for (int i = 0; i < visuals.Length; i++)
            if (visuals[i]?.Root != null) visuals[i].Root.transform.position = PedestalPosition(arenaCenter, i);
    }

    private void RefreshVisualState()
    {
        int mask = CurrentMask;
        for (int i = 0; i < visuals.Length; i++)
        {
            PedestalVisual visual = visuals[i];
            if (visual == null) continue;
            visual.Root.transform.position = PedestalPosition(arenaCenter, i);
            bool active = (mask & (1 << i)) != 0 && !CorruptionActive;
            // El altar permanece como punto de referencia para recorrer la
            // arena. Solo la flor y su halo desaparecen mientras recarga.
            visual.Root.SetActive(true);
            if (visual.Bloom != null) visual.Bloom.gameObject.SetActive(active);
            if (visual.Ring != null) visual.Ring.enabled = active;
            if (!active) continue;
            Color color = PickupColors[(int)CurrentTypeAt(i)];
            SetMaterial(visual.Bloom, color);
            visual.Ring.startColor = color;
            visual.Ring.endColor = color;
            if (visual.Ring.sharedMaterial == null)
                visual.Ring.sharedMaterial = NewMaterial(color);
        }
    }

    private void UpdateCorruptionRing()
    {
        if (corruptionRing == null)
        {
            GameObject root = new GameObject("Corruption boundary");
            root.transform.SetParent(transform, false);
            corruptionRing = root.AddComponent<LineRenderer>();
            corruptionRing.loop = true;
            corruptionRing.useWorldSpace = true;
            corruptionRing.widthMultiplier = 0.18f;
            corruptionRing.positionCount = 96;
            Color color = new Color(0.80f, 0.10f, 0.38f, 0.9f);
            corruptionRing.startColor = color;
            corruptionRing.endColor = color;
            corruptionRing.sharedMaterial = NewMaterial(color);
        }
        corruptionRing.gameObject.SetActive(CorruptionActive && !presentationSuppressed);
        if (!CorruptionActive || presentationSuppressed) return;
        for (int i = 0; i < corruptionRing.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / corruptionRing.positionCount;
            corruptionRing.SetPosition(i, arenaCenter + new Vector3(Mathf.Cos(angle) * SafeRadius,
                0.25f, Mathf.Sin(angle) * SafeRadius));
        }
    }

    /// <summary>
    /// Oculta únicamente la presentación de la Corrupción detrás de modales.
    /// La simulación y el daño continúan siendo autoridad de la arena.
    /// </summary>
    public void SetPresentationSuppressed(bool value)
    {
        presentationSuppressed = value;
        if (corruptionRing != null)
            corruptionRing.gameObject.SetActive(CorruptionActive && !presentationSuppressed);
    }

    public static Vector3 PedestalPosition(Vector3 center, int index)
    {
        float angle = 45f + Mathf.Clamp(index, 0, 3) * 90f;
        Vector3 desired = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 8.5f;
        return NavMesh.SamplePosition(desired, out NavMeshHit hit, 3f, NavMesh.AllAreas)
            ? hit.position
            : desired;
    }

    private static Vector3 ResolveGround(Vector3 desired)
    {
        return NavMesh.SamplePosition(desired, out NavMeshHit hit, 8f, NavMesh.AllAreas)
            ? hit.position
            : desired;
    }

    private static void SetMaterial(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        Material material = renderer.sharedMaterial;
        if (material == null || !material.name.StartsWith("Arena pickup"))
            renderer.sharedMaterial = NewMaterial(color);
        else
        {
            material.color = color;
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 0.4f);
        }
    }

    private static Material NewMaterial(Color color)
    {
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
        Material material = new Material(shader) { name = "Arena pickup runtime", color = color };
        material.EnableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 0.4f);
        return material;
    }

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0) { count += value & 1; value >>= 1; }
        return count;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
