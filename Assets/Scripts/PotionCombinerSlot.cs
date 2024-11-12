using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PotionCombinerSlot : MonoBehaviour
{
    [NonSerialized] public bool slotSelected;
    
    [SerializeField] private Color selectedColor;
    [SerializeField] private Color unselectedColor;
    [SerializeField] private Image slotTypeImage;
    [SerializeField] private TMP_Text slotTypeText;
    [SerializeField] private Image backgroundImage;

    private void Start()
    {
        backgroundImage.color = unselectedColor;
    }

    public void SetSprite(Sprite _sprite)
    {
        slotTypeImage.sprite = _sprite;
    }

    public void SetQuantityText(string _text)
    {
        slotTypeText.text = _text;
    }

    public void onButtonPressed()
    {   
        slotSelected = !slotSelected;
        backgroundImage.color = slotSelected ? selectedColor : unselectedColor;
        //slotTypeText.color = !slotSelected ? new Color(255f, 255f, 255f) : new Color(0, 0, 0);
    }
}
