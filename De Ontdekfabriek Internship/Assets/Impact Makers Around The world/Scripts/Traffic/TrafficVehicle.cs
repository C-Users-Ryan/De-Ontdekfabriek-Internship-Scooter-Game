using UnityEngine;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// A traffic vehicle with driver AI — follows turns, reacts to the player, has personality.
    ///
    /// WHAT CHANGED FROM THE ORIGINAL:
    ///   - Movement uses RoadDirection.Current → vehicles follow turns automatically.
    ///   - Lateral positioning uses RoadDirection.SteerpAxis → works on any travel axis.
    ///   - Driver personality system: Normal / Aggressive / Cautious / Distracted.
    ///   - Same-direction: yields toward road edge when player approaches from behind.
    ///   - Same-direction: distracted drivers drift in lane and vary speed randomly.
    ///   - Oncoming: slight swerve away if player enters their lane at close range.
    ///   - All lateral motion uses the steer axis, not hardcoded X — survives 90° turns.
    ///
    /// SAME-DIRECTION SPEED MODEL (unchanged from original):
    ///   In the world-moves model, same-dir vehicles appear slower because:
    ///   moveSpeed = WorldSpeed - ownSpeed   → world overtakes them → player catches up
    ///   Oncoming appear faster:
    ///   moveSpeed = WorldSpeed + ownSpeed   → approaching head-on
    ///
    /// PERSONALITY SUMMARY:
    ///   Aggressive  — high ownSpeed, small yield, barely notices player
    ///   Cautious    — low ownSpeed, large yield, yields early
    ///   Distracted  — drifts in lane, changes speed randomly, delayed reaction
    ///   Normal      — baseline everything
    /// </summary>
    public class TrafficVehicle : MonoBehaviour
    {
        // ── Static shared state — avoids FindObjectsByType in hot paths ────────
        /// <summary>All currently active vehicles. OvertakeDetector reads this instead of FindObjectsByType.</summary>
        public static readonly List<TrafficVehicle> Active = new();

        /// <summary>Set once by TrafficManager.Start() so Activate() never calls FindFirstObjectByType.</summary>
        public static Transform SharedPlayer { get; private set; }
        public static void SetSharedPlayer(Transform t) => SharedPlayer = t;

        [Header("Vehicle State")]
        public float ownSpeed;
        public bool  isOncoming;

        // ── Driver AI state (set on Activate) ─────────────────────────────────
        private DriverPersonality _personality;
        private float _awarenessRange;      // metres behind — same-dir yield trigger
        private float _yieldStrength;       // how far sideways they shift (metres)
        private float _jitter;              // lane-drift amplitude (metres)
        private float _speedVarianceFrac;   // fraction of ownSpeed for random variation

        // ── Runtime state ──────────────────────────────────────────────────────
        private bool      _active;
        private float     _laneSteerPos;    // spawn-time lateral position along SteerpAxis
        private float     _jitterOffset;    // current drift from lane centre (smooth)
        private float     _jitterPhase;
        private float     _speedDelta;      // current random speed offset
        private float     _speedTimer;      // countdown to next speed change
        private Transform _player;

        // ── Activate / Deactivate ─────────────────────────────────────────────

        public void Activate(Vector3 position, float speed, bool oncoming,
                             DriverPersonality personality = DriverPersonality.Normal)
        {
            transform.position = position;
            ownSpeed    = speed;
            isOncoming  = oncoming;
            _personality = personality;
            // Use the shared reference set by TrafficManager.Start() — never FindFirstObjectByType
            _player = SharedPlayer;

            // Lane centre — projected onto steer axis at activation time
            _laneSteerPos = Vector3.Dot(position, RoadDirection.SteerpAxis);

            // Facing — same-dir faces player's "forward", oncoming faces toward player
            Vector3 facing = isOncoming ? RoadDirection.Current : -RoadDirection.Current;
            if (facing != Vector3.zero) transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

            // Personality traits
            switch (_personality)
            {
                case DriverPersonality.Aggressive:
                    _awarenessRange    = 10f;
                    _yieldStrength     = 0.2f;
                    _jitter            = 0.05f;
                    _speedVarianceFrac = 0.25f;
                    break;
                case DriverPersonality.Cautious:
                    _awarenessRange    = 30f;
                    _yieldStrength     = 0.9f;
                    _jitter            = 0.08f;
                    _speedVarianceFrac = 0.08f;
                    break;
                case DriverPersonality.Distracted:
                    _awarenessRange    = 8f;
                    _yieldStrength     = 0.3f;
                    _jitter            = 0.5f;
                    _speedVarianceFrac = 0.45f;
                    break;
                default: // Normal
                    _awarenessRange    = 20f;
                    _yieldStrength     = 0.55f;
                    _jitter            = 0.15f;
                    _speedVarianceFrac = 0.12f;
                    break;
            }

            _jitterOffset = 0f;
            _jitterPhase  = Random.Range(0f, Mathf.PI * 2f);
            _speedDelta   = 0f;
            _speedTimer   = Random.Range(1.5f, 5f);

            gameObject.SetActive(true);
            _active = true;
            Active.Add(this);  // register for OvertakeDetector
        }

        public void Deactivate()
        {
            Active.Remove(this);  // unregister before deactivating
            _active = false;
            gameObject.SetActive(false);
        }

        // ── Update ────────────────────────────────────────────────────────────

        void Update()
        {
            if (!_active || WorldSpeed.Instance == null) return;

            float worldSpd  = WorldSpeed.Instance.Current;
            float moveSpeed = isOncoming
                ? worldSpd + ownSpeed
                : Mathf.Max(0f, worldSpd - ownSpeed + _speedDelta);

            // ── Primary movement along road direction ──
            transform.Translate(RoadDirection.Current * moveSpeed * Time.deltaTime, Space.World);

            // ── Driver behaviour ──
            if (isOncoming)
                UpdateOncoming(worldSpd);
            else
                UpdateSameDirection();

            // ── Lateral drift (personality jitter + yield offset) ──
            ApplyLateralOffset();
        }

        // ── Same-direction behaviour ──────────────────────────────────────────

        private void UpdateSameDirection()
        {
            // Random speed variation (distracted drivers change more often and more wildly)
            _speedTimer -= Time.deltaTime;
            if (_speedTimer <= 0f)
            {
                float range = ownSpeed * _speedVarianceFrac;
                _speedDelta = Random.Range(-range, range);
                _speedTimer = _personality == DriverPersonality.Distracted
                    ? Random.Range(1f, 3f)
                    : Random.Range(3f, 8f);
            }

            if (_player == null) return;

            // Is the player behind us and in roughly the same lane?
            Vector3 toPlayer  = _player.position - transform.position;
            float   behindDot = Vector3.Dot(toPlayer, RoadDirection.Current); // positive = player is "behind" (in road-travel direction)
            float   lateralDot= Vector3.Dot(toPlayer, RoadDirection.SteerpAxis);

            bool playerIsBehind = behindDot > 0f && behindDot < _awarenessRange;
            bool playerInLane   = Mathf.Abs(lateralDot) < 2.2f;

            if (playerIsBehind && playerInLane)
            {
                // Yield: drift toward the edge of the road (away from road centre)
                float edgeSign   = _laneSteerPos >= 0f ? 1f : -1f;
                float yieldTarget = _laneSteerPos + edgeSign * _yieldStrength;
                _jitterOffset = Mathf.Lerp(_jitterOffset, yieldTarget - _laneSteerPos, 4f * Time.deltaTime);
            }
            else
            {
                // Return toward lane centre when player isn't close behind
                _jitterOffset = Mathf.Lerp(_jitterOffset, 0f, 1.5f * Time.deltaTime);
            }
        }

        // ── Oncoming behaviour ────────────────────────────────────────────────

        private void UpdateOncoming()
        {
            UpdateOncoming(WorldSpeed.Instance != null ? WorldSpeed.Instance.Current : 10f);
        }

        private void UpdateOncoming(float worldSpd)
        {
            if (_player == null) return;

            Vector3 toPlayer   = _player.position - transform.position;
            float   distSqr    = toPlayer.sqrMagnitude;

            // Swerve if player is in our lane at close range
            if (distSqr < 18f * 18f)
            {
                float lateralDot = Vector3.Dot(toPlayer, RoadDirection.SteerpAxis);
                // Player is on the same side as us — swerve away
                bool playerOnOurSide = (_laneSteerPos >= 0f && lateralDot > -0.5f)
                                    || (_laneSteerPos <  0f && lateralDot <  0.5f);
                if (playerOnOurSide)
                {
                    float swerveDir = _laneSteerPos >= 0f ? 1f : -1f;
                    float urgency   = Mathf.InverseLerp(18f * 18f, 5f * 5f, distSqr);
                    float target    = swerveDir * 0.7f * urgency;
                    _jitterOffset = Mathf.Lerp(_jitterOffset, target, 6f * Time.deltaTime);
                }
                else
                {
                    _jitterOffset = Mathf.Lerp(_jitterOffset, 0f, 3f * Time.deltaTime);
                }
            }
            else
            {
                _jitterOffset = Mathf.Lerp(_jitterOffset, 0f, 2f * Time.deltaTime);
            }
        }

        // ── Lateral drift ─────────────────────────────────────────────────────

        private void ApplyLateralOffset()
        {
            // Sinusoidal lane drift (scale by jitter trait)
            _jitterPhase += Time.deltaTime * (_personality == DriverPersonality.Distracted ? 0.6f : 0.25f);
            float drift   = Mathf.Sin(_jitterPhase) * _jitter;

            float targetSteer = _laneSteerPos + _jitterOffset + drift;

            // Read current steer position
            Vector3 pos          = transform.position;
            float   currentSteer = Vector3.Dot(pos, RoadDirection.SteerpAxis);
            float   newSteer     = Mathf.Lerp(currentSteer, targetSteer, 3f * Time.deltaTime);

            // Recompose position: travel component + new steer component
            Vector3 travelComp  = pos - currentSteer * RoadDirection.SteerpAxis;
            transform.position  = travelComp + newSteer * RoadDirection.SteerpAxis;
        }
    }
}
