using System.Collections;
using UnityEngine;

public class AreaExplosionEfecto : MonoBehaviour
{
    [Header("1. TAMAÑO Y COLOR DEL ÁREA")]
    public float tamañoDelArea = 3.0f;
    public Color colorDelCirculo = new Color(1f, 0.8f, 0f, 0.4f);

    [Header("2. FUEGOS ARTIFICIALES / EXPLOSIÓN")]
    public int cantidadDeParticulas = 60;
    public float duracionDelEfecto = 0.5f;

    // =========================================================================
    // 🎮 ME CÁNICAS PRINCIPALES (Lo que hace el efecto)
    // =========================================================================

    // FASE 1: MOVER EL ÁREA (Usa esto desde el joystick para actualizar la posición)
    public void MoverAreaAPosicion(Vector3 nuevaPosicion)
    {
        transform.position = nuevaPosicion;
    }

    // FASE 2: MOSTRAR U OCULTAR EL CÍRCULO (Para cuando mantienes presionado el botón de apuntar)
    public void MostrarCirculoDeApuntado(bool activar)
    {
        PrepararObjetosSiNoExisten();
        indicadorPiso.SetActive(activar);
    }

    // FASE 3: DISPARAR LA EXPLOSIÓN (Llama a esto cuando el jugador suelta el botón o ataca)
    public void DispararExplosion()
    {
        PrepararObjetosSiNoExisten();
        StartCoroutine(RutinaFuegosArtificiales());
    }

    // FASE EXTRA: CANCELAR (Por si el jugador decide no atacar)
    public void CancelarAtaque()
    {
        if (indicadorPiso != null) indicadorPiso.SetActive(false);
        if (sistemaParticulas != null) sistemaParticulas.Stop();
    }

    // =========================================================================
    // 🛠️ DETALLES TÉCNICOS INTERNOS (No es necesario modificar esta sección)
    // =========================================================================

    private GameObject indicadorPiso;
    private ParticleSystem sistemaParticulas;
    private Material materialLuminoso;

    void Start()
    {
        PrepararObjetosSiNoExisten();
        MostrarCirculoDeApuntado(false);
    }

    private void PrepararObjetosSiNoExisten()
    {
        if (indicadorPiso == null) CrearCirculoEnElPiso();
        if (sistemaParticulas == null) CrearFuegosArtificiales();
    }

    private void CrearCirculoEnElPiso()
    {
        indicadorPiso = GameObject.CreatePrimitive(PrimitiveType.Quad);
        indicadorPiso.name = "CirculoPiso_Visual";
        indicadorPiso.transform.SetParent(transform, false);

        indicadorPiso.transform.localPosition = new Vector3(0, 0.01f, 0);
        indicadorPiso.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        indicadorPiso.transform.localScale = new Vector3(tamañoDelArea, tamañoDelArea, 1f);

        Destroy(indicadorPiso.GetComponent<Collider>());

        materialLuminoso = new Material(Shader.Find("Particles/Standard Unlit"));
        materialLuminoso.SetFloat("_Mode", 2); 
        materialLuminoso.SetColor("_Color", colorDelCirculo);

        indicadorPiso.GetComponent<MeshRenderer>().material = materialLuminoso;
    }

    private void CrearFuegosArtificiales()
    {
        GameObject objParticulas = new GameObject("ParticulasSubida_Visual");
        objParticulas.transform.SetParent(transform, false);

        sistemaParticulas = objParticulas.AddComponent<ParticleSystem>();
        var main = sistemaParticulas.main;
        main.playOnAwake = false;
        main.duration = duracionDelEfecto;
        main.startLifetime = 0.5f;
        main.startSpeed = 8f;
        main.startSize = 0.3f;
        main.startColor = colorDelCirculo;

        var shape = sistemaParticulas.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = tamañoDelArea / 2f;
        shape.rotation = new Vector3(-90f, 0f, 0f); 

        var emission = sistemaParticulas.emission;
        emission.rateOverTime = 0;

        var renderer = sistemaParticulas.GetComponent<ParticleSystemRenderer>();
        renderer.material = materialLuminoso;
    }

    private IEnumerator RutinaFuegosArtificiales()
    {
        MostrarCirculoDeApuntado(true);
        sistemaParticulas.Emit(cantidadDeParticulas);

        float tiempo = 0f;
        Vector3 escalaInicial = new Vector3(tamañoDelArea, tamañoDelArea, 1f);

        while (tiempo < duracionDelEfecto)
        {
            tiempo += Time.deltaTime;
            float progreso = tiempo / duracionDelEfecto;
            float pulso = Mathf.Lerp(1f, 1.25f, Mathf.Sin(progreso * Mathf.PI));

            indicadorPiso.transform.localScale = escalaInicial * pulso;
            yield return null;
        }

        indicadorPiso.transform.localScale = escalaInicial;
        MostrarCirculoDeApuntado(false);
    }
}