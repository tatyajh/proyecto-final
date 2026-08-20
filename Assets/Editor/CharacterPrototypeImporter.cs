using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CharacterPrototypeImporter
{
    private const string ModelPath = "Assets/Models 3D/Character animation/Campana sin voz rig.fbx";
    private const string OutputFolder = "Assets/Resources/Characters";
    private const string PrefabPath = OutputFolder + "/CampanaPrototype.prefab";
    private const string ControllerPath = OutputFolder + "/CampanaPrototype.controller";
    private const string ImportMarker = "BlightedBlossomsPrototypeCharacterV3";

    static CharacterPrototypeImporter()
    {
        EditorApplication.delayCall += EnsurePrototypeAssets;
    }

    [MenuItem("Blighted Blossoms/Actualizar personaje prototipo")]
    public static void EnsurePrototypeAssets()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) return;

        Directory.CreateDirectory(OutputFolder);
        AssetDatabase.Refresh();
        if (ConfigureAnimationImport())
        {
            EditorApplication.delayCall += EnsurePrototypeAssets;
            return;
        }

        GameObject instance = Object.Instantiate(model);
        instance.name = "Campana Prototype Character";

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);

        // El Animator se borraba porque com.unity.modules.animation no estaba
        // en el proyecto y dejaba un componente faltante. Ya está habilitado,
        // así que ahora se conserva y se le conecta el controller: sin esto el
        // personaje entraba a la arena completamente inmóvil.
        Animator animator = instance.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = instance.AddComponent<Animator>();

        if (animator.runtimeAnimatorController == null)
        {
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller != null)
                animator.runtimeAnimatorController = controller;
            else
                Debug.LogWarning($"[CharacterPrototypeImporter] No se encontró el controller en {ControllerPath}.");
        }

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Blighted Blossoms/Ver personaje Heliandra")]
    public static void OpenCharacterPreview()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            EnsurePrototypeAssets();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            AssetDatabase.OpenAsset(prefab);
        }
    }

    private static bool ConfigureAnimationImport()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null || importer.userData == ImportMarker) return false;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations
            .Where(clip => clip.lastFrame - clip.firstFrame > 0.01f)
            .ToArray();
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = true;
            clip.loopPose = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            clip.keepOriginalOrientation = true;
        }

        importer.clipAnimations = clips;
        importer.userData = ImportMarker;
        importer.SaveAndReimport();
        return true;
    }
}
