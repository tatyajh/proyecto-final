using System.Collections;
using UnityEngine;

public class ControladorRayo : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject objetoRayo; // Tu objeto Visual Ray Attack
    public Transform cilindroRayo; // El Transform del Cilindro 3D

    [Header("Configuración de Impacto")]
    public float duracionTotal = 0.25f;
    public float radioInicial = 0.1f; // Tamaño pequeño y concentrado al iniciar
    public float radioMaximo = 2.5f;  // Tamaño máximo al finalizar la expansión

    [Header("Curva de Expansión")]
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

            // Evaluamos la curva (0 al inicio, 1 al final)
            float factorCurva = curvaEscala.Evaluate(progreso);

            // Interpolamos de menor a mayor: radioInicial (0) -> radioMaximo (1)
            float radioActual = Mathf.Lerp(radioInicial, radioMaximo, factorCurva);

            if (cilindroRayo != null)
            {
                cilindroRayo.localScale = new Vector3(radioActual, escalaInicialCilindro.y, radioActual);
            }

            yield return null;
        }

        objetoRayo.SetActive(false);
    }
}