using UnityEngine;

public class TargetSeekingBehaviour : MonoBehaviour
{
    [Header("Gizmos")]
    [SerializeField] bool seekStateGizmo = false;

    [Header("Seeking")]
    [SerializeField] GameObject seekTarget = null;
    [SerializeField] bool seeking = true;

    [SerializeField] float seekMoveSpeed = 10f;
    [SerializeField] float seekTurnSpeed = 60f;

    [Header("Coasting")]
    [SerializeField] bool coastIfNotSeeking = false;
    [SerializeField] float coastMoveSpeed = 4f;

    /// <summary>
    /// Check if seeking is currently enabled and valid.
    /// </summary>
    /// <param name="distThreshold">The maximum distance at which seeking should be ignored.</param>
    /// <returns>Whether seeking actions should be performed.</returns>
    bool CanSeek(float distThreshold = 1f)
    {
        // 1. Seeking is not enabled.
        if (!seeking)
            return false;
        // 2.Target does not exist.
        else if (seekTarget == null)
        {
            Debug.Log("Invalid Target Error: Seeking is enabled but target is null.");
            return false;
        }
        // 3. Object is too close to the target.
        var target = seekTarget.transform;
        if ((target.position - transform.position).magnitude <= distThreshold)
            return false;
        // 4. Target is valid and the object should follow it.
        return true;
    }

    void Update()
    {
        if (CanSeek())
            SeekTarget();
        else if (coastIfNotSeeking)
            Coast();
    }

    /// <summary>
    /// If seeking is enabled and validated, SEEK by:
    /// 1. Rotating towards the target with a maximum speed of seekTurnSpeed.
    /// 2. Moving in the adjusted direction at a maximum speed of seekMoveSpeed.
    /// </summary>
    void SeekTarget()
    {
        var target = seekTarget.transform;
        var relativePos = target.position - transform.position;

        // Early exit case: We are very close to the target and don't want to look at it.
        if (relativePos.magnitude < 0.01f)
            return;

        // Rotation: The object should turn towards the target at seekTurnSpeed.
        var currRot = transform.rotation;
        var desiredRot = Quaternion.LookRotation(relativePos.normalized, Vector3.up);
        var newRot = Quaternion.RotateTowards(currRot, desiredRot, seekTurnSpeed * Time.deltaTime);
        transform.rotation = newRot;

        // Position: After the rotation is adjusted, object should move in the direction of its
        // adjusted forward trajectory (transform.forward) at seekMoveSpeed.
        transform.position += transform.forward.normalized * seekMoveSpeed * Time.deltaTime;
    }

    /// <summary>
    /// If the object isn't seeking forever, COAST by:
    /// 1. Continuing forward in the object's current direction at coastMoveSpeed.
    /// </summary>
    void Coast()
    {
        // Move straight ahead at the given speed.
        transform.position += transform.forward.normalized * coastMoveSpeed * Time.deltaTime;
    }

    void OnDrawGizmos()
    {
        if (!seekStateGizmo)
            return;

        // Seek Gizmo: Draw an arrow towards the target and an arrow for the current object's
        // trajectory, these should align over time.
        if (CanSeek())
        {
            Color seekColor = Color.yellow, fwdColor = Color.white;
            var target = seekTarget.transform;
            Gizmos.color = seekColor;
            DrawArrow.ForGizmo(transform.position,
                (target.position - transform.position).normalized * seekMoveSpeed);
            Gizmos.color = fwdColor;
            DrawArrow.ForGizmo(transform.position, transform.forward * seekMoveSpeed);
        }

        // Coast Gizmo: Draw a single arrow in the forward direction with a length proportional
        // to coastMoveSpeed.
        else if (coastIfNotSeeking)
        {
            var coastColor = Color.cyan;
            Gizmos.color = coastColor;
            DrawArrow.ForGizmo(transform.position, transform.forward * coastMoveSpeed);
        }
    }
}
