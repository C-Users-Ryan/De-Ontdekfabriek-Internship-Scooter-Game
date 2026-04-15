using UnityEngine;

/// <summary>
/// ScooterInputHandler — Reads raw player input and exposes clean values.
/// Supports keyboard (WASD / Arrow Keys) and gamepad (left stick + triggers).
///
/// SETUP:
///   Attach to the same GameObject as ScooterController.
///   ScooterController reads from this component instead of calling Input directly.
///
/// UNITY INPUT AXES NEEDED (Edit > Project Settings > Input Manager):
///   "Vertical"   — W/S or Up/Down arrows (keyboard) / Left Stick Y (gamepad)
///   "Horizontal" — A/D or Left/Right arrows (keyboard) / Left Stick X (gamepad)
///   "Brake"      — Left Shift or gamepad Left Trigger (add this axis manually)
///
///   To add "Brake" axis:
///     1. Edit > Project Settings > Input Manager
///     2. Duplicate an existing axis entry
///     3. Name it "Brake", set Positive Button to "left shift"
///     4. For gamepad: set Type to "Joystick Axis", Axis to "3rd axis"
/// </summary>
public class ScooterInputHandler : MonoBehaviour
{
    // ── Exposed Input Values ──────────────────────────────────────
    /// <summary>0..1 — how hard the throttle is pressed.</summary>
    public float Throttle { get; private set; }

    /// <summary>0..1 — how hard the brake is pressed.</summary>
    public float Brake    { get; private set; }

    /// <summary>-1..1 — left is negative, right is positive.</summary>
    public float Steer    { get; private set; }

    /// <summary>True on the frame the horn button is pressed.</summary>
    public bool  HornPressed { get; private set; }

    // ── Settings ──────────────────────────────────────────────────
    [Header("Input Settings")]
    [Tooltip("Smoothing applied to steer input. 0 = instant, 1 = very slow.")]
    [Range(0f, 0.95f)]
    public float steerSmoothing = 0.15f;

    private float _rawSteer;

    // ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (!GameManager.Instance.IsPlaying())
        {
            Throttle    = 0f;
            Brake       = 0f;
            Steer       = 0f;
            HornPressed = false;
            return;
        }

        ReadThrottleAndBrake();
        ReadSteer();
        HornPressed = Input.GetKeyDown(KeyCode.H) || Input.GetButtonDown("Jump");
    }

    // ─────────────────────────────────────────────────────────────

    void ReadThrottleAndBrake()
    {
        float vertical = Input.GetAxis("Vertical");

        // W / Up / Right trigger = throttle
        Throttle = Mathf.Clamp01(vertical);

        // S / Down / Left trigger = brake
        // Also check a dedicated Brake axis if it exists
        float brakeAxis = 0f;
        try { brakeAxis = Input.GetAxis("Brake"); } catch { }

        Brake = Mathf.Clamp01(Mathf.Max(-vertical, brakeAxis));
    }

    void ReadSteer()
    {
        float rawInput = Input.GetAxis("Horizontal");

        // Apply smoothing to prevent jerky steering
        _rawSteer = Mathf.Lerp(_rawSteer, rawInput, 1f - steerSmoothing);
        Steer     = _rawSteer;
    }
}
