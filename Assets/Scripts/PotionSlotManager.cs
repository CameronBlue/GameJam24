using UnityEngine;

public class PotionSlotManager : MonoBehaviour
{
    private PotionSlot[] potionSlots;
    private int currentSelection;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentSelection = 0;
        potionSlots = GetComponentsInChildren<PotionSlot>();
        print(potionSlots.Length);
        potionSlots[currentSelection].SwitchSelected();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            print("q pressed");
            if (currentSelection != 0)
            {
                potionSlots[currentSelection].SwitchSelected();
                currentSelection--;
                potionSlots[currentSelection].SwitchSelected();
            }
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            print("e pressed");
            if (currentSelection != potionSlots.Length - 1)
            {
                potionSlots[currentSelection].SwitchSelected();
                currentSelection++;
                potionSlots[currentSelection].SwitchSelected();
            }
        }
        print(currentSelection);
    }
}
