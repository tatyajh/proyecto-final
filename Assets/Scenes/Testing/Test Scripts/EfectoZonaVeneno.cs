using System.Collections;
using UnityEngine;

public class EfectoZonaVeneno : MonoBehaviour
{
    [Header("1. TIEMPO Y TAMAÑO")]
    [Tooltip("Duración en segundos que permanece activo el efecto visual.")]
    public float duracionDelEfecto = 4.0f;

    [Tooltip("Radio del área circular de veneno en el suelo.")]
    public float radioDelArea = 3.0f;

    [Tooltip("Altura desde donde comienzan a caer las partículas (la punta del personaje).")]
    public float alturaOrigenCaida = 2.0f;

    [Header("2. ESTÉ TICA Y COLORES")]
    [Tooltip("Color base de la zona y de las partículas (por defecto Morado/Veneno).")]
    public Color colorVeneno = new Color(0.5f, 0.1f, 0.8f, 0.4f);

    [Header("3. DENSIDAD DE PARTÍCULAS")]
    [Tooltip("Cantidad de partículas que caen desde la punta del personaje por segundo.")]
    public int cantidadParticulasCaida = 30;

    [Tooltip("Cantidad de partículas que brotan hacia arriba desde el suelo por segundo.")]
    public int cantidadParticulasSubida = 20;


    // =========================================================================
    // 🎮 ACCIONES PRINCIPALES (Lo que ejecutas desde tus botones o scripts)
    // =========================================================================

    // ACTIVA LA ZONA DE VENENO (Ejecuta todo el efecto durante los segundos configurados)
    public void ActivarEfectoVeneno()
    {
        ConstruirComponentesSiNoExisten();
        StopAllCoroutines();
        StartCoroutine(RutinaEjecucionEfecto());
    }

    // DESACTIVA Y LIMPIA EL EFECTO DE INMEDIATO (Por si necesitas cortarlo antes de tiempo)
    public void DetenerEfectoDeInmediato()
    {
        StopAllCoroutines();
        if (circuloPiso != null) circuloPiso.SetActive(false);
        if (particulasCaida != null) particulasCaida.Stop();
        if (particulasSubida != null) particulasSubida.Stop();
    }


    // =========================================================================
    // 🛠️ DETALLES TÉCNICOS INTERNOS Y OPTIMIZACIÓN (Generación automática)
    // =========================================================================

    private GameObject circuloPiso;
    private ParticleSystem particulasCaida;
    private ParticleSystem particulasSubida;
    private Material materialCompartido;

    void Start()
    {
        ConstruirComponentesSiNoExisten();
        DetenerEfectoDeInmediato();
    }

    private void ConstruirComponentesSiNoExisten()
    {
        if (materialCompartido == null)
        {
            // Usamos el shader optimizado de partículas Built-in
            materialCompartido = new Material(Shader.Find("Particles/Standard Unlit"));
            materialCompartido.SetFloat("_Mode", 2); // Modo Additive
        }
        
        materialCompartido.SetColor("_Color", colorVeneno);

        if (circuloPiso == null) CrearCirculoBasePiso();
        if (particulasCaida == null) CrearEmisorGotasCaida();
        if (particulasSubida == null) CrearEmisorBurbujasSubida();
    }

    private void CrearCirculoBasePiso()
    {
        circuloPiso = GameObject.CreatePrimitive(PrimitiveType.Quad);
        circuloPiso.name = "VFX_CirculoPiso_Veneno";
        circuloPiso.transform.SetParent(transform, false);

        // Posicionar plano en el piso (Y = 0.01f para evitar parpadeo de textura con el suelo)
        circuloPiso.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        circuloPiso.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        circuloPiso.transform.localScale = new Vector3(radioDelArea * 2f, radioDelArea * 2f, 1f);

        Destroy(circuloPiso.GetComponent<Collider>());
        circuloPiso.GetComponent<MeshRenderer>().material = materialCompartido;
    }

    private void CrearEmisorGotasCaida()
    {
        GameObject objCaida = new GameObject("VFX_Particulas_Caida");
        objCaida.transform.SetParent(transform, false);
        objCaida.transform.localPosition = new Vector3(0f, alturaOrigenCaida, 0f);

        particulasCaida = objCaida.AddComponent<ParticleSystem>();
        
        var main = particulasCaida.main;
        main.playOnAwake = false;
        main.duration = duracionDelEfecto;
        main.startLifetime = 0.6f;
        main.startSpeed = 4f; // Cae directo al suelo
        main.startSize = 0.2f;
        main.startColor = colorVeneno;

        var shape = particulasCaida.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radioDelArea;
        shape.rotation = new Vector3(90f, 0f, 0f); // Apuntar hacia abajo

        var emission = particulasCaida.emission;
        emission.rateOverTime = cantidadParticulasCaida;

        var renderer = particulasCaida.GetComponent<ParticleSystemRenderer>();
        renderer.material = materialCompartido;
    }

    private void CrearEmisorBurbujasSubida()
    {
        GameObject objSubida = new GameObject("VFX_Particulas_Subida");
        objSubida.transform.SetParent(transform, false);
        objSubida.transform.localPosition = Vector3.zero;

        particulasSubida = objSubida.AddComponent<ParticleSystem>();

        var main = particulasSubida.main;
        main.playOnAwake = false;
        main.duration = duracionDelEfecto;
        main.startLifetime = 1.0f;
        main.startSpeed = 1.2f; // Sube lentamente
        main.startSize = 0.25f;
        main.startColor = colorVeneno;

        var shape = particulasSubida.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radioDelArea;
        shape.rotation = new Vector3(-90f, 0f, 0f); // Apuntar hacia arriba

        var emission = particulasSubida.emission;
        emission.rateOverTime = cantidadParticulasSubida;

        var renderer = particulasSubida.GetComponent<ParticleSystemRenderer>();
        renderer.material = materialCompartido;
    }

    private IEnumerator RutinaEjecucionEfecto()
    {
        // 1. Mostrar área morada en el piso e iniciar emisión
        circuloPiso.SetActive(true);
        particulasCaida.Play();
        particulasSubida.Play();

        // 2. Esperar el tiempo configurado por el usuario
        yield return new WaitForSeconds(duracionDelEfecto);

        // 3. Detener la emisión y apagar el círculo
        particulasCaida.Stop();
        particulasSubida.Stop();
        circuloPiso.SetActive(false);
    }

    // Tecla Espacio para hacer pruebas rápidas en la escena
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ActivarEfectoVeneno();
        }
    }
}