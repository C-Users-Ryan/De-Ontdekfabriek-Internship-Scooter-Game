using UnityEngine;

/// <summary>
/// VehicleBehaviour — Base class for all traffic vehicles.
///
/// MOVEMENT MODEL:
///   Everything moves in -Z (toward the player's starting point).
///   Centre line is at x = 0.
///
///   LEFT lane  (x negative) — same-direction traffic
///     zSpeed = WorldScrollSpeed - ownSpeed
///     Player is faster so these drift backward past the player.
///     Visual orientation: facing -Z (Y rotation = 0)
///
///   RIGHT lane (x positive) — oncoming traffic
///     zSpeed = WorldScrollSpeed + ownSpeed
///     These rush toward the player quickly.
///     Visual orientation: facing +Z (Y rotation = 180) — nose points at player
///
/// ORIENTATION NOTE:
///   Unity's default forward is +Z.
///   A vehicle prefab built facing +Z will look correct for ONCOMING with Y=180.
///   If your prefab faces a different default direction, adjust prefabForwardOffset.
///
/// HOW TO ADD A NEW VEHICLE TYPE:
///   1. Create a script inheriting VehicleBehaviour.
///   2. Override OnBehaviourUpdate() for custom lateral movement.
///   3. Add the prefab to RunnerGameConfig.vehiclePrefabs[].
/// </summary>
[RequireComponent(typeof(Collider))]
public class VehicleBehaviour : MonoBehaviour
{
    [Header("Vehicle Settings")]
    [Tooltip("Set by TrafficManager at spawn. True = oncoming (right lane). False = same-dir (left lane).")]
    public bool isOncoming = true;

    [Tooltip("The vehicle's own speed in m/s.\n" +
             "Oncoming:  adds to WorldScrollSpeed → rushes at player.\n" +
             "Same-dir:  subtracted from WorldScrollSpeed → player overtakes.\n" +
             "Keep same-dir values LOW (3-5) so player always overtakes.")]
    public float baseSpeed = 5f;

    [Header("Orientation")]
    [Tooltip("Extra Y rotation applied on top of the lane rotation.\n" +
             "Use this if your prefab mesh faces the wrong way by default.\n" +
             "0   = prefab already faces +Z (Unity default forward)\n" +
             "90  = prefab faces +X by default\n" +
             "-90 = prefab faces -X by default\n" +
             "180 = prefab faces -Z by default")]
    public float prefabForwardOffset = 0f;

    [Header("Despawn")]
    [Tooltip("Z position behind the player at which this vehicle is destroyed.")]
    public float despawnBehindZ = -25f;

    // ── Protected ─────────────────────────────────────────────────
    protected float SpawnX;
    protected float CurrentSpeed;
    protected float WorldScrollSpeed => RoadSpawner.Instance != null
                                        ? RoadSpawner.Instance.WorldScrollSpeed
                                        : 6f;

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    protected virtual void Start()
    {
        SpawnX = transform.position.x;
        CurrentSpeed = baseSpeed;

        ApplyOrientation();

        if (RunnerGameManager.Instance != null)
        {
            RunnerGameManager.Instance.OnGameOver.AddListener(OnGameOver);
            RunnerGameManager.Instance.OnChargingBegin.AddListener(OnPause);
            RunnerGameManager.Instance.OnChargingEnd.AddListener(OnResume);
        }
    }

    protected virtual void OnDestroy()
    {
        if (RunnerGameManager.Instance == null) return;
        RunnerGameManager.Instance.OnGameOver.RemoveListener(OnGameOver);
        RunnerGameManager.Instance.OnChargingBegin.RemoveListener(OnPause);
        RunnerGameManager.Instance.OnChargingEnd.RemoveListener(OnResume);
    }

    void Update()
    {
        if (!RunnerGameManager.Instance.IsPlaying()) return;

        MoveVehicle();
        OnBehaviourUpdate();
        CheckDespawn();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Orientation

    void ApplyOrientation()
    {
        // Same-direction: facing -Z (driving away from player, same direction)
        // Y = 0 + prefabOffset  →  nose points in -Z when moving
        //
        // Oncoming: facing +Z (nose points toward the player)
        // Y = 180 + prefabOffset
        //
        // Both vehicles move in -Z regardless of which way they face visually.
        // The visual rotation is purely cosmetic.

        float laneRotation = isOncoming ? 180f : 0f;
        transform.rotation = Quaternion.Euler(0f, laneRotation + prefabForwardOffset, 0f);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Movement

    void MoveVehicle()
    {
        float zSpeed;

        if (isOncoming)
            // Oncoming: world scroll + own speed = rushes at player
            zSpeed = WorldScrollSpeed + CurrentSpeed;
        else
            // Same-dir: world scroll - own speed = player slowly overtakes
            zSpeed = Mathf.Max(0f, WorldScrollSpeed - CurrentSpeed);

        transform.position += Vector3.back * zSpeed * Time.deltaTime;
    }

    void CheckDespawn()
    {
        if (transform.position.z < despawnBehindZ)
            Destroy(gameObject);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Overridable

    /// <summary>Override in subclasses to add custom lateral movement each frame.</summary>
    protected virtual void OnBehaviourUpdate() { }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Events

    protected virtual void OnGameOver() { enabled = false; }
    protected virtual void OnPause() { enabled = false; }
    protected virtual void OnResume() { enabled = true; }

    #endregion
}