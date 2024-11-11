using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Me;

    public Texture2D overrideLevel;
    public int currentLevel = 0;
    
    [Serializable]
    public struct Level
    {
        public string m_name;
        public Texture2D m_data;
    }
    
    public Level[] m_levels;
    
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Me = this;
    }
    
    public void GetLevelTexture(ref Texture2D _tex)
    {
        if (overrideLevel == null)
            return;

        _tex = overrideLevel;
    }
}
