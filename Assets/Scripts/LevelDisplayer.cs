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
    
    
    private Dictionary<LevelOption, SaveManager.Level> m_levelDict = new();
    
    private void Awake()
    {
        Me = this;
    }

    private void Start()
    {
        foreach (var level in SaveManager.Me.m_levels)
        {
            var levelOption = Instantiate(m_levelOptionPrefab, m_levelHolder);
            levelOption.SetLevel(level);
            m_levelDict.Add(levelOption, level);
        }
    }

    public void SetLevel(LevelOption _level)
    {
        SaveManager.Me.overrideLevel = m_levelDict[_level];
    }
}
