using System.Collections;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public const int ACID_INV_REF = 0;
    public const int FIRE_INV_REF = 1;
    public const int PLAT_INV_REF = 2;
    public const int GAS_INV_REF = 3;
    public const int SLIME_INV_REF = 4;
    public const int BOUNCE_INV_REF = 5;
    
    [HideInInspector]
    public int[] potionQuantities;
    [HideInInspector]
    public GridHandler.Cell.Type[] inventoryTypeReference =
    {
        GridHandler.Cell.Type.Acid, 
        GridHandler.Cell.Type.Fire, 
        GridHandler.Cell.Type.Platform,
        GridHandler.Cell.Type.Gas
    }; //, GridHandler.Cell.Type.Slime, GridHandler.Cell.Type.Bounce};
    void Start()
    {
        //Remove this when the level dictates the player starting inventory
        int[] starter = {1, 1, 1, 1, 0, 0 };
        InitInventory(starter);
    }

    public void InitInventory(int[] starterQuantities)
    {
        potionQuantities = starterQuantities;
    }
}
