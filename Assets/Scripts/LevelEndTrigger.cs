using System;
using UnityEngine;
using UnityEngine.UI;

public class LevelEndTrigger : MonoBehaviour
{
    private const float c_PlayerMaxDistance = 1f;

    public BoxCollider2D m_collider;
    public SpriteRenderer m_image;
    public Sprite m_chestSprite;
    public Sprite m_doorSprite;
    private bool IsCloseEnough() => Vector2.Distance(transform.position, Character.Me.transform.position) < c_PlayerMaxDistance;


    private void Start()
    {
        transform.position = GridHandler.Me.GetEndPoint();
        m_image.sprite = SaveManager.Me.GetIsFinal() ? m_doorSprite : m_chestSprite;
        m_collider.size = m_image.size;
    }

    private void OnMouseDown()
    {
        if (IsCloseEnough())
        {
            SaveManager.Me.LevelComplete();
        }
    }
}
