using System;
using UnityEditor;
using UnityEngine;

public class MyCharacterController : MonoBehaviour
{
    
    [SerializeField] private ScriptableStats _stats;
    private Rigidbody2D _rb;
    private CustomCollider _customCol;
    private FrameInput _frameInput;
    private Vector2 _frameVelocity;
    private bool _cachedQueryStartInColliders;
    
    private SpriteRenderer m_sr;
    #region Interface

    public Vector2 FrameInput => _frameInput.Move;
    public event Action<bool, float> GroundedChanged;
    public event Action Jumped;
    public event Action<bool, int> WallSlideChanged;

    #endregion

    private float _time;
    
    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _customCol = GetComponent<CustomCollider>();
        m_sr = GetComponent<SpriteRenderer>();
        
        _cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
    }

    
    private void Update()
    {
        if (_touchingSlimeCeiling && _rb.linearVelocity.x != 0)
        {
            AudioManager.Play("slime", false);
            AudioManager.Stop("run");
        } 
        else if (_grounded && _rb.linearVelocity.x != 0)
        {
            AudioManager.Play("run", false);
            AudioManager.Stop("slime");
        }
        else
        {
            AudioManager.Stop("run");
            AudioManager.Stop("slime");
        }
        _time = Time.time;
        GatherInput();
    }
    
    private void GatherInput()
    {
        _frameInput = new FrameInput
        {
            JumpDown = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space),
            JumpHeld = Input.GetButton("Jump") || Input.GetKey(KeyCode.Space),
            Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))
        };

        if (_stats.SnapInput)
        {
            _frameInput.Move.x = Mathf.Abs(_frameInput.Move.x) < _stats.HorizontalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.x);
            _frameInput.Move.y = Mathf.Abs(_frameInput.Move.y) < _stats.VerticalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.y);
        }

        if (_frameInput.JumpDown)
        {
            _jumpToConsume = true;
            _timeJumpWasPressed = _time;
        }
    }

    void FixedUpdate()
    {
        CheckCollisions();
        
        HandleJump();
        //HandleWallJump();
        HandleDirection();
        HandleGravity();
        HandleWallClimbing();
            
        ApplyMovement();
    }
    
    #region Collisions
        
        private float _frameLeftGrounded = float.MinValue;
        private bool _grounded;
        private bool _touchingWall;


        private bool _touchingSlimeCeiling;
        private bool _touchingSlimeWall;

        private bool _touchingSlime;

        private void CheckCollisions()
        {
            Physics2D.queriesStartInColliders = false;

            // Ground and Ceiling
            //old
            // bool groundHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down, _stats.GrounderDistance, ~_stats.PlayerLayer);
            // bool ceilingHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.up, _stats.GrounderDistance, ~_stats.PlayerLayer);
            
            //new
            bool groundHit = _customCol.CanJump;
            
            
            
            // Hit a wall (Old)
            /*
            // if (Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.left, _stats.GrounderDistance, ~_stats.PlayerLayer)) {
            //     _wallJumpDirection = 1;
            // } else if (Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.right, _stats.GrounderDistance, ~_stats.PlayerLayer)) {
            //     _wallJumpDirection = -1;
            // } else {
            //     _wallJumpDirection = 0;
             }*/
            
            //Hit a Wall (new)
            if (_customCol.CanWallJumpRight)
            {
                _wallJumpDirection = 1;
                if (_customCol.RightWallState == 3)
                {
                    _isWallClimbing = true;
                }
            } 
            else if (_customCol.CanWallJumpLeft)
            {
                _wallJumpDirection = -1;
                if (_customCol.LeftWallState == 3)
                {
                    _isWallClimbing = true;
                }
            }
            else
            {
                _wallJumpDirection = 0;
            }

            bool wallHit = (_wallJumpDirection != 0);

            // Hit a Ceiling
            if (_customCol.HitCeiling)
            {
                if (_customCol.CeilingState == 3)
                {
                    m_sr.flipY = true;
                    _touchingSlimeCeiling = true;
                }
                _frameVelocity.y = 0;
                _endedJumpEarly = true;
            }
            
            //Left Slime Ceiling
            if (_customCol.CeilingState != 3 && _touchingSlimeCeiling)
            {
                _touchingSlimeCeiling = false;
                m_sr.flipY = false;
            }
            
            //Left Slime Wall
            if (_isWallClimbing && _customCol.LeftWallState != 3 && _customCol.RightWallState != 3)
            {
                _isWallClimbing = false;
            }
            
            //Touching slime (any side)
            _touchingSlime = _customCol.HitSlime;
            
            // Landed on bounce
            if (groundHit && !_grounded && _customCol.GroundState == 2)
            {
                _grounded = true;
                _coyoteUsable = true;
                _bufferedJumpUsable = true;
                _endedJumpEarly = false;
                GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));
                _hitBounce = true;
            }
            // Landed on the Ground
            else if (!_grounded && groundHit)
            {                
                _grounded = true;
                _coyoteUsable = true;
                _bufferedJumpUsable = true;
                _endedJumpEarly = false;
                GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));
            }
            // Left the Ground
            else if (_grounded && !groundHit)
            {
                _grounded = false;
                _frameLeftGrounded = _time;
                GroundedChanged?.Invoke(false, 0);
            }

            //Landed on Wall
            if (!_touchingWall && wallHit){
                _bufferedJumpUsable = true;
                _touchingWall = true;
                WallSlideChanged?.Invoke(true, _wallJumpDirection);
            } else if (_touchingWall && !wallHit) { // Left wall
                _touchingWall = false;
                _frameLeftGrounded = _time;
                WallSlideChanged?.Invoke(false, _wallJumpDirection);
            }

            // Reset wall jump if grounded 
            if (_grounded) {_wallJumpToConsume = true;}

            Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
        }

    #endregion
    
    
    
    
    #region Jumping

        private bool _jumpToConsume;
        private bool _bufferedJumpUsable;
        private bool _endedJumpEarly;
        private bool _coyoteUsable;
        private float _timeJumpWasPressed;

        //walljump stuff
        private bool _wallJumpToConsume;
        private int _wallJumpDirection;
        private bool CanWallJump => _touchingWall && !_grounded;


        private bool HasBufferedJump => _bufferedJumpUsable && _time < _timeJumpWasPressed + _stats.JumpBuffer;
        private bool CanUseCoyote => _coyoteUsable && !_grounded && _time < _frameLeftGrounded + _stats.CoyoteTime;

        private void HandleJump()
        {

            if (!_endedJumpEarly && !_grounded && !_frameInput.JumpHeld && _rb.linearVelocity.y > 0) _endedJumpEarly = true;

            if (!_jumpToConsume && !HasBufferedJump) return;

            if (_touchingSlimeCeiling)
            {
                _frameVelocity.y = -2f;
                _touchingSlimeCeiling = false;
                m_sr.flipY = false;
                return;
            }

            if (_isWallClimbing)
            {
                _frameVelocity.x = _wallJumpDirection * 2f;
                _isWallClimbing = false;
                return;
            }
            

            if (_grounded || CanUseCoyote) ExecuteJump();

            _jumpToConsume = false;
        }
        
        private void HandleWallJump(){
            if (!_jumpToConsume && !HasBufferedJump) return;

            if (CanWallJump && _wallJumpToConsume){
                ExecuteWallJump();
                _wallJumpToConsume = false;
            }

            _jumpToConsume = false;
            
        }


        private void ExecuteWallJump(){
            _endedJumpEarly = false;
            _timeJumpWasPressed = 0;
            _frameVelocity.x = _stats.JumpAwayFromWallSpeed * _wallJumpDirection;    
            _frameVelocity.y = _stats.JumpPower;
            Jumped?.Invoke();
        }

        private void ExecuteJump()
        {
            
            
            AudioManager.Play("jump");
            _endedJumpEarly = false;
            _timeJumpWasPressed = 0;
            _bufferedJumpUsable = false;
            _coyoteUsable = false;
            
            //prevent you from jumping
            if (!_hitBounce)
            {
                 _frameVelocity.y = _stats.JumpPower / (_touchingSlime ? _stats.SlimeJumpModifier : 1);
            }
            //_frameVelocity.y = _stats.JumpPower;
            Jumped?.Invoke();
        }

    #endregion
           
    #region Horizontal

        private void HandleDirection()
        {
            if (_isWallClimbing && !_touchingSlimeCeiling && !_grounded) return;
            
            if (_frameInput.Move.x == 0)
            {
                var deceleration = _grounded ? _stats.GroundDeceleration : _stats.AirDeceleration;
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
            }
            else
            {
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, _frameInput.Move.x * _stats.MaxSpeed / (_touchingSlime ? _stats.SlimeDecelerationModifier : 1), _stats.Acceleration * Time.fixedDeltaTime);
            }
        }

    #endregion

    #region Gravity

    private bool _hitBounce;
    
        private void HandleGravity()
        {
            if (_isWallClimbing) return;
            if (_touchingSlimeCeiling)
            {
                _frameVelocity.y = 0;
                return;
            }
            
            
            if (_hitBounce)
            {
            AudioManager.Play("jump");
                _frameVelocity.y = Mathf.Abs(_frameVelocity.y) < 0.3f ? 0f : -1.6f*(_frameVelocity.y);
                _hitBounce = false;
                _endedJumpEarly = true;
                return;
            }
            
            if (_grounded && _frameVelocity.y <= 0f)
            {
                _frameVelocity.y = _stats.GroundingForce;
            }
            else
            {
                var inAirGravity = _stats.FallAcceleration;
                if (_endedJumpEarly && _frameVelocity.y > 0) inAirGravity *= _stats.JumpEndEarlyGravityModifier;
                _frameVelocity.y = Mathf.MoveTowards(_frameVelocity.y, -_stats.MaxFallSpeed, inAirGravity * Time.fixedDeltaTime);
            }
        }

    #endregion
    
    #region WallClimbing

    private bool _isWallClimbing;

    private void HandleWallClimbing()
    {
        if (_isWallClimbing)
        {
            _frameVelocity.y = Mathf.MoveTowards(_frameVelocity.y, _frameInput.Move.y * _stats.MaxSpeed / (_touchingSlime ? _stats.SlimeDecelerationModifier : 1), _stats.Acceleration * Time.fixedDeltaTime);   
        }
    }
    
    
    #endregion
    
    

    private void ApplyMovement()
    {
        _rb.linearVelocity = _frameVelocity;
    }
}

public struct FrameInput
{
    public bool JumpDown;
    public bool JumpHeld;
    public Vector2 Move;
}

public interface IPlayerController
{
    public event Action<bool, float> GroundedChanged;

    public event Action Jumped;

    public event Action<bool, int> WallSlideChanged;
    public Vector2 FrameInput { get; }
}
