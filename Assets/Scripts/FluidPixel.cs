using System;
using System.Collections.Generic;
using UnityEngine;

public class FluidPixel : MonoBehaviour
{
    [SerializeField] private Rigidbody2D m_rb;
    [SerializeField] private SpriteRenderer m_rend;
    [SerializeField] private CustomCollider m_customColl;

    private GridHandler.Cell m_cell;
    
    public void Init(GridHandler.Cell.Type _type, float _amount, Vector2 _force)
    {
        transform.localScale = Manager.c_CellDiameter * Vector3.one;
        m_cell = new GridHandler.Cell { m_type = _type, m_amount = _amount };
        var properties = GridHandler.Me.GetProperties(_type);
        m_rend.color = properties.colour;
        m_rb.gravityScale = properties.weight;
        
        m_rb.AddForce(_force, ForceMode2D.Impulse);
    }

    private void Update()
    {
        Manager.AddIntoGrid(transform.position, m_cell);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
