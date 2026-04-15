using UnityEngine;

/// <summary>
/// AccelerationBrake — Handles throttle, braking, drag and momentum.
/// Must sit on the same GameObject as ScooterController.
/// Called each FixedUpdate by ScooterController.Tick().
/// </summary>
public class AccelerationBrake : MonoBehaviour
{
    // ── Tuning ────────────────────────────────────────────────────
    [Header("Acceleration")]
    [Tooltip("Forward force applied when throttle is full.")]
    public float driveForce = 1800f;

    [Tooltip("Maximum speed in km/h.")]
    public float maxSpeedKmh = 60f;

    [Header("Braking")]
    [Tooltip("Braking force when the brake input is held.")]
    public float brakeForce = 2800f;

    [Header("Drag / Coasting")]
    [Tooltip("Natural rolling drag when no throttle or brake is applied.")]
    public float coastDrag = 1.5f;

    [Tooltip("Extra drag applied on top of coast drag while braking.")]
    public float brakeDrag = 6f;

    [Tooltip("Drag applied while throttle is held (keeps top speed stable).")]
    public float throttleDrag = 0.4f;

    // ── Private ───────────────────────────────────────────────────
    private ScooterController _sc;
    private float _maxSpeedMs;

    void Awake()
    {
        _sc = GetComponent<ScooterController>();
        _maxSpeedMs = maxSpeedKmh / 3.6f;
    }

    /// <summary>Called by ScooterController each FixedUpdate.</summary>
    public void Tick()
    {
        if (!_sc.IsGrounded) return;

        ApplyDrag();
        ApplyThrottle();
        ApplyBrake();
    }

    // ─────────────────────────────────────────────────────────────

    void ApplyThrottle()
    {
        if (_sc.ThrottleInput <= 0f) return;

        // Don't accelerate past max speed
        if (_sc.SpeedKmh >= maxSpeedKmh) return;

        Vector3 force = transform.forward * driveForce * _sc.ThrottleInput;
        _sc.Rb.AddForce(force, ForceMode.Force);
    }

    void ApplyBrake()
    {
        if (_sc.BrakeInput <= 0f) return;

        // Opposing force in direction of travel
        Vector3 brakeDir = -_sc.Rb.linearVelocity.normalized;
        _sc.Rb.AddForce(brakeDir * brakeForce * _sc.BrakeInput, ForceMode.Force);
    }

    void ApplyDrag()
    {
        if (_sc.BrakeInput > 0f)
            _sc.Rb.linearDamping = brakeDrag;
        else if (_sc.ThrottleInput > 0f)
            _sc.Rb.linearDamping = throttleDrag;
        else
            _sc.Rb.linearDamping = coastDrag;
    }
}
