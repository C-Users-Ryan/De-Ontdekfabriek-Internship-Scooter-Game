using UnityEngine;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// A traffic vehicle with driver AI. Follows turns automatically by reading
    /// RoadDirection.Current and RoadDirection.SteerAxis each frame — no hard-coded axes.
    ///
    /// SPEED MODEL (world-moves-player architecture)
    ///   Same-direction:  moveSpeed = WorldSpeed − ownSpeed  → player catches up
    ///   Oncoming:        moveSpeed = WorldSpeed + ownSpeed  → head-on approach
    ///
    /// PERSONALITY SUMMARY
    ///   Aggressive  — high speed, small yield, barely yields
    ///   Cautious    — low speed, large early yield
    ///   Distracted  — drifts in lane, random speed changes, delayed reaction
    ///   Normal      — baseline everything
    /// </summary>
    public class TrafficVehicle : MonoBehaviour
    {
        // ── Static shared state ──────────────────────────────────────────────────

        /// <summary>All currently active vehicles. OvertakeDetector reads this list.</summary>
        public static readonly List<TrafficVehicle> Active = new();

        /// <summary>Set once by TrafficManager.Start(). Avoids FindFirstObjectByType in hot paths.</summary>
        public static Transform SharedPlayer { get; private set; }
        public static void SetSharedPlayer(Transform t) => SharedPlayer = t;

        // ── Vehicle state ────────────────────────────────────────────────────────

        public float ownSpeed;
        public bool  isOncoming;

        // ── Driver AI state (set on Activate) ────────────────────────────────────

        private DriverPersonality _personality;
        private float _awarenessRange;
        private float _yieldStrength;
        private float _jitter;
        private float _speedVarianceFrac;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private bool      _active;
        private float     _laneSteerPos;
        private float     _jitterOffset;
        private float     _jitterPhase;
        private float     _speedDelta;
        private float     _speedTimer;
        private Transform _player;

        // ── Activate / Deactivate ──────────────────────────────────────────────────

        public void Activate(Vector3 position, float speed, bool oncoming,
                             DriverPersonality personality = DriverPersonality.Normal)
        {
            transform.position = position;
            ownSpeed    = speed;
            isOncoming  = oncoming;
            _personality = personality;
            _player = SharedPlayer;

            _laneSteerPos = Vector3.Dot(position, RoadDirection.SteerAxis);

            Vector3 facing = isOncoming ? RoadDirection.Current : -RoadDirection.Current;
            if (facing != Vector3.zero) transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

            switch (_personality)
            {
                case DriverPersonality.Aggressive:
                    _awarenessRange = 10f; _yieldStrength = 0.2f;
                    _jitter = 0.05f;       _speedVarianceFrac = 0.25f; break;
                case DriverPersonality.Cautious:
                    _awarenessRange = 30f; _yieldStrength = 0.9f;
                    _jitter = 0.08f;       _speedVarianceFrac = 0.08f; break;
                case DriverPersonality.Distracted:
                    _awarenessRange = 8f;  _yieldStrength = 0.3f;
                    _jitter = 0.5f;        _speedVarianceFrac = 0.45f; break;
                default: // Normal
                    _awarenessRange = 20f; _yieldStrength = 0.55f;
                    _jitter = 0.15f;       _speedVarianceFrac = 0.12f; break;
            }

            _jitterOffset = 0f;
            _jitterPhase  = Random.Range(0f, Mathf.PI * 2f);
            _speedDelta   = 0f;
            _speedTimer   = Random.Range(1.5f, 5f);

            gameObject.SetActive(true);
            _active = true;
            Active.Add(this);
        }

        public void Deactivate()
        {
            Active.Remove(this);
            _active = false;
            gameObject.SetActive(false);
        }

        // ── Update ───────────────────────────────────────────────────────────────

        void Update()
        {
            if (!_active || WorldSpeed.Instance == null) return;

            float worldSpd  = WorldSpeed.Instance.Current;
            float moveSpeed = isOncoming
                ? worldSpd + ownSpeed
                : Mathf.Max(0f, worldSpd - ownSpeed + _speedDelta);

            transform.Translate(RoadDirection.Current * moveSpeed * Time.deltaTime, Space.World);

            if (isOncoming) UpdateOncoming(worldSpd);
            else            UpdateSameDirection();

            ApplyLateralOffset();
        }

        // ── Same-direction behaviour ──────────────────────────────────────────────

        private void UpdateSameDirection()
        {
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

            Vector3 toPlayer   = _player.position - transform.position;
            float   behindDot  = Vector3.Dot(toPlayer, RoadDirection.Current);
            float   lateralDot = Vector3.Dot(toPlayer, RoadDirection.SteerAxis);

            bool playerIsBehind = behindDot > 0f && behindDot < _awarenessRange;
            bool playerInLane   = Mathf.Abs(lateralDot) < 2.2f;

            if (playerIsBehind && playerInLane)
            {
                float edgeSign    = _laneSteerPos >= 0f ? 1f : -1f;
                float yieldTarget = _laneSteerPos + edgeSign * _yieldStrength;
                _jitterOffset = Mathf.Lerp(_jitterOffset, yieldTarget - _laneSteerPos, 4f * Time.deltaTime);
            }
            else
            {
                _jitterOffset = Mathf.Lerp(_jitterOffset, 0f, 1.5f * Time.deltaTime);
            }
        }

        // ── Oncoming behaviour ────────────────────────────────────────────────────

        private void UpdateOncoming(float worldSpd)
        {
            if (_player == null) return;

            Vector3 toPlayer = _player.position - transform.position;
            float   distSqr  = toPlayer.sqrMagnitude;

            if (distSqr < 18f * 18f)
            {
                float lateralDot = Vector3.Dot(toPlayer, RoadDirection.SteerAxis);
                bool  playerOnOurSide = (_laneSteerPos >= 0f && lateralDot > -0.5f)
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

        // ── Lateral position ─────────────────────────────────────────────────────

        private void ApplyLateralOffset()
        {
            _jitterPhase += Time.deltaTime * (_personality == DriverPersonality.Distracted ? 0.6f : 0.25f);
            float drift = Mathf.Sin(_jitterPhase) * _jitter;

            float targetSteer  = _laneSteerPos + _jitterOffset + drift;
            Vector3 pos        = transform.position;
            float currentSteer = Vector3.Dot(pos, RoadDirection.SteerAxis);
            float newSteer     = Mathf.Lerp(currentSteer, targetSteer, 3f * Time.deltaTime);

            Vector3 travelComp = pos - currentSteer * RoadDirection.SteerAxis;
            transform.position = travelComp + newSteer * RoadDirection.SteerAxis;
        }
    }
}
