using UnityEngine;

public static class Vector3Extension
{
    public static float Dist(this Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude;
    }
}