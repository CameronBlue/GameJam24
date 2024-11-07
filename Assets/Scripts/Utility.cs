using UnityEngine;

public static class Utility
{
    public static Vector2 GetForceForPosition(Vector2 _pos, Vector2 _target, float _speed, float _gravityFactor = 1f)
    {
        Vector2 displacement = _target - _pos;
        displacement.y += Mathf.Abs(displacement.x) * 0.5f;
        float dx = displacement.x;
        float dy = displacement.y;
        float a = Physics2D.gravity.y * _gravityFactor;
        float dx2 = dx * dx;
        float thing = a * dx2 / (_speed * _speed);
        var discriminant = dx2 - thing * (thing + 2 * dy);
        if (discriminant < 0)
            return Vector2.zero;
        float tanTheta = (Mathf.Abs(dx) - Mathf.Sqrt(discriminant)) / thing;
        float angle = (dx < 0) ? Mathf.PI - Mathf.Atan(tanTheta) : Mathf.Atan(tanTheta);
        return _speed * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    public static Vector2 Rotate(this Vector2 _v2, float _angle)
    {
        var angle = Mathf.Deg2Rad * _angle;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);

        return new Vector2(_v2.x * cos - _v2.y * sin, _v2.x * sin + _v2.y * cos);
    }

    public static Vector3 AddZ(this Vector2 _v2, float _z = 0f)
    {
        return new(_v2.x, _v2.y, _z);
    }
    
    public static Vector2 RandomInsideBounds(CustomBounds _bounds, float _deadZone = 0f)
    {
        return new Vector2(Random.Range(_bounds.min.x + _deadZone, _bounds.max.x - _deadZone), Random.Range(_bounds.min.y + _deadZone, _bounds.max.y - _deadZone));
    }
}
