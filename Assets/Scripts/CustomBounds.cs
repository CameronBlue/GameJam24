using Unity.Mathematics;
using UnityEngine;
public interface IBounds
{
    Vector2 min { get; set; }
    Vector2 max { get; set; }
    Vector2 extents { get; set; }
}

public class CustomBounds : MonoBehaviour, IBounds
{
    public Vector2 localCenter;
    public Vector2 size;

    public Vector2 extents
    {
        get => 0.5f * size;
        set => size = 2f * value;
    }
    
    public Vector2 center
    {
        get => (Vector2)transform.position + localCenter;
        set => transform.position = value - localCenter;
    }

    public Vector2 min
    {
        get => center - extents;
        set => center = value + extents;
    }

    public Vector2 max
    {
        get => center + extents;
        set => center = value - extents;
    }
    
    public void SetRight(float _x) => max = new Vector2(_x, max.y);
    public void SetBottom(float _y) => min = new Vector2(min.x, _y);
    public void SetLeft(float _x) => min = new Vector2(_x, min.y);
    public void SetTop(float _y) => max = new Vector2(max.x, _y);
    
    public int4 AsInt4 => new((int)min.x, (int)min.y, (int)max.x, (int)max.y);
    
    public BoundsCopy Copy => new BoundsCopy{ center = center, size = size };
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube((Vector2)transform.position + localCenter, size);
    }
}

public class BoundsCopy : IBounds
{
    public Vector2 center;
    public Vector2 size;

    public Vector2 extents
    {
        get => 0.5f * size;
        set => size = 2f * value;
    }

    public Vector2 min
    {
        get => center - extents;
        set => center = value + extents;
    }

    public Vector2 max
    {
        get => center + extents;
        set => center = value - extents;
    }
}