using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PotionCombiner : MonoBehaviour
{
    public static PotionCombiner Me;
    
    public PotionCombinerSlot acidSlot;
    public PotionCombinerSlot fireSlot;
    public PotionCombinerSlot platSlot;
    public PotionCombinerSlot gasSlot;
    public PotionCombinerSlot slimeSlot;
    public PotionCombinerSlot bounceSlot;

    [SerializeField] private Sprite emptyIcon;
    [SerializeField] private Sprite acidIcon;
    [SerializeField] private Sprite fireIcon;
    [SerializeField] private Sprite bounceIcon;
    [SerializeField] private Sprite platformIcon;
    [SerializeField] private Sprite gasIcon;
    [SerializeField] private Sprite slimeIcon;
    private Sprite[] potionIcons;
    
    [SerializeField] private Image ingredient1;
    [SerializeField] private Image ingredient2;

    private void Awake()
    {
        Me = this;
    }

    public void StartMe()
    {
        
        potionIcons = new [] { fireIcon, gasIcon, acidIcon, bounceIcon, slimeIcon, platformIcon };
        
        acidSlot.SetSprite(acidIcon);
        fireSlot.SetSprite(fireIcon);
        platSlot.SetSprite(platformIcon);
        gasSlot.SetSprite(gasIcon);
        slimeSlot.SetSprite(slimeIcon);
        bounceSlot.SetSprite(bounceIcon);
        
        UpdateSlotNumbers();
        
    }
    
    bool ingredient1full;
    bool ingredient2full;
    
    int ingredient1contents;
    int ingredient2contents;
    
    void Update()
    {
        UpdateCombineSlots(acidSlot.slotSelected, PlayerInventory.ACID_INV_REF);
        UpdateCombineSlots(fireSlot.slotSelected, PlayerInventory.FIRE_INV_REF);
        UpdateCombineSlots(bounceSlot.slotSelected, PlayerInventory.BOUNCE_INV_REF);
        
        ingredient1.sprite = ingredient1full ? potionIcons[ingredient1contents] : emptyIcon;
        ingredient2.sprite = ingredient2full ? potionIcons[ingredient2contents] : emptyIcon;
    }

    private void UpdateCombineSlots(bool slotActive, int inventoryRef)
    {
        if (slotActive) {
            if (!ingredient1full)
            {
                ingredient1contents = inventoryRef;
                ingredient1full = true;
            } 
            else if (!ingredient2full && ingredient1contents != inventoryRef)
            {
                ingredient2contents = inventoryRef;
                ingredient2full = true;
            }
        } if (!slotActive && ingredient1contents == inventoryRef) {
            ingredient1full = false;
        } if (!slotActive && ingredient2contents == inventoryRef) {
            ingredient2full = false;
        } if (slotActive && ingredient1contents == inventoryRef && ingredient2contents == inventoryRef) {
            ingredient2full = false;
        }
    }

    public void onCombinePressed ()
    {
        if (ingredient1full && ingredient2full && PlayerInventory.Me.potionQuantities[ingredient1contents] > 0 && PlayerInventory.Me.potionQuantities[ingredient2contents] > 0)
        {
            var resultContents = CombinePotions();
            if (resultContents == -1)
                return;
            PlayerInventory.Me.potionQuantities[ingredient1contents] -= 1;
            PlayerInventory.Me.potionQuantities[ingredient2contents] -= 1;
            PlayerInventory.Me.potionQuantities[resultContents] += 1;
        }
        UpdateSlotNumbers();
    }

    private void UpdateSlotNumbers()
    {
        var acidQuantityText = $"{PlayerInventory.Me.potionQuantities[PlayerInventory.ACID_INV_REF]}";
        var fireQuantityText = $"{PlayerInventory.Me.potionQuantities[PlayerInventory.FIRE_INV_REF]}";
        var platQuantityText = $"{PlayerInventory.Me.potionQuantities[PlayerInventory.PLAT_INV_REF]}";
        var gasQuantityText = $"{PlayerInventory.Me.potionQuantities[PlayerInventory.GAS_INV_REF]}";
        var slimeQuantityText = $"{PlayerInventory.Me.potionQuantities[PlayerInventory.SLIME_INV_REF]}";
        var bounceQuantityText = $"{PlayerInventory.Me.potionQuantities[PlayerInventory.BOUNCE_INV_REF]}";
        
        acidSlot.SetQuantityText(acidQuantityText);
        fireSlot.SetQuantityText(fireQuantityText);
        platSlot.SetQuantityText(platQuantityText);
        gasSlot.SetQuantityText(gasQuantityText);
        slimeSlot.SetQuantityText(slimeQuantityText);
        bounceSlot.SetQuantityText(bounceQuantityText);
    }
    
    private int CombinePotions()
    {
        var type1 = Mathf.Min(ingredient1contents, ingredient2contents);
        var type2 = Mathf.Max(ingredient1contents, ingredient2contents);

        if (type1 == PlayerInventory.FIRE_INV_REF && type2 == PlayerInventory.ACID_INV_REF)
        {
            return PlayerInventory.GAS_INV_REF;
        }
        if (type1 == PlayerInventory.FIRE_INV_REF && type2 == PlayerInventory.BOUNCE_INV_REF)
        {
            return PlayerInventory.PLAT_INV_REF;
        }
        if (type1 == PlayerInventory.ACID_INV_REF && type2 == PlayerInventory.BOUNCE_INV_REF)
        {
            return PlayerInventory.SLIME_INV_REF;
        }
        return -1;
    }
}

