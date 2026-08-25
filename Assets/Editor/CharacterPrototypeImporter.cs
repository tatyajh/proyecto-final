using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Genera los prefabs jugables a partir de los FBX que entrega arte.
///
/// Antes solo contemplaba a Quietmor con la ruta escrita a mano, así que los
/// personajes nuevos aparecían en el menú como cápsula aunque su modelo ya
/// estuviera en el proyecto. Ahora recorre la tabla de abajo: en cuanto un FBX
/// existe, su prefab se crea en Resources/Characters con el nombre que espera
/// CharacterCatalog, y el personaje sale en el juego sin tocar código.
/// </summary>
[InitializeOnLoad]
public static class CharacterPrototypeImporter
{
    private const string OutputFolder = "Assets/Resources/Characters";
    private const string MaterialOutputFolder = OutputFolder + "/Materials";
    private const string ImportMarker = "BlightedBlossomsPrototypeCharacterV8";

    private sealed class CharacterSource
    {
        public readonly string CatalogName;   // nombre del prefab que busca CharacterCatalog
        public readonly string ModelPath;
        public readonly string MaterialPath;
        public readonly string ControllerPath;

        public CharacterSource(string catalogName, string modelPath, string materialPath,
            string controllerPath = null)
        {
            CatalogName = catalogName;
            ModelPath = modelPath;
            MaterialPath = materialPath;
            ControllerPath = controllerPath;
        }

        public string PrefabPath => $"{OutputFolder}/{CatalogName}.prefab";
    }

    // Quietmor conserva el nombre CampanaPrototype porque ya hay escenas y un
    // Animator Controller apuntando a él; renombrarlo rompería esas referencias.
    private static readonly CharacterSource[] Sources =
    {
        new CharacterSource(
            "Heliandra",
            "Assets/Models 3D/Character animation/Newest/Heliandra.fbx",
            string.Empty,
            OutputFolder + "/Heliandra.controller"),
        new CharacterSource(
            "Lunara",
            "Assets/Models 3D/Character animation/Newest/Lunara.fbx",
            string.Empty,
            OutputFolder + "/Lunara.controller"),
        new CharacterSource(
            "CampanaPrototype",
            "Assets/Models 3D/Character animation/Newest/Quietmor/Campana sin voz rig.fbx",
            "Assets/Models 3D/Character animation/Newest/Quietmor/Campana sin voz_mat.mat",
            OutputFolder + "/CampanaPrototype.controller"),
        new CharacterSource(
            "Solmara",
            "Assets/Models 3D/Character animation/Newest/Solmara/reina girasol.fbx",
            "Assets/Models 3D/Character animation/Newest/Solmara/Sunflower_mat_actualizado.mat",
            OutputFolder + "/Solmara.controller"),
        new CharacterSource(
            "Acatheria",
            "Assets/Models 3D/Character animation/Newest/Acatheria/Perro.fbx",
            "Assets/Models 3D/Character animation/Newest/Acatheria/perro_mat_actualizado.mat",
            OutputFolder + "/Acatheria.controller"),
        new CharacterSource(
            "Terramor",
            "Assets/Models 3D/Character animation/Newest/Terramor.fbx",
            string.Empty,
            OutputFolder + "/Terramor.controller")
    };

    static CharacterPrototypeImporter()
    {
        EditorApplication.delayCall += EnsurePrototypeAssets;
    }

