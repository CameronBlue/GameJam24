using System;
using System.Collections.Generic;
using UnityEngine;

public class MenuTabSwitcher : MonoBehaviour
{
    public string m_currentTab;
    
    private Dictionary<string, RectTransform> m_tabDict;
    
    private void Awake()
    {
        m_tabDict = new Dictionary<string, RectTransform>();
        foreach (RectTransform tab in transform)
        {
            tab.gameObject.SetActive(false);
            m_tabDict.Add(tab.name, tab);
        }
    }

    private void Start()
    {
        m_tabDict[m_currentTab].gameObject.SetActive(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            SwitchTab("Main");
    }

    public void SwitchTab(string _newTab)
    {
        m_tabDict[m_currentTab].gameObject.SetActive(false);
        m_currentTab = _newTab;
        m_tabDict[m_currentTab].gameObject.SetActive(true);
        
        if (_newTab == "Main")
            MainMenu.Me.ReturnedToMain();
    }
}
