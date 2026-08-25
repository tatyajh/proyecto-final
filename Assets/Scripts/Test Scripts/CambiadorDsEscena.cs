using UnityEngine;
using UnityEngine.SceneManagement;

public class CambiadorDeEscena : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Escribe aquí el nombre exacto de la escena a la que quieres ir.")]
    [SerializeField] private string nombreDeEscena;

    /// <summary>
    /// Este método se asigna al evento OnClick() del botón.
    /// </summary>
    public void CargarEscena()
    {
        if (!string.IsNullOrWhiteSpace(nombreDeEscena))
        {
            SceneManager.LoadScene(nombreDeEscena);
        }
        else
        {
            Debug.LogWarning("[CambiadorDeEscena] ¡No has configurado el nombre de la escena en el Inspector!");
        }
    }
}