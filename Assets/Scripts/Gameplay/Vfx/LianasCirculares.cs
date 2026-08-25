using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Habilidades.Especiales
{
    public class LianasCirculares : MonoBehaviour
    {
        [System.Serializable]
        public struct ConfiguracionAoE
        {
            [Tooltip("Cantidad de lianas que brotarán en el círculo.")]
            [Range(3, 16)]
            public int cantidadLianas;

            [Tooltip("Radio máximo que alcanzará el área circular.")]
            public float radioAoE;

            [Tooltip("Tiempo en segundos que tardan en desplegarse.")]
            public float duracionBrote;

            [Tooltip("Segmentos por cada liana para dar la curvatura.")]
            [Range(10, 60)]
            public int segmentosPorLiana;

            [Header("Irregularidad y Retorcimiento")]
            [Tooltip("Intensidad de las desviaciones aleatorias (Ruido Perlin).")]
            public float fuerzaRuido;

            [Tooltip("Intensidad del efecto espiral / tirabuzón al extenderse.")]
            public float fuerzaTirabuzon;

            [Tooltip("Frecuencia de las vueltas del tirabuzón.")]
            public float frecuenciaTirabuzon;
        }

        [Header("Configuración General")]
        public ConfiguracionAoE datosAoE = new ConfiguracionAoE
        {
            cantidadLianas = 8,
            radioAoE = 4f,
            duracionBrote = 0.45f,
            segmentosPorLiana = 35,
            fuerzaRuido = 0.45f,
            fuerzaTirabuzon = 0.35f,
            frecuenciaTirabuzon = 3f
        };

        [Header("Curva de Expansión")]
        public AnimationCurve curvaBrote = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Componentes internos
        private List<LineRenderer> listaLianas = new List<LineRenderer>();
        private bool estaEjecutando = false;
        private float semillaRuido;

        private void Awake()
        {
            semillaRuido = Random.Range(0f, 100f);
            InicializarLianas();
        }

        private void InicializarLianas()
        {
            for (int i = 0; i < datosAoE.cantidadLianas; i++)
            {
                GameObject objetoLiana = new GameObject($"Liana_Rama_{i}");
                objetoLiana.transform.SetParent(transform, false);

                LineRenderer lr = objetoLiana.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = new Color(0.1f, 0.4f, 0.1f);
                lr.endColor = new Color(0.05f, 0.2f, 0.05f);

                AnimationCurve curvaGrosor = new AnimationCurve();
                curvaGrosor.AddKey(0.0f, 0.2f);
                curvaGrosor.AddKey(0.7f, 0.08f);
                curvaGrosor.AddKey(1.0f, 0.01f);
                lr.widthCurve = curvaGrosor;

                lr.enabled = false;
                listaLianas.Add(lr);
            }
        }

        // =========================================================================
        // 🧪 PRUEBA PERIÓDICA
        // =========================================================================
        [Header("Configuración de Prueba Periódica")]
        public float intervaloPrueba = 2.5f;
        private float contadorTiempo = 0f;

        private void Update()
        {
            contadorTiempo += Time.deltaTime;

            if (contadorTiempo >= intervaloPrueba && !estaEjecutando)
            {
                contadorTiempo = 0f;
                EjecutarBroteCircular();
            }
        }
        // =========================================================================

        public void EjecutarBroteCircular()
        {
            if (!estaEjecutando)
            {
                // Cambiamos la semilla en cada disparo para que la forma retorcida cambie levemente
                semillaRuido = Random.Range(0f, 100f);
                StopAllCoroutines();
                StartCoroutine(RutinaBroteCircular());
            }
        }

        private IEnumerator RutinaBroteCircular()
        {
            estaEjecutando = true;

            for (int i = 0; i < listaLianas.Count; i++)
            {
                listaLianas[i].enabled = true;
                listaLianas[i].positionCount = datosAoE.segmentosPorLiana;
            }

            float tiempoTranscurrido = 0f;

            while (tiempoTranscurrido < datosAoE.duracionBrote)
            {
                tiempoTranscurrido += Time.deltaTime;
                float progreso = tiempoTranscurrido / datosAoE.duracionBrote;
                float factorCurva = curvaBrote.Evaluate(progreso);

                CalcularPosicionesAoERetorcidas(factorCurva);

                yield return null;
            }

            yield return new WaitForSeconds(0.2f);

            for (int i = 0; i < listaLianas.Count; i++)
            {
                listaLianas[i].enabled = false;
            }

            estaEjecutando = false;
        }

        private void CalcularPosicionesAoERetorcidas(float progreso)
        {
            Vector3 centro = transform.position;
            float pasoAngulo = (Mathf.PI * 2f) / datosAoE.cantidadLianas;

            for (int i = 0; i < datosAoE.cantidadLianas; i++)
            {
                float angulo = i * pasoAngulo;
                Vector3 direccionRadial = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
                Vector3 tangencial = new Vector3(-Mathf.Sin(angulo), 0f, Mathf.Cos(angulo));

                Vector3 destinoFinalRama = centro + (direccionRadial * datosAoE.radioAoE);
                Vector3 destinoActualRama = Vector3.Lerp(centro, destinoFinalRama, progreso);

                LineRenderer lr = listaLianas[i];

                for (int j = 0; j < datosAoE.segmentosPorLiana; j++)
                {
                    float t = (float)j / (datosAoE.segmentosPorLiana - 1);
                    Vector3 puntoBase = Vector3.Lerp(centro, destinoActualRama, t);

                    // 1. Elevación base del suelo (Arco primario)
                    float arcoBase = Mathf.Sin(t * Mathf.PI) * 0.5f;

                    // 2. Tirabuzón / Espiral (Giro en torno a la dirección de avance)
                    float anguloTirabuzon = t * Mathf.PI * 2f * datosAoE.frecuenciaTirabuzon;
                    Vector3 offsetTirabuzon = (Vector3.up * Mathf.Sin(anguloTirabuzon) + tangencial * Mathf.Cos(anguloTirabuzon))
                                              * datosAoE.fuerzaTirabuzon * (1f - t * 0.3f);

                    // 3. Ruido Perlin (Desviaciones quebradas e irregulares)
                    float ruidoX = (Mathf.PerlinNoise(t * 5f + semillaRuido, i * 2f) - 0.5f) * 2f;
                    float ruidoY = (Mathf.PerlinNoise(i * 3f, t * 5f + semillaRuido) - 0.5f) * 2f;
                    Vector3 offsetRuido = (tangencial * ruidoX + Vector3.up * ruidoY) * datosAoE.fuerzaRuido * (1f - t);

                    // Suma de todas las fuerzas de deformación
                    Vector3 posicionFinal = puntoBase + (Vector3.up * arcoBase) + offsetTirabuzon + offsetRuido;
                    lr.SetPosition(j, posicionFinal);
                }
            }
        }
    }
}