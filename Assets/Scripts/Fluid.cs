using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Fluid : MonoBehaviour
{
    private const float c_attractiveForce = 0.01f;
    private const float c_repulsiveForce = 0.000001f;
    
    public FluidPixel m_fluidPixelPrefab;
    
    public List<FluidPixel> m_fluidPixels = new();

    public void Init(Vector2 _force, int _pixelCount)
    {
        for (int i = 0; i < _pixelCount; ++i)
        {
            _force = _force.Rotate(Random.Range(-2f, 2f));
            var fluidPixel = Instantiate(m_fluidPixelPrefab, transform.position + (_force.normalized * 2).AddZ(), Quaternion.identity);
            fluidPixel.m_rb.AddForce(_force);
            
            m_fluidPixels.Add(fluidPixel);
            fluidPixel.Fluid = this;
        }
    }

    public void Remove(FluidPixel _pixel)
    {
        m_fluidPixels.Remove(_pixel);
    }

    private void FixedUpdate()
    {
        Vector3 centerOfMass = Vector3.zero;
        int count = m_fluidPixels.Count;

        foreach (var pixel in m_fluidPixels)
            centerOfMass += pixel.transform.position;
        centerOfMass /= count;

        foreach (var pixel in m_fluidPixels)
        {
            var force = Attraction(pixel.transform.position, centerOfMass);
            foreach (var other in m_fluidPixels)
            {
                if (other == pixel)
                    continue;
                force += Repulsion(pixel.transform.position, other.transform.position);
            }
            pixel.m_rb.AddForce(force);
        }
    }

    private Vector2 Attraction(Vector2 _pixelPos, Vector2 _centrePos)
    {
        var toCenter = _centrePos - _pixelPos;
        var sqrDist = toCenter.sqrMagnitude;
        if (sqrDist < 0.01f * 0.01f)
            sqrDist = 0.01f * 0.01f;
        return toCenter * c_attractiveForce / sqrDist;
    }
    
    private Vector2 Repulsion(Vector2 _pixelPos, Vector2 _otherPos)
    {
        var sqrDist = (_otherPos - _pixelPos).sqrMagnitude;
        if (sqrDist > 0.1f * 0.1f)
            return Vector2.zero;
        if (sqrDist < 0.01f * 0.01f)
            sqrDist = 0.01f * 0.01f;
        return (_pixelPos - _otherPos) * c_repulsiveForce / sqrDist;
    }
}
