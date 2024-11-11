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
    
    [Serializable]
    public class Level
    {
        public string m_name;
        public Texture2D m_data;
        public int[] m_potions = new int[6];
    }
    
    public Level[] m_levels;
    
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
        return GetLevel().m_potions;
    }

    private Level GetLevel()
    {
        return overrideLevel == -1 ? m_levels[currentLevel] : m_levels[overrideLevel];
    }

    public void LevelComplete()
    {
        currentLevel++;
        SceneManager.LoadScene(overrideLevel == -1 && currentLevel < m_levels.Length ? "Game" : "Main Menu");
    }
}
