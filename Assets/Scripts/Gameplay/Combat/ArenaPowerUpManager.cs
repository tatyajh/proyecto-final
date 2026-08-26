using System.Collections.Generic;
using Gameplay.Combat;
using TMPro;
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

    private static readonly string[] PickupSpriteResources =
    {
        "Vfx/Pickups/VitalityBloom",
        "Vfx/Pickups/HasteSeed",
        "Vfx/Pickups/PowerSeed"
    };

    private static readonly string[] PickupNames =
    {
        "Flor de vitalidad",
        "Semilla de celeridad",
        "Semilla de poder"
    };

    private static Sprite[] pickupSprites;

    private sealed class PedestalVisual
    {
        public GameObject Root;
        public GameObject Bud;
        public Renderer BudRenderer;
        public ArenaBudTarget BudTarget;
        public readonly List<Renderer> BudPetals = new List<Renderer>();
        public TextMeshPro BudLabel;
        public LineRenderer Beacon;
        public Transform Symbol;
        public SpriteRenderer SymbolRenderer;
        public TextMeshPro PickupLabel;
        public LineRenderer Ring;
        public bool WasPickupActive;
        public float RevealStartedAt;
    }

    public static ArenaPowerUpManager Instance { get; private set; }

    private readonly PedestalVisual[] visuals = new PedestalVisual[4];
    private readonly Dictionary<PlayerController, float> corruptionDamageAt = new Dictionary<PlayerController, float>();
    private PlayerController owner;
    private PlayerController trainingOpponent;
    private PlayerController coordinator;
    private Vector3 arenaCenter;
    private int localMask;
    private int localBudMask;
    private int localBudHealthBits;
    private int localTypes;
    private int localGeneration;
    private float nextLocalSpawnAt;
    private float localRoundStartedAt;
    private int requestedGeneration = -1;
    private int requestedPedestal = -1;
    private bool visualsBuilt;
    private bool presentationSuppressed;
    private LineRenderer corruptionRing;
    private string pickupToast = string.Empty;
    private float pickupToastUntil;

    public bool CorruptionActive { get; private set; }
    public float CorruptionProgress { get; private set; }
    public float SafeRadius => Mathf.Lerp(20f, 6f, CorruptionProgress);
    public string StatusText
    {
        get
        {
            if (!string.IsNullOrEmpty(pickupToast) && Time.unscaledTime < pickupToastUntil)
                return pickupToast;
            if (CorruptionActive)
                return GameLocalization.Choose($"CORRUPCIÓN · RADIO {SafeRadius:0}",
                    $"CORRUPTION · RADIUS {SafeRadius:0}");
            int buds = CurrentBudMask;
            for (int i = 0; i < 4; i++)
            {
                if ((buds & (1 << i)) == 0) continue;
                int health = Mathf.Max(1, CurrentBudHealthAt(i));
                return GameLocalization.Choose($"CAPULLO CORRUPTO · {health} IMPACTOS",
                    $"CORRUPTED BUD · {health} HITS");
            }
            return string.Empty;
        }
    }

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
        localBudMask = 0;
        localBudHealthBits = 0;
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
        AnimatePowerUpSymbols();
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
        if (CountBits(localMask | localBudMask) < 1 && Time.unscaledTime >= nextLocalSpawnAt)
            SpawnLocalBud();

        if (!CorruptionActive && elapsed >= 180f) CorruptionActive = true;
        if (CorruptionActive)
            CorruptionProgress = Mathf.Clamp01((elapsed - 180f) / 45f);

        TryClaimLocal(owner);
        TryClaimLocal(trainingOpponent);
        ApplyCorruptionIfNeeded(owner);
        ApplyCorruptionIfNeeded(trainingOpponent);
    }

    private void SpawnLocalBud()
    {
        localGeneration++;
        int pedestal = Random.Range(0, 4);
        int type = Random.Range(0, 3);
        localBudMask = 1 << pedestal;
        localBudHealthBits = 3 << (pedestal * 2);
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

    public bool TryGetActiveBud(out Vector3 position, out int remainingHits)
    {
        position = Vector3.zero;
        remainingHits = 0;
        int mask = CurrentBudMask;
        for (int i = 0; i < visuals.Length; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            position = PedestalPosition(arenaCenter, i) + Vector3.up * 1.9f;
            remainingHits = Mathf.Max(1, CurrentBudHealthAt(i));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Los altares primero generan un capullo corrupto. Tres impactos válidos
    /// lo abren y revelan uno de los tres beneficios al azar.
    /// </summary>
    public void TryStrikeBud(AbilityDefinition ability, Vector3 origin, Vector3 direction,
        float travel, Vector3 areaCenter, int attackerPlayerId)
    {
        if (ability == null || CorruptionActive) return;
        int budMask = CurrentBudMask;
        if (budMask == 0) return;

        for (int i = 0; i < 4; i++)
        {
            if ((budMask & (1 << i)) == 0) continue;
            Vector3 budPosition = PedestalPosition(arenaCenter, i) + Vector3.up;
            if (!AbilityReachesPoint(ability, origin, direction, travel, areaCenter, budPosition)) continue;
            visuals[i]?.BudTarget?.FlashHit();

            if (owner != null && owner.IsOnlinePlayer)
            {
                if (coordinator != null)
                    coordinator.RequestArenaBudStrike(i, coordinator.ArenaPickupGeneration, attackerPlayerId);
            }
            else
            {
                int shift = i * 2;
                int health = Mathf.Max(0, ((localBudHealthBits >> shift) & 0x3) - 1);
                localBudHealthBits = (localBudHealthBits & ~(0x3 << shift)) | (health << shift);
                if (health <= 0)
                {
                    localBudMask &= ~(1 << i);
                    localMask |= 1 << i;
                }
            }
            break;
        }
    }

    private static bool AbilityReachesPoint(AbilityDefinition ability, Vector3 origin, Vector3 direction,
        float travel, Vector3 areaCenter, Vector3 point)
    {
        origin.y = areaCenter.y = point.y = 0f;
        direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        switch (ability.shape)
        {
            case AbilityShape.Line:
            case AbilityShape.Dash:
                Vector3 end = origin + direction * travel;
                return DistanceToSegment(point, origin, end) <= Mathf.Max(1.55f, ability.radius);
            case AbilityShape.Cone:
                Vector3 delta = point - origin;
                return delta.magnitude <= ability.range + 1.35f &&
                       Vector3.Angle(direction, delta.normalized) <= ability.coneAngle * 0.5f + 8f;
            case AbilityShape.Area:
            case AbilityShape.Leap:
            case AbilityShape.Wall:
                return Vector3.Distance(point, areaCenter) <= Mathf.Max(1.75f, ability.radius);
            default:
                return false;
        }
    }

    private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.001f) return Vector3.Distance(point, start);
        float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
        return Vector3.Distance(point, start + segment * t);
    }

    private int CurrentMask => owner != null && owner.IsOnlinePlayer && coordinator != null
        ? coordinator.ArenaPickupMask
        : localMask;

    private int CurrentBudMask => owner != null && owner.IsOnlinePlayer && coordinator != null
        ? coordinator.ArenaBudMask
        : localBudMask;

    private int CurrentBudHealthAt(int pedestal)
    {
        return owner != null && owner.IsOnlinePlayer && coordinator != null
            ? coordinator.NetworkBudHealthAt(pedestal)
            : (localBudHealthBits >> (Mathf.Clamp(pedestal, 0, 3) * 2)) & 0x3;
    }

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

            GameObject bud = new GameObject("Capullo corrupto · golpéalo 3 veces");
            bud.name = "Capullo corrupto · golpéalo 3 veces";
            bud.transform.SetParent(root.transform, false);
            bud.transform.localPosition = Vector3.up * 1.36f;

            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Núcleo visible del capullo";
            bulb.transform.SetParent(bud.transform, false);
            bulb.transform.localScale = new Vector3(1.25f, 1.55f, 1.25f);
            collider = bulb.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer budRenderer = bulb.GetComponent<Renderer>();
            SetMaterial(budRenderer, new Color(0.48f, 0.025f, 0.12f, 1f));
            SphereCollider budCollider = bulb.GetComponent<SphereCollider>();
            budCollider.isTrigger = true;
            budCollider.radius = 0.72f;
            ArenaBudTarget budTarget = bulb.AddComponent<ArenaBudTarget>();
            budTarget.Configure(this, i, budRenderer);

            List<Renderer> petals = new List<Renderer>();
            for (int petalIndex = 0; petalIndex < 6; petalIndex++)
            {
                float angle = petalIndex * 60f;
                GameObject petal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                petal.name = $"Pétalo corrupto {petalIndex + 1}";
                petal.transform.SetParent(bud.transform, false);
                petal.transform.localPosition = Quaternion.Euler(0f, angle, 0f) *
                    new Vector3(0f, -0.25f, 0.68f);
                petal.transform.localRotation = Quaternion.Euler(32f, angle, 0f);
                petal.transform.localScale = new Vector3(0.62f, 1.18f, 0.28f);
                Collider petalCollider = petal.GetComponent<Collider>();
                if (petalCollider != null) Destroy(petalCollider);
                Renderer petalRenderer = petal.GetComponent<Renderer>();
                SetMaterial(petalRenderer, new Color(0.66f, 0.035f, 0.18f, 1f));
                petals.Add(petalRenderer);
            }

            GameObject labelObject = new GameObject("Indicador del capullo", typeof(TextMeshPro));
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = Vector3.up * 3.45f;
            TextMeshPro budLabel = labelObject.GetComponent<TextMeshPro>();
            budLabel.text = GameLocalization.Choose("CAPULLO CORRUPTO\n3 GOLPES", "CORRUPTED BUD\n3 HITS");
            budLabel.fontSize = 3.2f;
            budLabel.alignment = TextAlignmentOptions.Center;
            budLabel.color = new Color(1f, 0.78f, 0.48f, 1f);
            budLabel.outlineWidth = 0.22f;
            budLabel.outlineColor = new Color32(55, 3, 15, 255);
            budLabel.rectTransform.sizeDelta = new Vector2(9f, 2.2f);

            GameObject beaconObject = new GameObject("Haz del capullo");
            beaconObject.transform.SetParent(root.transform, false);
            LineRenderer beacon = beaconObject.AddComponent<LineRenderer>();
            beacon.useWorldSpace = false;
            beacon.positionCount = 2;
            beacon.SetPosition(0, new Vector3(0f, 0.18f, 0f));
            beacon.SetPosition(1, new Vector3(0f, 4.7f, 0f));
            beacon.widthMultiplier = 0.07f;
            Color beaconColor = new Color(0.95f, 0.08f, 0.28f, 0.62f);
            beacon.startColor = beaconColor;
            beacon.endColor = new Color(beaconColor.r, beaconColor.g, beaconColor.b, 0f);
            beacon.sharedMaterial = NewMaterial(beaconColor);

            GameObject symbol = new GameObject("Símbolo primordial");
            symbol.transform.SetParent(root.transform, false);
            symbol.transform.localPosition = Vector3.up * 1.18f;
            SpriteRenderer symbolRenderer = symbol.AddComponent<SpriteRenderer>();
            symbolRenderer.color = Color.white;
            symbolRenderer.sortingOrder = 4;
            symbolRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            symbolRenderer.receiveShadows = false;

            GameObject pickupLabelObject = new GameObject("Nombre del beneficio", typeof(TextMeshPro));
            pickupLabelObject.transform.SetParent(root.transform, false);
            pickupLabelObject.transform.localPosition = Vector3.up * 4.25f;
            TextMeshPro pickupLabel = pickupLabelObject.GetComponent<TextMeshPro>();
            pickupLabel.fontSize = 3.4f;
            pickupLabel.alignment = TextAlignmentOptions.Center;
            pickupLabel.color = new Color(1f, 0.92f, 0.68f, 1f);
            pickupLabel.outlineWidth = 0.22f;
            pickupLabel.outlineColor = new Color32(25, 8, 18, 255);
            pickupLabel.rectTransform.sizeDelta = new Vector2(12f, 2.5f);

            // Unity solo admite un LineRenderer por GameObject. El haz y el
            // anillo son piezas independientes para que ambos puedan existir
            // sin dejar el segundo componente nulo durante BuildVisuals.
            GameObject ringObject = new GameObject("Anillo del altar");
            ringObject.transform.SetParent(root.transform, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.widthMultiplier = 0.08f;
            ring.positionCount = 48;
            for (int p = 0; p < ring.positionCount; p++)
            {
                float angle = p * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(p, new Vector3(Mathf.Cos(angle) * 1.25f, 0.14f, Mathf.Sin(angle) * 1.25f));
            }
            visuals[i] = new PedestalVisual
            {
                Root = root,
                Bud = bud,
                BudRenderer = budRenderer,
                BudTarget = budTarget,
                BudLabel = budLabel,
                Beacon = beacon,
                Symbol = symbol.transform,
                SymbolRenderer = symbolRenderer,
                PickupLabel = pickupLabel,
                Ring = ring
            };
            visuals[i].BudPetals.AddRange(petals);
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
        int budMask = CurrentBudMask;
        for (int i = 0; i < visuals.Length; i++)
        {
            PedestalVisual visual = visuals[i];
            if (visual == null) continue;
            visual.Root.transform.position = PedestalPosition(arenaCenter, i);
            bool active = (mask & (1 << i)) != 0 && !CorruptionActive;
            bool budActive = (budMask & (1 << i)) != 0 && !CorruptionActive;
            // El altar permanece como punto de referencia para recorrer la
            // arena. Solo el símbolo botánico y su halo desaparecen al recargar.
            visual.Root.SetActive(true);
            if (visual.Symbol != null) visual.Symbol.gameObject.SetActive(active);
            if (visual.PickupLabel != null) visual.PickupLabel.gameObject.SetActive(active);
            if (visual.Bud != null) visual.Bud.SetActive(budActive);
            if (visual.BudLabel != null) visual.BudLabel.gameObject.SetActive(budActive);
            if (visual.Beacon != null) visual.Beacon.enabled = budActive;
            if (visual.Ring != null) visual.Ring.enabled = active || budActive;
            if (budActive)
            {
                int health = Mathf.Max(1, CurrentBudHealthAt(i));
                float healthScale = Mathf.Lerp(0.78f, 1f, health / 3f);
                visual.Bud.transform.localScale = Vector3.one * healthScale;
                Color budColor = Color.Lerp(new Color(0.28f, 0.01f, 0.05f),
                    new Color(0.72f, 0.035f, 0.18f), health / 3f);
                SetMaterial(visual.BudRenderer, budColor);
                foreach (Renderer petal in visual.BudPetals)
                    SetMaterial(petal, Color.Lerp(budColor, new Color(0.76f, 0.08f, 0.28f), 0.42f));
                if (visual.BudLabel != null)
                    visual.BudLabel.text = GameLocalization.Choose(
                        $"CAPULLO CORRUPTO\n{health} GOLPES",
                        $"CORRUPTED BUD\n{health} HITS");
                visual.Ring.startColor = budColor;
                visual.Ring.endColor = budColor;
                if (visual.Ring.sharedMaterial == null)
                    visual.Ring.sharedMaterial = NewMaterial(budColor);
                continue;
            }
            if (!active) continue;

            if (!visual.WasPickupActive)
            {
                visual.WasPickupActive = true;
                visual.RevealStartedAt = Time.unscaledTime;
            }

            ArenaPickupType type = CurrentTypeAt(i);
            int typeIndex = (int)type;
            Sprite sprite = GetPickupSprite(typeIndex);
            if (visual.SymbolRenderer != null && visual.SymbolRenderer.sprite != sprite)
            {
                visual.SymbolRenderer.sprite = sprite;
                visual.Symbol.name = PickupNames[typeIndex];
                FitSymbolToHeight(visual.Symbol, sprite, 3.05f);
            }

            if (visual.PickupLabel != null)
                visual.PickupLabel.text = GameLocalization.Choose(
                    $"{PickupNames[typeIndex].ToUpperInvariant()}\nACÉRCATE PARA RECOGER",
                    $"{PickupNames[typeIndex].ToUpperInvariant()}\nMOVE CLOSER TO COLLECT");

            Color color = PickupColors[typeIndex];
            visual.Ring.startColor = color;
            visual.Ring.endColor = color;
            if (visual.Ring.sharedMaterial == null)
                visual.Ring.sharedMaterial = NewMaterial(color);
            continue;
        }

        for (int i = 0; i < visuals.Length; i++)
            if ((mask & (1 << i)) == 0 && visuals[i] != null) visuals[i].WasPickupActive = false;
    }

    private void AnimatePowerUpSymbols()
    {
        Camera camera = Camera.main;
        float time = Time.unscaledTime;
        for (int i = 0; i < visuals.Length; i++)
        {
            PedestalVisual visual = visuals[i];
            if (visual == null) continue;
            if (visual.Symbol != null && visual.Symbol.gameObject.activeSelf)
            {
                float reveal = Mathf.Clamp01((time - visual.RevealStartedAt) / 0.55f);
                visual.Symbol.localPosition = Vector3.up *
                    (Mathf.Lerp(1.35f, 2.72f, Mathf.SmoothStep(0f, 1f, reveal)) +
                     Mathf.Sin(time * 2f + i * 0.8f) * 0.12f);
                if (camera != null)
                    visual.Symbol.rotation = camera.transform.rotation;
                if (camera != null && visual.PickupLabel != null)
                    visual.PickupLabel.transform.rotation = camera.transform.rotation;
            }

            if (visual.Bud != null && visual.Bud.activeSelf)
            {
                float pulse = 1f + Mathf.Sin(time * 4f + i) * 0.06f;
                int health = Mathf.Max(1, CurrentBudHealthAt(i));
                float healthScale = Mathf.Lerp(0.78f, 1f, health / 3f);
                visual.Bud.transform.localScale = Vector3.one * healthScale * pulse;
                if (camera != null && visual.BudLabel != null)
                    visual.BudLabel.transform.rotation = camera.transform.rotation;
            }
        }
    }

    private static Sprite GetPickupSprite(int typeIndex)
    {
        if (pickupSprites == null || pickupSprites.Length != PickupSpriteResources.Length)
            pickupSprites = new Sprite[PickupSpriteResources.Length];
        if (pickupSprites[typeIndex] == null)
            pickupSprites[typeIndex] = Resources.Load<Sprite>(PickupSpriteResources[typeIndex]);
        return pickupSprites[typeIndex];
    }

    private static void FitSymbolToHeight(Transform symbol, Sprite sprite, float desiredHeight)
    {
        if (symbol == null || sprite == null)
        {
            if (symbol != null) symbol.localScale = Vector3.one;
            return;
        }
        float scale = desiredHeight / Mathf.Max(0.01f, sprite.bounds.size.y);
        symbol.localScale = Vector3.one * scale;
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

    public void NotifyPickupGranted(ArenaPickupType type)
    {
        int index = Mathf.Clamp((int)type, 0, PickupNames.Length - 1);
        pickupToast = GameLocalization.Choose($"OBTUVISTE: {PickupNames[index].ToUpperInvariant()}",
            $"PICKED UP: {PickupNames[index].ToUpperInvariant()}");
        pickupToastUntil = Time.unscaledTime + 3.5f;
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
