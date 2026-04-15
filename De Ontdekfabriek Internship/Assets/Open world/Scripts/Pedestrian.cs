using UnityEngine;

/// <summary>
/// Pedestrian — A simple walking NPC that paces back and forth on the pavement.
/// Pedestrians are INVINCIBLE — the scooter passes through them (trigger collider only).
/// They exist purely for visual city atmosphere.
///
/// SETUP:
///   1. Create a pedestrian prefab (capsule or character mesh).
///   2. Set its Collider to isTrigger = true.
///   3. Attach this script.
///   4. Set pointA and pointB as the two ends of their walking route.
///      OR use the built-in offset method (set walkDistance and they pace from spawn).
/// </summary>
public class Pedestrian : MonoBehaviour
{
    // ── Movement ──────────────────────────────────────────────────
    [Header("Walk Path")]
    [Tooltip("If assigned, pedestrian walks between these two transforms.")]
    public Transform pointA;
    public Transform pointB;

    [Tooltip("If pointA/B not set, pedestrian paces this many metres from spawn.")]
    public float walkDistance = 5f;

    [Header("Speed")]
    public float walkSpeed   = 1.2f;
    public float turnSpeed   = 180f;  // degrees per second

    // ── Private ───────────────────────────────────────────────────
    private Vector3 _targetA;
    private Vector3 _targetB;
    private Vector3 _currentTarget;
    private bool    _goingToB = true;

    // ─────────────────────────────────────────────────────────────

    void Start()
    {
        // Build path from assigned transforms or from spawn position
        _targetA = pointA != null
            ? pointA.position
            : transform.position;

        _targetB = pointB != null
            ? pointB.position
            : transform.position + transform.forward * walkDistance;

        _currentTarget = _targetB;

        // Make sure collider is a trigger so physics doesn't block the scooter
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Update()
    {
        if (!GameManager.Instance.IsPlaying()) return;

        Walk();
    }

    void Walk()
    {
        Vector3 toTarget = _currentTarget - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude < 0.3f)
        {
            // Reached target — turn around
            _goingToB      = !_goingToB;
            _currentTarget = _goingToB ? _targetB : _targetA;
            return;
        }

        // Rotate toward target
        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation   = Quaternion.RotateTowards(
            transform.rotation, targetRot, turnSpeed * Time.deltaTime);

        // Move forward
        transform.position += transform.forward * walkSpeed * Time.deltaTime;
    }
}
