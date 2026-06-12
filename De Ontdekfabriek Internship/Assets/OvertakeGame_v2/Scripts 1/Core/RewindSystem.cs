using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Post-crash rewind. On a hard collision, the last 3–4 seconds of world state are
    /// replayed in reverse with a B&W visual, then the world resumes from the rewound position.
    /// This keeps relay sessions alive — a crash becomes a setback, not a session-ender.
    ///
    /// SETUP:
    ///   1. Attach to the same GameObject as GameManager.
    ///   2. Assign playerTransform.
    ///   3. Create a URP Global Volume (Weight=0) with ColorAdjustments Saturation=-100.
    ///      Assign it to bwVolume.
    ///   4. Wire rewindSystem in GameManager Inspector.
    /// </summary>
    public class RewindSystem : MonoBehaviour
    {
        [Header("Rewind Behaviour")]
        [Tooltip("How far back (seconds) to restore after a hard hit.")]
        public float rewindDurationSec    = 3.5f;
        [Tooltip("Maximum rewinds per student turn. After the cap, hard hits trigger game over.")]
        public int   maxRewindsPerRound   = 2;
        [Tooltip("Points deducted per rewind.")]
        public int   rewindPenaltyPoints  = 80;

        [Header("Animation Timing")]
        public float rewindBwRampIn       = 0.4f;
        public float rewindPlaybackSec    = 1.2f;
        public float rewindBwRampOut      = 0.5f;

        [Header("Snapshot Rate")]
        [Range(0.05f, 0.3f)]
        [Tooltip("Seconds between history snapshots. Lower = smoother rewind.")]
        public float captureInterval      = 0.15f;

        [Header("References")]
        public Transform  playerTransform;
        public ScoreManager scoreManager;
        [Tooltip("URP Global Volume (Weight=0) with ColorAdjustments Saturation=-100.")]
        public Volume     bwVolume;

        // ── State ──────────────────────────────────────────────────────────────
        public bool IsRewinding     { get; private set; }
        public int  RewindsUsed     { get; private set; }
        public bool RewindAvailable => RewindsUsed < maxRewindsPerRound;

        // ── Ring buffer ────────────────────────────────────────────────────────
        private const int BUFFER_SIZE = 32;

        private struct VehicleState { public int instanceId; public Vector3 position; }

        private struct Snapshot
        {
            public float          worldSpeed;
            public float          playerSteer;   // dot(position, SteerAxis)
            public VehicleState[] vehicles;
            public int            vehicleCount;
            public float          timestamp;
        }

        private Snapshot[]             _buffer;
        private int                    _head;
        private int                    _count;
        private readonly List<TrafficVehicle> _tempVehicles = new(16);
        private float                  _captureTimer;

        void Awake()
        {
            _buffer = new Snapshot[BUFFER_SIZE];
            for (int i = 0; i < BUFFER_SIZE; i++)
                _buffer[i].vehicles = new VehicleState[16];
            if (bwVolume != null) bwVolume.weight = 0f;
        }

        void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
            _captureTimer += Time.deltaTime;
            if (_captureTimer >= captureInterval) { _captureTimer = 0f; RecordSnapshot(); }
        }

        // ── Recording ──────────────────────────────────────────────────────────

        private void RecordSnapshot()
        {
            _head = (_head + 1) % BUFFER_SIZE;
            if (_count < BUFFER_SIZE) _count++;

            ref Snapshot snap = ref _buffer[_head];
            snap.timestamp   = Time.time;
            snap.worldSpeed  = WorldSpeed.Instance != null ? WorldSpeed.Instance.Current : 0f;
            snap.playerSteer = playerTransform != null
                             ? Vector3.Dot(playerTransform.position, RoadDirection.SteerAxis)
                             : 0f;

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
        /// Attempt to trigger a rewind. Returns false if the per-round cap is reached;
        /// GameManager should call TriggerGameOver() in that case.
        /// </summary>
        public bool TriggerRewind()
        {
            if (IsRewinding || !RewindAvailable) return false;
            StartCoroutine(RewindSequence());
            return true;
        }

        public void ResetRewindCount() => RewindsUsed = 0;

        // ── Rewind coroutine ───────────────────────────────────────────────────

        private IEnumerator RewindSequence()
        {
            IsRewinding = true;
            RewindsUsed++;
            GameManager.Instance?.SetState(GameManager.GameState.Rewinding);
            WorldSpeed.Instance?.OverrideSpeed(0f);

            yield return StartCoroutine(LerpBwVolume(0f, 1f, rewindBwRampIn));

            if (rewindPenaltyPoints > 0)
                scoreManager?.AddPoints(-rewindPenaltyPoints, "CRASH!");

            int targetIndex = FindRewindTargetIndex(rewindDurationSec);
            yield return StartCoroutine(PlayRewind(targetIndex, rewindPlaybackSec));
            ApplySnapshot(targetIndex);

            yield return StartCoroutine(LerpBwVolume(1f, 0f, rewindBwRampOut));

            WorldSpeed.Instance?.ClearOverride();
            GameManager.Instance?.SetState(GameManager.GameState.Playing);
            IsRewinding = false;
        }

        private int FindRewindTargetIndex(float targetAgeSec)
        {
            for (int i = 0; i < _count; i++)
            {
                int idx = ((_head - i) + BUFFER_SIZE) % BUFFER_SIZE;
                if (Time.time - _buffer[idx].timestamp >= targetAgeSec) return idx;
            }
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

                ref Snapshot a = ref _buffer[startIdx];
                ref Snapshot b = ref _buffer[targetIndex];

                WorldSpeed.Instance?.OverrideSpeed(Mathf.Lerp(a.worldSpeed, b.worldSpeed, t));

                if (playerTransform != null)
                {
                    Vector3 axis       = RoadDirection.SteerAxis;
                    float   curSteer   = Vector3.Dot(playerTransform.position, axis);
                    float   destSteer  = Mathf.Lerp(a.playerSteer, b.playerSteer, t);
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
            WorldSpeed.Instance?.OverrideSpeed(snap.worldSpeed);

            if (playerTransform != null)
            {
                Vector3 axis     = RoadDirection.SteerAxis;
                float   curSteer = Vector3.Dot(playerTransform.position, axis);
                playerTransform.position = playerTransform.position
                                         - curSteer       * axis
                                         + snap.playerSteer * axis;
            }

            _tempVehicles.Clear();
            _tempVehicles.AddRange(TrafficVehicle.Active);
            foreach (var vehicle in _tempVehicles)
            {
                int  id    = vehicle.gameObject.GetInstanceID();
                bool found = false;
                for (int i = 0; i < snap.vehicleCount; i++)
                {
                    if (snap.vehicles[i].instanceId == id)
                    {
                        vehicle.transform.position = snap.vehicles[i].position;
                        found = true; break;
                    }
                }
                if (!found) vehicle.Deactivate();
            }

            _head  = index;
            _count = Mathf.Min(_count, BUFFER_SIZE / 2);
        }

        private IEnumerator LerpBwVolume(float from, float to, float duration)
        {
            if (bwVolume == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                bwVolume.weight = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            bwVolume.weight = to;
        }
    }
}
