using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Gameplay.Combat;

namespace UIScripts
{
    public class AttackJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("UI Elements")]
        [SerializeField] private RectTransform handle;
        [SerializeField] private float handleRange = 100f;

        // Eventos para desacoplar la UI de la lógica de combate
        public event Action<AimData> OnAiming;
        public event Action<AimData> OnReleased;

        private Vector2 inputVector = Vector2.zero;
        private RectTransform baseRect;
        private Camera mainCamera;

        private void Awake()
        {
            baseRect = GetComponent<RectTransform>();
            mainCamera = Camera.main; // Referencia a la cámara principal
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 position = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, baseRect.position);
            inputVector = (eventData.position - position) / (baseRect.sizeDelta / 2f);
            
            if (inputVector.magnitude > 1f) 
                inputVector.Normalize();

            handle.anchoredPosition = inputVector * (baseRect.sizeDelta.x / 2f) * (handleRange / 100f);

            // Convertir la dirección relativa al ángulo actual de la cámara
            Vector3 worldDirection = GetCameraRelativeDirection(inputVector);
            
            OnAiming?.Invoke(new AimData
            {
                Direction = worldDirection,
                DistanceRatio = Mathf.Clamp01(inputVector.magnitude),
                IsTap = false
            });
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Vector3 worldDirection = GetCameraRelativeDirection(inputVector);
            bool isTap = inputVector.magnitude < 0.2f;

            OnReleased?.Invoke(new AimData
            {
                Direction = worldDirection,
                DistanceRatio = Mathf.Clamp01(inputVector.magnitude),
                IsTap = isTap
            });

            // Reset UI
            inputVector = Vector2.zero;
            handle.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Transforma la entrada 2D del Joystick en una dirección 3D (X, Z) alineada con la vista de la cámara.
        /// </summary>
        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            if (input == Vector2.zero) return Vector3.zero;

            if (mainCamera == null)
                mainCamera = Camera.main;

            // Obtener los vectores Forward y Right de la cámara en el plano horizontal (XZ)
            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;

            // Anular el componente Y para que el disparo siempre quede paralelo al suelo
            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            // Proyectar el input 2D en el espacio 3D de la cámara
            Vector3 direction = (camForward * input.y) + (camRight * input.x);
            return direction.normalized;
        }
    }
}