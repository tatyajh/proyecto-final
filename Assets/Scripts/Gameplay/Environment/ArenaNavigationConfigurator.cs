using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Ajusta la navegación del Árbol de la Abundancia al cargar una arena. Las
/// raíces bajas forman parte del suelo jugable; troncos, pilares y paredes
/// conservan sus bloqueos. Se hace en runtime para que Movement y OnlineArena
/// compartan exactamente la misma regla sin duplicar cambios del prefab.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-120)]
public sealed class ArenaNavigationConfigurator : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForArena()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene != "Movement" && scene != "OnlineArena") return;
        if (FindFirstObjectByType<ArenaNavigationConfigurator>() != null) return;
        new GameObject("Arena Navigation Rules").AddComponent<ArenaNavigationConfigurator>();
    }

    private void Awake()
    {
        ConfigureTreeNavigation();
    }

    private static void ConfigureTreeNavigation()
    {
        Transform treeRoot = null;
        foreach (Transform candidate in FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!candidate.name.Equals("Arbol de la Abundancia",
                    System.StringComparison.OrdinalIgnoreCase)) continue;
            // El nombre está sobrescrito sobre la raíz del modelo anidado. Sus
            // obstáculos viven en el prefab exterior, por eso necesitamos la
            // raíz real de la instancia y no solo el Transform nombrado.
            treeRoot = candidate.root;
            break;
        }
        if (treeRoot == null)
        {
            foreach (Renderer renderer in FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!renderer.name.Equals("Tree", System.StringComparison.OrdinalIgnoreCase)) continue;
                treeRoot = renderer.transform.root;
                break;
            }
        }
        if (treeRoot == null)
        {
            Debug.LogWarning("[ArenaNavigation] No se encontró el árbol para liberar sus raíces bajas.");
            return;
        }

        int released = 0;
        foreach (NavMeshObstacle obstacle in treeRoot.GetComponentsInChildren<NavMeshObstacle>(true))
        {
            if (!obstacle.gameObject.activeInHierarchy) continue;
            Collider collider = obstacle.GetComponent<Collider>();
            Vector3 worldSize = collider != null
                ? collider.bounds.size
                : EstimateWorldSize(obstacle);
            if (!IsWalkableLowRoot(obstacle, worldSize)) continue;
            obstacle.enabled = false;
            released++;
        }

        Debug.Log($"[ArenaNavigation] {released} raíces bajas liberadas; troncos altos permanecen bloqueados.");
    }

    private static Vector3 EstimateWorldSize(NavMeshObstacle obstacle)
    {
        Vector3 localSize = obstacle.shape == NavMeshObstacleShape.Box
            ? obstacle.size
            : new Vector3(obstacle.radius * 2f, obstacle.height, obstacle.radius * 2f);
        Vector3 scale = obstacle.transform.lossyScale;
        return Vector3.Scale(localSize,
            new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
    }

    private static bool IsWalkableLowRoot(NavMeshObstacle obstacle, Vector3 size)
    {
        // El árbol usa una caja ancha para bloquear en conjunto sus raíces y
        // cilindros altos independientes para cada tronco. Liberar solo cajas
        // permite atravesar/subir las raíces sin volver transitables troncos.
        if (obstacle.shape == NavMeshObstacleShape.Box) return true;

        float horizontal = Mathf.Max(size.x, size.z);
        float thickness = Mathf.Min(size.x, size.z);
        bool lowEnoughToStepOnto = size.y <= 2.8f;
        bool shapedLikeRoot = horizontal >= Mathf.Max(1.2f, thickness * 1.75f);
        return lowEnoughToStepOnto && shapedLikeRoot;
    }
}
