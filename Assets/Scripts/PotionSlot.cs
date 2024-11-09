using UnityEngine;
using UnityEngine.UI;

public class PotionSlot : MonoBehaviour
{
    [SerializeField] private Sprite m_selectedSprite;
    [SerializeField] private Sprite m_unselectedSprite;
    [SerializeField] public Image m_background;
    [SerializeField] public Image m_potionImage;

    [HideInInspector] public bool isEmpty;
    [HideInInspector] public GridHandler.Cell.Type m_type;
    [HideInInspector] public bool isSelected;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isEmpty = true;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SwitchSelected()
    {
        isSelected = !isSelected;
        m_background.sprite = isSelected ? m_selectedSprite : m_unselectedSprite;
    }

    
}