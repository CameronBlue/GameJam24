using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Me;

    [NonSerialized] public int overrideLevel = -1;
    [NonSerialized] public int currentLevel;
    [NonSerialized] public int cutsceneIndex;
    
    [Serializable]
    public class Level
    {
        public string m_name;
        public Texture2D m_data;
        public int[] m_potions = new int[6];
    }
    
    public Level[] m_levels;
    
    public bool CanContinue => currentLevel > 0 && currentLevel < m_levels.Length;
    
    private void Awake()
    {
        if (Me != null)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        
        DontDestroyOnLoad(gameObject);
        Me = this;
    }
    
    public Texture2D GetLevelTexture()
    {
        return GetLevel().m_data;
    }

    public int[] GetLevelPotions()
    {
        var copy = new int[6];
        GetLevel().m_potions.CopyTo(copy, 0);
        return copy;
    }

    private Level GetLevel()
    {
        return overrideLevel == -1 ? m_levels[currentLevel] : m_levels[overrideLevel];
    }

    public void Restart(bool _skipOpeningCutscene)
    {
        overrideLevel = -1;
        currentLevel = 0;
        cutsceneIndex = _skipOpeningCutscene ? 1 : 0;
        SceneManager.LoadScene(_skipOpeningCutscene ? "Game" : "Cutscenes");
    }
    
    public void LevelComplete()
    {
        if (overrideLevel != -1)
        {
            SceneManager.LoadScene("Main Menu");
            return;
        }
        currentLevel++;
        SceneManager.LoadScene(currentLevel < m_levels.Length ? "Game" : "Cutscenes");
    }
    
    public void RestartLevel()
    {
        SceneManager.LoadScene("Game");
    }
    
    public void CutsceneFinished()
    {
        cutsceneIndex++;
        bool last = cutsceneIndex == CutsceneManager.Me.cutscenes.Count;
        SceneManager.LoadScene(last ? "Main Menu" : "Game");
    }

    public void ExitToMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }
}
