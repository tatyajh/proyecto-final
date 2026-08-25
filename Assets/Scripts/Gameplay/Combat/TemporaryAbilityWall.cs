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
        private const string CursedRoseWallResource = "Vfx/Terramor/CursedRoseWall";

        public static void Spawn(Vector3 center, Vector3 forward, float width, float duration, Color color)
        {
            GameObject wall = new GameObject("Terramor · Bastión de rosas marchitas");
            wall.transform.position = center;
            Vector3 planarForward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            wall.transform.rotation = Quaternion.LookRotation(planarForward);

            TemporaryAbilityWall behaviour = wall.AddComponent<TemporaryAbilityWall>();
            behaviour.Configure(Mathf.Max(3f, width * 2f), duration);
        }

        private void Configure(float wallWidth, float duration)
        {
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.center = Vector3.up * 1.5f;
            collider.size = new Vector3(wallWidth, 3f, 0.75f);

            NavMeshObstacle obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = collider.center;
            obstacle.size = collider.size;
            obstacle.carving = true;

            Sprite cursedRoses = Resources.Load<Sprite>(CursedRoseWallResource);
            if (cursedRoses != null)
            {
                CreateRosePlane(cursedRoses, wallWidth, 0f, 0f, 1f, 3);
                CreateRosePlane(cursedRoses, wallWidth, -0.12f, -8f, 0.72f, 2);
                CreateRosePlane(cursedRoses, wallWidth, 0.12f, 8f, 0.72f, 2);
            }
            else
            {
                Debug.LogWarning($"[Terramor] No se encontró el arte {CursedRoseWallResource}; " +
                                 "el Bastión conserva su colisión para no alterar el combate.", this);
            }

            Destroy(gameObject, Mathf.Max(0.5f, duration));
        }

        private void CreateRosePlane(Sprite sprite, float wallWidth, float depth, float yaw,
            float alpha, int sortingOrder)
        {
            GameObject plane = new GameObject("Rosas malditas");
            plane.transform.SetParent(transform, false);
            plane.transform.localPosition = new Vector3(0f, 1.55f, depth);
            plane.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            float scaleByWidth = wallWidth / Mathf.Max(0.01f, sprite.bounds.size.x);
            float scaleByHeight = 3.4f / Mathf.Max(0.01f, sprite.bounds.size.y);
            float visualScale = Mathf.Min(scaleByWidth, scaleByHeight);
            plane.transform.localScale = Vector3.one * visualScale;

            SpriteRenderer renderer = plane.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 1f, 1f, alpha);
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
