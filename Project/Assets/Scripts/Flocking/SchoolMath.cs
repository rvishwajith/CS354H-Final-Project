/// SchoolMath.cs
/// Author: Rohith Vishwajith
/// Created 4/21/2024

using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// A helper class for school simulation calculations, such as:
/// - Computing acceleration / velocity values.
/// - Computing and caching avoidance rays.
/// - Getting constants using Unity.Mathematics (SIMD) types.
/// </summary>
public static class SchoolMath
{
    // GPU RENDERING / INSTANCING ----------------------------------------------------------------

    /// <summary>
    /// The maximum instance count for drawing instanced meshes using DrawMeshIndirect.
    /// </summary>
    public static readonly int MAX_INSTANCE_COUNT = 1023;

    /// <summary>
    /// The equivalent to Vector3.up but as a float3.
    /// </summary>
    public static readonly float3 WORLD_UP = new(0, 1, 0);

    /// <summary>
    /// REMOVEME. Precomputed world-space turn directions for obstacle avoidance, using an array
    /// of Vector3 instead of float3.
    /// </summary>
    public static Vector3[] TURN_DIRS_V3 = ComputeTurnRays(100);

    /// <summary>
    /// Precomputed world-space turn directions for obstacle avoidance, with LOW precision.
    /// Note: LOW precision = 50 samples.
    /// </summary>
    public static float3[] TURN_DIRS_LOW = ComputeTurnRaysF3(50);

    /// <summary>
    /// World-space turn directions for obstacle avoidance, with MEDIUM precision (100 samples).
    /// </summary>
    public static float3[] TURN_DIRS_MED = ComputeTurnRaysF3(100);

    /// <summary>
    /// World-space turn directions for obstacle avoidance, with HIGH precision (300 samples).
    /// </summary>
    public static float3[] TURN_DIRS_HIGH = ComputeTurnRaysF3(300);

    public static Vector3[] ComputeTurnRays(int samples)
    {
        var goldenRatio = (1 + Mathf.Sqrt(5)) / 2;
        var angleIncr = Mathf.PI * 2 * goldenRatio;
        var dirs = new Vector3[samples];
        for (int i = 0; i < samples; i++)
        {
            var t = (float)i / samples;
            var incl = Mathf.Acos(1 - 2 * t);
            var azimuth = angleIncr * i;
            dirs[i] = new(Mathf.Sin(incl) * Mathf.Cos(azimuth),
                Mathf.Sin(incl) * Mathf.Sin(azimuth),
                Mathf.Cos(incl));
        }
        return dirs;
    }

    public static float3[] ComputeTurnRaysF3(int samples = 100)
    {
        var goldenRatio = (1 + Mathf.Sqrt(5)) / 2;
        var angleIncr = Mathf.PI * 2 * goldenRatio;
        var dirs = new float3[samples];
        for (int i = 0; i < samples; i++)
        {
            var t = (float)i / samples;
            var incl = Mathf.Acos(1 - 2 * t);
            var azimuth = angleIncr * i;
            dirs[i] = new(Mathf.Sin(incl) * Mathf.Cos(azimuth),
                Mathf.Sin(incl) * Mathf.Sin(azimuth),
                Mathf.Cos(incl));
        }
        return dirs;
    }

    public static float3 SteerTowards(float3 direction, float3 targetDirection, float steerForce, float speed)
    {
        // if (math.length(vector) == 0 && math.length(velocity) == 0)
        //     return new(0, 0, 1);
        if (math.length(targetDirection) == 0)
            return direction;
        // else if (math.length(velocity) == 0)
        //     return vector;
        var dir = (speed * math.normalize(targetDirection)) - direction;
        if (math.length(dir) == 0)
            return targetDirection;
        // FIXME: Change this to a clamp of 0.0001 and remove the length check above.
        return math.normalize(dir) * math.clamp(math.length(dir), 0, steerForce);
    }

    public static NativeArray<float> ToNativeArray(float f, int size, Allocator allocator)
    {
        var arr = new float[size];
        for (int i = 0; i < size; i++)
            arr[i] = f;
        return new NativeArray<float>(arr, allocator);
    }
}