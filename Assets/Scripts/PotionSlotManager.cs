using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PotionSlotManager : MonoBehaviour
{
    [Serializable]
    public struct PotionSlot
    {
        public Sprite largeSprite;
        public Sprite smallSprite;
        [FormerlySerializedAs("m_type")] public GridHandler.Cell.Type type;
    }
    
    [SerializeField] private Image largeImage;
    [SerializeField] private Image smallImageLeft;
    [SerializeField] private Image smallImageRight;
    [SerializeField] private PotionSlot[] potionSlots;
    private int currentSelection = 0;

    private void Start()
    {
        UpdateSelection();
    }

    void Update()
    {
        bool changed = false;
        if (Input.GetKeyDown(KeyCode.Q))
        {
            currentSelection--;
            changed = true;
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            currentSelection++;
            changed = true;
        }
        if (!changed) 
            return;
        
        UpdateSelection();
    }
    
    private void UpdateSelection()
    {
        currentSelection = Mod(currentSelection, potionSlots.Length);
        Character.Me.m_potionType = potionSlots[currentSelection].type;
        
        largeImage.sprite = potionSlots[currentSelection].largeSprite;
        smallImageLeft.sprite = potionSlots[Mod(currentSelection - 1, potionSlots.Length)].smallSprite;
        smallImageRight.sprite = potionSlots[Mod(currentSelection + 1, potionSlots.Length)].smallSprite;
    }

    private int Mod(int x, int y)
    {
        return (x < 0) ? (y - (-x % y)) : (x % y);
    }
}
