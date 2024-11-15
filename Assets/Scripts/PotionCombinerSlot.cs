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

    private int quantity;

    private void Start()
    {
        backgroundImage.color = unselectedColor;
    }

    public void SetSprite(Sprite _sprite)
    {
        slotTypeImage.sprite = _sprite;
    }

    public void SetQuantityText(string _text, int _quantity)
    {
        quantity = _quantity;
        slotTypeText.text = _text;
    }

    public void onButtonPressed()
    {
        if (quantity <= 0)
            return;
        slotSelected = !slotSelected;
        backgroundImage.color = slotSelected ? selectedColor : unselectedColor;
    }

    public void Deselect()
    {
        slotSelected = false;
        backgroundImage.color = unselectedColor;
    }
}
