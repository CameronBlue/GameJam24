using UnityEngine;

public static class Utility
{
    public static Vector2 GetForceForPosition(Vector2 _pos, Vector2 _target, float _speed, float _gravityFactor = 1f)
    {
        Vector2 displacement = _target - _pos;
        float dx = displacement.x;
        float dy = displacement.y;

        float g = Physics2D.gravity.y * _gravityFactor;
        float sqrSpeed = _speed * _speed;

        float d = (sqrSpeed * sqrSpeed) - (g * (g * dx * dx + 2 * dy * sqrSpeed));
        if (d < 0)
            return Vector2.zero;

        float tanTheta = (sqrSpeed - Mathf.Sqrt(d)) / (g * dx);
        float angle = dx < 0 ? Mathf.PI + Mathf.Atan(tanTheta) : Mathf.Atan(tanTheta);

        return new Vector2(_speed * Mathf.Cos(angle), _speed * Mathf.Sin(angle));
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
}
