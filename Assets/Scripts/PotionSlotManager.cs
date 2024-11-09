using UnityEngine;
using UnityEngine.UI;

public class PotionSlotManager : MonoBehaviour
{
    private PotionSlot[] potionSlots;
    private int currentSelection;

    [SerializeField] private Sprite acidIcon;
    [SerializeField] private Sprite fireIcon;
    [SerializeField] private Sprite bounceIcon;
    [SerializeField] private Sprite platformIcon;
    [SerializeField] private Sprite gasIcon;
    [SerializeField] private Sprite slimeIcon;
    
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentSelection = 0;
        potionSlots = GetComponentsInChildren<PotionSlot>();
        potionSlots[currentSelection].SwitchSelected();

        potionSlots[0].m_type = GridHandler.Cell.Type.Acid;
        potionSlots[0].potionImage.sprite = acidIcon;

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (currentSelection != 0)
            {
                potionSlots[currentSelection].SwitchSelected();
                currentSelection--;
                potionSlots[currentSelection].SwitchSelected();
            }
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentSelection != potionSlots.Length - 1)
            {
                potionSlots[currentSelection].SwitchSelected();
                currentSelection++;
                potionSlots[currentSelection].SwitchSelected();
            }
        }
    }
}
