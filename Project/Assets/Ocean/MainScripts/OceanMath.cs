/// OceanMath.cs
/// Author: Rohith Vishwajith
/// Created 4/30/2024

using Unity.Burst;
using Unity.Mathematics;

public static class OceanMath
{
    [BurstCompile]
    public static float ComputeCascadeBoundary(float lengthScale)
    {
        return 6f * 2f * math.PI / lengthScale;
    }
}