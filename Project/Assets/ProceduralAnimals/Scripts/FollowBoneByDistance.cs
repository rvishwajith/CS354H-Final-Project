using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

struct TargetHistoryPoint
{
    // The position of the target at this point in time.
    public Vector3 position;
    // How much the target has moved since the last point.
    public float deltaDistance;
    // Rotation of this target at this point in time.
    public Quaternion rotation;
    // The frame-time this element was added to the list.
    public float time;
    // How much time has passed between this point in history and the previous point.
    public float deltaTime;
}

public class FollowBoneByDistance : MonoBehaviour
{
    // OBJECTS -------------------------------------------------------------------------------------
    [Header("Objects")]
    [SerializeField] Transform target = null;
    [SerializeField] Transform follower = null;

    [Header("Angle Differential")]
    [SerializeField] bool limitAngleDifferential = false;
    [SerializeField] float maxAngleDifferential = 60f;

    // GIZMOS --------------------------------------------------------------------------------------
    [Header("Gizmos")]
    [SerializeField] GizmoMode orientationHistoryGizmo = GizmoMode.Never;
    [SerializeField] GizmoMode positionHistoryGizmo = GizmoMode.Never;
    // INTERNAL ------------------------------------------------------------------------------------

    /// <summary>
    /// The target's history data, which updated at each necessary frame-time. Each element contains
    /// orientation, rotation, and time data.
    /// </summary>
    List<TargetHistoryPoint> history = new();
    float followDistance = -1;
    bool ValidTarget { get { return target != null; } }

    void Start()
    {
        // Exit if there's no target available.
        if (!ValidTarget)
            return;
        else if (!follower)
            follower = this.transform;

        void SetTargetDistance()
        {
            followDistance = Vector3.Distance(target.position, follower.position);
            // Debug.Log("Follower " + follower.name + ": Initial target distance = " + followDistance);
        }

        follower.SetParent(null);
        SetTargetDistance();
        UpdateHistory();
    }

    void LateUpdate()
    {
        // Exit if:
        // 1. There's no target available.
        // 2. The target's history hasn't been updated iwth new position/rotation data.
        if (!ValidTarget || !UpdateHistory())
            return;

        // The history was updated, so try to update the follower as well.
        // 1. If the follower couldn't be updated, post a warning. This shouldn't happen!
        // 2. If the follower was updated, remove all the points on the path that it already passed
        //    which is history[:removeBeforeIndex], assuming the list is big enough.
        var removeBeforeIndex = UpdateFollowerInitial();
        if (removeBeforeIndex != -1 && history.Count > removeBeforeIndex + 2)
        {
            history.RemoveRange(0, removeBeforeIndex);
            // Debug.Log("Warning: Couldn't update follower " + follower.name + " with target " + target.name);
        }
        follower.position = target.position + (follower.position - target.position).normalized * followDistance;
        // if (follower.name == "Bone_1")
        // {
        //     Debug.Log(AngleBetweenTargetAndFollower());
        //     Debug.Log("History size: " + history.Count + ", distance from target: " + Vector3.Distance(target.position, follower.position));
        // }
    }

    /// <summary>
    /// Try to update the history with the current target's data. The history will not be updated
    /// if the target's position and orientation are BOTH unchanged since the last time the history
    /// was updated.
    /// Note: This function assumes that there is a valid target.
    /// </summary>
    /// <returns>Whether the history was updated with a new element. FALSE means the target's
    /// position and rotation are unchanged.</returns>
    bool UpdateHistory()
    {
        // The history list is empty, so the target's current data cannot be compared to anything.
        // Add it no matter what.
        if (history.Count == 0)
        {
            history.Add(new()
            {
                position = target.position,
                deltaDistance = 0,
                rotation = target.rotation,
                time = Time.time,
                deltaTime = 0,
            });
            return true;
        }

        // There is data in the history list, check if the target's transform data has changed.
        var targetMoved = target.position != history[^1].position;
        var targetRotated = target.rotation != history[^1].rotation;

        // If the target has not moved or rotated at all, update the last value of the list's time
        // data only.
        if (!targetMoved)
        {
            history[^1] = new()
            {
                position = history[^1].position,
                deltaDistance = history[^1].deltaDistance,
                rotation = history[^1].rotation,
                time = Time.time,
                deltaTime = history[^1].deltaTime + Time.deltaTime
            };
            return false;
        }

        history.Add(new()
        {
            position = target.position,
            deltaDistance = Vector3.Distance(target.position, history[^1].position),
            rotation = target.rotation,
            time = Time.time,
            deltaTime = Time.time - history[^1].time
        });
        return true;
    }

    /// <summary>
    /// Updates the follower's position and rotation based on the target history data.
    /// </summary>
    /// <returns>
    /// Returns i if the follower moved, where history[i::] should be kept, or -1 if the follower 
    /// did not move.
    /// </returns>
    int UpdateFollowerInitial()
    {
        var distLeft = followDistance;
        // Backtrace through the list, starting at the NEWEST (last) positions.
        for (var i = history.Count - 1; i > 0; i--)
        {
            // Check if the remaining distance is shorter than the length of a line between the
            // current and previous history points.
            var segmentDist = history[i].deltaDistance;
            // If the distance does fit within this line, the position is somewhere between i and
            // i-1, so lerp between them.
            if (distLeft <= segmentDist)
            {
                if (Vector3.Distance(history[i].position, target.position) <= followDistance * 0.95f && Vector3.Distance(history[i - 1].position, target.position) <= followDistance * 0.95f)
                    continue;
                var lerpT = Mathf.InverseLerp(0, segmentDist, distLeft);
                follower.position = Vector3.Lerp(
                    history[i].position, history[i - 1].position, lerpT);
                follower.rotation = Quaternion.Slerp(
                    history[i].rotation, history[i - 1].rotation, lerpT);
                follower.LookAt(target.position, follower.up);
                return i - 1;
            }
            distLeft -= segmentDist;
        }
        // Debug.Log("Not removing points for follower " + follower.name);
        return -1;
    }

    void OnDrawGizmos()
    {
        if (!ValidTarget)
            return;

        if (follower == null)
            follower = this.transform;

        if (positionHistoryGizmo == GizmoMode.Always)
            DrawPositionHistoryGizmo();
        if (orientationHistoryGizmo == GizmoMode.Always)
            DrawOrientationHistoryGizmo();
    }

    void OnDrawGizmosSelected()
    {
        if (positionHistoryGizmo == GizmoMode.OnSelection)
            DrawPositionHistoryGizmo();
        if (orientationHistoryGizmo == GizmoMode.OnSelection)
            DrawOrientationHistoryGizmo();
    }

    void DrawPositionHistoryGizmo(float stepFactor = 50)
    {
        Gizmos.color = Color.gray;
        var step = (int)(history.Count / stepFactor) + 1;
        for (var i = 0; i < history.Count - step; i += step)
        {
            Gizmos.DrawLine(history[i].position, history[i + step].position);
        }
    }

    void DrawOrientationHistoryGizmo(float stepFactor = 10)
    {
        Gizmos.color = Color.gray;
        var step = (int)(history.Count / stepFactor) + 1;
        for (var i = 0; i < history.Count; i += step)
        {
            GizmosExtras.DrawQuaternion(history[i].position, history[i].rotation,
                Vector3.right + Vector3.forward);
        }
    }
}
