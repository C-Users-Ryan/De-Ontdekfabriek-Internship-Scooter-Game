using UnityEngine;

/// <summary>
/// TerrainProfile — A ScriptableObject that defines how the scooter
/// behaves on a specific terrain type.
///
/// HOW TO CREATE A NEW TERRAIN:
///   Right-click in the Project window → Create → Scooter → Terrain Profile
///   Name it (e.g. "Tarmac", "Dirt", "Gravel", "Mud") and tune the values.
/// </summary>
[CreateAssetMenu(fileName = "NewTerrainProfile", menuName = "Scooter/Terrain Profile")]
public class TerrainProfile : ScriptableObject
{
    [Header("General")]
    [Tooltip("Display name for this terrain (used in debug/UI).")]
    public string terrainName = "New Terrain";

    // ─────────────────────────────────────────────
    // MOVEMENT / CONTROLS
    // ─────────────────────────────────────────────

    [Header("Controls Feel")]
    [Tooltip("Side movement speed multiplier. 1 = normal, <1 = sluggish (mud), >1 = snappy (tarmac).")]
    [Range(0.2f, 2f)]
    public float sideMoveSpeedMultiplier = 1f;

    [Tooltip("Side acceleration multiplier. Lower = harder to steer, like loose gravel or mud.")]
    [Range(0.2f, 2f)]
    public float sideAccelerationMultiplier = 1f;

    // ─────────────────────────────────────────────
    // SPEED FEEL
    // ─────────────────────────────────────────────

    [Header("Speed Feel")]
    [Tooltip("Camera FOV added on top of base FOV. Positive = faster feel, negative = slower/heavier.")]
    [Range(-20f, 30f)]
    public float fovBoost = 0f;

    [Tooltip("How fast the FOV transitions to this terrain's target. Higher = snappier.")]
    [Range(1f, 10f)]
    public float fovTransitionSpeed = 4f;

    // ─────────────────────────────────────────────
    // CAMERA SHAKE
    // ─────────────────────────────────────────────

    [Header("Camera Shake")]
    [Tooltip("Intensity of random camera shake. 0 = none (smooth tarmac), 1 = heavy (rocky offroad).")]
    [Range(0f, 1f)]
    public float shakeIntensity = 0f;

    [Tooltip("Speed of the camera shake oscillation. Higher = more frantic.")]
    [Range(0f, 30f)]
    public float shakeFrequency = 10f;

    [Tooltip("A slow persistent sway added on top of shake — feels like uneven ground, not random jitter.")]
    [Range(0f, 1f)]
    public float wobbleIntensity = 0f;

    [Tooltip("Speed of the persistent wobble cycle.")]
    [Range(0f, 5f)]
    public float wobbleFrequency = 1f;

    // ─────────────────────────────────────────────
    // BODY LEAN
    // ─────────────────────────────────────────────

    [Header("Body Lean")]
    [Tooltip("Multiplier on the scooter body lean angle. 1 = normal, <1 = stiffer, >1 = more dramatic.")]
    [Range(0.2f, 2f)]
    public float leanAngleMultiplier = 1f;

    [Tooltip("Multiplier on lean speed. Lower = sluggish lean response on slippery terrain.")]
    [Range(0.2f, 2f)]
    public float leanSpeedMultiplier = 1f;

    // ─────────────────────────────────────────────
    // SOUND
    // ─────────────────────────────────────────────

    [Header("Sound (optional)")]
    [Tooltip("Ambient terrain sound (gravel crunch, dirt rumble, etc). Leave empty if unused.")]
    public AudioClip terrainAmbientSound;

    [Tooltip("Volume of the terrain ambient sound.")]
    [Range(0f, 1f)]
    public float ambientVolume = 0.5f;

    [Tooltip("Pitch of the terrain ambient sound. Tweak for variation.")]
    [Range(0.5f, 2f)]
    public float ambientPitch = 1f;


    // ─────────────────────────────────────────────
    // BLEND
    // ─────────────────────────────────────────────

    [Header("Transition")]
    [Tooltip("How many seconds it takes to fully blend into this terrain from another.")]
    [Range(0.1f, 3f)]
    public float blendDuration = 0.5f;
}
