using UnityEngine;

[ExecuteAlways] // Permite ajustar el ángulo y distancia en tiempo de diseño desde el Editor
public class MobaCamera : MonoBehaviour
{
    [Header("Objetivo")]
    [SerializeField] private Transform target;

    [Header("Ángulos de Vista LoL/Wild Rift")]
    [Range(30f, 70f)] 
    [SerializeField] private float pitch = 52f;   // Inclinación vertical (hacia abajo)
    [Range(-180f, 180f)] 
    [SerializeField] private float yaw = -35f;   // Giro en diagonal para ver la grieta como en LoL

    [Header("Distancia y Posición")]
    [SerializeField] private float distance = 18f; // Lejanía perfecta (estilo Wild Rift/PC)
    [SerializeField] private Vector3 targetOffset = new Vector3(0, 1.2f, 0); // Altura del centro del jugador

    [Header("Suavizado de Seguimiento")]
    [SerializeField] private bool smoothFollow = true;
    [SerializeField] private float followSpeed = 12f;

    [Header("Colisión de Cámara")]
    [SerializeField] private bool avoidObstacles = true;
    [SerializeField, Min(0.1f)] private float collisionRadius = 0.55f;
    [SerializeField, Min(0.05f)] private float collisionPadding = 0.35f;
    [SerializeField, Min(1f)] private float minimumDistance = 3f;

    private void LateUpdate()
    {
        if (target == null) return;

        // 1. Calcular la rotación exacta según Pitch y Yaw
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);

        // 2. Determinar la posición calculando el vector hacia atrás según la distancia
        Vector3 focusPoint = target.position + targetOffset;
        Vector3 desiredPosition = focusPoint - (cameraRotation * Vector3.forward * distance);

        if (Application.isPlaying && avoidObstacles)
            desiredPosition = ResolveObstacleCollision(focusPoint, desiredPosition);

        // 3. Aplicar posición (Suave para no causar tirones o Rígido si desactivas el bool)
        if (Application.isPlaying && smoothFollow)
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        }
        else
        {
            transform.position = desiredPosition;
        }

        // 4. Fijar la rotación exacta
        transform.rotation = cameraRotation;
    }

    private Vector3 ResolveObstacleCollision(Vector3 focusPoint, Vector3 desiredPosition)
    {
        Vector3 cameraVector = desiredPosition - focusPoint;
        float desiredDistance = cameraVector.magnitude;
        if (desiredDistance <= 0.001f) return desiredPosition;

        Vector3 direction = cameraVector / desiredDistance;
        RaycastHit[] hits = Physics.SphereCastAll(
            focusPoint,
            collisionRadius,
            direction,
            desiredDistance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float nearestObstacle = desiredDistance;
        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;
            if (hitTransform == target || hitTransform.IsChildOf(target))
                continue;
            if (hit.collider.GetComponentInParent<PlayerController>() != null)
                continue;

            nearestObstacle = Mathf.Min(nearestObstacle, hit.distance);
        }

        float obstacleSafeDistance = nearestObstacle - collisionPadding;
        float safeDistance = nearestObstacle < minimumDistance + collisionPadding
            ? Mathf.Clamp(obstacleSafeDistance, 0.2f, desiredDistance)
            : Mathf.Clamp(obstacleSafeDistance, minimumDistance, desiredDistance);
        return focusPoint + direction * safeDistance;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
