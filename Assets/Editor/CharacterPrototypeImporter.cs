using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class CharacterPrototypeImporter
{
    private const string ModelPath = "Assets/Models 3D/Character animation/Campana sin voz rig.fbx";
    private const string OutputFolder = "Assets/Resources/Characters";
    private const string ControllerPath = OutputFolder + "/CampanaPrototype.controller";
    private const string PrefabPath = OutputFolder + "/CampanaPrototype.prefab";
    private const string ImportMarker = "BlightedBlossomsPrototypeCharacterV2";

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

        AnimationClip idleClip = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__") && clip.length > 0.01f)
            .OrderByDescending(clip => clip.length)
            .FirstOrDefault();

        if (idleClip == null)
            Debug.LogWarning("El FBX del personaje no contiene un clip de animación con fotogramas válidos. Se generará el prefab sin movimiento idle.");

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state.name == "Idle");

        if (idleState == null)
            idleState = stateMachine.AddState("Idle");

        idleState.motion = idleClip;
        stateMachine.defaultState = idleState;

        GameObject instance = Object.Instantiate(model);
        instance.name = "Campana Prototype Character";

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);

        Animator sourceAnimator = instance.GetComponentInChildren<Animator>(true);
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
            animator = instance.AddComponent<Animator>();
        if (sourceAnimator != null && sourceAnimator != animator && sourceAnimator.avatar != null)
            animator.avatar = sourceAnimator.avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

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
