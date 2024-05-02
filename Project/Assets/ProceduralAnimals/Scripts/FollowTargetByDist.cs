using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A component used to automatically follow a target based on the initial distance from the
/// target, while approximately recreating the path the target has traveled.
/// </summary>
public class FollowTargetByDist : MonoBehaviour
{
    class HistoryPoint
    {
        public Vector3 pos;
        public Quaternion rot;
        public float time, deltaTime;
        public float deltaDist;
    }

    [Serializable]
    class Target
    {
        public Transform transform;
        public Vector3 position { get { return transform.position; } }
        public Quaternion rotation { get { return transform.rotation; } }
        [HideInInspector] public float distanceOffset;
        [HideInInspector] public List<HistoryPoint> history = new();
    }

    [SerializeField] bool pathGizmo = true;
    [SerializeField] Target target = null;

    [Header("Heirarchy Settings")]
    [SerializeField] bool keepRelativeRotation = true;
    Quaternion relativeRotation = Quaternion.identity;

    [SerializeField] bool unsetParent = true;

    [Header("Rotation Limiting")]
    [SerializeField] bool limitRotationRelativeToTarget = false;
    [SerializeField] float maxRotationRelativeToTarget = 60;

    /// <summary>
    /// Checks if a target object exists and is a valid target. 
    /// </summary>
    /// <returns></returns>
    bool TargetIsValid()
    {
        if (target == null)
            return false;
        else if (target.transform == null)
            return false;
        return true;
    }

    void Start()
    {
        if (!TargetIsValid())
            return;

        void CalculateTargetOffset()
        {
            target.distanceOffset = Vector3.Distance(transform.position, target.position);
        }

        // Get the rotation relative to the target.
        // Source: https://discussions.unity.com/t/how-to-assign-a-rotation-relative-to-another-
        // object/221770/2
        void CalculateRelativeRotation()
        {
            if (!keepRelativeRotation)
                return;
            relativeRotation = Quaternion.Inverse(target.transform.rotation) * transform.rotation;
        }

        void UnparentObject()
        {
            if (!unsetParent)
                return;
            transform.parent = null;
        }

        CalculateTargetOffset();
        UnparentObject();
    }

    void LimitRotationFromTarget()
    {
        if (!limitRotationRelativeToTarget)
            return;

        // Get the angle difference between the target's heading and the current transform heading.
        // If the current angle is within the acceptable range, we don't need to do anything,
        // var currAngle = Vector3.Angle(transform.forward, target.transform.forward);
        // if (currAngle <= maxRotationRelativeToTarget)
        //     return;

        // if (transform.name.Contains("spine2"))
        //     Debug.Log("Rotation Limit: " + transform.name + " offset angle from target = " + currAngle);

        // // 1. Calculate the rotation that would fit within the max rotation range.
        // // 2. Get the forward direction from the rotation.
        // // 3. Starting at the origin point (target's position), move backwards away from it
        // //    using the new forward to the desired new position of the object.
        // var slerpAmount = maxRotationRelativeToTarget / currAngle;
        // var newRot = Quaternion.Slerp(target.rotation, transform.rotation, slerpAmount);
        // var newFwd = newRot * Vector3.forward;
        // var newPos = target.position - newFwd * target.distanceOffset;

        // transform.position = newPos;
        // transform.rotation = newRot;
        transform.LookAt(target.position, target.history[^1].rot * Vector3.up);
    }

    void Update()
    {
        if (!TargetIsValid())
            return;
    }

    void LateUpdate()
    {
        if (!TargetIsValid())
            return;

        void UpdateHistory()
        {
            var point = new HistoryPoint
            {
                pos = target.position,
                time = Time.time,
                rot = target.transform.rotation
            };

            if (target.history.Count > 0)
            {
                point.deltaDist = Vector3.Distance(target.history[^1].pos, point.pos);
                point.deltaTime = point.time - target.history[^1].time;

                // Special case: The target hasn't moved/rotated this frame, so there is no
                // difference  in position/rotation (or the difference is too small to notice) and
                // the list should not be updated.
                if (target.history[^1].pos == target.position && target.history[^1].rot == target.transform.rotation)
                {
                    target.history[^1].time = Time.time;
                    target.history[^1].deltaTime += Time.deltaTime;
                    return;
                }
            }

            // We know our target has moved and/or rotated.
            target.history.Add(point);
        }

        int MoveTowardsTarget()
        {
            var remDist = target.distanceOffset;
            for (var i = target.history.Count - 1; i > 0; i--)
            {
                if (remDist <= target.history[i].deltaDist)
                {
                    var t = 0f;
                    if (target.history[i].deltaDist > 0f)
                        t = remDist / target.history[i].deltaDist;
                    // Update the position.
                    transform.position = Vector3.Lerp(target.history[i].pos, target.history[i - 1].pos, t);
                    // Update the rotation.
                    transform.rotation = Quaternion.Slerp(target.history[i].rot, target.history[i - 1].rot, t);
                    return i - 1;
                }
                else
                    remDist -= target.history[i].deltaDist;
            }
            return target.history.Count;
        }

        void RemoveAllOldPoints(int firstValidIndex)
        {
            if (firstValidIndex < 0 || firstValidIndex >= target.history.Count - 1)
                return;
            // Remove all values before the first valid index.
            target.history.RemoveRange(0, firstValidIndex);

            // var removedCount = 0;
            // while (target.history.Count > 1 && removedCount < firstValidIndex - 1)
            // {
            //     target.history.RemoveAt(0);
            //     removedCount += 1;
            // }
        }
        UpdateHistory();
        var firstValidIndex = MoveTowardsTarget();
        RemoveAllOldPoints(firstValidIndex);
        LimitRotationFromTarget();
    }

    void OnDrawGizmos()
    {
        if (!pathGizmo || !TargetIsValid())
            return;

        void DrawHistory(float stepFactor = 50)
        {
            // Draw the path history.
            var step = (int)(target.history.Count / stepFactor) + 1;
            Gizmos.color = Color.gray;
            for (var i = 0; i < target.history.Count - step; i += step)
            {
                Gizmos.DrawLine(target.history[i].pos, target.history[i + step].pos);
            }
        }

        void DrawPosAndRotation()
        {
            // Draw the object's position and rotaton.
            Gizmos.color = Color.gray;
            DrawArrow.ForGizmo(transform.position, transform.forward);
            DrawArrow.ForGizmo(transform.position, transform.up);
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }

        DrawHistory();
        DrawPosAndRotation();
    }
}