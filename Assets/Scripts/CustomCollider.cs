using System;
using System.Collections.Generic;
using UnityEngine;

public class CustomCollider : MonoBehaviour
{
    public CustomBounds m_bounds;
    
    [SerializeField]
    private Rigidbody2D m_rb;
    
    private BoundsCopy prevBounds;

    private int m_groundedState;
    
    public bool HitObstacle => m_groundedState > 0;
    public bool OnGround => (m_groundedState & 1) == 1;
    public bool OnWall => (m_groundedState & 6) > 0;
    public bool OnRightWall => (m_groundedState & 2) == 2;
    public bool OnLeftWall => (m_groundedState & 4) == 4;
    public bool Stuck => (m_groundedState & 16) == 16;

    private void Start()
    {
        Manager.AddCollider(this);
        prevBounds = m_bounds.Copy;
    }

    public void HandleCollisions(Vector3[] _blocks)
    {
        var velocity = m_rb.linearVelocity;
        if (velocity.sqrMagnitude < 0.0001f * 0.0001f)
            return;
        m_groundedState = 0;
        
        List<Vector2> unhappyBlocks = new();
        foreach (var block in _blocks)
        {
            var viscosity = block.z;
            var extent = Manager.c_CellRadius * Vector2.one;
            var blockMin = (Vector2)block - extent;
            var blockMax = (Vector2)block + extent;

            if (viscosity < 1f)
                continue;
            
            var (xRel, yRel) = GetRelative(m_bounds, blockMin, blockMax);
            if (xRel != 0 || yRel != 0)
                continue;
            
            var (prevXRel, prevYRel) = GetRelative(prevBounds, blockMin, blockMax);
            bool xChanged = prevXRel != 0;
            bool yChanged = prevYRel != 0;
            if (!xChanged && !yChanged)
            {
                m_groundedState |= 16;
                continue;
            }
            if (!xChanged)
            {
                if (prevYRel == 1) //Above
                {
                    m_bounds.SetBottom(blockMax.y);
                    velocity.y = Mathf.Max(0f, velocity.y);
                    m_groundedState |= 1;
                }
                else //Below
                {
                    m_bounds.SetTop(blockMin.y);
                    velocity.y = Mathf.Min(0f, velocity.y);
                    m_groundedState |= 8;
                }
                continue;
            }
            if (!yChanged)
            {
                if (prevXRel == 1) //Right
                {
                    m_bounds.SetLeft(blockMax.x);
                    velocity.x = Mathf.Max(0f, velocity.x);
                    m_groundedState |= 2;
                }
                else //Left
                {
                    m_bounds.SetRight(blockMin.x);
                    velocity.x = Mathf.Min(0f, velocity.x);
                    m_groundedState |= 4;
                }
                continue;
            }
            unhappyBlocks.Add(block);
        }
        foreach (var block in unhappyBlocks)
        {
            var extent = Manager.c_CellRadius * Vector2.one;
            var blockMin = block - extent;
            var blockMax = block + extent;
            
            var (xRel, yRel) = GetRelative(m_bounds, blockMin, blockMax);
            if (xRel != 0 || yRel != 0)
                continue;

            var (prevXRel, prevYRel) = GetRelative(prevBounds, blockMin, blockMax);
            if (velocity.y * velocity.y > velocity.x * velocity.x)
            {
                if (prevYRel == 1) //Above
                {
                    m_bounds.SetBottom(blockMax.y);
                    velocity.y = Mathf.Max(0f, velocity.y);
                    m_groundedState |= 1;
                }
                else //Below
                {
                    m_bounds.SetTop(blockMin.y);
                    velocity.y = Mathf.Min(0f, velocity.y);
                }
            }
            else
            {
                if (prevXRel == 1) //Right
                {
                    m_bounds.SetLeft(blockMax.x);
                    velocity.x = Mathf.Max(0f, velocity.x);
                    m_groundedState |= 2;
                }
                else //Left
                {
                    m_bounds.SetRight(blockMin.x);
                    velocity.x = Mathf.Min(0f, velocity.x);
                    m_groundedState |= 4;
                }
            }
        }
        m_rb.linearVelocity = velocity;
        prevBounds = m_bounds.Copy;
    }

    private (int, int) GetRelative(IBounds _bounds, Vector2 _min, Vector2 _max)
    {
        int x = (_bounds.min.x >= _max.x) ? 1 : ((_bounds.max.x <= _min.x) ? -1 : 0);
        int y = (_bounds.min.y >= _max.y) ? 1 : ((_bounds.max.y <= _min.y) ? -1 : 0);
        return (x, y);
    }

    private void OnDestroy()
    {
        Manager.RemoveCollider(this);
    }
}
