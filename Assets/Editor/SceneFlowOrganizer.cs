using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SceneFlowOrganizer
{
    private const string MainMenuPath = "Assets/Scenes/Menus/Main Menu.unity";
    private const string MultiplayerMenuPath = "Assets/Scenes/Menus/MultiplayerMenu.unity";

    [MenuItem("Blighted Blossoms/Organizar flujo de escenas")]
    public static void OrganizeScenes()
    {
        var mainScene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(mainScene, MultiplayerMenuPath, true))
            throw new System.InvalidOperationException("No se pudo crear MultiplayerMenu desde el menú funcional.");

        var multiplayerScene = EditorSceneManager.OpenScene(MultiplayerMenuPath, OpenSceneMode.Single);
        RemoveNamedObject(multiplayerScene, "Welcome Mesage");
        RemoveNamedObject(multiplayerScene, "Button Container");
        RemoveNamedObject(multiplayerScene, "Main Menu Manager");

        LobbyManager multiplayerLobby = Object.FindFirstObjectByType<LobbyManager>();
        Transform close = FindDeepChild(multiplayerScene, "Close");
        Button closeButton = close != null ? close.GetComponent<Button>() : null;
        if (multiplayerLobby != null && closeButton != null)
        {
            RemovePersistentListeners(closeButton);
            UnityEventTools.AddPersistentListener(closeButton.onClick, multiplayerLobby.ReturnToMainMenu);
        }
        EditorSceneManager.MarkSceneDirty(multiplayerScene);
        EditorSceneManager.SaveScene(multiplayerScene);

        mainScene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        RemoveNamedObject(mainScene, "Multyplayer Pop up");
        RemoveNamedObject(mainScene, "Lobby Manager");
        RemoveNamedObject(mainScene, "Network manager");

        MainMenuController mainController = Object.FindFirstObjectByType<MainMenuController>();
        Transform onlineButtonTransform = FindDeepChild(mainScene, "Online Mode Selection Button");
        Button onlineButton = onlineButtonTransform != null ? onlineButtonTransform.GetComponent<Button>() : null;
        if (mainController != null && onlineButton != null)
        {
            RemovePersistentListeners(onlineButton);
            UnityEventTools.AddPersistentListener(onlineButton.onClick, mainController.GoToMultiplayer);
        }
        EditorSceneManager.MarkSceneDirty(mainScene);
        EditorSceneManager.SaveScene(mainScene);
        AssetDatabase.SaveAssets();
        Debug.Log("Flujo organizado: Main Menu -> MultiplayerMenu -> OnlineArena");
    }

    public static void PrintMainMenuHierarchy()
    {
        var scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        StringBuilder output = new StringBuilder("MAIN_MENU_HIERARCHY\n");
        foreach (GameObject root in scene.GetRootGameObjects())
            AppendHierarchy(root.transform, output, 0);
        Debug.Log(output.ToString());
    }

    public static void ValidateSceneFlow()
    {
        var mainScene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        if (Object.FindFirstObjectByType<MainMenuController>() == null)
            throw new System.InvalidOperationException("Main Menu no contiene MainMenuController.");
        if (Object.FindFirstObjectByType<LobbyManager>() != null || Object.FindFirstObjectByType<NetworkLauncher>() != null)
            throw new System.InvalidOperationException("Main Menu todavía contiene managers multijugador.");

        var multiplayerScene = EditorSceneManager.OpenScene(MultiplayerMenuPath, OpenSceneMode.Single);
        if (Object.FindFirstObjectByType<LobbyManager>() == null || Object.FindFirstObjectByType<NetworkLauncher>() == null)
            throw new System.InvalidOperationException("MultiplayerMenu no contiene los managers requeridos.");
        if (FindDeepChild(multiplayerScene, "Multyplayer Pop up") == null)
            throw new System.InvalidOperationException("MultiplayerMenu no contiene el popup de modos.");

        Debug.Log("SCENE_FLOW_VALID: Main Menu -> MultiplayerMenu -> OnlineArena");
    }

    private static void AppendHierarchy(Transform current, StringBuilder output, int depth)
    {
        output.Append(' ', depth * 2).Append(current.name);
        Component[] components = current.GetComponents<Component>();
        output.Append(" [");
        for (int i = 0; i < components.Length; i++)
        {
            if (i > 0) output.Append(", ");
            output.Append(components[i] != null ? components[i].GetType().Name : "Missing");
        }
        output.AppendLine("]");

        foreach (Transform child in current)
            AppendHierarchy(child, output, depth + 1);
    }

    private static void RemoveNamedObject(UnityEngine.SceneManagement.Scene scene, string objectName)
    {
        Transform target = FindDeepChild(scene, objectName);
        if (target != null)
            Object.DestroyImmediate(target.gameObject);
    }

    private static Transform FindDeepChild(UnityEngine.SceneManagement.Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform result = FindDeepChild(root.transform, objectName);
            if (result != null) return result;
        }
        return null;
    }

    private static Transform FindDeepChild(Transform current, string objectName)
    {
        if (current.name == objectName) return current;
        foreach (Transform child in current)
        {
            Transform result = FindDeepChild(child, objectName);
            if (result != null) return result;
        }
        return null;
    }

    private static void RemovePersistentListeners(Button button)
    {
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
    }
}
