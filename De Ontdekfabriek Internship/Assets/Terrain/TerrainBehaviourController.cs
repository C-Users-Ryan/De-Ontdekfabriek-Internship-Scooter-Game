using UnityEngine;

/// <summary>
/// TerrainBehaviourController — The single owner of all camera rotation writes.
///
/// This script reads the tilt value from ScreenTilt, adds terrain shake and wobble
/// on top, and writes the final combined rotation to the cameraRig once per frame
/// in LateUpdate — AFTER ScreenTilt has finished its Update.
///
/// SETUP:
///   1. Attach to the same player GameObject as ScreenTilt.
///   2. Assign scooterController, cameraRig, playerCamera, and optionally audioSource.
///   3. Assign a defaultTerrain profile (e.g. your Tarmac asset).
///   4. Set cameraBaseRotationY to match your camera's natural Y rotation (default -90).
///   5. On each road section prefab, add a TerrainZone component with a trigger collider.
/// </summary>
public class TerrainBehaviourController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // REFERENCES
    // ─────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The ScreenTilt script on this GameObject.")]
    public ScreenTilt scooterController;

    [Tooltip("The camera rig transform. This script is the ONLY thing that writes to it.")]
    public Transform cameraRig;

    [Tooltip("The actual Camera component (for FOV changes).")]
    public Camera playerCamera;

    [Tooltip("AudioSource for terrain ambient sounds (optional).")]
    public AudioSource terrainAudioSource;

    // ─────────────────────────────────────────────
    // CAMERA BASE ROTATION
    // ─────────────────────────────────────────────

    [Header("Camera")]
    [Tooltip("The natural Y rotation of your camera rig. Default -90.")]
    public float cameraBaseRotationY = -90f;

    // ─────────────────────────────────────────────
    // TERRAIN
    // ─────────────────────────────────────────────

    [Header("Terrain")]
    [Tooltip("Fallback terrain used when no zone is active (e.g. Tarmac).")]
    public TerrainProfile defaultTerrain;

    // ─────────────────────────────────────────────
    // BASE VALUES — cached from ScreenTilt on Start
    // ─────────────────────────────────────────────

    private float baseSideMoveSpeed;
    private float baseSideAcceleration;
    private float baseLeanAngle;
    private float baseLeanSpeed;
    private float baseFOV;

    // ─────────────────────────────────────────────
    // BLEND STATE
    // ─────────────────────────────────────────────

    private TerrainProfile currentProfile;
    private TerrainProfile previousProfile;
    private float blendT = 1f;
    private float blendDuration = 0.5f;

    // ─────────────────────────────────────────────
    // SHAKE / WOBBLE STATE
    // ─────────────────────────────────────────────

    private float shakeTimer = 0f;
    private float wobbleTimer = 0f;
    private float smoothShakeX = 0f;
    private float smoothShakeY = 0f;
    private float smoothWobbleZ = 0f;

    // ─────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    private void Start()
    {
        if (scooterController != null)
        {
            baseSideMoveSpeed = scooterController.sideMoveSpeed;
            baseSideAcceleration = scooterController.sideAcceleration;
            baseLeanAngle = scooterController.maxLeanAngle;
            baseLeanSpeed = scooterController.leanSpeed;
        }

        if (playerCamera != null)
            baseFOV = playerCamera.fieldOfView;

        currentProfile = defaultTerrain;
        previousProfile = defaultTerrain;
        blendT = 1f;
    }

    private void Update()
    {
        AdvanceBlend();
        ApplyBlendedMovementValues();
        ApplyFOV();
        ApplyAudioVolume();
    }

    /// <summary>
    /// LateUpdate runs AFTER all Update() calls, so ScreenTilt has already
    /// calculated its tilt value for this frame. We read it here and write
    /// the final camera rotation — guaranteed no overwrites.
    /// </summary>
    private void LateUpdate()
    {
        ApplyCameraRotation();
    }

    // ─────────────────────────────────────────────
    // TERRAIN ZONE DETECTION
    // Called by TerrainZone when player enters/exits
    // ─────────────────────────────────────────────

    public void EnterTerrain(TerrainProfile newProfile)
    {
        if (newProfile == null || newProfile == currentProfile) return;

        previousProfile = currentProfile;
        currentProfile = newProfile;
        blendDuration = newProfile.blendDuration;
        blendT = 0f;

        if (terrainAudioSource != null)
        {
            if (newProfile.terrainAmbientSound != null)
            {
                terrainAudioSource.clip = newProfile.terrainAmbientSound;
                terrainAudioSource.pitch = newProfile.ambientPitch;
                terrainAudioSource.loop = true;
                terrainAudioSource.Play();
            }
            else
            {
                terrainAudioSource.Stop();
            }
        }

        Debug.Log($"[TerrainSystem] Entered: {newProfile.terrainName}");
    }

    public void ExitTerrain(TerrainProfile exitedProfile)
    {
        if (exitedProfile == currentProfile)
            EnterTerrain(defaultTerrain);
    }

    // ─────────────────────────────────────────────
    // BLEND
    // ─────────────────────────────────────────────

    private void AdvanceBlend()
    {
        if (blendT >= 1f) return;
        blendT += Time.deltaTime / Mathf.Max(blendDuration, 0.01f);
        blendT = Mathf.Clamp01(blendT);
    }

    private float Blend(float prev, float curr)
    {
        return Mathf.Lerp(prev, curr, blendT);
    }

    // ─────────────────────────────────────────────
    // APPLY MOVEMENT VALUES TO SCREENTILT
    // ─────────────────────────────────────────────

    private void ApplyBlendedMovementValues()
    {
        if (scooterController == null) return;

        scooterController.sideMoveSpeed = baseSideMoveSpeed
            * Blend(previousProfile.sideMoveSpeedMultiplier, currentProfile.sideMoveSpeedMultiplier);

        scooterController.sideAcceleration = baseSideAcceleration
            * Blend(previousProfile.sideAccelerationMultiplier, currentProfile.sideAccelerationMultiplier);

        scooterController.maxLeanAngle = baseLeanAngle
            * Blend(previousProfile.leanAngleMultiplier, currentProfile.leanAngleMultiplier);

        scooterController.leanSpeed = baseLeanSpeed
            * Blend(previousProfile.leanSpeedMultiplier, currentProfile.leanSpeedMultiplier);
    }

    // ─────────────────────────────────────────────
    // APPLY FOV
    // ─────────────────────────────────────────────

    private void ApplyFOV()
    {
        if (playerCamera == null) return;

        float targetFOV = baseFOV + Blend(previousProfile.fovBoost, currentProfile.fovBoost);
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            Time.deltaTime * Blend(previousProfile.fovTransitionSpeed, currentProfile.fovTransitionSpeed)
        );
    }

    // ─────────────────────────────────────────────
    // APPLY AUDIO VOLUME
    // ─────────────────────────────────────────────

    private void ApplyAudioVolume()
    {
        if (terrainAudioSource != null && terrainAudioSource.isPlaying)
            terrainAudioSource.volume = Blend(previousProfile.ambientVolume, currentProfile.ambientVolume);
    }

    // ─────────────────────────────────────────────
    // CAMERA ROTATION — single write, owns everything
    // ─────────────────────────────────────────────

    private void ApplyCameraRotation()
    {
        if (cameraRig == null) return;

        // ── Shake ──
        float shakeIntensity = Blend(previousProfile.shakeIntensity, currentProfile.shakeIntensity);
        float shakeFrequency = Blend(previousProfile.shakeFrequency, currentProfile.shakeFrequency);

        shakeTimer += Time.deltaTime * shakeFrequency;

        float targetShakeX = (Mathf.PerlinNoise(shakeTimer, 0.3f) - 0.5f) * 2f * shakeIntensity;
        float targetShakeY = (Mathf.PerlinNoise(0.7f, shakeTimer) - 0.5f) * 2f * shakeIntensity;

        smoothShakeX = Mathf.Lerp(smoothShakeX, targetShakeX, Time.deltaTime * 20f);
        smoothShakeY = Mathf.Lerp(smoothShakeY, targetShakeY, Time.deltaTime * 20f);

        // ── Wobble ──
        float wobbleIntensity = Blend(previousProfile.wobbleIntensity, currentProfile.wobbleIntensity);
        float wobbleFrequency = Blend(previousProfile.wobbleFrequency, currentProfile.wobbleFrequency);

        wobbleTimer += Time.deltaTime * wobbleFrequency;
        float targetWobbleZ = Mathf.Sin(wobbleTimer * Mathf.PI * 2f) * wobbleIntensity;
        smoothWobbleZ = Mathf.Lerp(smoothWobbleZ, targetWobbleZ, Time.deltaTime * 5f);

        // ── Tilt from ScreenTilt ──
        float tilt = scooterController != null ? scooterController.CurrentCamTilt : 0f;

        // ── Single combined write ──
        // X = shake up/down, Y = base rotation + shake side, Z = tilt + wobble
        cameraRig.localRotation = Quaternion.Euler(
            smoothShakeX,
            cameraBaseRotationY + smoothShakeY,
            tilt + smoothWobbleZ
        );
    }
}