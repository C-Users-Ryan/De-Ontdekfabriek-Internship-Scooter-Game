using UnityEngine;

/// <summary>
/// ScooterController — Root driving script. Coordinates all scooter sub-systems.
/// Delegates input reading to ScooterInputHandler,
/// physics to AccelerationBrake and SteeringAndSway,
/// and collision response to CollisionHandler.
///
/// REQUIRED COMPONENTS ON SAME GAMEOBJECT:
///   Rigidbody, ScooterInputHandler, AccelerationBrake, SteeringAndSway, CollisionHandler
///
/// SCOOTER PREFAB HIERARCHY:
///   ScooterRoot  [Rigidbody + all scripts + BoxCollider]
///   └─ VisualRoot  [Mesh — this is what leans visually]
///      └─ CameraMount  [empty transform at eye height ~1.1m]
///         └─ Main Camera  [FirstPersonCamera script]
///   └─ GroundCheck  [empty transform at wheel base level]
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ScooterInputHandler))]
[RequireComponent(typeof(AccelerationBrake))]
[RequireComponent(typeof(SteeringAndSway))]
[RequireComponent(typeof(CollisionHandler))]
public class ScooterController : MonoBehaviour
{
    public static ScooterController Instance { get; private set; }

    // ── Sub-systems (public so other scripts can read their settings) ─
    [HideInInspector] public Rigidbody          Rb;
    [HideInInspector] public ScooterInputHandler Input;
    [HideInInspector] public AccelerationBrake  AccelBrake;
    [HideInInspector] public SteeringAndSway     Steering;

    // ── Ground Detection ──────────────────────────────────────────
    [Header("Ground Detection")]
    public LayerMask groundLayer;
    public Transform groundCheck;
    public float     groundCheckRadius = 0.2f;

    public bool IsGrounded { get; private set; }

    // ── Input Passthrough (convenience getters for sub-systems) ──
    public float ThrottleInput => Input.Throttle;
    public float BrakeInput    => Input.Brake;
    public float SteerInput    => Input.Steer;

    // ── Speed ─────────────────────────────────────────────────────
    public float SpeedKmh => Rb.linearVelocity.magnitude * 3.6f;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake()
    {
        Instance   = this;
        Rb         = GetComponent<Rigidbody>();
        Input      = GetComponent<ScooterInputHandler>();
        AccelBrake = GetComponent<AccelerationBrake>();
        Steering   = GetComponent<SteeringAndSway>();

        // Prevent physics from tipping the scooter sideways
        Rb.constraints = RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ;
    }

    void FixedUpdate()
    {
        CheckGround();

        if (!GameManager.Instance.IsPlaying()) return;

        AccelBrake.Tick();
        Steering.Tick();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────

    void CheckGround()
    {
        IsGrounded = Physics.CheckSphere(
            groundCheck.position, groundCheckRadius, groundLayer);
    }
}
