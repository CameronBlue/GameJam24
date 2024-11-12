using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Me;
    
    public const int FIRE_INV_REF = 0;
    public const int GAS_INV_REF = 1;
    public const int ACID_INV_REF = 2;
    public const int BOUNCE_INV_REF = 3;
    public const int SLIME_INV_REF = 4;
    public const int PLAT_INV_REF = 5;
    
    [NonSerialized]
    public GridHandler.Cell.Type[] inventoryTypeReference =
    {
        GridHandler.Cell.Type.Fire, 
        GridHandler.Cell.Type.Gas,
        GridHandler.Cell.Type.Acid, 
        GridHandler.Cell.Type.Bounce,
        GridHandler.Cell.Type.Slime,
        GridHandler.Cell.Type.Platform
    };

    public int[] potionQuantities;

    public Dictionary<GridHandler.Cell.Type, int> typeQuantities = new();
    
    private void Awake()
    {
        Me = this;
    }

    public void StartMe()
    {
        potionQuantities = SaveManager.Me == null ? new []{ 9, 9, 9, 9, 9, 9 } : SaveManager.Me.GetLevelPotions();
        for (int i = 0; i < inventoryTypeReference.Length; ++i)
            typeQuantities.Add(inventoryTypeReference[i], potionQuantities[i]);
    }

    public void RemovePotion(GridHandler.Cell.Type type)
    {
        var quantity = typeQuantities[type];
        if (--quantity == 0)
            PotionSlotManager.Me.PotionTypeFinished(type);
        typeQuantities[type] = quantity;
    }
}
