using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelSelectionController : MonoBehaviour
{
    [Header("Progress Config")]
    int maxUnlockedLevel = 1;

    [Header("Botones de Niveles")]
    public Button levelSelectionButton1;
    public Button levelSelectionButton2;
    public Button levelSelectionButton3;
    public Button levelSelectionButton4;

    [Header("Referencias del Popup / Brief")]
    public GameObject briefPanel;
    public TMP_Text txtLevelTitle;
    public TMP_Text txtLevelDescription;

    private string selectedScene;
    [Tooltip("Put the exaxt name of the escene of the main menu")]
    public string mainMenu;

    void Start()
    {
        SelectMaxUnlockedLevel();
        UpdateButtonStates();
        HidPopUp();
    }

    void UpdateButtonStates()
    {
        if (levelSelectionButton1 != null)
            levelSelectionButton1.interactable = (maxUnlockedLevel >= 1);
        if (levelSelectionButton2 != null)
            levelSelectionButton2.interactable = (maxUnlockedLevel >= 2);
        if (levelSelectionButton3 != null)
            levelSelectionButton3.interactable = (maxUnlockedLevel >= 3);
        if (levelSelectionButton4 != null)
            levelSelectionButton4.interactable = (maxUnlockedLevel >= 4);
    }

    public void OnClickLevel1()
    {
        SelectLevel("Nivel 1: El Inicio", "Aprende los controles básicos y supera los primeros obstáculos.", "Level 1");
    }

    public void OnClickLevel2()
    {
        SelectLevel("Nivel 2: La Cueva", "Cuidado con las trampas en la oscuridad.", "Level 2");
    }

    public void OnClickLevel3()
    {
        SelectLevel("Nivel 3: El Castillo", "Enfréntate a los enemigos custodios.", "Level 3");
    }

    public void OnClickLevel4()
    {
        SelectLevel("Nivel 4: La Batalla Final", "Supera la última prueba del juego.", "Level 4");
    }


    private void SelectLevel(string title, string description, string levelSceneName)
    {
        selectedScene = levelSceneName;

        if (txtLevelTitle != null) txtLevelTitle.text = title; 
        if (txtLevelDescription != null) txtLevelDescription.text = description;

        if (briefPanel != null) briefPanel.SetActive(true); 
    }

    public void ConfirmAndStartLevel()
    {
        if (!string.IsNullOrEmpty(selectedScene)) 
        {
            SceneManager.LoadScene(selectedScene);
        }
    }

    public void CloseBrief()
    {
        if (briefPanel != null) 
        {
            briefPanel.SetActive(false);
        }
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }

    void SelectMaxUnlockedLevel()
    {
        maxUnlockedLevel = PlayerPrefs.GetInt("NivelAlcanzado", 1);
    }

    void HidPopUp()
    {
        if (briefPanel != null) 
        {
            briefPanel.SetActive(false);
        }
    }
}