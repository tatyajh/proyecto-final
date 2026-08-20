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
    [SerializeField] private bool settingsAvailable = true;

    void Start()
    {
        WireMissingButtons();
        ShowWelcomeText();
        MultiplayerButtonSetAvailability();
        ConfigureUnavailableFeatures();
    }

    /// <summary>
    /// "Settings Button" quedó sin onClick en la escena, así que el clic no
    /// hacía absolutamente nada. Se cablea aquí para no depender de que la
    /// referencia serializada esté puesta.
    /// </summary>
    /// <summary>
    /// "Settings Button" tenía una entrada de evento con el objeto destino
    /// puesto pero sin función elegida: contar entradas daba 1 y parecía
    /// cableado, aunque al pulsarlo no ocurría nada. Hay que mirar si alguna
    /// entrada tiene método de verdad.
    /// </summary>
    private static bool HasRealPersistentCall(Button button)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            if (!string.IsNullOrEmpty(button.onClick.GetPersistentMethodName(i)))
                return true;

        return false;
    }

    private void WireMissingButtons()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (HasRealPersistentCall(button)) continue;

            string id = button.name.ToLowerInvariant();
            if (id.Contains("settings"))
            {
                button.onClick.AddListener(GoToSettings);
                Debug.Log("[MainMenuController] 'Settings Button' no tenía onClick. Se conectó a GoToSettings.");
            }
            else if (id.Contains("story"))
                button.onClick.AddListener(GoToStorymode);
            else if (id.Contains("online"))
                button.onClick.AddListener(GoToMultiplayer);
        }
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
            welcomeText.text = GameLocalization.Choose(
                "¡Bienvenido de nuevo, " + savedName + "!",
                "Welcome back, " + savedName + "!");
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
            string buttonId = button.name.ToLowerInvariant();
            bool unavailable = (!settingsAvailable && buttonId.Contains("settings")) ||
                               (!storyModeAvailable && buttonId.Contains("story"));
            if (!unavailable)
                continue;

            button.interactable = false;
            label.text = feature + GameLocalization.Choose(" · Próximamente", " · Coming soon");
            label.color = new Color(0.55f, 0.53f, 0.50f, 1f);
        }
    }
}
