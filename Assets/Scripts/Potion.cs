using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Potion : MonoBehaviour
{
    [SerializeField] private FluidPixel m_fluidPixelPrefab;
    [SerializeField] private CustomCollider m_customColl;
    [SerializeField] private CustomBounds m_bounds;
    [SerializeField] private Rigidbody2D m_rb;
    [SerializeField] private SpriteRenderer m_contentsA;
    [SerializeField] private SpriteRenderer m_contentsB;
    private int m_capacity;
    private Vector2 _prevVelocity;
    private GridHandler.Cell.Type m_type;
    
    public void Init(Vector2 _force, int _capacity, GridHandler.Cell.Type _type)
    {
        m_capacity = _capacity;
        m_type = _type;
        m_contentsA.color = GridHandler.Me.GetProperties(_type).colour;
        m_contentsB.color = GridHandler.Me.GetProperties(_type).colour2;
        _force = _force.Rotate(Random.Range(-2f, 2f));
        _force *= Random.Range(0.98f, 1.02f);
        m_rb.AddForce(_force, ForceMode2D.Impulse);
    }

    private void FixedUpdate()
    {
        if (m_customColl.HitObstacle)
        {
            Shatter();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
        _prevVelocity = m_rb.linearVelocity;
    }

    private void Shatter()
    {
        var inertia = _prevVelocity;
        for (int i = 0; i < m_capacity; ++i)
        {
            var pos = Utility.RandomInsideBounds(m_bounds, Manager.c_CellRadius);
            var fluidPixel = Instantiate(m_fluidPixelPrefab, pos, Quaternion.identity, Manager.Me.m_fluidPixelHolder);
            fluidPixel.Init(m_type, 1f, new Vector2(inertia.x * Random.Range(0.98f, 1.02f), inertia.y * Random.Range(0.98f, 1.02f)));
        }
    }
}
