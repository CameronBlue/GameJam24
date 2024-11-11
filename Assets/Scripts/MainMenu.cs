using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void ClickedStart()
    {
        SaveManager.Me.currentLevel = 0;
        ClickedContinue();
    }

    public void ClickedContinue()
    {
        SaveManager.Me.overrideLevel = -1;
        ClickedStartLevel();
    }

    public void ClickedStartLevel()
    {
        SceneManager.LoadScene(Input.GetKey(KeyCode.Escape) ? "Game" : "Cutscenes");
    }
}
