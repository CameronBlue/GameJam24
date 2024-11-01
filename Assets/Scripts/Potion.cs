using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Potion : MonoBehaviour
{
    private const float c_attractiveForce = 0.001f;
    private const float c_repulsiveForce = 0.000001f;
    
    [SerializeField] private FluidPixel m_fluidPixelPrefab;
    [SerializeField] private CustomCollider m_customColl;
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
        _prevVelocity = m_rb.linearVelocity;
    }

    private void Shatter()
    {
        var inertia = _prevVelocity;
        for (int i = 0; i < m_capacity; ++i)
        {
            var posOffset = Random.insideUnitCircle * 0.1f;
            var fluidPixel = Instantiate(m_fluidPixelPrefab, transform.position + posOffset.AddZ(), Quaternion.identity);
            fluidPixel.Init(m_type, 1f, new Vector2(_prevVelocity.x * Random.Range(0.98f, 1.02f), _prevVelocity.y * Random.Range(0.98f, 1.02f)));
        }
    }
}
