using System.Collections;
using UnityEngine;

public class ControladorRayo : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject objetoRayo; // Tu objeto Visual Ray Attack
    public Transform cilindroRayo; // El Transform del Cilindro 3D

    [Header("Configuración de Impacto")]
    public float duracionTotal = 0.25f;
    public float radioMaximo = 2.0f; // Tamaño del estallido
    public float radioFinal = 0.5f;   // Tamaño al encogerse

    [Header("Curva de Expansión")]
    // Nos permite controlar visualmente cómo se escala en el tiempo
    public AnimationCurve curvaEscala = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Vector3 escalaInicialCilindro;

    void Start()
    {
        if (objetoRayo != null)
            objetoRayo.SetActive(false);

        if (cilindroRayo != null)
            escalaInicialCilindro = cilindroRayo.localScale;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            Disparar();
        }
    }

    public void Disparar()
    {
        StopAllCoroutines();
        StartCoroutine(RutinaAnimacionRayo());
    }

    IEnumerator RutinaAnimacionRayo()
    {
        objetoRayo.SetActive(true);

        float tiempo = 0f;

        while (tiempo < duracionTotal)
        {
            tiempo += Time.deltaTime;
            float progreso = tiempo / duracionTotal; // De 0 a 1

            // Evaluamos el punto actual de la curva
            float factorCurva = curvaEscala.Evaluate(progreso);

            // Calculamos el radio actual (X y Z) manteniendo la altura constante (Y)
            float radioActual = Mathf.Lerp(radioMaximo, radioFinal, factorCurva);

            if (cilindroRayo != null)
            {
                cilindroRayo.localScale = new Vector3(radioActual, escalaInicialCilindro.y, radioActual);
            }

            yield return null; // Espera al siguiente frame
        }

        objetoRayo.SetActive(false);
    }
}