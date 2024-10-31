using System;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class Character : MonoBehaviour
{
    private const float c_CameraTightness = 0.1f;
    private const float c_PlayerSpeed = 2000f;
    private const float c_JumpImpulse = 2000f;
    private const float c_GroundNormalThreshold = 0.8f;
    
    public Fluid m_fluidPrefab;
    
    private Vector3 m_smoothedPos = Vector3.zero;
    private Camera m_mainCam;

    [SerializeField]
    private Rigidbody2D m_rb;

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
        var force = Vector3.zero;
        if ((m_groundedState & 1) == 1)
        {
            if (Input.GetKey(KeyCode.A))
                force += Vector3.left * (c_PlayerSpeed * Time.deltaTime);
            if (Input.GetKey(KeyCode.D))
                force += Vector3.right * (c_PlayerSpeed * Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.Space))
            {
                force += Vector3.up * c_JumpImpulse;
                m_groundedState &= ~1;
            }
        }
        else if ((m_groundedState & 6) > 0)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var side = (m_groundedState & 2) == 2 ? Vector3.right : Vector3.left;
                force += (Vector3.up + side).normalized * c_JumpImpulse;
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

    private void FixedUpdate()
    {
        m_groundedState = 0;
        
        m_smoothedPos = Vector3.Lerp(m_smoothedPos, transform.position, c_CameraTightness);
        m_smoothedPos.z = m_mainCam.transform.position.z;
        m_mainCam.transform.position = m_smoothedPos;
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
