using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ScreenTilt — Handles player input, movement, and body lean.
///
/// CAMERA WRITING IS HANDLED BY TerrainBehaviourController.
/// This script only calculates the tilt value and exposes it via
/// CurrentCamTilt so the two scripts never fight over the camera.
///
/// SETUP:
///   1. Attach to your player GameObject (same one with the Rigidbody).
///   2. Assign scooterBody for lean (optional, skip if still a ball).
///   3. Do NOT assign cameraRig here — assign it in TerrainBehaviourController.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ScreenTilt : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // REFERENCES
    // ─────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Visual body/mesh child that leans left and right. Leave empty for a plain ball.")]
    public Transform scooterBody;

    // ─────────────────────────────────────────────
    // SIDE MOVEMENT
    // ─────────────────────────────────────────────

    [Header("Side Movement")]
    [Tooltip("Maximum side movement speed in m/s.")]
    public float sideMoveSpeed = 8f;

    [Tooltip("How snappy the movement is. Higher = instant. Lower = slidey/momentum feel.")]
    public float sideAcceleration = 15f;

    [Tooltip("Hard left/right boundary. Player cannot pass this X position.")]
    public float laneLimit = 4f;

    // ─────────────────────────────────────────────
    // FORWARD / BACKWARD (TESTING ONLY)
    // ─────────────────────────────────────────────

    [Header("Forward Movement (Testing)")]
    [Tooltip("Maximum forward/backward speed in m/s.")]
    public float forwardMoveSpeed = 12f;

    [Tooltip("How snappy forward/backward acceleration is.")]
    public float forwardAcceleration = 15f;

    // ─────────────────────────────────────────────
    // BODY LEAN
    // ─────────────────────────────────────────────

    [Header("Body Lean")]
    [Tooltip("Max lean angle in degrees when moving sideways.")]
    public float maxLeanAngle = 25f;

    [Tooltip("How fast the body leans in and snaps back.")]
    public float leanSpeed = 8f;

    // ─────────────────────────────────────────────
    // CAMERA TILT
    // ─────────────────────────────────────────────

    [Header("Camera Tilt")]
    [Tooltip("Max camera roll angle when moving sideways.")]
    public float maxCameraTilt = 10f;

    [Tooltip("How fast the camera tilts and recovers.")]
    public float cameraTiltSpeed = 5f;

    // ─────────────────────────────────────────────
    // PUBLIC READ-ONLY — used by TerrainBehaviourController
    // ─────────────────────────────────────────────

    /// <summary>
    /// The current camera tilt angle calculated by this script.
    /// TerrainBehaviourController reads this and applies it to the camera
    /// together with shake and wobble in one single write.
    /// </summary>
    public float CurrentCamTilt { get; private set; } = 0f;

    // ─────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────

    private Rigidbody rb;
    private float currentSideVelocity = 0f;
    private float currentForwardVelocity = 0f;
    private float currentLean = 0f;
    private float horizontalInput = 0f;
    private float verticalInput = 0f;

    // ─────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.freezeRotation = true;
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
    }

    private void Update()
    {
        horizontalInput = 0f;
        verticalInput = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                horizontalInput -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                horizontalInput += 1f;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                verticalInput += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                verticalInput -= 1f;
        }

        if (Gamepad.current != null)
        {
            float stickX = Gamepad.current.leftStick.x.ReadValue();
            float stickY = Gamepad.current.leftStick.y.ReadValue();

            if (Mathf.Abs(stickX) > 0.1f) horizontalInput += stickX;
            if (Mathf.Abs(stickY) > 0.1f) verticalInput += stickY;

            horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);
            verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
        }

        HandleBodyLean(horizontalInput);
        CalculateCameraTilt(horizontalInput); // calculate only — do NOT write to camera here
    }

    private void FixedUpdate()
    {
        HandleSideMovement(horizontalInput);
        HandleForwardMovement(verticalInput);
    }

    // ─────────────────────────────────────────────
    // SIDE MOVEMENT
    // ─────────────────────────────────────────────

    private void HandleSideMovement(float input)
    {
        float targetVelocity = input * sideMoveSpeed;
        currentSideVelocity = Mathf.MoveTowards(
            currentSideVelocity,
            targetVelocity,
            sideAcceleration * Time.fixedDeltaTime
        );

        Vector3 vel = rb.linearVelocity;
        vel.x = currentSideVelocity;
        rb.linearVelocity = vel;

        Vector3 pos = rb.position;
        if (pos.x < -laneLimit || pos.x > laneLimit)
        {
            pos.x = Mathf.Clamp(pos.x, -laneLimit, laneLimit);
            rb.MovePosition(pos);
            currentSideVelocity = 0f;
        }
    }

    // ─────────────────────────────────────────────
    // FORWARD / BACKWARD MOVEMENT
    // ─────────────────────────────────────────────

    private void HandleForwardMovement(float input)
    {
        float targetVelocity = input * forwardMoveSpeed;
        currentForwardVelocity = Mathf.MoveTowards(
            currentForwardVelocity,
            targetVelocity,
            forwardAcceleration * Time.fixedDeltaTime
        );

        Vector3 vel = rb.linearVelocity;
        vel.z = currentForwardVelocity;
        rb.linearVelocity = vel;
    }

    // ─────────────────────────────────────────────
    // BODY LEAN
    // ─────────────────────────────────────────────

    private void HandleBodyLean(float input)
    {
        if (scooterBody == null) return;

        float targetLean = -input * maxLeanAngle;
        currentLean = Mathf.Lerp(currentLean, targetLean, Time.deltaTime * leanSpeed);

        scooterBody.localRotation = Quaternion.Euler(0f, 0f, currentLean);
    }

    // ─────────────────────────────────────────────
    // CAMERA TILT — calculate only, no camera write
    // ─────────────────────────────────────────────

    private void CalculateCameraTilt(float input)
    {
        float targetTilt = -input * maxCameraTilt;
        CurrentCamTilt = Mathf.Lerp(CurrentCamTilt, targetTilt, Time.deltaTime * cameraTiltSpeed);
        // TerrainBehaviourController reads CurrentCamTilt and writes to the camera
    }
}