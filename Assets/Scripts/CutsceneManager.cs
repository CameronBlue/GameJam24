using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CutsceneManager : MonoBehaviour
{
    public Image m_image;
    public TextMeshProUGUI m_text;

    [Serializable]
    public struct Cutscene
    {
        [Serializable]
        public struct Page
        {
            public Sprite image;
            public string text;
        }
        public List<Page> content;
        public int Count => content.Count;
        public Page this[int i] => content[i];
    }


    public List<Cutscene> cutscenes;
    
    private Cutscene currentCutscene;
    private int pageIndex;

    private void Start()
    {
        currentCutscene = cutscenes[SaveManager.Me.cutsceneIndex];
        LoadPage();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            pageIndex++;
            if (pageIndex >= currentCutscene.Count)
            {
                SaveManager.Me.CutsceneFinished();
                gameObject.SetActive(false);
                return;
            }
            LoadPage();
        }
    }

    private void LoadPage()
    {
        m_image.sprite = currentCutscene[pageIndex].image;
        m_text.text = currentCutscene[pageIndex].text;
    }
}
