using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Potion : MonoBehaviour
{
    [SerializeField] private FluidPixel m_fluidPixelPrefab;
    [SerializeField] private CustomCollider m_customColl;
    [SerializeField] private Collider2D m_coll;
    [SerializeField] private Rigidbody2D m_rb;

    private int m_capacity;
    private Vector2 _prevVelocity;
    private GridHandler.Cell.Type m_type;
    
    public void Init(Vector2 _force, int _capacity, GridHandler.Cell.Type _type)
    {
        m_capacity = _capacity;
        m_type = _type;
        _force = _force.Rotate(Random.Range(-2f, 2f));
        _force *= Random.Range(0.98f, 1.02f);
        m_rb.AddForce(_force, ForceMode2D.Impulse);
    }

    private void FixedUpdate()
    {
        var hit = m_customColl.OnGround || m_customColl.OnWall;
        if (hit)
        {
            Shatter();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    private void Shatter()
    {
        var inertia = m_rb.linearVelocity;
        for (int i = 0; i < m_capacity; ++i)
        {
            var fluidPixel = Instantiate(m_fluidPixelPrefab, Utility.RandomInsideBounds(m_coll.bounds), Quaternion.identity);
            fluidPixel.Init(m_type, 1f, new Vector2(inertia.x * Random.Range(0.98f, 1.02f), inertia.y * Random.Range(0.98f, 1.02f)));
        }
    }
}
