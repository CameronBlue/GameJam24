using System;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    private const float c_CameraTightness = 0.1f;
    private const float c_PlayerSpeed = 500f;
    private const float c_JumpImpulse = 1000f;
    private const float c_GroundNormalThreshold = 0.8f;
    
    public Fluid m_fluidPrefab;
    
    private Vector3 m_smoothedPos = Vector3.zero;
    private Camera m_mainCam;

    [SerializeField]
    private Rigidbody2D m_rb;
    [SerializeField]
    private Collider2D m_coll;

    private int m_groundedState;
    
    private void Start()
    {
        m_smoothedPos = transform.position;
        
        m_mainCam = Camera.main;
    }

    private void Update()
    {
        UpdateMovement();
        UpdateGun();
    }

    private void UpdateMovement()
    {
        var force = Vector2.zero;
        if ((m_groundedState & 1) == 1)
        {
            if (Input.GetKey(KeyCode.A))
                force += Vector2.left * (c_PlayerSpeed * Time.deltaTime);
            if (Input.GetKey(KeyCode.D))
                force += Vector2.right * (c_PlayerSpeed * Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.Space))
            {
                force += Vector2.up * c_JumpImpulse;
                m_groundedState &= ~1;
            }
        }
        else if ((m_groundedState & 6) > 0)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var side = (m_groundedState & 2) == 2 ? Vector2.right : Vector2.left;
                force += (Vector2.up + side).normalized * c_JumpImpulse;
                m_groundedState &= ~6;
            }
        }
        m_rb.AddForce(force);
    }

    private void UpdateGun()
    {
        var target = m_mainCam.ScreenToWorldPoint(Input.mousePosition);
        target.z = 0;
        if (Input.GetMouseButtonDown(0))
            Shoot(target);
    }

    private void Shoot(Vector3 _target)
    {
        var force = Utility.GetForceForPosition(transform.position, _target, 20f);
        var fluid = Instantiate(m_fluidPrefab, transform.position, Quaternion.identity);
        fluid.Init(force, 50);
    }

    private Vector3 m_lastPos;
    private void FixedUpdate()
    {
        m_groundedState = 0;
        
        m_smoothedPos = Vector3.Lerp(m_smoothedPos, transform.position, c_CameraTightness);
        m_smoothedPos.z = m_mainCam.transform.position.z;
        m_mainCam.transform.position = m_smoothedPos;
        
        var result = GridHandler.Me.CheckCells(m_coll.bounds);
        HandleCollisions(result);
    }

    private void HandleCollisions(Vector3[] _blocks)
    {
        var bounds = m_coll.bounds;
        var pos = (Vector2)transform.position;
        bounds.size += Vector3.one * GridHandler.c_CellDiameter;
        
        var velocity = m_rb.linearVelocity;
        var directions = new HashSet<Vector2>();
        Array.Sort(_blocks, (a,b) => b.z.CompareTo(a.z));
        foreach (var block in _blocks)
        {
            var blockPos = new Vector2(block.x, block.y);
            var viscosity = block.z;
            
            if (blockPos.x < bounds.min.x || blockPos.x > bounds.max.x || 
                blockPos.y < bounds.min.y || blockPos.y > bounds.max.y)
                continue;

            var offset = pos - blockPos;

            if (Mathf.Abs(offset.x) > Mathf.Abs(offset.y))
                offset.y = 0;
            else
                offset.x = 0;
            var normal = offset.normalized;

            if (directions.Contains(normal))
                continue;
            directions.Add(normal);
            
            velocity -= viscosity * Vector2.Dot(velocity, normal) * normal;

            if (viscosity is < 0.999f or > 1.001f)
                continue;
                
            var ideal = normal * (Vector2.Dot(normal, bounds.extents));
            var actual = normal * (Vector2.Dot(normal, offset));
            pos += ideal - actual;
        }
        
        if (directions.Contains(Vector2.up))
            m_groundedState |= 1;
        if (directions.Contains(Vector2.right))
            m_groundedState |= 2;
        if (directions.Contains(Vector2.left))
            m_groundedState |= 4;
        
        transform.position = pos;
        m_rb.linearVelocity = velocity;
    }
    
    private void OnCollisionEnter2D(Collision2D _c)
    {
        var contact = _c.GetContact(0);
        if (contact.normal.y > c_GroundNormalThreshold)
            m_groundedState |= 1;
        else if (contact.normal.x > 0)
            m_groundedState |= 2;
        else
            m_groundedState |= 4;
    }
    
    private void OnCollisionStay2D(Collision2D _c)
    {
        var contact = _c.GetContact(0);
        if (contact.normal.y > c_GroundNormalThreshold)
            m_groundedState |= 1;
        else if (contact.normal.x > 0)
            m_groundedState |= 2;
        else
            m_groundedState |= 4;
    }
}
