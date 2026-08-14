using UnityEngine;

namespace Gameplay.Combat
{
    public class SkillAimIndicator : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private GameObject rangeIndicator;     // Quad del Círculo
        [SerializeField] private GameObject directionIndicator; // Quad de la Flecha Roja

        [Header("Floor Alignment")]
        [SerializeField] private float floorOffset = -1f; 

        private void Awake()
        {
            HideIndicators();
        }

        public void ShowIndicators(float range)
        {
            if (rangeIndicator) 
            {
                rangeIndicator.SetActive(true);
                rangeIndicator.transform.localScale = new Vector3(range * 2f, range * 2f, 1f);
            }

            if (directionIndicator) 
            {
                directionIndicator.SetActive(true);
            }
        }

        public void UpdateAim(Vector3 direction)
        {
            if (direction == Vector3.zero) return;

            // 1. Moverlo a los pies del jugador
            transform.localPosition = new Vector3(0f, floorOffset, 0f);

            // 2. ROTACIÓN ABSOLUTA EN EL MUNDO: Rompe la interferencia de rotación del padre
            transform.rotation = Quaternion.LookRotation(direction);
        }

        public void HideIndicators()
        {
            if (rangeIndicator) rangeIndicator.SetActive(false);
            if (directionIndicator) directionIndicator.SetActive(false);
        }
    }
}