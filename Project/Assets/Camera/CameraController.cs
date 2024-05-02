/// CameraController.cs
/// Author: Rohith Vishwajith
/// Created 5/2/2024

using UnityEngine;
using DG.Tweening;

public class CameraController : MonoBehaviour
{
    public Transform[] targets = new Transform[0];
    int targetIndex = 0;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
            targetIndex++;
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
            targetIndex--;
        targetIndex = targetIndex >= targets.Length ? 0 : targetIndex;
        targetIndex = targetIndex < 0 ? targets.Length - 1 : targetIndex;
    }

    void LateUpdate()
    {
        transform.position = Vector3.Lerp(transform.position, targets[targetIndex].position, Time.deltaTime * 2.5f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targets[targetIndex].rotation, Time.deltaTime * 2.5f);
    }
}