using UnityEngine;

public static class GizmosExtras
{
    public static void DrawQuaternion(Vector3 point, Quaternion quat, Vector3 axesSizes)
    {
        Gizmos.DrawLine(point, point + quat * Vector3.right * axesSizes.x);
        Gizmos.DrawLine(point, point + quat * Vector3.up * axesSizes.y);
        Gizmos.DrawLine(point, point + quat * Vector3.forward * axesSizes.z);
    }

    public static void DrawRotation(Transform transform, Vector3 axesSizes)
    {
        GizmosExtras.DrawQuaternion(transform.position, transform.rotation, axesSizes);
    }
}