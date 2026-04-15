using UnityEngine;

/// <summary>
/// FirstPersonCamera — Mounts to the scooter and gives a first-person rider view.
/// Features:
///   • Speed-based FOV (widens at high speed for sense of velocity)
///   • Subtle head bob while moving
///   • Smooth look-ahead that tilts slightly into turns
///
/// SETUP:
///   Attach to the Camera child of your scooter hierarchy.
///   Set the Camera's local position to approx (0, 1.1, 0.1) — eye height.
/// </summary>
[RequireComponent(typeof(Camera))]
public class FirstPersonCamera : MonoBehaviour
{
    public static FirstPersonCamera Instance { get; private set; }

    // ── FOV ───────────────────────────────────────────────────────
    [Header("Field of View")]
    public float baseFov     = 75f;
    public float maxFovBoost = 20f;   // Added at full speed
    public float fovSmoothing = 4f;

    // ── Head Bob ──────────────────────────────────────────────────
    [Header("Head Bob")]
    public bool  headBobEnabled = true;
    public float bobFrequency   = 1.8f;
    public float bobAmplitude   = 0.025f;

    // ── Look-Ahead Lean ───────────────────────────────────────────
    [Header("Turn Lean")]
    [Tooltip("Camera tilts slightly into turns for realism.")]
    public float leanAngle     = 5f;
    public float leanSmoothing = 5f;

    // ── Private ───────────────────────────────────────────────────
    private Camera _cam;
    private Vector3 _baseLocalPos;
    private float   _bobTimer;
    private float   _currentLean;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake()
    {
        Instance     = this;
        _cam         = GetComponent<Camera>();
        _baseLocalPos = transform.localPosition;
    }

    void LateUpdate()
    {
        if (ScooterController.Instance == null) return;

        UpdateFOV();
        if (headBobEnabled) UpdateHeadBob();
        UpdateLean();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Camera Updates

    void UpdateFOV()
    {
        float normSpeed = Mathf.Clamp01(
            ScooterController.Instance.SpeedKmh /
            ScooterController.Instance.AccelBrake.maxSpeedKmh);

        float targetFov = baseFov + normSpeed * maxFovBoost;
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, fovSmoothing * Time.deltaTime);
    }

    void UpdateHeadBob()
    {
        float speed = ScooterController.Instance.SpeedKmh;
        if (speed < 2f)
        {
            // Settle back to base position when nearly stopped
            transform.localPosition = Vector3.Lerp(
                transform.localPosition, _baseLocalPos, 5f * Time.deltaTime);
            return;
        }

        _bobTimer += Time.deltaTime * bobFrequency * (speed / 30f);
        float bobY = Mathf.Sin(_bobTimer * Mathf.PI * 2f) * bobAmplitude;

        transform.localPosition = _baseLocalPos + new Vector3(0f, bobY, 0f);
    }

    void UpdateLean()
    {
        float steer  = ScooterController.Instance.SteerInput;
        float target = -steer * leanAngle;
        _currentLean = Mathf.Lerp(_currentLean, target, leanSmoothing * Time.deltaTime);

        Vector3 euler = transform.localEulerAngles;
        euler.z = _currentLean;
        transform.localEulerAngles = euler;
    }

    #endregion
}
