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

public static class PrototypeBuildTools
{
    private const string FirstScene = "Assets/Scenes/Menus/Type Ypur Name.unity";
    private const string BuildFolderName = "ProyectoFinal-WebGL";
    private const string ZipName = "ProyectoFinal-WebGL-ITCH.zip";

    [MenuItem("Blighted Blossoms/Validar prototipo")]
    public static void ValidateFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        List<string> errors = ValidateProject();
        if (errors.Count == 0)
            EditorUtility.DisplayDialog("Prototipo", "Validación completada sin errores.", "Aceptar");
        else
            EditorUtility.DisplayDialog("Prototipo", string.Join("\n", errors), "Aceptar");
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
            foreach (string scenePath in scenes)
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
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        foreach (string error in errors) Debug.LogError("[Preflight] " + error);
        if (errors.Count == 0) Debug.Log("[Preflight] Proyecto listo para compilar.");
        return errors;
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
