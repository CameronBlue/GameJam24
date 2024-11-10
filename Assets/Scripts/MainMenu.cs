using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void ClickedStart()
    {
        SceneManager.LoadScene(Input.GetKey(KeyCode.Escape) ? "Game" : "Cutscenes");
    }
}
