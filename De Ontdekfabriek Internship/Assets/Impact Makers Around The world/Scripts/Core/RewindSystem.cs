using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Rewinds the game world by 3–4 seconds after a hard collision that would otherwise
    /// end the run. The student sees the crash play out in black-and-white, loses points
    /// for dangerous driving, then the world steps backward so they can try again.
    ///
    /// WHY THIS EXISTS (RELAY GAME CONTEXT):
    ///   This is a school relay game. One student's hard crash should NOT end the session
    ///   for the whole class — other students still need to reach the checkpoint.
    ///   A rewind keeps the session going, applies a real point penalty, and still
    ///   communicates the lesson ("that driving was dangerous") without total failure.
    ///   Precedents: Forza Motorsport rewind (2009), GRID flashback, Trackmania respawn.
    ///   Toggle useRewindOnCrash = false in GameManager to restore hard game-over behavior.
    ///
    /// HOW IT WORKS:
    ///   Every captureInterval seconds, RecordSnapshot() saves:
    ///     - WorldSpeed.Current
    ///     - Player lateral (X) position
    ///     - World position of every active traffic vehicle (by instance ID)
    ///   On TriggerRewind():
    ///     1. GameManager enters GameState.Rewinding → all Update loops pause.
    ///     2. Post-processing volume ramps to full B&W desaturation (rewindBwDuration).
    ///     3. World state lerps backward through the ring buffer (rewindDuration).
    ///     4. Score deduction fires with label "CRASH!" → ScorePopupUI shows it.
    ///     5. Post-processing ramps back to colour.
    ///     6. GameManager returns to GameState.Playing.
    ///
    /// RING BUFFER:
    ///   Allocation-free after Awake — struct array, no List/Dictionary at runtime.
    ///   BUFFER_SIZE = 32 snapshots. At captureInterval = 0.15s → 4.8 seconds of history.
    ///   Only the most recent rewindDurationSec / captureInterval snapshots are used.
    ///
    /// TRAFFIC VEHICLE DESYNC:
    ///   Vehicles that spawned during the rewound window are returned to the pool.
    ///   Vehicles that existed before the rewind are moved back to their recorded positions.
    ///   TrafficManager.ReturnToPool(vehicle) handles the deactivation.
    ///
    /// REWIND LIMIT:
    ///   maxRewindsPerRound caps how many times the rewind fires in one student turn.
    ///   After the cap is reached, hard hits fall back to the GraceSystem (light hits) or
    ///   TriggerGameOver (hard hits). This prevents the mechanic from feeling consequence-free.
    ///
    /// SETUP:
    ///   1. Attach to same GameObject as GameManager.
    ///   2. Create a Unity URP Global Volume (Weight = 0) with a ColorAdjustments override
    ///      that has Saturation = -100. Assign it to bwVolume.
    ///   3. Assign trafficManager so RewindSystem can return out-of-history vehicles to pool.
    ///   4. Assign playerTransform.
    /// </summary>
    public class RewindSystem : MonoBehaviour
    {
        // ── Tuning ─────────────────────────────────────────────────────────────

        [Header("Rewind Behaviour")]
        [Tooltip("How far back (seconds) to rewind after a hard hit. 3–4 is comfortable.")]
        public float rewindDurationSec   = 3.5f;

        [Tooltip("Maximum rewinds allowed per student turn. After the cap, hard hits become fatal.")]
        public int maxRewindsPerRound    = 2;

        [Tooltip("Points deducted on each rewind. Fires with 'CRASH!' label for popup.")]
        public int rewindPenaltyPoints   = 80;

        [Header("Animation Timing")]
        [Tooltip("Seconds to ramp to full B&W before rewinding.")]
        public float rewindBwRampIn      = 0.4f;
        [Tooltip("Duration of the actual world rewind playback.")]
        public float rewindPlaybackSec   = 1.2f;
        [Tooltip("Seconds to return to colour after the rewind completes.")]
        public float rewindBwRampOut     = 0.5f;

        [Header("Snapshot Rate")]
        [Tooltip("Seconds between history snapshots. Lower = smoother rewind, more work per frame.")]
        [Range(0.05f, 0.3f)]
        public float captureInterval     = 0.15f;

        [Header("References")]
        public Transform       playerTransform;
        public ScoreManager    scoreManager;
        // trafficManager removed — vehicle access uses TrafficVehicle.Active static list directly

        [Tooltip("A URP Global Volume (Weight=0) with ColorAdjustments Saturation=-100. "
               + "RewindSystem lerps its Weight to create the B&W flash.")]
        public Volume          bwVolume;

        // ── State ──────────────────────────────────────────────────────────────

        public bool IsRewinding { get; private set; }
        public int  RewindsUsed { get; private set; }
        public bool RewindAvailable => RewindsUsed < maxRewindsPerRound;

        // ── Ring buffer ────────────────────────────────────────────────────────

        private const int BUFFER_SIZE = 32;

        private struct VehicleState
        {
            public int     instanceId;
            public Vector3 position;
        }

        private struct Snapshot
        {
            public float        worldSpeed;
            public float        playerX;
            public VehicleState[] vehicles; // allocated once per slot in Awake
            public int          vehicleCount;
            public float        timestamp;
        }

        private Snapshot[] _buffer;
        private int        _head;   // index of the most-recent snapshot
        private int        _count;  // how many snapshots are valid

        // Pre-fetch active vehicles list to avoid per-frame allocation
        private readonly List<TrafficVehicle> _tempVehicles = new(16);

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Awake()
        {
            // Pre-allocate vehicle state arrays so recording is GC-free
            _buffer = new Snapshot[BUFFER_SIZE];
            for (int i = 0; i < BUFFER_SIZE; i++)
                _buffer[i].vehicles = new VehicleState[16]; // supports up to 16 active vehicles

            if (bwVolume != null) bwVolume.weight = 0f;
        }

        private float _captureTimer;

        void Update()
        {
            // Don't record during rewind or non-playing states
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

            _captureTimer += Time.deltaTime;
            if (_captureTimer >= captureInterval)
            {
                _captureTimer = 0f;
                RecordSnapshot();
            }
        }

        // ── Snapshot recording ─────────────────────────────────────────────────

        private void RecordSnapshot()
        {
            _head = (_head + 1) % BUFFER_SIZE;
            if (_count < BUFFER_SIZE) _count++;

            ref Snapshot snap = ref _buffer[_head];
            snap.timestamp   = Time.time;
            snap.worldSpeed  = WorldSpeed.Instance != null ? WorldSpeed.Instance.Current : 0f;
            // Project onto the current steer axis so rewind works correctly after road turns
            snap.playerX     = playerTransform != null
                             ? Vector3.Dot(playerTransform.position, RoadDirection.SteerpAxis)
                             : 0f;

            // TrafficVehicle.Active is the authoritative list of all active vehicles
            _tempVehicles.Clear();
            _tempVehicles.AddRange(TrafficVehicle.Active);
            snap.vehicleCount = Mathf.Min(_tempVehicles.Count, snap.vehicles.Length);
            for (int i = 0; i < snap.vehicleCount; i++)
            {
                snap.vehicles[i].instanceId = _tempVehicles[i].gameObject.GetInstanceID();
                snap.vehicles[i].position   = _tempVehicles[i].transform.position;
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called by GameManager when a hard hit occurs and useRewindOnCrash is enabled.
        /// Returns true if the rewind was triggered, false if the cap was reached.
        /// GameManager calls TriggerGameOver() when this returns false.
        /// </summary>
        public bool TriggerRewind()
        {
            if (IsRewinding || !RewindAvailable) return false;
            StartCoroutine(RewindSequence());
            return true;
        }

        /// <summary>
        /// Called by CheckpointManager / GameManager.StartGame() to reset the per-turn counter.
        /// </summary>
        public void ResetRewindCount()
        {
            RewindsUsed = 0;
        }

        // ── Rewind coroutine ───────────────────────────────────────────────────

        private IEnumerator RewindSequence()
        {
            IsRewinding = true;
            RewindsUsed++;

            // Enter Rewinding state — pauses SpeedMonitor, RewardSystem, etc.
            GameManager.Instance?.SetState(GameManager.GameState.Rewinding);

            // Freeze world movement immediately
            WorldSpeed.Instance?.OverrideSpeed(0f);

            // ── 1. Ramp to B&W ─────────────────────────────────────────────
            yield return StartCoroutine(LerpBwVolume(0f, 1f, rewindBwRampIn));

            // ── 2. Deduct points for the crash ─────────────────────────────
            if (rewindPenaltyPoints > 0)
                scoreManager?.AddPoints(-rewindPenaltyPoints, "CRASH!");

            // ── 3. Determine which snapshot to rewind to ────────────────────
            int targetIndex = FindRewindTargetIndex(rewindDurationSec);

            // ── 4. Play rewind animation — lerp world back through snapshots ─
            yield return StartCoroutine(PlayRewind(targetIndex, rewindPlaybackSec));

            // ── 5. Apply the target snapshot as the restored state ──────────
            ApplySnapshot(targetIndex);

            // ── 6. Ramp back to colour ──────────────────────────────────────
            yield return StartCoroutine(LerpBwVolume(1f, 0f, rewindBwRampOut));

            // ── 7. Resume ──────────────────────────────────────────────────
            WorldSpeed.Instance?.ClearOverride();
            GameManager.Instance?.SetState(GameManager.GameState.Playing);
            IsRewinding = false;
        }

        private int FindRewindTargetIndex(float targetAgeSec)
        {
            // Walk backward from head until we find a snapshot old enough
            for (int i = 0; i < _count; i++)
            {
                int idx = ((_head - i) + BUFFER_SIZE) % BUFFER_SIZE;
                if (Time.time - _buffer[idx].timestamp >= targetAgeSec)
                    return idx;
            }
            // Fallback: oldest available snapshot
            return ((_head - (_count - 1)) + BUFFER_SIZE) % BUFFER_SIZE;
        }

        private IEnumerator PlayRewind(int targetIndex, float duration)
        {
            float elapsed = 0f;
            int   startIdx = _head;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Lerp between head snapshot and target snapshot
                Snapshot a = _buffer[startIdx];
                Snapshot b = _buffer[targetIndex];

                float speed = Mathf.Lerp(a.worldSpeed, b.worldSpeed, t);
                WorldSpeed.Instance?.OverrideSpeed(speed);

                if (playerTransform != null)
                {
                    Vector3 axis       = RoadDirection.SteerpAxis;
                    float   curSteer   = Vector3.Dot(playerTransform.position, axis);
                    float   destSteer  = Mathf.Lerp(a.playerX, b.playerX, t);
                    playerTransform.position = playerTransform.position
                        - curSteer  * axis
                        + destSteer * axis;
                }

                yield return null;
            }
        }

        private void ApplySnapshot(int index)
        {
            ref Snapshot snap = ref _buffer[index];

            // Restore world speed
            WorldSpeed.Instance?.OverrideSpeed(snap.worldSpeed);

            // Restore player lateral position along the current steer axis
            if (playerTransform != null)
            {
                Vector3 axis     = RoadDirection.SteerpAxis;
                float   curSteer = Vector3.Dot(playerTransform.position, axis);
                playerTransform.position = playerTransform.position
                    - curSteer    * axis
                    + snap.playerX * axis;
            }

            // Restore or despawn traffic vehicles via TrafficVehicle.Active static list
            _tempVehicles.Clear();
            _tempVehicles.AddRange(TrafficVehicle.Active);

            foreach (var vehicle in _tempVehicles)
            {
                int id = vehicle.gameObject.GetInstanceID();
                bool found = false;
                for (int i = 0; i < snap.vehicleCount; i++)
                {
                    if (snap.vehicles[i].instanceId == id)
                    {
                        vehicle.transform.position = snap.vehicles[i].position;
                        found = true;
                        break;
                    }
                }
                // Vehicle spawned during the rewound window — return it to the pool
                if (!found)
                    vehicle.Deactivate();
            }

            // Reset ring buffer head to target so new recording continues from here
            _head  = index;
            _count = Mathf.Min(_count, BUFFER_SIZE / 2);
        }

        private IEnumerator LerpBwVolume(float from, float to, float duration)
        {
            if (bwVolume == null) { yield break; }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; // unscaled — works even if timeScale changes
                bwVolume.weight = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            bwVolume.weight = to;
        }
    }
}
