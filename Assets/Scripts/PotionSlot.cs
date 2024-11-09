using UnityEngine;
using UnityEngine.UI;

public class PotionSlot : MonoBehaviour
{
    [SerializeField] private Sprite selectedSprite;
    [SerializeField] private Sprite unselectedSprite;
    [SerializeField] public Image background;
    [SerializeField] public Image potionImage;

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
        background.sprite = isSelected ? selectedSprite : unselectedSprite;
    }

    
}