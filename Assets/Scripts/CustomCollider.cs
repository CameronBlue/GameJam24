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
            
            int xRel = (bounds.min.x >= blockMax.x) ? 1 : ((bounds.max.x <= blockMin.x) ? -1 : 0);
            int yRel = (bounds.min.y >= blockMax.y) ? 1 : ((bounds.max.y <= blockMin.y) ? -1 : 0);

            if (xRel != 0 || yRel != 0)
                continue;
            
            int prevXRel = (prevBounds.min.x >= blockMax.x) ? 1 : ((prevBounds.max.x <= blockMin.x) ? -1 : 0);
            int prevYRel = (prevBounds.min.y >= blockMax.y) ? 1 : ((prevBounds.max.y <= blockMin.y) ? -1 : 0);
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
            
            int xRel = (bounds.min.x >= blockMax.x) ? 1 : ((bounds.max.x <= blockMin.x) ? -1 : 0);
            int yRel = (bounds.min.y >= blockMax.y) ? 1 : ((bounds.max.y <= blockMin.y) ? -1 : 0);

            if (xRel != 0 || yRel != 0)
                continue;

            if (velocity.y * velocity.y > velocity.x * velocity.x)
            {
                int prevYRel = (prevBounds.min.y > blockMax.y) ? 1 : ((prevBounds.max.y < blockMin.y) ? -1 : 0);
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
            int prevXRel = (prevBounds.min.x > blockMax.x) ? 1 : ((prevBounds.max.x < blockMin.x) ? -1 : 0);
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
        }
        
        transform.position = pos;
        m_rb.linearVelocity = velocity;
        prevBounds = bounds;
    }

    private Vector2 Overlap(Bounds _a, Bounds _b)
    {
        return Overlap(_a, _b, Vector2.zero);
    }
    
    private Vector2 Overlap(Bounds _a, Bounds _b, Vector2 _offset)
    {
        var minMax = Vector2.Min((Vector2)_a.max + _offset, _b.max);
        var maxMin = Vector2.Max((Vector2)_a.min + _offset, _b.min);
        return Vector2.Max(Vector2.zero, minMax - maxMin);
    }

    private float Unoverlap(Bounds _a, Bounds _b, Vector2 _velocity)
    {
        var toMoveX = _velocity.x > 0f ? (_a.max.x - _b.min.x) : (_a.min.x - _b.max.x);
        var toMoveY = _velocity.y > 0f ? (_a.max.y - _b.min.y) : (_a.min.y - _b.max.y);
        
        if (_velocity.x == 0f)
            return toMoveY / _velocity.y;
        if (_velocity.y == 0f)
            return toMoveX / _velocity.x;
        
        return Mathf.Min(toMoveX / _velocity.x, toMoveY / _velocity.y);
    }
}
