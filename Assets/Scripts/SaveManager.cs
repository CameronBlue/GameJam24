using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Me;

    public Level overrideLevel;
    public int currentLevel = 0;
    
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
        DontDestroyOnLoad(gameObject);
        Me = this;
    }
    
    public Texture2D GetLevelTexture()
    {
        if (overrideLevel != null)
            return overrideLevel.m_data;
        
        return m_levels[currentLevel].m_data;
    }

    public int[] GetLevelPotions()
    {
        if (overrideLevel != null)
            return overrideLevel.m_potions;
        
        return m_levels[currentLevel].m_potions;
    }

    public void LevelComplete()
    {
        currentLevel++;
    }
}
