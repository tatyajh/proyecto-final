using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement; 

public class SaveName : MonoBehaviour
{
    private bool isLoading;

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
        if (isLoading || campoDeTexto == null)
            return;

        if (!string.IsNullOrEmpty(campoDeTexto.text.Trim()))
        {
            isLoading = true;
            SetWarningFalse();
            SaveNameAction();
            SceneManager.LoadScene(nextEsceneName);
        }
        else
        {
            Debug.LogWarning("Please enter your name before continue");
            if (errorMessage != null)
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
