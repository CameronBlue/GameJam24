using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelOption : MonoBehaviour
{
    public Image m_image;
    public TextMeshProUGUI m_name;
    
    private Texture2D m_texture;
    
    public void SetLevel(SaveManager.Level _level)
    {
        m_texture = _level.m_data;
        
        m_image.preserveAspect = true;
        m_image.sprite = Sprite.Create(m_texture, new Rect(0, 0, m_texture.width, m_texture.height), new Vector2(0.5f, 0.5f));
        
        m_name.text = _level.m_name;
    }

    public void OnClicked()
    {
        LevelDisplayer.Me.SetLevel(this);
    }
}