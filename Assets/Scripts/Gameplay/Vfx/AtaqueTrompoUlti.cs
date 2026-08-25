using System.Collections;
using UnityEngine;

namespace Habilidades.Especiales
{
    public class AtaqueTrompoUlti : MonoBehaviour
    {
        [System.Serializable]
        public struct ConfiguracionTrompo
        {
            [Tooltip("Objeto o modelo 3D que va a girar sobre su propio eje.")]
            public Transform objetivoRotacion;

            [Tooltip("Velocidad de giro en grados por segundo.")]
            public float velocidadGiro;

            [Tooltip("Duración total del estado de Ultimate en segundos.")]
            public float duracionUlti;

            [Tooltip("Eje de rotación (Normalmente Vector3.up para girar sobre Z/Y local).")]
            public Vector3 ejeRotacion;
        }

        [Header("Configuración de la Ulti")]
        public ConfiguracionTrompo datosTrompo = new ConfiguracionTrompo
        {
            velocidadGiro = 1080f, // 3 vueltas completas por segundo (360 * 3)
            duracionUlti = 3.0f,
            ejeRotacion = Vector3.up
        };

        [Header("Estado de Control")]
        [Tooltip("Booleano para saber si la ulti está activa en este momento.")]
        public bool estaUsandoUlti = false;

        private void Awake()
        {
            // Si no se asigna un objetivo, toma el propio transform del GameObject
            if (datosTrompo.objetivoRotacion == null)
            {
                datosTrompo.objetivoRotacion = transform;
            }
        }

        // =========================================================================
        // 🧪 PRUEBA PERIÓDICA (Mismo formato de testing)
        // =========================================================================
        [Header("Configuración de Prueba Periódica")]
        [Tooltip("Intervalo en segundos entre cada prueba de la Ulti.")]
        public float intervaloPrueba = 4.0f;
        private float contadorTiempo = 0f;

        private void Update()
        {
            contadorTiempo += Time.deltaTime;

            if (contadorTiempo >= intervaloPrueba && !estaUsandoUlti)
            {
                contadorTiempo = 0f;
                ActivarUltiTrompo();
            }

            // Si el booleano está activo, ejecuta la rotación sobre su propio eje
            if (estaUsandoUlti)
            {
                RotarPersonaje();
            }
        }
        // =========================================================================

        /// <summary>
        /// Método público para disparar la Ulti (Se puede llamar desde un botón de UI o Evento de Animación).
        /// </summary>
        public void ActivarUltiTrompo()
        {
            if (!estaUsandoUlti)
            {
                StartCoroutine(RutinaUltiTrompo());
            }
        }

        private IEnumerator RutinaUltiTrompo()
        {
            estaUsandoUlti = true;

            // Espera el tiempo de duración de la Ulti
            yield return new WaitForSeconds(datosTrompo.duracionUlti);

            estaUsandoUlti = false;
        }

        /// <summary>
        /// Void encargado del cálculo de rotación pura sobre su propio eje (Espacio Local).
        /// </summary>
        private void RotarPersonaje()
        {
            if (datosTrompo.objetivoRotacion != null)
            {
                // Space.Self garantiza que gire sobre su propio eje sin importar la orientación global
                datosTrompo.objetivoRotacion.Rotate(
                    datosTrompo.ejeRotacion * datosTrompo.velocidadGiro * Time.deltaTime,
                    Space.Self
                );
            }
        }
    }
}