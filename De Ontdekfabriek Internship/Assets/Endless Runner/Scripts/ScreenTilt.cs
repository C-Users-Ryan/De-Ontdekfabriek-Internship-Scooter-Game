using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ScooterController — Realistic scooter driving with physics movement,
/// body lean, and camera tilt. Attach to your scooter root GameObject.
///
/// SETUP:
///   1. Add a Rigidbody to this GameObject (freeze rotation X/Z in constraints).
///   2. Assign scooterBody (the visual mesh/child that visually leans).
///   3. Assign cameraRig (your camera or a camera pivot child object).
///   4. Optionally assign frontWheel & rearWheel transforms for wheel spin.
///   5. The script uses Rigidbody forces — no CharacterController needed.
/// </summary>
/// 

[RequireComponent(typeof(Rigidbody))]

public class ScreenTilt : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // REFERENCES
    // ─────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Visual body/mesh child that leans left and right. Leave empty for a plain ball.")]
    public Transform scooterBody;

    [Tooltip("Empty parent of your Camera. Assign this to get camera tilt.")]
    public Transform cameraRig;

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

    [Tooltip("The base Y rotation of your camera rig in degrees. Default -90.")]
    public float cameraBaseRotationY = -90f;

    // ─────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────

    private Rigidbody rb;
    private float currentSideVelocity = 0f;
    private float currentLean = 0f;
    private float currentCamTilt = 0f;
    private float horizontalInput = 0f;

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

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                horizontalInput -= 1f;

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                horizontalInput += 1f;
        }

        if (Gamepad.current != null)
        {
            float stickX = Gamepad.current.leftStick.x.ReadValue();
            if (Mathf.Abs(stickX) > 0.1f)
                horizontalInput += stickX;

            horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);
        }

        HandleBodyLean(horizontalInput);
        HandleCameraTilt(horizontalInput);
    }

    private void FixedUpdate()
    {
        HandleSideMovement(horizontalInput);
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
        vel.z = 0f;
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
    // CAMERA TILT
    // ─────────────────────────────────────────────

    private void HandleCameraTilt(float input)
    {
        if (cameraRig == null) return;

        float targetTilt = -input * maxCameraTilt;
        currentCamTilt = Mathf.Lerp(currentCamTilt, targetTilt, Time.deltaTime * cameraTiltSpeed);

        // Preserve the base Y rotation (-90) and only animate Z (roll/tilt)
        cameraRig.localRotation = Quaternion.Euler(0f, cameraBaseRotationY, currentCamTilt);
    }
}



