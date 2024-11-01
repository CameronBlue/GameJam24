using System;
using System.Collections.Generic;
using UnityEngine;

public class CustomCollider : MonoBehaviour
{
    [SerializeField]
    private Collider2D m_coll;
    [SerializeField]
    private Rigidbody2D m_rb;

    private int m_groundedState;
    
    public bool OnGround => (m_groundedState & 1) == 1;
    public bool OnWall => (m_groundedState & 6) > 0;
    public bool OnRightWall => (m_groundedState & 2) == 2;
    public bool OnLeftWall => (m_groundedState & 4) == 4;

    private void LateUpdate()
    {
        m_groundedState = 0;
    }

    private void FixedUpdate()
    {
        m_groundedState = 0;
        
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
        Array.Sort(_blocks, (a, b) => b.z.CompareTo(a.z));
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
}
