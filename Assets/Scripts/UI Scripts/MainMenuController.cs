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
    public string storymodeScene = "Story Mode Menu";
    public string multiplayerScene = "MultiplayerMenu";
    public string settingsScene = "Main Settings";

    [Header("Multiplayer control")]
    [SerializeField] public bool multiplayerState = false;

    [Header("Prototype availability")]
    [SerializeField] private bool storyModeAvailable = false;
    [SerializeField] private bool settingsAvailable = false;

    void Start()
    {
        ShowWelcomeText();
        MultiplayerButtonSetAvailability();
        ConfigureUnavailableFeatures();
    }

    public void GoToStorymode()
    {
        if (!storyModeAvailable)
        {
            Debug.Log("El modo Historia estará disponible próximamente.");
            return;
        }
        SceneManager.LoadScene(storymodeScene);
    }

    public void GoToMultiplayer()
    {
        SceneManager.LoadScene(multiplayerScene);
    }
    
    public void GoToSettings()
    {
        if (!settingsAvailable)
        {
            Debug.Log("Settings estará disponible próximamente.");
            return;
        }
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

    private void ConfigureUnavailableFeatures()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;

            string feature = label.text.Trim();
            bool unavailable = (!settingsAvailable && feature.Equals("Settings", System.StringComparison.OrdinalIgnoreCase)) ||
                               (!storyModeAvailable && feature.Equals("Story Mode", System.StringComparison.OrdinalIgnoreCase));
            if (!unavailable)
                continue;

            button.interactable = false;
            label.text = feature + " · Próximamente";
            label.color = new Color(0.55f, 0.53f, 0.50f, 1f);
        }
    }
}
