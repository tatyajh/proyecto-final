using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Combat
{
    /// <summary>
    /// Obstáculo determinista creado por el RPC de Bastión. No necesita un
    /// NetworkObject: todos los clientes lo crean con la misma pose y duración.
    /// </summary>
    public sealed class TemporaryAbilityWall : MonoBehaviour
    {
        private Material runtimeMaterial;

        public static void Spawn(Vector3 center, Vector3 forward, float width, float duration, Color color)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Terramor Bastion";
            wall.transform.position = center + Vector3.up * 1.5f;
            Vector3 planarForward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            wall.transform.rotation = Quaternion.LookRotation(planarForward);
            wall.transform.localScale = new Vector3(Mathf.Max(3f, width * 2f), 3f, 0.75f);

            TemporaryAbilityWall behaviour = wall.AddComponent<TemporaryAbilityWall>();
            behaviour.Configure(color, duration);
        }

        private void Configure(Color color, float duration)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            runtimeMaterial = shader != null ? new Material(shader) : null;
            if (runtimeMaterial != null)
            {
                runtimeMaterial.color = color;
                Renderer renderer = GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = runtimeMaterial;
            }

            NavMeshObstacle obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            Destroy(gameObject, Mathf.Max(0.5f, duration));
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}
