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
    [SerializeField] private Button combineButton;

    private void Awake()
    {
        Me = this;
    }

    private void OnEnable()
    {
        Time.timeScale = 0f;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
    }

    public void StartMe()
    {
        bool canCombine = PotionSlotManager.Me.CanCombine;
        gameObject.SetActive(canCombine);
        if (!canCombine)
            return;
        
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
    int ResultType => CombinePotions();
    
    void Update()
    {
        UpdateCombineSlots(acidSlot.slotSelected, PlayerInventory.ACID_INV_REF);
        UpdateCombineSlots(fireSlot.slotSelected, PlayerInventory.FIRE_INV_REF);
        UpdateCombineSlots(bounceSlot.slotSelected, PlayerInventory.BOUNCE_INV_REF);
        
        ingredient1.sprite = ingredient1full ? potionIcons[ingredient1contents] : emptyIcon;
        ingredient2.sprite = ingredient2full ? potionIcons[ingredient2contents] : emptyIcon;
        combineButton.interactable = ResultType != -1;
        
        UpdateSlotNumbers();
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
            if (ResultType == -1)
                return;
            PlayerInventory.Me.potionQuantities[ingredient1contents] -= 1;
            PlayerInventory.Me.potionQuantities[ingredient2contents] -= 1;
            PlayerInventory.Me.potionQuantities[ResultType] += 1;
        }
        PotionSlotManager.Me.StartMe();
    }

    private void UpdateSlotNumbers()
    {
        string[] quantityTexts = new string[PlayerInventory.PLAT_INV_REF + 1];
        for (int i = 0; i <= PlayerInventory.PLAT_INV_REF; ++i)
        {
            var isInput = (ingredient1full && i == ingredient1contents) || (ingredient2full && i == ingredient2contents);
            var isOutput = i == ResultType;
            var extra = isInput ? "<color=\"red\"> (-1)" : (isOutput ? "<color=\"green\"> (+1)" : "");
            quantityTexts[i] = $"{PlayerInventory.Me.potionQuantities[i]}{extra}";
        }
        
        acidSlot.SetQuantityText(quantityTexts[PlayerInventory.ACID_INV_REF]);
        fireSlot.SetQuantityText(quantityTexts[PlayerInventory.FIRE_INV_REF]);
        platSlot.SetQuantityText(quantityTexts[PlayerInventory.PLAT_INV_REF]);
        gasSlot.SetQuantityText(quantityTexts[PlayerInventory.GAS_INV_REF]);
        slimeSlot.SetQuantityText(quantityTexts[PlayerInventory.SLIME_INV_REF]);
        bounceSlot.SetQuantityText(quantityTexts[PlayerInventory.BOUNCE_INV_REF]);
    }
    
    private int CombinePotions()
    {
        if (!ingredient1full || !ingredient2full)
            return -1;
        
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

