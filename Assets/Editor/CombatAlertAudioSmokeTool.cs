using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Verifica recursos, canal SFX, prioridad y reinicio de alertas.</summary>
[InitializeOnLoad]
public static class CombatAlertAudioSmokeTool
{
    private const string ScenePath = "Assets/Scenes/Testing/Movement.unity";
    private const string SessionKey = "BlightedBlossoms.CombatAudioSmoke";
    private static readonly List<string> failures = new List<string>();
    private static double nextStep;
    private static int step;
    private static PlayerController player;
    private static CombatAlertAudioController alerts;

    static CombatAlertAudioSmokeTool() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

    public static void RunBatch()
    {
        failures.Clear();
        step = 0;
        player = null;
        alerts = null;
        SessionState.SetBool(SessionKey, true);
        EditorSceneManager.OpenScene(ScenePath);
        EditorSceneManager.playModeStartScene = null;
        EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            nextStep = EditorApplication.timeSinceStartup + 2.5;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(SessionKey, false);
            foreach (string failure in failures) Debug.LogError("[CombatAudioSmoke] " + failure);
            if (failures.Count == 0)
                Debug.Log("[CombatAudioSmoke] Proximidad, vida crítica y reinicio de ronda verificados.");
            if (Application.isBatchMode) EditorApplication.Exit(failures.Count == 0 ? 0 : 2);
        }
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextStep) return;
        try
        {
            switch (step++)
            {
                case 0:
                    foreach (PlayerController candidate in UnityEngine.Object.FindObjectsByType<PlayerController>(
                                 FindObjectsSortMode.None))
                        if (candidate.HasLocalControl) { player = candidate; break; }
                    if (player == null) { Fail("No se creó el jugador local."); return; }
                    alerts = CombatAlertAudioController.EnsureFor(player);
                    alerts.ResetForRound();
                    alerts.DebugPlayApproachAlert();
                    nextStep = EditorApplication.timeSinceStartup + 0.25;
                    break;
                case 1:
                    if (alerts.DebugCurrentAlert != "enemy_approach" || !alerts.DebugIsPlaying)
                        failures.Add("La alerta de proximidad no inició en el canal SFX.");
                    player.ApplyCorruptionDamage(Mathf.CeilToInt(player.HealthMaximum * 0.8f));
                    nextStep = EditorApplication.timeSinceStartup + 0.35;
                    break;
                case 2:
                    if (alerts.DebugCurrentAlert != "low_health" || !alerts.DebugIsPlaying)
                        failures.Add("La alerta de vida crítica no interrumpió la proximidad.");
                    player.ResetLocalCombatState(player.transform.position, player.transform.rotation);
                    if (alerts.DebugResultPlayed || alerts.DebugIsPlaying)
                        failures.Add("El canal no se reinició al comenzar una ronda.");
                    Stop();
                    break;
            }
        }
        catch (Exception exception) { Fail(exception.ToString()); }
    }

    private static void Fail(string message)
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