    [MenuItem("Blighted Blossoms/Actualizar personajes")]
    public static void EnsurePrototypeAssets()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsurePrototypeAssets;
            return;
        }

        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Characters");
        }
        if (!AssetDatabase.IsValidFolder(MaterialOutputFolder))
            AssetDatabase.CreateFolder(OutputFolder, "Materials");

        ConfigurePortraitImporters();

        int built = 0;
        foreach (CharacterSource source in Sources)
        {
            if (BuildPrefab(source)) built++;
        }

        if (built > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterPrototypeImporter] {built} personaje(s) actualizados en {OutputFolder}.");
        }
    }

    private static void ConfigurePortraitImporters()
    {
        const string portraitFolder = "Assets/Resources/UI/Portraits";
        if (!AssetDatabase.IsValidFolder(portraitFolder)) return;

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { portraitFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool dirty = false;
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (!importer.sRGBTexture) { importer.sRGBTexture = true; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }
    }

    private static bool BuildPrefab(CharacterSource source)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(source.ModelPath);
        if (model == null)
        {
            // Normal mientras arte no entrega ese personaje: el menú lo mostrará
            // como cápsula provisional y el juego sigue funcionando.
            return false;
        }

        ConfigureModelImporter(source.ModelPath);
        ConfigureTextureImporters(source.ModelPath);
        Material characterMaterial = EnsureCharacterMaterial(source);
        BuildAnimatorController(source);

        // Solo se regenera si el marcador cambió, para no reescribir el prefab
        // (y perder ajustes manuales) en cada recarga de scripts.
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(source.PrefabPath);
        if (existing != null && AssetDatabase.GetLabels(existing).Length > 0 &&
            System.Array.IndexOf(AssetDatabase.GetLabels(existing), ImportMarker) >= 0)
        {
            return false;
        }

        GameObject instance = Object.Instantiate(model);
        instance.name = source.CatalogName;

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);

        if (characterMaterial != null) AssignCharacterMaterial(instance, characterMaterial);
        else AssignEmbeddedUrpMaterials(instance, source);
        AttachAnimator(instance, source);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, source.PrefabPath);
        Object.DestroyImmediate(instance);

        if (saved != null)
            AssetDatabase.SetLabels(saved, new[] { ImportMarker });

        return true;
    }

    /// <summary>
    /// Los FBX de arte usan un único atlas por personaje. Unity puede volver a
    /// enlazar el material embebido al reimportar y dejar la malla gris; por
    /// eso la ruta canónica forma parte de la definición y se reasigna antes de
    /// guardar el prefab. Si el shader personalizado no compila en la máquina,
    /// Standard conserva al menos albedo, normal y metal.
    /// </summary>
    private static Material EnsureCharacterMaterial(CharacterSource source)
    {
        // Los FBX más recientes traen el material embebido. Cuando no existe
        // una ruta canónica se conserva ese material en vez de sustituirlo por
        // uno vacío; Unity lo enlaza al instanciar el modelo.
        if (string.IsNullOrWhiteSpace(source.MaterialPath))
            return null;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(source.MaterialPath);
        if (material == null)
        {
            Debug.LogWarning($"[CharacterPrototypeImporter] No se encontró el material de {source.CatalogName}: {source.MaterialPath}");
            return null;
        }

        UpgradeMaterialToUrp(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AssignEmbeddedUrpMaterials(GameObject instance, CharacterSource source)
    {
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                Material embedded = slots[i];
                if (embedded == null) continue;
                string safeName = string.Concat((embedded.name ?? $"Material{i}")
                    .Select(character => char.IsLetterOrDigit(character) ? character : '_'));
                string path = $"{MaterialOutputFolder}/{source.CatalogName}_{safeName}.mat";
                Material external = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (external == null)
                {
                    external = new Material(embedded) { name = $"{source.CatalogName}_{safeName}" };
                    AssetDatabase.CreateAsset(external, path);
                }
                else
                {
                    EditorUtility.CopySerialized(embedded, external);
                    external.name = $"{source.CatalogName}_{safeName}";
                }

                UpgradeMaterialToUrp(external);
                EditorUtility.SetDirty(external);
                slots[i] = external;
            }
            renderer.sharedMaterials = slots;
        }
    }

    private static void UpgradeMaterialToUrp(Material material)
    {
        if (material == null) return;
        Texture albedo = FirstTexture(material, "_BaseMap", "_MainTex");
        Texture normal = FirstTexture(material, "_BumpMap", "_NormalMap");
        Texture metallic = FirstTexture(material, "_MetallicGlossMap", "_MetallicMap");
        Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
            : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return;
        material.shader = shader;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (albedo != null)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
        }
        if (normal != null && material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        if (metallic != null && material.HasProperty("_MetallicGlossMap"))
        {
            material.SetTexture("_MetallicGlossMap", metallic);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
    }

    private static Texture FirstTexture(Material material, params string[] properties)
    {
        foreach (string property in properties)
            if (material.HasProperty(property) && material.GetTexture(property) != null)
                return material.GetTexture(property);
        return null;
    }

    private static void AssignCharacterMaterial(GameObject instance, Material material)
    {
        if (instance == null || material == null) return;

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            if (slots == null || slots.Length == 0)
            {
                renderer.sharedMaterial = material;
                continue;
            }

            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
        }
    }

    /// <summary>
    /// Sin Animator con controller el personaje entra a la arena inmóvil. Si el
    /// personaje no trae controller propio se deja el Animator vacío: se moverá
    /// por navegación aunque no tenga animación de idle todavía.
    /// </summary>
    private static void AttachAnimator(GameObject instance, CharacterSource source)
    {
        Animator animator = instance.GetComponentInChildren<Animator>(true);
        if (animator == null) animator = instance.AddComponent<Animator>();

        if (animator.runtimeAnimatorController == null && !string.IsNullOrEmpty(source.ControllerPath))
        {
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(source.ControllerPath);
            if (controller != null) animator.runtimeAnimatorController = controller;
        }

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private static void ConfigureModelImporter(string modelPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) return;

        bool dirty = false;

        // Generic basta para estos rigs y evita el remapeo de huesos de Humanoid.
        if (importer.animationType == ModelImporterAnimationType.None)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            dirty = true;
        }

        // importMaterials quedó obsoleto y de solo lectura en Unity 6.
        if (importer.materialImportMode == ModelImporterMaterialImportMode.None)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            dirty = true;
        }

        // La rama de animación entregó las tomas correctas, pero al mover los
        // FBX dejó las rutas duplicadas y los loops configurados solo en sus
        // .meta experimentales. Aplicamos esa información sobre la ruta canónica.
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
            dirty = clips != null && clips.Length > 0;
        }

        if (clips != null)
        {
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string clipName = (clip.name ?? string.Empty).ToLowerInvariant();
                bool shouldLoop = clipName.Contains("idle") || clipName.Contains("iddle") ||
                                  clipName.Contains("walk") || clipName.Contains("running");
                if (clip.loopTime == shouldLoop) continue;
                clip.loopTime = shouldLoop;
                dirty = true;
            }

            if (dirty) importer.clipAnimations = clips;
        }

        if (dirty)
        {
            importer.SaveAndReimport();
        }
    }

    private static void ConfigureTextureImporters(string modelPath)
    {
        string folder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(folder)) return;

        string[] dependencies = AssetDatabase.GetDependencies(modelPath, true);
        foreach (string path in dependencies.Where(path =>
                     AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D)))
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            bool normalMap = name.Contains("normal") || name.EndsWith("_n") ||
                             name.EndsWith("_nrm") || name.EndsWith("_norm");
            bool dataMap = name.Contains("metallic") || name.Contains("roughness") ||
                           name.Contains("height") || name.EndsWith("_rm");
            bool colorMap = name.Contains("basecolor") || name.Contains("base_color") ||
                            name.Contains("albedo") || name.Contains("diffuse");

            bool dirty = false;
            if (normalMap && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                dirty = true;
            }

            if ((dataMap || normalMap) && importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                dirty = true;
            }

            if (colorMap && !importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }
    }

    /// <summary>
    /// Los FBX entregados ya incluyen clips, pero en la rama de arte los
    /// controllers quedaron ligados a rutas rotas. Se reconstruyen aquí con
    /// parámetros comunes para que gameplay pueda conducir cualquier modelo.
    /// </summary>
    private static void BuildAnimatorController(CharacterSource source)
    {
        if (string.IsNullOrEmpty(source.ControllerPath)) return;

        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(source.ControllerPath);
        if (existing != null && existing.parameters.Any(parameter => parameter.name == "isMoving") &&
            existing.parameters.Any(parameter => parameter.name == "attack") &&
            existing.parameters.Any(parameter => parameter.name == "ultimate"))
        {
            return;
        }

        AnimationClip[] clips = System.Array.FindAll(
            AssetDatabase.LoadAllAssetsAtPath(source.ModelPath),
            asset => asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            .Cast<AnimationClip>()
            .ToArray();
        if (clips.Length == 0) return;

        AnimationClip Find(params string[] terms) => System.Array.Find(clips, clip =>
            System.Array.Exists(terms, term => clip.name.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0));

        AnimationClip idle = Find("idle", "iddle") ?? clips[0];
        AnimationClip walk = Find("walk", "running");
        AnimationClip attack = Find("attack", "action");
        AnimationClip ultimate = Find("ulti");

        AssetDatabase.DeleteAsset(source.ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(source.ControllerPath);
        controller.AddParameter("isMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("ultimate", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle");
        idleState.motion = idle;
        machine.defaultState = idleState;

        if (walk != null)
        {
            AnimatorState walkState = machine.AddState("Move");
            walkState.motion = walk;
            AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.14f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "isMoving");
            AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.14f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "isMoving");
        }

        AddActionState(machine, idleState, attack, "Attack", "attack");
        AddActionState(machine, idleState, ultimate, "Ultimate", "ultimate");
        EditorUtility.SetDirty(controller);
    }

    private static void AddActionState(AnimatorStateMachine machine, AnimatorState idle,
        AnimationClip clip, string stateName, string trigger)
    {
        if (clip == null) return;
        AnimatorState state = machine.AddState(stateName);
        state.motion = clip;
        AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
        enter.hasExitTime = false;
        enter.duration = 0.08f;
        enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        AnimatorStateTransition exit = state.AddTransition(idle);
        exit.hasExitTime = true;
        exit.exitTime = 0.92f;
        exit.duration = 0.12f;
    }

    [MenuItem("Blighted Blossoms/Regenerar personajes (forzar)")]
    public static void ForceRebuild()
    {
        foreach (CharacterSource source in Sources)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(source.PrefabPath);
            if (existing != null) AssetDatabase.SetLabels(existing, new string[0]);
        }
        EnsurePrototypeAssets();
    }
}
