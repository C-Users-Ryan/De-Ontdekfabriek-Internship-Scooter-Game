using UnityEngine;

/// <summary>
/// TrafficVehicle — Simple waypoint-following AI for traffic cars/matatus.
/// No NavMesh needed — just place empty GameObjects as waypoints along roads.
///
/// SETUP:
///   1. Create a traffic vehicle prefab (a car/matatu mesh + Rigidbody + BoxCollider).
///   2. Attach this script.
///   3. In the scene, create empty GameObjects along roads as waypoints.
///   4. Assign them to the `waypoints` array.
///   5. Set `loopWaypoints = true` for circular routes.
///
/// TAGS:
///   Tag traffic vehicles as "Traffic" so CollisionHandler can use the crashLayers mask.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TrafficVehicle : MonoBehaviour
{
    // ── Waypoints ─────────────────────────────────────────────────
    [Header("Waypoints")]
    public Transform[] waypoints;
    public bool        loopWaypoints = true;

    [Tooltip("How close to a waypoint before moving to the next one (metres).")]
    public float waypointReachDistance = 2f;

    // ── Movement ──────────────────────────────────────────────────
    [Header("Movement")]
    public float moveSpeed    = 8f;    // m/s (~29 km/h — city traffic speed)
    public float turnSpeed    = 80f;   // degrees per second
    public float stopDistance = 6f;    // slow down if something is ahead

    // ── Private ───────────────────────────────────────────────────
    private Rigidbody _rb;
    private int       _currentWaypoint;
    private bool      _active = true;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ;
    }

    void FixedUpdate()
    {
        if (!_active || waypoints == null || waypoints.Length == 0) return;
        if (!GameManager.Instance.IsPlaying()) return;

        MoveToWaypoint();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Navigation

    void MoveToWaypoint()
    {
        Transform target = waypoints[_currentWaypoint];
        Vector3   toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        // Arrived at waypoint?
        if (dist < waypointReachDistance)
        {
            AdvanceWaypoint();
            return;
        }

        // Check for obstacles ahead — slow down
        float speed = moveSpeed;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f,
            transform.forward, out _, stopDistance))
        {
            speed *= 0.3f;
        }

        // Rotate toward target
        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        _rb.MoveRotation(Quaternion.RotateTowards(
            transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime));

        // Move forward
        _rb.MovePosition(transform.position + transform.forward * speed * Time.fixedDeltaTime);
    }

    void AdvanceWaypoint()
    {
        _currentWaypoint++;

        if (_currentWaypoint >= waypoints.Length)
        {
            if (loopWaypoints)
                _currentWaypoint = 0;
            else
                _active = false;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        // Start at a random waypoint to stagger traffic
        if (waypoints != null && waypoints.Length > 0)
            _currentWaypoint = Random.Range(0, waypoints.Length);
    }
}
