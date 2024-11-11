using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PotionSlotManager : MonoBehaviour
{
    public static PotionSlotManager Me;
    
    [Serializable]
    public struct PotionSlot
    {
        public Sprite largeSprite;
        public Sprite smallSprite;
        [FormerlySerializedAs("m_type")] public GridHandler.Cell.Type type;
    }
    [SerializeField] private PotionSlot[] potionSlots;
    
    [SerializeField] private Image largeImage;
    [SerializeField] private Image smallImageLeft;
    [SerializeField] private Image smallImageRight;
    [SerializeField] private List<PotionSlot> currentPotionSlots;
    private int currentSelection = 0;

    private void Awake()
    {
        Me = this;
    }

    public void StartMe()
    {
        currentPotionSlots = new();
        for (var i = 0; i < PlayerInventory.Me.potionQuantities.Length; i++)
        {
            var q = PlayerInventory.Me.potionQuantities[i];
            if (q > 0)
                currentPotionSlots.Add(potionSlots[i]);
        }

        UpdateSelection();
    }

    void Update()
    {
        bool changed = false;
        if (Input.GetKeyDown(KeyCode.Q))
        {
            currentSelection++;
            changed = true;
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            currentSelection--;
            changed = true;
        }
        if (!changed) 
            return;
        
        UpdateSelection();
    }

    public void PotionTypeFinished(GridHandler.Cell.Type _type)
    {
        currentPotionSlots.RemoveAll(x => x.type == _type);
        UpdateSelection();
    }
    
    private void UpdateSelection()
    {
        if (currentPotionSlots.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }
        
        currentSelection = Mod(currentSelection);
        Character.Me.m_potionType = currentPotionSlots[currentSelection].type;
        largeImage.sprite = currentPotionSlots[currentSelection].largeSprite;
        
        if (currentPotionSlots.Count <= 1)
        {
            smallImageLeft.gameObject.SetActive(false);
            smallImageRight.gameObject.SetActive(false);
            return;
        }

        smallImageLeft.sprite = currentPotionSlots[Mod(currentSelection - 1)].smallSprite;
        smallImageRight.sprite = currentPotionSlots[Mod(currentSelection + 1)].smallSprite;
    }

    private int Mod(int x)
    {
        int y = currentPotionSlots.Count;
        return (x < 0) ? (y - (-x % y)) : (x % y);
    }
}