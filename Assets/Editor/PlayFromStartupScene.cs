using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Makes the Editor Play button follow the same entry point as a real build,
/// regardless of which scene a developer currently has open.
/// </summary>
[InitializeOnLoad]
internal static class PlayFromStartupScene
{
    private const string StartupScenePath = "Assets/Scenes/Menus/Type Ypur Name.unity";

    static PlayFromStartupScene()
    {
        SceneAsset startupScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(StartupScenePath);

        if (startupScene == null)
        {
            UnityEngine.Debug.LogError($"No se encontró la escena inicial en '{StartupScenePath}'.");
            return;
        }

        EditorSceneManager.playModeStartScene = startupScene;
    }
}
