using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Entrada directa a la arena para probar el personaje guardado sin Photon.</summary>
[InitializeOnLoad]
public static class LocalCharacterTestTool
{
    private const string ArenaPath = "Assets/Scenes/Multiplayer/OnlineArena.unity";
    private const string IntroPath = "Assets/Scenes/Menus/Blighted Intro.unity";
    private const string SmokeKey = "BlightedBlossoms.LocalCharacterSmoke";
    private static int smokeFrames;

    static LocalCharacterTestTool()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    [MenuItem("Blighted Blossoms/Pruebas/Probar personaje seleccionado %#t", false, 40)]
    public static void PlaySelectedCharacter()
    {
        SceneAsset arena = AssetDatabase.LoadAssetAtPath<SceneAsset>(ArenaPath);
        if (arena == null)
        {
            Debug.LogError($"No se encontró la arena local en '{ArenaPath}'.");
            return;
        }

        PlayModeContext.UseLocalStory();
        OnlineMatchState.Reset();
        EditorSceneManager.playModeStartScene = arena;
        SessionState.SetBool(SmokeKey, true);
        Debug.Log("[LocalCharacterTest] Iniciando arena sin matchmaking. WASD/clic mueve, Q ataca y E usa la definitiva.");
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Blighted Blossoms/Pruebas/Probar menú completo %#m", false, 41)]
    public static void PlayIntroMenu()
    {
        SceneAsset intro = AssetDatabase.LoadAssetAtPath<SceneAsset>(IntroPath);
        if (intro == null)
        {
            Debug.LogError($"No se encontró el menú en '{IntroPath}'.");
            return;
        }

        BlightedIntroFlow.ReturnDirectlyToMenu = false;
        EditorSceneManager.playModeStartScene = intro;
        Debug.Log("[LocalCharacterTest] Iniciando el flujo completo del menú.");
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SmokeKey, false)) return;
        smokeFrames = 0;
        EditorApplication.update += RunSmokeWhenReady;
    }

    private static void RunSmokeWhenReady()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= RunSmokeWhenReady;
            return;
        }

        if (++smokeFrames < 30) return;
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        bool basic = player.TryExecuteAttack(Vector3.forward, false);
        bool ultimate = player.TryExecuteAttack(Vector3.forward, true);
        Debug.Log($"[LocalCharacterTest] Smoke test: movimiento listo, ataque={basic}, definitiva={ultimate}.");
        SessionState.SetBool(SmokeKey, false);
        EditorApplication.update -= RunSmokeWhenReady;
    }
}
