using UnityEngine;

/// <summary>
/// SteeringAndSway — Yaw steering plus visual lean/sway on the scooter body.
/// Attach alongside ScooterController. Called each FixedUpdate by ScooterController.
///
/// HOW THE SWAY WORKS:
///   The scooter's Rigidbody rotates on Y (yaw) for actual turning.
///   A separate child GameObject (_visualRoot) is lerped on Z (roll) to fake a lean.
///   This gives the feel of a real scooter leaning into corners without
///   messing with the physics collider orientation.
/// </summary>
public class SteeringAndSway : MonoBehaviour
{
    // ── Steering ──────────────────────────────────────────────────
    [Header("Steering")]
    [Tooltip("How fast the scooter rotates (yaw) at full steer input.")]
    public float steerSpeed = 70f;

    [Tooltip("Steering sensitivity scales with speed — this curve maps normalised speed (0-1) to a steer multiplier.")]
    public AnimationCurve steerSpeedCurve = AnimationCurve.Linear(0, 0.3f, 1, 1f);

    [Tooltip("Minimum speed (km/h) before steering has any effect.")]
    public float minSpeedToSteer = 2f;

    // ── Visual Sway / Lean ────────────────────────────────────────
    [Header("Visual Sway")]
    [Tooltip("Child GameObject that holds the visible scooter mesh. This is what leans.")]
    public Transform visualRoot;

    [Tooltip("Maximum lean angle in degrees when turning at full steer.")]
    public float maxLeanAngle = 18f;

    [Tooltip("How quickly the lean catches up to the target angle.")]
    public float leanSmoothing = 6f;

    // ── Private ───────────────────────────────────────────────────
    private ScooterController _sc;
    private float _currentLeanAngle;

    void Awake()
    {
        _sc = GetComponent<ScooterController>();

        if (visualRoot == null)
            Debug.LogWarning("[SteeringAndSway] visualRoot is not assigned. " +
                             "Lean effect won't be visible. Assign the scooter mesh child.");
    }

    /// <summary>Called by ScooterController each FixedUpdate.</summary>
    public void Tick()
    {
        ApplySteering();
        ApplyLean();
    }

    // ─────────────────────────────────────────────────────────────

    void ApplySteering()
    {
        if (_sc.SpeedKmh < minSpeedToSteer) return;
        if (Mathf.Abs(_sc.SteerInput) < 0.01f) return;

        float normSpeed  = Mathf.Clamp01(_sc.SpeedKmh / _sc.AccelBrake.maxSpeedKmh);
        float multiplier = steerSpeedCurve.Evaluate(normSpeed);
        float yawDelta   = _sc.SteerInput * steerSpeed * multiplier * Time.fixedDeltaTime;

        _sc.Rb.MoveRotation(_sc.Rb.rotation * Quaternion.Euler(0f, yawDelta, 0f));
    }

    void ApplyLean()
    {
        if (visualRoot == null) return;

        // Target lean: steer input drives the angle, speed amplifies it slightly
        float normSpeed   = Mathf.Clamp01(_sc.SpeedKmh / _sc.AccelBrake.maxSpeedKmh);
        float targetLean  = -_sc.SteerInput * maxLeanAngle * Mathf.Lerp(0.4f, 1f, normSpeed);

        // Smooth towards target
        _currentLeanAngle = Mathf.Lerp(
            _currentLeanAngle, targetLean, leanSmoothing * Time.deltaTime);

        // Apply only Z rotation to the visual root; preserve existing X/Y rotations
        Vector3 euler = visualRoot.localEulerAngles;
        euler.z = _currentLeanAngle;
        visualRoot.localEulerAngles = euler;
    }
}
