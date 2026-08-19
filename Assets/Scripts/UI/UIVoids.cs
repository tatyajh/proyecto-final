using UnityEngine;

public class UIVoids : MonoBehaviour
{
    public void GetName()
    {
        PlayerPrefs.GetString("PlayerName");
    }
}
