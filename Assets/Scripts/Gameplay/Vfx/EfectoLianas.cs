using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*Para configurar: 
Crea Empty objects que serán el punto de partida y el punto final
 de donde queremos que empiece y donde queremos que acabe el efecto de Lianas.

 El script ya crea el line renderer
*/
namespace Habilidades.Especiales
{
    [RequireComponent(typeof(LineRenderer))]
    public class EfectoLianas : MonoBehaviour
    {
        [System.Serializable]
        public struct ConfiguracionLiana
        {
            public Transform puntoOrigen;
            public Transform puntoObjetivo;
            public float duracionAtaque;

            [Tooltip("Cantidad de segmentos que componen la curva de la liana.")]
            [Range(5, 50)]
            public int cantidadSegmentos;
            public float amplitudOndulacion;

            [Header("Configuración de Espinas Procedurales")]
            [Tooltip("Largo proyectado de cada espina/pico.")]
            public float longitudEspina;
            [Tooltip("Frecuencia con la que aparece una espina (Ej: cada 3 segmentos).")]
            [Range(1, 10)]
            public int pasoEspinas;
        }

        [Header("Configuración General")]
        public ConfiguracionLiana datosLiana = new ConfiguracionLiana 
        { 
            duracionAtaque = 0.35f, 
            cantidadSegmentos = 30, 
            amplitudOndulacion = 0.5f,
            longitudEspina = 0.3f,
            pasoEspinas = 3
        };

        [Header("Efectos Visuales (Built-in)")]
        [Tooltip("Sistema de Partículas configurado para espinas o rosas.")]
        public ParticleSystem sistemaEspinas;

        [Header("Curva de Expansión")]
        public AnimationCurve curvaExtension = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Componentes Internos
        private LineRenderer renderizadorLinea;
        private bool estaAtacando = false;

        private void Awake()
        {
            renderizadorLinea = GetComponent<LineRenderer>();

            // Configuración por código para evitar la línea gruesa magenta
            if (renderizadorLinea.sharedMaterial == null)
            {
                renderizadorLinea.material = new Material(Shader.Find("Sprites/Default"));
                renderizadorLinea.startColor = new Color(0.1f, 0.4f, 0.1f);
                renderizadorLinea.endColor = new Color(0.05f, 0.2f, 0.05f);
            }

            AnimationCurve curvaGrosor = new AnimationCurve();
            curvaGrosor.AddKey(0.0f, 0.15f);
            curvaGrosor.AddKey(1.0f, 0.03f);
            renderizadorLinea.widthCurve = curvaGrosor;

            renderizadorLinea.enabled = false;
        }
/// Esto es para pruebas, se puede borrar
        [Header("Configuración de Prueba Periódica")]
[Tooltip("Intervalo en segundos entre cada latigazo de prueba.")]
public float intervaloPrueba = 2.0f;
private float contadorTiempo = 0f;

void Update()
{
    contadorTiempo += Time.deltaTime;

    // Solo ejecuta la liana si ya pasó el intervalo Y no está ejecutando un ataque previo
    if (contadorTiempo >= intervaloPrueba && !estaAtacando)
    {
        contadorTiempo = 0f; // Reinicia el temporizador
        EjecutarAtaqueLiana();
    }
}

// Hasta aquí ------------------------------------------------------____--__-


        public void EjecutarAtaqueLiana()
        {
            if (datosLiana.puntoOrigen == null || datosLiana.puntoObjetivo == null)
            {
                Debug.LogWarning("[EfectoLianasEspecial] Asigna los puntos de Origen y Objetivo en el Inspector.");
                return;
            }

            StopAllCoroutines();
            StartCoroutine(RutinaLiana());
        }

        private IEnumerator RutinaLiana()
        {
            estaAtacando = true;
            renderizadorLinea.enabled = true;
            renderizadorLinea.positionCount = datosLiana.cantidadSegmentos;

            if (sistemaEspinas != null)
                sistemaEspinas.Play();

            float tiempoTranscurrido = 0f;

            while (tiempoTranscurrido < datosLiana.duracionAtaque)
            {
                tiempoTranscurrido += Time.deltaTime;
                float progreso = tiempoTranscurrido / datosLiana.duracionAtaque;
                float factorCurva = curvaExtension.Evaluate(progreso);

                CalcularPosicionesLiana(factorCurva);

                yield return null;
            }

            // Mantiene la liana visible un instante breve antes de retraerla
            yield return new WaitForSeconds(0.05f);

            renderizadorLinea.enabled = false;
            
            if (sistemaEspinas != null)
                sistemaEspinas.Stop();

            estaAtacando = false;
        }

        private void CalcularPosicionesLiana(float progresoExtension)
        {
            Vector3 origen = datosLiana.puntoOrigen.position;
            Vector3 destinoFinal = datosLiana.puntoObjetivo.position;

            Vector3 destinoActual = Vector3.Lerp(origen, destinoFinal, progresoExtension);
            Vector3 direccionGeneral = (destinoFinal - origen).normalized;
            
            // Calculamos un vector perpendicular para proyectar las espinas hacia los lados
            Vector3 perpendicular = Vector3.Cross(direccionGeneral, Vector3.up).normalized;
            if (perpendicular == Vector3.zero) perpendicular = Vector3.right;

            for (int i = 0; i < datosLiana.cantidadSegmentos; i++)
            {
                float t = (float)i / (datosLiana.cantidadSegmentos - 1);
                Vector3 puntoBase = Vector3.Lerp(origen, destinoActual, t);

                float desplazamientoOnda = Mathf.Sin(t * Mathf.PI * 2f) * datosLiana.amplitudOndulacion * (1f - t);
                Vector3 offset = Vector3.up * desplazamientoOnda;

                Vector3 posicionFinalSegmento = puntoBase + offset;

                // Generación procedural de espinas alternadas (picos)
                if (i > 0 && i < datosLiana.cantidadSegmentos - 1 && i % datosLiana.pasoEspinas == 0)
                {
                    float lado = (i % (datosLiana.pasoEspinas * 2) == 0) ? 1f : -1f;
                    Vector3 direccionEspina = (perpendicular * lado + Vector3.up * 0.5f).normalized;
                    posicionFinalSegmento += direccionEspina * datosLiana.longitudEspina;
                }

                renderizadorLinea.SetPosition(i, posicionFinalSegmento);
            }
        }
    }
}