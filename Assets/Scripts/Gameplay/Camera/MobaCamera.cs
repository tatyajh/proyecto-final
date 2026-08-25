using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Cámara isométrica compartida por entrenamiento y online. Libre sigue al
/// jugador; con soft-lock encuadra al jugador y a su objetivo sin cambiar la
/// simulación ni el apuntado replicado por Fusion.
/// </summary>
[ExecuteAlways]
public sealed class MobaCamera : MonoBehaviour
{
    [Header("Objetivo")]
    [SerializeField] private Transform target;

    [Header("Vista isométrica")]
    [Range(30f, 70f)] [SerializeField] private float pitch = 52f;
    [Range(-180f, 180f)] [SerializeField] private float yaw = -51f;
    [SerializeField, Min(1f)] private float distance = 38f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 3.2f, 0f);

    [Header("Soft lock")]
    [SerializeField, Min(1f)] private float unlockedDistance = 38f;
    [SerializeField, Min(1f)] private float minimumLockedDistance = 18f;
    [SerializeField, Min(1f)] private float maximumLockedDistance = 34f;
    [SerializeField, Range(0f, 30f)] private float yawDeadZone = 8f;
    [SerializeField, Min(1f)] private float maximumYawSpeed = 45f;
    [SerializeField, Range(0.2f, 0.8f)] private float lockedTargetWeight = 0.48f;

    [Header("Suavizado")]
    [SerializeField] private bool smoothFollow = true;
    [SerializeField, Min(0.1f)] private float followSpeed = 9f;
    [SerializeField, Min(0.1f)] private float zoomSpeed = 8f;

    [Header("Visibilidad")]
    [SerializeField] private bool fadeOccluders = true;
    [SerializeField, Min(0.05f)] private float occlusionRadius = 0.42f;
    [SerializeField, Min(0.05f)] private float visualOcclusionRefresh = 0.18f;
    [SerializeField, Range(0.08f, 0.8f)] private float occludedAlpha = 0.38f;
    [SerializeField, Min(0.5f)] private float occlusionFadeSpeed = 5f;

    private CombatTargetingController targeting;
    private float currentYaw;
    private float currentDistance;
    private bool snapNextFrame = true;
    private readonly HashSet<Renderer> occludingRenderers = new HashSet<Renderer>();
    private readonly HashSet<Transform> combatantRoots = new HashSet<Transform>();
    private readonly Dictionary<Renderer, OccluderFade> fadingOccluders = new Dictionary<Renderer, OccluderFade>();
    private Renderer[] sceneRenderers = System.Array.Empty<Renderer>();
    private float nextVisualOcclusionRefresh;

    public Transform FollowTarget => target;
    public Transform LockedTarget => targeting != null ? targeting.LockedTargetTransform : null;

    private sealed class OccluderFade
    {
        public Renderer Renderer;
        public Material[] Originals;
        public Material[] Faded;
        public Color[] Colors;
        public float Alpha = 1f;
        public float Target = 1f;
    }

    private void OnEnable()
    {
        currentYaw = yaw;
        currentDistance = Mathf.Max(1f, distance);
        snapNextFrame = true;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Transform locked = LockedTarget;
        Vector3 playerCenter = VisualCenter(target);
        Vector3 focusPoint = playerCenter;
        float desiredYaw = yaw;
        float desiredDistance = unlockedDistance;

        if (locked != null)
        {
            Vector3 enemyCenter = VisualCenter(locked);
            Vector3 combatAxis = enemyCenter - playerCenter;
            combatAxis.y = 0f;
            if (combatAxis.sqrMagnitude > 0.01f)
                desiredYaw = NormalizeAngle(Quaternion.LookRotation(combatAxis.normalized).eulerAngles.y);

            focusPoint = Vector3.Lerp(playerCenter, enemyCenter, lockedTargetWeight);
            float separation = Vector3.Distance(playerCenter, enemyCenter);
            desiredDistance = Mathf.Clamp(17f + separation * 0.72f,
                minimumLockedDistance, maximumLockedDistance);
        }
        else
        {
            focusPoint += targetOffset;
        }

        if (snapNextFrame || !Application.isPlaying)
        {
            currentYaw = desiredYaw;
            currentDistance = desiredDistance;
        }
        else
        {
            float yawDelta = Mathf.DeltaAngle(currentYaw, desiredYaw);
            if (locked != null && Mathf.Abs(yawDelta) > yawDeadZone)
                currentYaw = Mathf.MoveTowardsAngle(currentYaw, desiredYaw,
                    maximumYawSpeed * Time.unscaledDeltaTime);
            else if (locked == null)
                currentYaw = Mathf.MoveTowardsAngle(currentYaw, yaw,
                    maximumYawSpeed * 0.7f * Time.unscaledDeltaTime);

            currentDistance = Mathf.Lerp(currentDistance, desiredDistance,
                1f - Mathf.Exp(-zoomSpeed * Time.unscaledDeltaTime));
        }

        Quaternion cameraRotation = Quaternion.Euler(pitch, currentYaw, 0f);
        Vector3 desiredPosition = focusPoint - cameraRotation * Vector3.forward * currentDistance;

        if (snapNextFrame || !Application.isPlaying || !smoothFollow)
            transform.position = desiredPosition;
        else
            transform.position = Vector3.Lerp(transform.position, desiredPosition,
                1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));

        transform.rotation = cameraRotation;
        snapNextFrame = false;

        if (Application.isPlaying && fadeOccluders)
            RefreshOccluders(playerCenter, locked != null ? VisualCenter(locked) : playerCenter);
        UpdateOccluderFades();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targeting = newTarget != null ? newTarget.GetComponent<CombatTargetingController>() : null;
        snapNextFrame = true;
    }

    public void BindTargeting(CombatTargetingController controller)
    {
        targeting = controller;
        if (controller != null) target = controller.transform;
        snapNextFrame = true;
    }

    public void AddImpulse(Vector3 worldImpulse)
    {
        if (Application.isPlaying) transform.position += worldImpulse;
    }

    public void RequestImmediateReframe()
    {
        snapNextFrame = true;
    }

    private void RefreshOccluders(Vector3 playerCenter, Vector3 enemyCenter)
    {
        RefreshSceneRendererCache();
        HashSet<Renderer> next = new HashSet<Renderer>();
        CollectOccluders(playerCenter, next);
        if ((enemyCenter - playerCenter).sqrMagnitude > 0.01f) CollectOccluders(enemyCenter, next);

        // El Arbol de la Abundancia usa colliders auxiliares colocados como
        // hermanos de las mallas. Un raycast encuentra el obstaculo, pero no
        // necesariamente el Renderer que dibuja la rama. Esta segunda pasada
        // trabaja con los bounds visuales y cubre tambien adornos sin collider.
        CollectVisualOccluders(playerCenter, next);
        if ((enemyCenter - playerCenter).sqrMagnitude > 0.01f)
            CollectVisualOccluders(enemyCenter, next);

        foreach (OccluderFade state in fadingOccluders.Values)
            state.Target = 1f;
        foreach (Renderer renderer in next)
            BeginOccluderFade(renderer);

        occludingRenderers.Clear();
        foreach (Renderer renderer in next) occludingRenderers.Add(renderer);
    }

    private void CollectOccluders(Vector3 focus, HashSet<Renderer> results)
    {
        Vector3 ray = focus - transform.position;
        float length = ray.magnitude;
        if (length <= 0.5f) return;

        foreach (RaycastHit hit in Physics.SphereCastAll(transform.position, occlusionRadius,
                     ray / length, Mathf.Max(0f, length - 0.65f), Physics.AllLayers,
                     QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;
            AddRelatedRenderers(hit.collider.transform, hit.point, results);
        }
    }

    private void AddRelatedRenderers(Transform hitTransform, Vector3 hitPoint, HashSet<Renderer> results)
    {
        Transform cursor = hitTransform;
        for (int depth = 0; cursor != null && depth < 5; depth++, cursor = cursor.parent)
        {
            Renderer[] related = cursor.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            foreach (Renderer renderer in related)
            {
                if (!CanFadeOccluder(renderer)) continue;
                results.Add(renderer);
                found = true;
            }

            if (found) return;
        }
    }

    private void RefreshSceneRendererCache()
    {
        if (Time.unscaledTime < nextVisualOcclusionRefresh && sceneRenderers.Length > 0) return;
        sceneRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        combatantRoots.Clear();
        foreach (PlayerController player in FindObjectsByType<PlayerController>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            combatantRoots.Add(player.transform.root);
        nextVisualOcclusionRefresh = Time.unscaledTime + visualOcclusionRefresh;
    }

    private void CollectVisualOccluders(Vector3 focus, HashSet<Renderer> results)
    {
        Vector3 toFocus = focus - transform.position;
        float focusDistance = toFocus.magnitude;
        if (focusDistance <= 0.5f) return;

        Ray viewRay = new Ray(transform.position, toFocus / focusDistance);
        foreach (Renderer renderer in sceneRenderers)
        {
            if (!CanFadeOccluder(renderer)) continue;
            Bounds bounds = renderer.bounds;

            if (!bounds.IntersectRay(viewRay, out float enterDistance)) continue;
            // El mesh central del árbol tiene un AABB enorme que contiene la
            // propia cámara. En ese caso IntersectRay devuelve una entrada
            // negativa aunque las ramas sí crucen la imagen; se desvanece el
            // ornamento botánico en vez de dejarlo tapar a los luchadores.
            bool cameraInsideBotanicalBounds = enterDistance <= 0.15f && IsBotanicalOccluder(renderer);
            if (!cameraInsideBotanicalBounds &&
                (enterDistance <= 0.15f || enterDistance >= focusDistance - 0.8f)) continue;
            results.Add(renderer);
        }
    }

    private static bool IsBotanicalOccluder(Renderer renderer)
    {
        if (renderer == null) return false;
        Transform cursor = renderer.transform;
        while (cursor != null)
        {
            string objectName = cursor.name.ToLowerInvariant();
            if (objectName.Contains("arbol") || objectName.Contains("árbol") || objectName.Contains("tree"))
                return true;
            cursor = cursor.parent;
        }

        string name = renderer.name.ToLowerInvariant();
        return name.Contains("tree") || name.Contains("liana") || name.Contains("branch") ||
               name.Contains("rama") || name.Contains("vine") || name.Contains("tronco") ||
               name.Contains("root") || name.Contains("raiz") || name.Contains("raíz");
    }

    private bool CanOcclude(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled) return false;
        if (renderer is LineRenderer || renderer is ParticleSystemRenderer) return false;
        if (combatantRoots.Contains(renderer.transform.root)) return false;
        if (renderer.GetComponentInParent<PlayerController>() != null) return false;
        if (renderer.GetComponentInParent<ArenaPowerUpManager>() != null) return false;
        return true;
    }

    private bool CanFadeOccluder(Renderer renderer)
    {
        // El desvanecido es una ayuda de lectura, no una regla general del
        // escenario. Suelo, plataformas y rocas transitables deben conservarse
        // siempre; solo la vegetacion elevada que cruza la vista puede volverse
        // translucida.
        return CanOcclude(renderer) && IsBotanicalOccluder(renderer);
    }

    private void BeginOccluderFade(Renderer renderer)
    {
        if (renderer == null) return;
        renderer.forceRenderingOff = false;
        if (!fadingOccluders.TryGetValue(renderer, out OccluderFade state))
        {
            state = CreateFadeState(renderer);
            if (state == null) return;
            fadingOccluders.Add(renderer, state);
        }
        state.Target = occludedAlpha;
    }

    private static OccluderFade CreateFadeState(Renderer renderer)
    {
        Material[] originals = renderer.sharedMaterials;
        if (originals == null || originals.Length == 0) return null;

        Material[] faded = new Material[originals.Length];
        Color[] colors = new Color[originals.Length];
        for (int i = 0; i < originals.Length; i++)
        {
            Material original = originals[i];
            if (original == null) continue;
            Material material = new Material(original) { name = $"{original.name} (Camera Fade)" };
            ConfigureTransparent(material);
            faded[i] = material;
            colors[i] = ReadMaterialColor(original);
        }

        renderer.sharedMaterials = faded;
        return new OccluderFade
        {
            Renderer = renderer,
            Originals = originals,
            Faded = faded,
            Colors = colors
        };
    }

    private void UpdateOccluderFades()
    {
        if (fadingOccluders.Count == 0) return;
        List<Renderer> finished = null;
        float step = occlusionFadeSpeed * Time.unscaledDeltaTime;

        foreach (KeyValuePair<Renderer, OccluderFade> pair in fadingOccluders)
        {
            OccluderFade state = pair.Value;
            if (state.Renderer == null)
            {
                (finished ??= new List<Renderer>()).Add(pair.Key);
                DestroyFadeMaterials(state);
                continue;
            }

            state.Alpha = Mathf.MoveTowards(state.Alpha, state.Target, step);
            for (int i = 0; i < state.Faded.Length; i++)
            {
                Material material = state.Faded[i];
                if (material == null) continue;
                Color color = state.Colors[i];
                color.a *= state.Alpha;
                WriteMaterialColor(material, color);
            }

            if (state.Target >= 0.999f && state.Alpha >= 0.999f)
            {
                state.Renderer.sharedMaterials = state.Originals;
                DestroyFadeMaterials(state);
                (finished ??= new List<Renderer>()).Add(pair.Key);
            }
        }

        if (finished == null) return;
        foreach (Renderer renderer in finished) fadingOccluders.Remove(renderer);
    }

    private static void ConfigureTransparent(Material material)
    {
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static Color ReadMaterialColor(Material material)
    {
        if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color")) return material.GetColor("_Color");
        return Color.white;
    }

    private static void WriteMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private static void DestroyFadeMaterials(OccluderFade state)
    {
        if (state?.Faded == null) return;
        foreach (Material material in state.Faded)
            if (material != null) Destroy(material);
    }

    private static Vector3 VisualCenter(Transform root)
    {
        if (root == null) return Vector3.zero;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer is LineRenderer || renderer is ParticleSystemRenderer || renderer.forceRenderingOff)
                continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found ? bounds.center : root.position + Vector3.up * 3.2f;
    }

    private static float NormalizeAngle(float value)
    {
        while (value > 180f) value -= 360f;
        while (value < -180f) value += 360f;
        return value;
    }

    private void OnDisable()
    {
        foreach (OccluderFade state in fadingOccluders.Values)
        {
            if (state.Renderer != null)
            {
                state.Renderer.forceRenderingOff = false;
                state.Renderer.sharedMaterials = state.Originals;
            }
            DestroyFadeMaterials(state);
        }
        fadingOccluders.Clear();
        foreach (Renderer renderer in occludingRenderers)
            if (renderer != null) renderer.forceRenderingOff = false;
        occludingRenderers.Clear();
    }
}
