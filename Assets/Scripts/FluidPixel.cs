using System;
using System.Collections.Generic;
using UnityEngine;

public class FluidPixel : MonoBehaviour
{
    [SerializeField] private Rigidbody2D m_rb;
    [SerializeField] private Collider2D m_coll;
    [SerializeField] private SpriteRenderer m_rend;
    [SerializeField] private CustomCollider m_customColl;

    public Potion Potion { get; set; }

    private GridHandler.Cell m_cell;
    
    public void Init(GridHandler.Cell.Type _type, float _amount, Vector2 _force)
    {
        m_cell = new GridHandler.Cell { m_type = _type, m_amount = _amount };
        m_rend.color = GridHandler.Me.GetProperties(m_cell).colour;
        
        m_rb.AddForce(_force, ForceMode2D.Impulse);
    }

    private void FixedUpdate()
    {
        if (m_customColl.OnGround)
        {
            GridHandler.Me.AddIntoGrid(transform.position, m_cell);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
