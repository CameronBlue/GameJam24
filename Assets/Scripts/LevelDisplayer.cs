using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;

public class LevelDisplayer : MonoBehaviour
{
    private const string m_levelPath = "/Art/Levels/";
    
    public static LevelDisplayer Me;
    
    public LevelOption m_levelOptionPrefab;
    public Transform m_levelHolder;
    
    
    private Dictionary<LevelOption, int> m_levelDict = new();
    
    private void Awake()
    {
        Me = this;
    }

    private void Start()
    {
        for (var i = 0; i < SaveManager.Me.m_levels.Length; i++)
        {
            var level = SaveManager.Me.m_levels[i];
            var levelOption = Instantiate(m_levelOptionPrefab, m_levelHolder);
            levelOption.SetLevel(level);
            m_levelDict.Add(levelOption, i);
        }
    }

    public void SetLevel(LevelOption _level)
    {
        MainMenu.Me.SelectedLevel(m_levelDict[_level]);
    }
}
