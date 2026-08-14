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

    private void LateUpdate()
    {
        if (target == null) return;

        // 1. Calcular la rotación exacta según Pitch y Yaw
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);

        // 2. Determinar la posición calculando el vector hacia atrás según la distancia
        Vector3 focusPoint = target.position + targetOffset;
        Vector3 desiredPosition = focusPoint - (cameraRotation * Vector3.forward * distance);

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

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
