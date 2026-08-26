using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Gameplay.Combat;
using UnityEngine.AI;

public static class PrototypeBuildTools
{
    private const string FirstScene = "Assets/Scenes/Menus/Blighted Intro.unity";
    private const string BuildFolderName = "ProyectoFinal-WebGL";
    private const string ZipName = "ProyectoFinal-WebGL-ITCH.zip";

    [MenuItem("Blighted Blossoms/Validar prototipo _F9")]
    public static void ValidateFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        List<string> errors = ValidateProject();
        if (errors.Count == 0)
            EditorUtility.DisplayDialog("Prototipo", "Validación completada sin errores.", "Aceptar");
        else
            EditorUtility.DisplayDialog("Prototipo", string.Join("\n", errors), "Aceptar");
    }

    public static void ValidateBatch()
    {
        List<string> errors = ValidateProject();
        if (errors.Count > 0)
            throw new InvalidOperationException("Validación fallida:\n" + string.Join("\n", errors));
        Debug.Log("[Preflight] Validación batch completa.");
    }

    [MenuItem("Blighted Blossoms/Crear WebGL para itch.io")]
    public static void BuildItchWebGl()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        List<string> errors = ValidateProject();
        if (errors.Count > 0)
            throw new InvalidOperationException("Preflight falló:\n" + string.Join("\n", errors));

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string buildsRoot = Path.Combine(projectRoot, "Builds");
        string outputFolder = Path.Combine(buildsRoot, BuildFolderName);
        string zipPath = Path.Combine(buildsRoot, ZipName);

        Directory.CreateDirectory(buildsRoot);
        if (Directory.Exists(outputFolder)) Directory.Delete(outputFolder, true);
        if (File.Exists(zipPath)) File.Delete(zipPath);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = outputFolder,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"El build WebGL falló: {report.summary.result}");

        string indexPath = Path.Combine(outputFolder, "index.html");
        if (!File.Exists(indexPath))
            throw new FileNotFoundException("Unity terminó el build, pero no creó index.html.", indexPath);

        CreateItchZip(outputFolder, zipPath);
        Debug.Log($"[Build] WebGL: {outputFolder}\n[Build] ZIP itch.io: {zipPath}");
        EditorUtility.RevealInFinder(zipPath);
    }

    public static void BuildMultiplayerSmoke()
    {
        List<string> errors = ValidateProject();
        if (errors.Count > 0)
            throw new InvalidOperationException("Preflight smoke falló:\n" + string.Join("\n", errors));

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputFolder = Path.Combine(projectRoot, "Builds", "MultiplayerSmoke");
        Directory.CreateDirectory(outputFolder);
        string executable = Path.Combine(outputFolder, "BlightedBlossomsSmoke.exe");
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = executable,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"Build multijugador falló: {report.summary.result}");
        Debug.Log($"[Build] Cliente de smoke multijugador: {executable}");
    }

    private static List<string> ValidateProject()
    {
        List<string> errors = new List<string>();
        string[] scenes = EnabledScenes();
        if (scenes.Length == 0) errors.Add("No hay escenas habilitadas en Build Settings.");
        else if (scenes[0] != FirstScene) errors.Add($"La primera escena debe ser {FirstScene}.");

        string photonSettings = Path.Combine(Application.dataPath, "Photon/Fusion/Resources/PhotonAppSettings.asset");
        string appIdLine = File.Exists(photonSettings)
            ? File.ReadLines(photonSettings).FirstOrDefault(line => line.TrimStart().StartsWith("AppIdFusion:"))
            : null;
        string appId = appIdLine == null ? string.Empty : appIdLine.Split(new[] { ':' }, 2)[1].Trim();
        if (string.IsNullOrWhiteSpace(appId))
            errors.Add("Photon Fusion no tiene un App ID detectable.");

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            string[] allScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .ToArray();
            foreach (string scenePath in allScenes)
            {
                if (!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, scenePath)))
                {
                    errors.Add($"No existe la escena: {scenePath}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                    CountMissingScripts(root, scenePath, errors);
            }
        }
        finally
        {
            // En batchmode Unity arranca sin una escena de Editor cargada y
            // devuelve un setup vacío. RestoreSceneManagerSetup exige al menos
            // una escena activa, así que solo se restaura cuando realmente
            // existía una sesión visual previa.
            if (previousSetup != null && previousSetup.Any(item => item.isLoaded))
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        ValidateCharacters(errors);
        ValidateArenaResources(errors);
        ValidateMultiplayer(errors);
        ValidatePrefabs(errors);

        foreach (string error in errors) Debug.LogError("[Preflight] " + error);
        if (errors.Count == 0) Debug.Log("[Preflight] Proyecto listo para compilar.");
        return errors;
    }

    private static void ValidateCharacters(List<string> errors)
    {
        CharacterDefinition[] definitions = Resources.LoadAll<CharacterDefinition>("Characters/Definitions")
            .OrderBy(definition => definition.sortOrder)
            .ToArray();
        if (definitions.Length != 6)
            errors.Add($"Se esperaban 6 fichas de personaje y se encontraron {definitions.Length}.");

        for (int i = 0; i < definitions.Length; i++)
        {
            CharacterDefinition definition = definitions[i];
            if (definition.sortOrder != i)
                errors.Add($"{definition.name}: sortOrder {definition.sortOrder}; se esperaba {i}.");
            if (definition.basicAbility == null || definition.ultimateAbility == null)
                errors.Add($"{definition.name}: faltan habilidades configurables.");
            else
            {
                if (Mathf.Abs(definition.basicAbility.cooldown - 3f) > 0.001f)
                    errors.Add($"{definition.name}: cooldown básico distinto de 3 s.");
                if (Mathf.Abs(definition.ultimateAbility.cooldown - 15f) > 0.001f)
                    errors.Add($"{definition.name}: cooldown de definitiva distinto de 15 s.");
            }

            GameObject prefab = Resources.Load<GameObject>(definition.prefabPath);
            if (prefab == null)
            {
                errors.Add($"{definition.name}: no existe Resources/{definition.prefabPath}.");
                continue;
            }
            if (prefab.GetComponentInChildren<Renderer>(true) == null)
                errors.Add($"{definition.name}: prefab sin renderer.");
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null)
                errors.Add($"{definition.name}: prefab sin Animator Controller.");

            float expectedHeight = PlayerController.DesiredCharacterHeight(i);
            float measuredHeight = CharacterPrototypeImporter.MeasureGameplayHeight(prefab);
            if (expectedHeight <= 0.1f)
                errors.Add($"{definition.name}: falta calibrar expectedGameplayHeight (Actualizar personajes/F6).");
            else if (measuredHeight <= 0.1f)
                errors.Add($"{definition.name}: no fue posible medir los bounds visuales.");
            else
            {
                float deviation = Mathf.Abs(measuredHeight - expectedHeight) / expectedHeight;
                if (deviation > 0.08f)
                    errors.Add($"{definition.name}: altura visual {measuredHeight:0.00}; " +
                               $"se esperaba {expectedHeight:0.00} (desvío {deviation:P0}).");
            }

            float authoredVerticalOffset = Mathf.Abs(definition.modelLocalOffset.y *
                                                       PlayerController.GameplayPlayerScale);
            if (expectedHeight > 0.1f && authoredVerticalOffset > expectedHeight * 0.15f)
                errors.Add($"{definition.name}: modelLocalOffset.y es demasiado grande y puede dejar el mesh flotando o enterrado.");
        }
    }

    private static void ValidateArenaResources(List<string> errors)
    {
        if (Resources.Load<Material>("Vfx/Powers/RayUltimate") == null)
            errors.Add("Falta Resources/Vfx/Powers/RayUltimate: WebGL podría ocultar los VFX por shader stripping.");

        string[] pickupSprites =
        {
            "Vfx/Pickups/VitalityBloom",
            "Vfx/Pickups/HasteSeed",
            "Vfx/Pickups/PowerSeed"
        };
        foreach (string path in pickupSprites)
            if (Resources.Load<Sprite>(path) == null)
                errors.Add($"Falta el sprite identificable del beneficio Resources/{path}.");

        for (int character = 0; character < CharacterCatalog.Count; character++)
        {
            foreach (AbilitySlot slot in new[] { AbilitySlot.Basic, AbilitySlot.Ultimate })
            {
                AbilityDefinition ability = CharacterCatalog.AbilityOf(character, slot);
                if (ability == null) errors.Add($"{CharacterCatalog.NameOf(character)} no tiene {slot}.");
                else if (ability.range < 5.5f)
                    errors.Add($"{CharacterCatalog.NameOf(character)} · {ability.DisplayName}: rango demasiado corto ({ability.range:0.0}).");
            }
        }
    }

    private static void ValidateMultiplayer(List<string> errors)
    {
        int[] expectedPlayers = { 2, 4, 6 };
        for (int modeIndex = 0; modeIndex < MatchModeCatalog.All.Length; modeIndex++)
        {
            MatchModeDefinition mode = MatchModeCatalog.All[modeIndex];
            if (mode.PlayerCount != expectedPlayers[modeIndex])
                errors.Add($"{mode.Key}: capacidad {mode.PlayerCount}; se esperaba {expectedPlayers[modeIndex]}.");

            HashSet<Vector3> positions = new HashSet<Vector3>();
            for (int team = 0; team < MatchTeams.TeamCount; team++)
                for (int slot = 0; slot < mode.TeamSize; slot++)
                    if (!positions.Add(MatchTeams.SpawnOffset(team, slot, mode.TeamSize)))
                        errors.Add($"{mode.Key}: spawn duplicado para equipo {team}, slot {slot}.");
        }

        const string playerPath = "Assets/Scenes/Testing/Prefabs/Player.prefab";
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
        if (player == null) errors.Add($"No existe {playerPath}.");
        else
        {
            if (player.GetComponent<NetworkObject>() == null) errors.Add("Player prefab sin NetworkObject.");
            if (player.GetComponent<NetworkTransform>() == null) errors.Add("Player prefab sin NetworkTransform.");
            if (player.GetComponent<NavMeshAgent>() == null) errors.Add("Player prefab sin NavMeshAgent.");
            if (player.GetComponent<PlayerController>() == null) errors.Add("Player prefab sin PlayerController.");
        }
    }

    private static void ValidatePrefabs(List<string> errors)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) CountMissingScripts(prefab, path, errors);
        }
    }

    private static void CountMissingScripts(GameObject gameObject, string scenePath, List<string> errors)
    {
        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
        if (missing > 0) errors.Add($"{scenePath}: {gameObject.name} tiene {missing} script(s) perdido(s).");
        foreach (Transform child in gameObject.transform)
            CountMissingScripts(child.gameObject, scenePath, errors);
    }

    private static string[] EnabledScenes()
    {
        return EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
    }

    private static void CreateItchZip(string sourceFolder, string zipPath)
    {
        using FileStream stream = new FileStream(zipPath, FileMode.CreateNew);
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (string file in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceFolder, file).Replace('\\', '/');
            ZipArchiveEntry entry = archive.CreateEntry(relativePath, System.IO.Compression.CompressionLevel.Optimal);
            using Stream input = File.OpenRead(file);
            using Stream output = entry.Open();
            input.CopyTo(output);
        }
    }
}
