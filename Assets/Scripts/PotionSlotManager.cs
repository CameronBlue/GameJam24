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
        potionSlots[1].m_type = GridHandler.Cell.Type.Fire;
        potionSlots[1].potionImage.sprite = fireIcon;
        potionSlots[2].m_type = GridHandler.Cell.Type.Platform;
        potionSlots[2].potionImage.sprite = platformIcon;
        potionSlots[3].m_type = GridHandler.Cell.Type.Gas;
        potionSlots[3].potionImage.sprite = gasIcon;
        
                
        Character.Me.m_potionType = potionSlots[currentSelection].m_type;

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
                Character.Me.m_potionType = potionSlots[currentSelection].m_type;
            }
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentSelection != potionSlots.Length - 1)
            {
                potionSlots[currentSelection].SwitchSelected();
                currentSelection++;
                potionSlots[currentSelection].SwitchSelected();
                Character.Me.m_potionType = potionSlots[currentSelection].m_type;
            }
        }
    }
}
