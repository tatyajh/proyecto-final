using System;
using System.Collections.Generic;
using Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Smoke test ejecutable en batch: abre Movement directamente, instancia los
/// seis personajes y lanza ambas habilidades de cada uno.
/// </summary>
[InitializeOnLoad]
public static class MovementCombatSmokeTool
{
    private const string MovementScene = "Assets/Scenes/Testing/Movement.unity";
    private const string SessionKey = "BlightedBlossoms.MovementCombatSmoke";
    private static readonly List<string> failures = new List<string>();
    private static CharacterSpawner spawner;
    private static PlayerController player;
    private static int characterIndex = -1;
    private static double nextStep;
    private static int exitCode;

    static MovementCombatSmokeTool()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void RunBatch()
    {
        SessionState.SetBool(SessionKey, true);
        failures.Clear();
        characterIndex = -1;
        exitCode = 0;
        EditorSceneManager.OpenScene(MovementScene);
        EditorSceneManager.playModeStartScene = null;
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            nextStep = EditorApplication.timeSinceStartup + 2.0;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(SessionKey, false);
            if (failures.Count > 0)
            {
                foreach (string failure in failures) Debug.LogError("[MovementSmoke] " + failure);
                exitCode = 2;
            }
            else Debug.Log("[MovementSmoke] Seis personajes, materiales, animadores y 12 lanzamientos verificados.");

            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextStep) return;
        try
        {
            if (spawner == null) spawner = UnityEngine.Object.FindFirstObjectByType<CharacterSpawner>();
            if (spawner == null)
            {
                FailAndStop("Movement no contiene CharacterSpawner.");
                return;
            }

            if (player == null)
            {
                characterIndex++;
                if (characterIndex >= CharacterCatalog.Count)
                {
                    Stop();
                    return;
                }

                GameObject instance = spawner.SpawnSelectedCharacter(characterIndex);
                player = instance != null ? instance.GetComponent<PlayerController>() : null;
                if (player == null)
                    failures.Add($"{CharacterCatalog.NameOf(characterIndex)} no creó PlayerController.");
                nextStep = EditorApplication.timeSinceStartup + 1.0;
                return;
            }

            ValidateCurrentCharacter();
            player = null;
            nextStep = EditorApplication.timeSinceStartup + 0.35;
        }
        catch (Exception exception)
        {
            FailAndStop(exception.ToString());
        }
    }

    private static void ValidateCurrentCharacter()
    {
        string name = CharacterCatalog.NameOf(characterIndex);
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) failures.Add($"{name} no tiene renderers.");
        foreach (Renderer renderer in renderers)
            foreach (Material material in renderer.sharedMaterials)
                if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                    failures.Add($"{name} tiene un material o shader inválido en {renderer.name}.");

        Animator animator = player.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
            failures.Add($"{name} no tiene Animator Controller en Movement.");

        AimData aim = new AimData { Direction = player.transform.forward, DistanceRatio = 1f, IsTap = true };
        if (!player.TryCastAbility(AbilitySlot.Basic, aim)) failures.Add($"{name}: básica no se pudo lanzar.");
        if (!player.TryCastAbility(AbilitySlot.Ultimate, aim)) failures.Add($"{name}: definitiva no se pudo lanzar.");
    }

    private static void FailAndStop(string message)
    {
        failures.Add(message);
        Stop();
    }

    private static void Stop()
    {
        EditorApplication.update -= Tick;
        EditorApplication.isPlaying = false;
    }
}
