using System;
using System.Collections.Generic;
using UnityEngine;

public class CustomCollider : MonoBehaviour
{
    [SerializeField]
    private Collider2D m_coll;
    [SerializeField]
    private Rigidbody2D m_rb;
    
    private Bounds prevBounds;

    private int m_groundedState;
    
    public bool OnGround => (m_groundedState & 1) == 1;
    public bool OnWall => (m_groundedState & 6) > 0;
    public bool OnRightWall => (m_groundedState & 2) == 2;
    public bool OnLeftWall => (m_groundedState & 4) == 4;
    public bool CanJump => OnGround;
    public bool CanWallJumpLeft => OnLeftWall && !OnGround && !OnRightWall;
    public bool CanWallJumpRight => OnRightWall && !OnGround && !OnLeftWall;

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
        var velocity = m_rb.linearVelocity;
        if (velocity.sqrMagnitude < 0.0001f * 0.0001f)
            return;
        
        m_groundedState = 0;
        
        var bounds = m_coll.bounds;
        var pos = bounds.center;
        List<Vector2> unhappyBlocks = new();
        
        foreach (var block in _blocks)
        {
            var viscosity = block.z;
            var extent = 0.5f * GridHandler.c_CellDiameter * Vector2.one;
            var blockMin = (Vector2)block - extent;
            var blockMax = (Vector2)block + extent;

            if (viscosity < 1f)
                continue;
            
            var (xRel, yRel) = GetRelative(bounds, blockMin, blockMax);
            if (xRel != 0 || yRel != 0)
                continue;
            
            var (prevXRel, prevYRel) = GetRelative(prevBounds, blockMin, blockMax);
            bool xChanged = prevXRel != 0;
            bool yChanged = prevYRel != 0;
            if (!xChanged && !yChanged)
            {
                Debug.LogError("Stuck in something");
                continue;
            }
            if (!xChanged)
            {
                if (prevYRel == 1) //Above
                {
                    pos.y = blockMax.y + bounds.extents.y;
                    velocity.y = Mathf.Max(0f, velocity.y);
                    m_groundedState |= 1;
                }
                else //Below
                {
                    pos.y = blockMin.y - bounds.extents.y;
                    velocity.y = Mathf.Min(0f, velocity.y);
                }
                bounds.center = pos;
                continue;
            }
            if (!yChanged)
            {
                if (prevXRel == 1) //Right
                {
                    pos.x = blockMax.x + bounds.extents.x;
                    velocity.x = Mathf.Max(0f, velocity.x);
                    m_groundedState |= 2;
                }
                else //Left
                {
                    pos.x = blockMin.x - bounds.extents.x;
                    velocity.x = Mathf.Min(0f, velocity.x);
                    m_groundedState |= 4;
                }
                bounds.center = pos;
                continue;
            }
            unhappyBlocks.Add(block);
        }
        foreach (var block in unhappyBlocks)
        {
            var extent = 0.5f * GridHandler.c_CellDiameter * Vector2.one;
            var blockMin = block - extent;
            var blockMax = block + extent;
            
            var (xRel, yRel) = GetRelative(bounds, blockMin, blockMax);
            if (xRel != 0 || yRel != 0)
                continue;

            var (prevXRel, prevYRel) = GetRelative(prevBounds, blockMin, blockMax);
            if (velocity.y * velocity.y > velocity.x * velocity.x)
            {
                if (prevYRel == 1) //Above
                {
                    pos.y = blockMax.y + bounds.extents.y;
                    velocity.y = Mathf.Max(0f, velocity.y);
                    m_groundedState |= 1;
                }
                else //Below
                {
                    pos.y = blockMin.y - bounds.extents.y;
                    velocity.y = Mathf.Min(0f, velocity.y);
                }
            }
            else
            {
                if (prevXRel == 1) //Right
                {
                    pos.x = blockMax.x + bounds.extents.x;
                    velocity.x = Mathf.Max(0f, velocity.x);
                    m_groundedState |= 2;
                }
                else //Left
                {
                    pos.x = blockMin.x - bounds.extents.x;
                    velocity.x = Mathf.Min(0f, velocity.x);
                    m_groundedState |= 4;
                }
            }
            bounds.center = pos;
        }
        
        transform.position = pos;
        m_rb.linearVelocity = velocity;
        prevBounds = bounds;
    }

    private (int, int) GetRelative(Bounds _bounds, Vector2 _min, Vector2 _max)
    {
        int x = (_bounds.min.x >= _max.x) ? 1 : ((_bounds.max.x <= _min.x) ? -1 : 0);
        int y = (_bounds.min.y >= _max.y) ? 1 : ((_bounds.max.y <= _min.y) ? -1 : 0);
        return (x, y);
    }
}
