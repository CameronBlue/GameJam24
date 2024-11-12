using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public static MainMenu Me;
    
    [SerializeField] private Button m_startNewButton;
    [SerializeField] private Button m_startLevelButton;
    [SerializeField] private Button m_continueButton;

    private void Awake()
    {
        Me = this;
        
        m_startNewButton.onClick.AddListener(ClickedStart);
        m_startLevelButton.onClick.AddListener(ClickedStartLevel);
        m_continueButton.onClick.AddListener(ClickedContinue);
        ReturnedToMain();
    }

    public void ClickedStart()
    {
        SaveManager.Me.Restart(Input.GetKey(KeyCode.F));
    }

    public void ClickedContinue()
    {
        SaveManager.Me.overrideLevel = -1;
        ClickedStartLevel();
    }

    public void ClickedStartLevel()
    {
        SceneManager.LoadScene("Game");
    }

    public void SelectedLevel(int _level)
    {
        m_startLevelButton.interactable = true;
        SaveManager.Me.overrideLevel = _level;
    }

    public void ReturnedToMain()
    {
        m_startLevelButton.interactable = false;
        m_continueButton.interactable = SaveManager.Me.CanContinue;
    }
}
