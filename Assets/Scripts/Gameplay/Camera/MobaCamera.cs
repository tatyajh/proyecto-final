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
    [Range(-180f, 180f)] [SerializeField] private float yaw = 129f;
    [SerializeField, Min(1f)] private float distance = 38f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    [Header("Soft lock")]
    [SerializeField, Min(1f)] private float unlockedDistance = 38f;
    [SerializeField, Min(1f)] private float minimumUnlockedDistance = 20f;
    [SerializeField, Min(1f)] private float maximumUnlockedDistance = 48f;
    [SerializeField, Min(1f)] private float minimumLockedDistance = 22f;
    [SerializeField, Min(1f)] private float maximumLockedDistance = 32f;
    [SerializeField, Min(0.1f)] private float manualZoomSpeed = 3f;
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
    [SerializeField, Range(0.03f, 0.8f)] private float occludedAlpha = 0.12f;
    [SerializeField, Range(0.08f, 0.45f)] private float treeOccludedAlpha = 0.22f;
    [SerializeField, Min(0.5f)] private float occlusionFadeSpeed = 5f;

    [Header("Identidad de la arena")]
    [Tooltip("Integra sutilmente la esfera roja en la vista libre sin centrarla sobre el jugador.")]
    [SerializeField, Range(0f, 0.35f)] private float arenaFeatureWeight = 0.26f;
    [Tooltip("Eleva el encuadre para mostrar la esfera sin alejar la cámara ni reducir a los personajes.")]
    [SerializeField, Range(0f, 0.75f)] private float arenaFeatureVerticalWeight = 0.52f;
    [SerializeField, Min(5f)] private float arenaFeatureMaximumDistance = 48f;
    [SerializeField, Min(1f)] private float arenaFeatureDiameter = 10f;
    [SerializeField] private Vector3 arenaFeatureFallbackPosition = new Vector3(64.1f, 26.3f, -27.1f);

    private CombatTargetingController targeting;
    private float currentYaw;
    private float currentDistance;
    private float requestedUnlockedDistance;
    private float previousPinchDistance = -1f;
    private bool snapNextFrame = true;
    private readonly HashSet<Renderer> occludingRenderers = new HashSet<Renderer>();
    private readonly HashSet<Transform> combatantRoots = new HashSet<Transform>();
    private readonly Dictionary<Renderer, OccluderFade> fadingOccluders = new Dictionary<Renderer, OccluderFade>();
    private Renderer[] sceneRenderers = System.Array.Empty<Renderer>();
    private float nextVisualOcclusionRefresh;
    private Renderer arenaFeatureRenderer;
    private GameObject arenaFeaturePresentation;
    private Material arenaFeatureRuntimeMaterial;
    private float nextArenaFeatureSearch;
    private bool arenaFeaturePrepared;

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
        float configuredDistance = unlockedDistance > 0f ? unlockedDistance : distance;
        requestedUnlockedDistance = Mathf.Clamp(configuredDistance,
            minimumUnlockedDistance, maximumUnlockedDistance);
        currentDistance = requestedUnlockedDistance;
        snapNextFrame = true;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Transform locked = LockedTarget;
        Vector3 playerCenter = VisualCenter(target);
        Vector3 focusPoint = playerCenter;
        float desiredYaw = yaw;
        UpdateManualZoom(locked == null);
        // El encuadre aprobado por arte usa 38 unidades exactas. La escala de
        // los meshes no debe volver a alterar la cámara de manera implícita.
        float desiredDistance = Mathf.Clamp(requestedUnlockedDistance,
            minimumUnlockedDistance, maximumUnlockedDistance);

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
            if (TryGetArenaFeature(out Vector3 featureCenter))
            {
                Vector3 planar = featureCenter - playerCenter;
                planar.y = 0f;
                if (planar.magnitude <= arenaFeatureMaximumDistance)
                {
                    // El peso horizontal conserva al luchador como sujeto
                    // principal. El vertical es independiente porque Blood
                    // está por encima de la copa: así entra en plano sin zoom.
                    focusPoint.x = Mathf.Lerp(focusPoint.x, featureCenter.x, arenaFeatureWeight);
                    focusPoint.z = Mathf.Lerp(focusPoint.z, featureCenter.z, arenaFeatureWeight);
                    focusPoint.y = Mathf.Lerp(focusPoint.y, featureCenter.y,
                        arenaFeatureVerticalWeight);
                }
            }
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
        string name = renderer.name.ToLowerInvariant();
        if (name == "tree" || name.StartsWith("liana") ||
               name.Contains("branch") || name.Contains("rama") || name.Contains("vine") ||
               name.Contains("canopy") || name.Contains("copa")) return true;

        // El FBX agrupa parte del tronco y de la esfera en mallas con nombres
        // genéricos. Se aceptan como vegetación solo si pertenecen al árbol y
        // no son piso/roca/raíz transitable; así se despeja la línea de cámara
        // sin hacer desaparecer el terreno cuando el jugador se acerca.
        string rootName = renderer.transform.root.name.ToLowerInvariant();
        bool belongsToArenaTree = rootName.Contains("arbol de la abundancia") ||
                                  rootName.Contains("árbol de la abundancia");
        if (!belongsToArenaTree) return false;
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material == null) continue;
            string materialName = material.name.ToLowerInvariant();
            if (materialName.Contains("suel") || materialName.Contains("ground") ||
                materialName.Contains("bricksrock") || materialName.Contains("pilar") ||
                materialName.Contains("stone") || materialName.Contains("roca"))
                return false;
        }
        return !name.Contains("floor") && !name.Contains("ground") &&
               !name.Contains("suelo") && !name.Contains("terrain") &&
               !name.Contains("rock") && !name.Contains("roca") &&
               !name.Contains("stone") && !name.Contains("piedra") &&
               !name.Contains("platform") && !name.Contains("plataforma") &&
               !name.Contains("root") && !name.Contains("raiz") &&
               !name.Contains("raíz");
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
        return CanOcclude(renderer) &&
               (IsBotanicalOccluder(renderer) || IsArenaFeature(renderer));
    }

    private bool TryGetArenaFeature(out Vector3 center)
    {
        if (Application.isPlaying && !arenaFeaturePrepared)
            PrepareArenaFeature();

        if (arenaFeatureRenderer == null && Time.unscaledTime >= nextArenaFeatureSearch)
        {
            nextArenaFeatureSearch = Time.unscaledTime + 1f;
            foreach (Renderer renderer in FindObjectsByType<Renderer>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!IsArenaFeature(renderer)) continue;
                arenaFeatureRenderer = renderer;
                break;
            }
        }

        if (arenaFeatureRenderer != null && arenaFeatureRenderer.enabled &&
            !arenaFeatureRenderer.forceRenderingOff)
        {
            center = arenaFeatureRenderer.bounds.center;
            return true;
        }

        // La posición corresponde al nodo Blood del FBX (centro local a
        // 39,27 unidades de altura) ya transformado por el prefab de la arena.
        // Sirve durante el primer frame o si Unity aún no actualizó sus bounds.
        center = arenaFeatureFallbackPosition;
        return true;
    }

    private void PrepareArenaFeature()
    {
        arenaFeaturePrepared = true;
        Transform feature = null;
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate == null || !candidate.gameObject.scene.IsValid() ||
                !candidate.gameObject.scene.isLoaded ||
                !candidate.name.Equals("Blood", System.StringComparison.OrdinalIgnoreCase)) continue;
            feature = candidate;
            break;
        }

        MeshFilter sourceFilter = feature != null
            ? feature.GetComponentInChildren<MeshFilter>(true)
            : null;
        Renderer sourceRenderer = feature != null
            ? feature.GetComponentInChildren<Renderer>(true)
            : null;
        Mesh sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;

        if (sourceRenderer != null && sourceRenderer.enabled &&
            sourceRenderer.gameObject.activeInHierarchy)
        {
            float visibleDiameter = Mathf.Max(sourceRenderer.bounds.size.x,
                sourceRenderer.bounds.size.y, sourceRenderer.bounds.size.z);
            if (visibleDiameter >= arenaFeatureDiameter * 0.72f)
            {
                arenaFeatureRenderer = sourceRenderer;
                return;
            }
        }

        // El prefab recibido oculta Blood con localScale = 0. Restaurarlo en
        // su jerarquía es frágil por las escalas anidadas del FBX. Una copia
        // visual independiente conserva exactamente su malla/material, no
        // añade colisión y no modifica el asset ni el gameplay.
        if (sourceMesh != null)
        {
            arenaFeaturePresentation = new GameObject("Arena Blood Orb (Presentation)");
            arenaFeaturePresentation.transform.SetPositionAndRotation(
                feature.position, feature.rotation);
            arenaFeaturePresentation.layer = feature.gameObject.layer;

            MeshFilter presentationFilter = arenaFeaturePresentation.AddComponent<MeshFilter>();
            presentationFilter.sharedMesh = sourceMesh;
            MeshRenderer presentationRenderer = arenaFeaturePresentation.AddComponent<MeshRenderer>();
            if (sourceRenderer != null)
            {
                presentationRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
                presentationRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                presentationRenderer.receiveShadows = sourceRenderer.receiveShadows;
            }

            float meshDiameter = Mathf.Max(sourceMesh.bounds.size.x,
                sourceMesh.bounds.size.y, sourceMesh.bounds.size.z);
            arenaFeaturePresentation.transform.localScale = Vector3.one *
                (meshDiameter > 0.0001f ? arenaFeatureDiameter / meshDiameter : 1f);
            arenaFeatureRenderer = presentationRenderer;
            return;
        }

        CreateFallbackArenaFeature();
    }

    private void CreateFallbackArenaFeature()
    {
        arenaFeaturePresentation = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        arenaFeaturePresentation.name = "Arena Blood Orb (Presentation Fallback)";
        arenaFeaturePresentation.transform.position = arenaFeatureFallbackPosition;
        arenaFeaturePresentation.transform.localScale = Vector3.one * (arenaFeatureDiameter * 0.5f);

        Collider presentationCollider = arenaFeaturePresentation.GetComponent<Collider>();
        if (presentationCollider != null)
        {
            presentationCollider.enabled = false;
            Destroy(presentationCollider);
        }

        arenaFeatureRenderer = arenaFeaturePresentation.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                        Shader.Find("Standard");
        if (shader == null || arenaFeatureRenderer == null) return;

        arenaFeatureRuntimeMaterial = new Material(shader)
        {
            name = "Arena Blood Orb Runtime Material",
            color = new Color(0.32f, 0.005f, 0.025f, 1f)
        };
        if (arenaFeatureRuntimeMaterial.HasProperty("_BaseColor"))
            arenaFeatureRuntimeMaterial.SetColor("_BaseColor",
                new Color(0.32f, 0.005f, 0.025f, 1f));
        arenaFeatureRuntimeMaterial.EnableKeyword("_EMISSION");
        if (arenaFeatureRuntimeMaterial.HasProperty("_EmissionColor"))
            arenaFeatureRuntimeMaterial.SetColor("_EmissionColor",
                new Color(0.42f, 0.008f, 0.035f, 1f));
        arenaFeatureRenderer.sharedMaterial = arenaFeatureRuntimeMaterial;
    }

    private void ReleaseArenaFeaturePresentation()
    {
        if (arenaFeaturePresentation != null)
        {
            if (Application.isPlaying) Destroy(arenaFeaturePresentation);
            else DestroyImmediate(arenaFeaturePresentation);
            arenaFeaturePresentation = null;
        }

        if (arenaFeatureRuntimeMaterial != null)
        {
            if (Application.isPlaying) Destroy(arenaFeatureRuntimeMaterial);
            else DestroyImmediate(arenaFeatureRuntimeMaterial);
            arenaFeatureRuntimeMaterial = null;
        }
    }

    private static bool IsArenaFeature(Renderer renderer)
    {
        if (renderer == null) return false;
        if (renderer.name.Contains("Arena Blood Orb") ||
            renderer.transform.root.name.Contains("Arena Blood Orb")) return true;
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material == null) continue;
            string materialName = material.name.ToLowerInvariant();
            if (materialName.Contains("bloodshpare") || materialName.Contains("bloodsphere"))
                return true;
        }
        return false;
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
        state.Target = FadeAlphaFor(renderer);
    }

    private float FadeAlphaFor(Renderer renderer)
    {
        if (renderer == null) return occludedAlpha;
        // Si la esfera cruza la línea de visión sigue siendo reconocible, pero
        // se vuelve translúcida antes de tapar a los combatientes.
        if (IsArenaFeature(renderer)) return 0.55f;
        string rendererName = renderer.name.ToLowerInvariant();
        // El mesh Tree también contiene la esfera roja que da identidad a la
        // arena. Se vuelve marca de agua cuando obstruye, pero nunca desaparece.
        bool centralTree = rendererName == "tree" ||
                           renderer.transform.root.name.ToLowerInvariant()
                               .Contains("arbol de la abundancia");
        return centralTree ? treeOccludedAlpha : occludedAlpha;
    }

    private void UpdateManualZoom(bool enabledForCurrentView)
    {
        if (!Application.isPlaying || !enabledForCurrentView) return;

        float delta = Input.mouseScrollDelta.y;
        if (Input.touchCount == 2)
        {
            Touch first = Input.GetTouch(0);
            Touch second = Input.GetTouch(1);
            float pinchDistance = Vector2.Distance(first.position, second.position);
            if (previousPinchDistance > 0f)
                delta += (pinchDistance - previousPinchDistance) / Mathf.Max(35f, Screen.dpi * 0.35f);
            previousPinchDistance = pinchDistance;
        }
        else
        {
            previousPinchDistance = -1f;
        }

        if (Mathf.Abs(delta) <= 0.001f) return;
        requestedUnlockedDistance = Mathf.Clamp(
            requestedUnlockedDistance - delta * manualZoomSpeed,
            minimumUnlockedDistance,
            maximumUnlockedDistance);
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
        ReleaseArenaFeaturePresentation();
        arenaFeaturePrepared = false;
        arenaFeatureRenderer = null;
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
