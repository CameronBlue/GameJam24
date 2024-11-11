using System;
using System.Collections;
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
    
    private void Awake()
    {
        Me = this;
    }

    public void StartMe()
    {
        int[] starter = SaveManager.Me == null ? (new []{ 1, 0, 1, 1, 0, 0 }) : SaveManager.Me.GetLevelPotions();
        InitInventory(starter);
    }

    public void InitInventory(int[] starterQuantities)
    {
        potionQuantities = starterQuantities;
    }

    public void RemovePotion(GridHandler.Cell.Type type)
    {
        var slot = Array.FindIndex(inventoryTypeReference, x => x == type);
        potionQuantities[slot]--;
        if (potionQuantities[slot] == 0)
            PotionSlotManager.Me.PotionTypeFinished(type);
    }
}
