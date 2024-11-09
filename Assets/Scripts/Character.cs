using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Character : MonoBehaviour
{
    private const float c_CameraTightness = 0.2f;

    public static Character Me;
    
    public Potion m_PotionPrefab;
    
    private Vector3 m_smoothedPos = Vector3.zero;
    private Camera m_mainCam;

    [SerializeField]
    private SpriteRenderer m_sr;
    [SerializeField] 
    private Animator m_anim;
    [SerializeField]
    private Rigidbody2D m_rb;
    [SerializeField]
    private CustomCollider m_customColl;

    public int m_potionNum;
    
    private void Awake()
    {
        Me = this;
    }

    private void Start()
    {
        transform.position = GridHandler.Me.GetSpawnPoint();
        m_smoothedPos = transform.position;
        
        m_mainCam = Camera.main;
    }

    private void Update()
    {
        UpdateAnimator();
        UpdateGun();
        
        for (int i = 0; i <= 9; ++i)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                m_potionNum = i + 1;
                break;
            }
        }
        
    }
    
    private void UpdateAnimator()
    {
        var xVel = m_rb.linearVelocity.x;
        m_sr.flipX = xVel < 0;
        var speed = Mathf.Abs(xVel);
        m_anim.SetFloat("Speed", speed);
        m_anim.SetBool("Moving", speed > 0.1f);
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
        var startPos = (Vector2)transform.position + Vector2.up * 0.5f;
        var force = Utility.GetForceForPosition(startPos, _target, 10f);
        var potion = Instantiate(m_PotionPrefab, startPos, Quaternion.identity);
        potion.Init(force, 250, (GridHandler.Cell.Type)m_potionNum);
    }
    
    private void FixedUpdate()
    {
        m_smoothedPos = Vector3.Lerp(m_smoothedPos, transform.position, c_CameraTightness);
        m_smoothedPos.z = m_mainCam.transform.position.z;
        m_mainCam.transform.position = m_smoothedPos;
    }
}
