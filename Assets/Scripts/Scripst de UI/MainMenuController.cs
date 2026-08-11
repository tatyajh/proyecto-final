using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text welcomeText;
    public Button multiplayerButton;// Solo creamos el public button de multiplayer ya que lo desactivaremos más adelante.

    [Header("Escene Name")]
    public string storymodeScene = "SeleccionDeNivel";
    public string multiplayerScene = "LobbyMultiplayer";
    public string settingsScene = "LobbyMultiplayer";

    [Header("Multiplayer control")]
    [SerializeField] public bool multiplayerState = false;

    void Start()
    {
        ShowWelcomeText();
        MultiplayerButtonSetAvailability();
    }

    public void GoToStorymode()
    {
        SceneManager.LoadScene(storymodeScene);
    }

    public void GoToMultiplayer()
    {
        SceneManager.LoadScene(multiplayerScene);
    }
    
    public void GoToSettings()
    {
        SceneManager.LoadScene(settingsScene);
    }

    void ShowWelcomeText()
    {
        string savedName = PlayerPrefs.GetString("PlayerName");

        if (welcomeText != null)
        {
            welcomeText.text = "Welcome back, " + savedName + "!";
        }
    }

    void MultiplayerButtonSetAvailability()
    {
         if (multiplayerButton != null)
        {
            multiplayerButton.interactable = multiplayerState; 
        }
    }
}