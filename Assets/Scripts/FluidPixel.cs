using System;
using UnityEngine;

public class FluidPixel : MonoBehaviour
{
    private const float c_Lifetime = 10f;
    private const int c_Pixels = 4;
    
    public Rigidbody2D m_rb;
    [SerializeField] private Collider2D m_coll;

    public Fluid Fluid { get; set; }

    private float m_expirationTime;
    void Start()
    {
        var pixelSize = 2 * c_Pixels * Camera.main.orthographicSize / Screen.height;
        transform.localScale = new Vector3(pixelSize, pixelSize, 1);
        
        m_expirationTime = Time.time + c_Lifetime;
    }

    private void Update()
    {
        if (Time.time > m_expirationTime)
        {
            if (m_coll.enabled)
                Fluid.Remove(this);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
    
    private void OnCollisionEnter2D(Collision2D _c)
    {
        m_rb.linearVelocity = Vector2.zero;
        m_rb.constraints = RigidbodyConstraints2D.FreezeAll;
        m_coll.enabled = false;
        Fluid.Remove(this);
    }
}
