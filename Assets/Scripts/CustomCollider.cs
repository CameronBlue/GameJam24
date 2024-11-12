using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CustomCollider : MonoBehaviour
{
    public CustomBounds m_bounds;
    
    [SerializeField]
    private Rigidbody2D m_rb;
    
    private BoundsCopy prevBounds;

    private int4 m_groundedState; //x: left, y: bottom, z: right, w: top; 0=empty, 1=block, 2=slime, 3=bounce
    
    public bool Colliding => math.any(m_groundedState > 0);
    public bool CollidingHorizontal => math.any(m_groundedState.xz > 0);
    public bool CanJump => m_groundedState.y > 0;
    public bool CanWallJumpLeft => m_groundedState.z == 1;
    public bool CanWallJumpRight => m_groundedState.x == 1;
    public int GroundState => m_groundedState.y;
    public int LeftWallState => m_groundedState.x;
    public int RightWallState => m_groundedState.z;
    
    public bool HitCeiling => m_groundedState.w > 0;
    
    private void Start()
    {
        Manager.AddCollider(this);
        prevBounds = m_bounds.Copy;
    }

    public BoundsCopy GetPrevBounds()
    {
        return prevBounds;
    }
    
    public Vector2 GetVelocity()
    {
        return m_rb.linearVelocity;
    }
    
    public void UpdateWithResults(Vector2 _newPos, Vector2 _velocity, int4 _slimeAround, int4 _bounceAround, int4 _blocksAround)
    {
        m_bounds.center = _newPos;
        m_rb.linearVelocity = _velocity;
        
        prevBounds = m_bounds.Copy;
        
        m_groundedState = 0;
        for (int i = 0; i < 4; ++i)
        {
            if (_bounceAround[i] > 0)
            {
                m_groundedState[i] = 3;
            }
            else if (_slimeAround[i] > 0)
            {
                m_groundedState[i] = 2;
            }
            else if (_blocksAround[i] > 0)
            {
                m_groundedState[i] = 1;
            }
        }
    }

    private void OnDestroy()
    {
        Manager.RemoveCollider(this);
    }
}
