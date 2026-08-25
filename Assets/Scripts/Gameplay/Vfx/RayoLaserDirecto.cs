using System.Collections;
using UnityEngine;

namespace Habilidades.Especiales
{
    [RequireComponent(typeof(LineRenderer))]
    public class RayoLaserDirecto : MonoBehaviour
    {
        [System.Serializable]
        public struct ConfiguracionLaser
        {
            [Tooltip("Punto de origen (mano o punta del arma).")]
            public Transform puntoOrigen;

            [Tooltip("Punto objetivo donde impacta el láser.")]
            public Transform puntoObjetivo;

            [Tooltip("Duración en segundos que permanece encendido el láser.")]
            public float duracionDisparo;

            [Header("Dimensiones y Color")]
            [Range(0.05f, 1f)]
            public float grosorBase;

            public Color colorNucleo;
            public Color colorBorde;
        }

        [Header("Configuración del Laser")]
        public ConfiguracionLaser datosLaser = new ConfiguracionLaser
        {
            duracionDisparo = 0.4f,
            grosorBase = 0.3f,
            colorNucleo = Color.white,
            colorBorde = Color.cyan
        };

        [Header("Efectos Visuales")]
        [Tooltip("Partículas de impacto opcionales en el punto de contacto.")]
        public ParticleSystem chispasImpacto;

        private LineRenderer renderizadorLaser;
        private bool estaDisparando = false;

        private void Awake()
        {
            renderizadorLaser = GetComponent<LineRenderer>();

            // Configurar el material del LineRenderer para Built-in
            if (renderizadorLaser.sharedMaterial == null)
            {
                renderizadorLaser.material = new Material(Shader.Find("Sprites/Default"));
            }

            renderizadorLaser.enabled = false;
        }

        // =========================================================================
        // 🧪 PRUEBA PERIÓDICA
        // =========================================================================
        [Header("Configuración de Prueba Periódica")]
        public float intervaloPrueba = 2.0f;
        private float contadorTiempo = 0f;

        private void Update()
        {
            contadorTiempo += Time.deltaTime;

            if (contadorTiempo >= intervaloPrueba && !estaDisparando)
            {
                contadorTiempo = 0f;
                EjecutarRayoLaser();
            }
        }
        // =========================================================================

        public void EjecutarRayoLaser()
        {
            if (datosLaser.puntoOrigen == null || datosLaser.puntoObjetivo == null)
            {
                Debug.LogWarning("[RayoLaserDirecto] Asigna los puntos de Origen y Objetivo en el Inspector.");
                return;
            }

            if (!estaDisparando)
            {
                StopAllCoroutines();
                StartCoroutine(RutinaLaser());
            }
        }

        private IEnumerator RutinaLaser()
        {
            estaDisparando = true;
            renderizadorLaser.enabled = true;
            renderizadorLaser.positionCount = 2; // Solo 2 puntos para una línea recta perfecta

            // Configurar colores (Gradiente: Centro brillante, extremos coloreados)
            renderizadorLaser.startColor = datosLaser.colorNucleo;
            renderizadorLaser.endColor = datosLaser.colorBorde;

            if (chispasImpacto != null)
            {
                chispasImpacto.transform.position = datosLaser.puntoObjetivo.position;
                chispasImpacto.Play();
            }

            float tiempoTranscurrido = 0f;

            while (tiempoTranscurrido < datosLaser.duracionDisparo)
            {
                tiempoTranscurrido += Time.deltaTime;
                float progreso = tiempoTranscurrido / datosLaser.duracionDisparo;

                // Mantener los puntos sincronizados por si los personajes se mueven durante el disparo
                renderizadorLaser.SetPosition(0, datosLaser.puntoOrigen.position);
                renderizadorLaser.SetPosition(1, datosLaser.puntoObjetivo.position);

                // Pulso de grosor: El rayo se expande rápido al inicio y se degrada
                float factorGrosor = Mathf.Sin(progreso * Mathf.PI) * datosLaser.grosorBase;
                renderizadorLaser.startWidth = factorGrosor;
                renderizadorLaser.endWidth = factorGrosor * 0.8f;

                if (chispasImpacto != null)
                {
                    chispasImpacto.transform.position = datosLaser.puntoObjetivo.position;
                }

                yield return null;
            }

            renderizadorLaser.enabled = false;

            if (chispasImpacto != null)
            {
                chispasImpacto.Stop();
            }

            estaDisparando = false;
        }
    }
}