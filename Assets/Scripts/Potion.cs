using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Potion : MonoBehaviour
{
    private const float c_shortBoundRadius = 0.2f;
    private const float c_longBoundRadius = 1.5f;
    
    [SerializeField] private FluidPixel m_fluidPixelPrefab;
    [SerializeField] private CustomCollider m_customColl;
    [SerializeField] private CustomBounds m_bounds;
    [SerializeField] private Rigidbody2D m_rb;
    [SerializeField] private SpriteRenderer m_contentsA;
    [SerializeField] private SpriteRenderer m_contentsB;
    private int m_capacity;
    private Vector2 _prevVelocity;
    private GridHandler.Cell.Type m_type;
    private int m_potionStyle;
    
    public void Init(Vector2 _force, int _capacity, GridHandler.Cell.Type _type)
    {
        m_capacity = _capacity;
        m_type = _type;
        var p = GridHandler.Me.GetProperties(_type);
        m_potionStyle = p.potionStyle;
        m_contentsA.color = p.colour;
        m_contentsB.color = p.colour2;

        m_bounds.size = m_potionStyle switch
        {
            0 or 1 => new Vector2(c_longBoundRadius, c_longBoundRadius),
            _ => new Vector2(c_shortBoundRadius, c_shortBoundRadius)
        };
        
        _force = _force.Rotate(Random.Range(-2f, 2f));
        _force *= Random.Range(0.98f, 1.02f);
        m_rb.AddForce(_force, ForceMode2D.Impulse);
        m_rb.angularVelocity = _force.x * 180f;
    }

    private void FixedUpdate()
    {
        if (m_customColl.Colliding)
        {
            if (m_potionStyle != 0)
            {
                var hitWall = m_customColl.CollidingHorizontal;
                var perpendicular = m_potionStyle == 1;
                m_bounds.size = perpendicular == hitWall ? new Vector2(c_longBoundRadius, c_shortBoundRadius) : new Vector2(c_shortBoundRadius, c_longBoundRadius);
            }

            Shatter();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
        _prevVelocity = m_rb.linearVelocity;
    }

    private void Shatter()
    {
        if (m_type == GridHandler.Cell.Type.Gas)
        {
            AudioManager.PlayAtPoint("explosion", transform.position);
            Manager.Me.m_lastExplosionTime = Time.time;
            GridHandler.Me.Explode(transform.position, 50);
            return;
        }
        
        AudioManager.PlayAtPoint("shatter", transform.position);
        var inertia = _prevVelocity;
        for (int i = 0; i < m_capacity; ++i)
        {
            var pos = Utility.RandomInsideBounds(m_bounds, Manager.c_CellRadius);
            var fluidPixel = Instantiate(m_fluidPixelPrefab, pos, Quaternion.identity, Manager.Me.m_fluidPixelHolder);
            fluidPixel.Init(m_type, 1f, new Vector2(inertia.x * Random.Range(0.98f, 1.02f), inertia.y * Random.Range(0.98f, 1.02f)));
        }
    }
}
