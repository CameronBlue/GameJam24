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
        
        potionIcons = new [] { acidIcon, fireIcon, platformIcon, gasIcon, slimeIcon, bounceIcon };
        
        PlayerInventory inv = PlayerInventory.Me;
        acidSlot.slotSprite = acidIcon;
        acidSlot.slotType = inv.inventoryTypeReference[PlayerInventory.ACID_INV_REF];
        
        fireSlot.slotSprite = fireIcon;
        fireSlot.slotType = inv.inventoryTypeReference[PlayerInventory.FIRE_INV_REF];
        
        platSlot.slotSprite = platformIcon;
        platSlot.slotType = inv.inventoryTypeReference[PlayerInventory.PLAT_INV_REF];
        gasSlot.slotSprite = gasIcon;
        gasSlot.slotType = inv.inventoryTypeReference[PlayerInventory.GAS_INV_REF];
        
        slimeSlot.slotSprite = slimeIcon;
        slimeSlot.slotType = inv.inventoryTypeReference[PlayerInventory.SLIME_INV_REF];
        
        bounceSlot.slotSprite = bounceIcon;
        bounceSlot.slotType = inv.inventoryTypeReference[PlayerInventory.BOUNCE_INV_REF];
        
        UpdateSlotNumbers();
        
    }

    // Update is called once per frame
    
    bool ingredient1full;
    bool ingredient2full;
    
    int ingredient1contents;
    int ingredient2contents;
    
    void Update()
    {
        UpdateCombineSlots(acidSlot.slotSelected, PlayerInventory.ACID_INV_REF);
        UpdateCombineSlots(fireSlot.slotSelected, PlayerInventory.FIRE_INV_REF);
        UpdateCombineSlots(bounceSlot.slotSelected, PlayerInventory.BOUNCE_INV_REF);
        
        ingredient1.sprite = ingredient1full ? potionIcons[ingredient1contents] : null;
        ingredient2.sprite = ingredient2full ? potionIcons[ingredient2contents] : null;
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
            PlayerInventory.Me.potionQuantities[ingredient1contents] -= 1;
            PlayerInventory.Me.potionQuantities[ingredient2contents] -= 1;
            
            PlayerInventory.Me.potionQuantities[CombinePotions(ingredient1contents, ingredient2contents)] += 1;
        }
        UpdateSlotNumbers();
    }

    private void UpdateSlotNumbers()
    {
        acidSlot.slotNumber = PlayerInventory.Me.potionQuantities[PlayerInventory.ACID_INV_REF];
        fireSlot.slotNumber = PlayerInventory.Me.potionQuantities[PlayerInventory.FIRE_INV_REF];
        platSlot.slotNumber = PlayerInventory.Me.potionQuantities[PlayerInventory.PLAT_INV_REF];
        gasSlot.slotNumber = PlayerInventory.Me.potionQuantities[PlayerInventory.GAS_INV_REF];
        slimeSlot.slotNumber = PlayerInventory.Me.potionQuantities[PlayerInventory.SLIME_INV_REF];
        bounceSlot.slotNumber = PlayerInventory.Me.potionQuantities[PlayerInventory.BOUNCE_INV_REF];
    }
    
    private int CombinePotions(int ingredient1, int ingredient2)
    {
        // ACID_INV_REF = 0;
        // FIRE_INV_REF = 1;
        // PLAT_INV_REF = 2;
        // GAS_INV_REF = 3;
        // SLIME_INV_REF = 4;
        // BOUNCE_INV_REF = 5;
        
        

        if (ingredient1 > ingredient2)
        {
            (ingredient1, ingredient2) = (ingredient2, ingredient1);
        }

        if (ingredient1 == 0 && ingredient2 == 1)
        {
            return 3;
        }
        if (ingredient1 == 0 && ingredient2 == 5)
        {
            return 4;
        }
        if (ingredient1 == 1 && ingredient2 == 5)
        {
            return 2;
        }
        return -1;
    }
}

