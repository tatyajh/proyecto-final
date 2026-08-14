using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement; 

public class SaveName : MonoBehaviour
{
    //La casilla donde el usuario escribe.
    [Header("UI Reference")]
    public TMP_InputField campoDeTexto;
    public GameObject errorMessage;

    [Header("Escene Config")]
    public string nextEsceneName = "LevelSelection";

    void Start()
    {
        SetWarningFalse();
    }

    public void Continue()
    {
        
           // Validamos que el jugador no deje el nombre vacío
        if (!string.IsNullOrEmpty(campoDeTexto.text.Trim()))
        {
            SetWarningFalse();
            SaveNameAction();
            SceneManager.LoadScene(nextEsceneName);
        }
        else
        {
            Debug.LogWarning("Please enter your name before continue");
            errorMessage.SetActive(true);
        }
    }

    public void SaveNameAction()
    {
        PlayerPrefs.SetString("PlayerName", campoDeTexto.text.Trim());
        Debug.Log("¡Nombre guardado!: " + campoDeTexto.text.Trim());
        PlayerPrefs.Save(); 
    }    

    void SetWarningFalse()
    {
        if (errorMessage != null)
        {
            errorMessage.SetActive(false);
        }
    }
}
