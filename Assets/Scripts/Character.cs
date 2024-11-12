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
    
    public GridHandler.Cell.Type m_potionType;
    
    [SerializeField] private GameObject m_potionCombiner;
    
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

        if (!m_potionCombiner.activeSelf)
        {
            UpdateGun();
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            var currentlyActive = m_potionCombiner.activeSelf;
            m_potionCombiner.SetActive(!currentlyActive);
            Time.timeScale = currentlyActive ? 1f : 0f;
        }
    }
    
    private void UpdateAnimator()
    {
        var xVel = m_rb.linearVelocity.x;
        m_sr.flipX = xVel < 0.1f;
        var speed = Mathf.Abs(xVel) * 0.2f;
        m_anim.SetFloat("Speed", speed);
        m_anim.SetBool("Moving", speed > 0.1f);
    }

    private void UpdateGun()
    {
        if (m_potionType == GridHandler.Cell.Type.Null)
            return;
        
        var target = m_mainCam.ScreenToWorldPoint(Input.mousePosition);
        target.z = 0;
        if (Input.GetMouseButtonDown(0))
            Shoot(target);
    }

    private void Shoot(Vector2 _target)
    {
        var startPos = (Vector2)transform.position;
        var force = (_target - startPos).normalized * 10f + m_rb.linearVelocity;
        var potion = Instantiate(m_PotionPrefab, startPos, Quaternion.identity);
        potion.Init(force, 250, m_potionType);
        
        PlayerInventory.Me.RemovePotion(m_potionType);
    }
    
    public void FixedUpdateMe()
    {
        m_smoothedPos = Vector3.Lerp(m_smoothedPos, transform.position, c_CameraTightness);
        m_smoothedPos.z = m_mainCam.transform.position.z;
        m_mainCam.transform.position = m_smoothedPos;
    }
}
