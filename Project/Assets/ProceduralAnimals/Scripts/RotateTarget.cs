using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateTarget : MonoBehaviour
{
    [SerializeField] Transform pivot;
    [SerializeField] float rotationSpeed = 180;
    [SerializeField] Vector3 rotationAxis = Vector3.up;

    float rotation = 0;

    void Update()
    {
        if (pivot == null)
            return;
        rotation = rotationSpeed * Time.deltaTime * Random.Range(0.8f, 1.2f);
        pivot.localRotation *= Quaternion.Euler(rotationAxis * rotation);
    }
}
