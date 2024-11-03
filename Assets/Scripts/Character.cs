using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Character : MonoBehaviour
{
    private const float c_CameraTightness = 1f;
    private const float c_PlayerSpeed = 500f;
    private const float c_JumpImpulse = 1000f;
    
    public Potion m_PotionPrefab;
    
    private Vector3 m_smoothedPos = Vector3.zero;
    private Camera m_mainCam;

    [SerializeField]
    private Rigidbody2D m_rb;
    [SerializeField]
    private Collider2D m_coll;
    [SerializeField]
    private CustomCollider m_customColl;
    
    private void Start()
    {
        transform.position = GridHandler.Me.GetSpawnPoint();
        m_smoothedPos = transform.position;
        
        m_mainCam = Camera.main;
    }

    private void Update()
    {
        //UpdateMovement();
        UpdateGun();
    }

    private void UpdateMovement()
    {
        var force = Vector2.zero;
        if (m_customColl.OnGround)
        {
            if (Input.GetKey(KeyCode.A))
                force += Vector2.left * (c_PlayerSpeed * Time.deltaTime);
            if (Input.GetKey(KeyCode.D))
                force += Vector2.right * (c_PlayerSpeed * Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.Space))
            {
                force += Vector2.up * c_JumpImpulse;
            }
        }
        else if (m_customColl.OnWall)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var side = m_customColl.OnRightWall ? Vector2.right : Vector2.left;
                force += (Vector2.up + side).normalized * c_JumpImpulse;
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
        var startPos = (Vector2)m_coll.bounds.center + Vector2.up * 0.5f;
        var force = Utility.GetForceForPosition(startPos, _target, 10f);
        var potion = Instantiate(m_PotionPrefab, startPos, Quaternion.identity);
        potion.Init(force, 20, GridHandler.Cell.Type.Acid);
    }

    private Vector3 m_lastPos;

    private void FixedUpdate()
    {
        m_smoothedPos = Vector3.Lerp(m_smoothedPos, transform.position, c_CameraTightness);
        m_smoothedPos.z = m_mainCam.transform.position.z;
        m_mainCam.transform.position = m_smoothedPos;
    }
}
