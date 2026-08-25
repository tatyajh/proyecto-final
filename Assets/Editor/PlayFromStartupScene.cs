using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the Editor Play button follow the real build entry point, except for
/// Movement: that scene is the intentional direct-entry combat workbench.
/// </summary>
[InitializeOnLoad]
internal static class PlayFromStartupScene
{
    private const string StartupScenePath = "Assets/Scenes/Menus/Blighted Intro.unity";
    private const string MovementScenePath = "Assets/Scenes/Testing/Movement.unity";
    internal const string ExplicitPlayStartKey = "BlightedBlossoms.ExplicitPlayStart";

    static PlayFromStartupScene()
    {
        EditorApplication.delayCall += () => ApplyForActiveScene();
        EditorSceneManager.activeSceneChangedInEditMode += (_, _) => ApplyForActiveScene();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (SessionState.GetBool(ExplicitPlayStartKey, false))
                    SessionState.SetBool(ExplicitPlayStartKey, false);
                else
                    ApplyForActiveScene(true);
            }

            // Las herramientas "Probar personaje" y "Probar menú" pueden
            // imponer su propia escena durante Play. Al volver a edición se
            // restaura la regla normal según la escena que el usuario dejó abierta.
            if (state == PlayModeStateChange.EnteredEditMode) ApplyForActiveScene();
        };
    }

    private static void ApplyForActiveScene(bool allowPlayTransition = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode && !allowPlayTransition) return;

        // Movement es el banco técnico: Play debe ejecutar exactamente lo que
        // está abierto para que CombatTrainingBootstrap arranque sin menú.
        if (SceneManager.GetActiveScene().path == MovementScenePath)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        SceneAsset startupScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(StartupScenePath);
        if (startupScene == null)
        {
            UnityEngine.Debug.LogError($"No se encontró la escena inicial en '{StartupScenePath}'.");
            return;
        }

        EditorSceneManager.playModeStartScene = startupScene;
    }
}
