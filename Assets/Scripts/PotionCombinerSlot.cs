using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PotionCombinerSlot : MonoBehaviour
{
    private RectTransform m_rt;

    [HideInInspector] public GridHandler.Cell.Type slotType;

    [HideInInspector] public Sprite slotSprite;
    [HideInInspector] public int slotNumber;
    
    [HideInInspector] public bool slotSelected;
    
    [SerializeField] private Image slotTypeImage;
    [SerializeField] private TMP_Text slotTypeText;
    [SerializeField] private Image backgroundImage;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_rt = GetComponent<RectTransform>();
        slotSelected = false;
    }

    void Update()
    {
        slotTypeImage.sprite = slotSprite;
        slotTypeText.text = slotNumber.ToString();
    }

    public void onButtonPressed()
    {   
        slotSelected = !slotSelected;
        backgroundImage.color = slotSelected ? new Color(255f, 255f, 255f) : new Color(0, 0, 0);
        slotTypeText.color = !slotSelected ? new Color(255f, 255f, 255f) : new Color(0, 0, 0);
        
    }
}
