using System.Collections;
using UnityEngine;

public class ControlAuraEspecial : MonoBehaviour
{
    [Header("Referencias de Materiales")]
    [Tooltip("Arrastra aquí el SkinnedMeshRenderer o MeshRenderer de tu personaje.")]
    public Renderer mallaPersonaje;

    [Tooltip("Material original del personaje.")]
    public Material materialBase;

    [Tooltip("Material con la pasada del Aura Roja (M_AuraRoja).")]
    public Material materialAura;

    [Header("Configuración de Prueba")]
    [Tooltip("Duración en segundos que permanece el aura activa durante el ataque.")]
    public float duracionAura = 2.0f;

    void Start()
    {
        // Aseguramos que el personaje arranque con su material normal
        DesactivarAura();
    }

    // =========================================================================
    // 🎮 MÉTODOS PÚBLICOS (Para llamar desde tu script de ataques)
    // =========================================================================

    // Activa el aura de forma permanente hasta que decidas quitarla
    public void ActivarAura()
    {
        if (mallaPersonaje != null && materialAura != null)
        {
            mallaPersonaje.material = materialAura;
        }
    }

    // Vuelve al material original
    public void DesactivarAura()
    {
        if (mallaPersonaje != null && materialBase != null)
        {
            mallaPersonaje.material = materialBase;
        }
    }

    // Activa el aura solo por un tiempo límite (ej. durante el golpe especial)
    public void ActivarAuraPorTiempo(float segundos)
    {
        StopAllCoroutines();
        StartCoroutine(RutinaAuraTemporal(segundos));
    }

    private IEnumerator RutinaAuraTemporal(float segundos)
    {
        ActivarAura();
        yield return new WaitForSeconds(segundos);
        DesactivarAura();
    }

    // Tecla de prueba rápida (Tecla 'E' para disparar el aura)
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            ActivarAuraPorTiempo(duracionAura);
        }
    }
}



//Malla Personaje: Arrastra el objeto que tiene el SkinnedMeshRenderer.

//Material Base: Arrastra tu material original del personaje.

//Material Aura: Arrastra M_AuraRoja.

//Como instalar:
/*// Opción A: Prender por 2 segundos durante el impacto
componenteAura.ActivarAuraPorTiempo(2.0f);

// Opción B: Prender al iniciar el estado de furia y apagar al terminar
componenteAura.ActivarAura();   // Al iniciar habilidad
componenteAura.DesactivarAura(); // Al finalizar habilidad*/