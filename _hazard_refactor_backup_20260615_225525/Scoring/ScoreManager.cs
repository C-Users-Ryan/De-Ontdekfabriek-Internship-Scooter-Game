using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Player;
using KenyaScooter.Roads;
using KenyaScooter.Traffic;

namespace KenyaScooter.Scoring
{
    /// <summary>
    /// The single writer of the score (M19, Req §7.5) — every other system only
    /// raises events. Orchestration order matters and lives here exclusively:
    /// an overtake reads the streak multiplier, awards, THEN increments the streak;
    /// every violation resets the streak before its deduction. Each deduction type
    /// is individually toggleable via ScoreConfig (Req §16). Score never drops below
    /// the floor. Pothole and speed-bump deductions scale with speed — slowing down
    /// genuinely pays (Req §6.1).
    /// </summary>
    public sealed class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [SerializeField] private ScoreConfig config;
        [SerializeField] private StreakSystem streak;
        [SerializeField] private WrongLaneDetector wrongLane;

        public int Score { get; private set; }

        private float trickleAccumulator;
        private float correctLaneTimer;
        private bool speedingThisSequence;
        private bool sequenceSeen;

        private void Awake() => Instance = this;

        private void OnEnable()
        {
            GameEvents.SessionReset += HandleSessionReset;
            GameEvents.OvertakeCompleted += HandleOvertake;
            GameEvents.CollisionOccurred += HandleCollision;
            GameEvents.HazardHit += HandleHazard;
            GameEvents.WrongLaneTick += HandleWrongLaneTick;
            GameEvents.SpeedingTick += HandleSpeedingTick;
            GameEvents.RewindCompleted += HandleRewindCompleted;
            GameEvents.SequenceChanged += HandleSequenceChanged;
        }

        private void OnDisable()
        {
            GameEvents.SessionReset -= HandleSessionReset;
            GameEvents.OvertakeCompleted -= HandleOvertake;
            GameEvents.CollisionOccurred -= HandleCollision;
            GameEvents.HazardHit -= HandleHazard;
            GameEvents.WrongLaneTick -= HandleWrongLaneTick;
            GameEvents.SpeedingTick -= HandleSpeedingTick;
            GameEvents.RewindCompleted -= HandleRewindCompleted;
            GameEvents.SequenceChanged -= HandleSequenceChanged;
        }

        private void Update()
        {
            if (GameManager.State != GameState.Playing)
                return;

            float dt = Time.deltaTime;

            if (config.trickleEnabled && WorldSpeed.Instance.Current >= WorldSpeed.Instance.BaseSpeed * 0.5f)
            {
                trickleAccumulator += dt;
                if (trickleAccumulator >= 1f)
                {
                    trickleAccumulator -= 1f;
                    Add(config.tricklePerSecond, null, Vector3.zero, false);
                }
            }

            if (config.correctLaneBonusEnabled && wrongLane != null && wrongLane.IsInOwnLane)
            {
                correctLaneTimer += dt;
                if (correctLaneTimer >= config.correctLaneBonusInterval)
                {
                    correctLaneTimer = 0f;
                    Add(config.correctLaneBonus, null, Vector3.zero, false);
                }
            }
        }

        // ---- Event handlers ----------------------------------------------------------

        private void HandleOvertake(TrafficVehicle vehicle)
        {
            if (!config.scoreOvertakes)
                return;

            // Award with the CURRENT multiplier, then climb — the new tier applies
            // to the next overtake (M20).
            int points = Mathf.RoundToInt(vehicle.overtakeScore * streak.Multiplier);
            Add(points, "POPUP_OVERTAKE", vehicle.transform.position, true);
            streak.RegisterCleanOvertake();
        }

        private void HandleCollision(CollisionSeverity severity, float relativeKmh, Vector3 position, bool absorbed)
        {
            // Any collision breaks the streak, absorbed or not — grace forgives the
            // points, not the cleanliness (D17).
            streak.ResetStreak();

            if (absorbed)
            {
                GameEvents.RaisePopupRequested(0, "POPUP_GRACE", position, true);
                return;
            }
            if (!config.deductCollisions)
                return;

            // Hard crashes pay this too; when a rewind follows, the rewind penalty
            // stacks on top — a rewound crash costs the most of any single mistake (D17).
            Deduct(config.collisionDeduction, "POPUP_DEDUCT", position, true);
        }

        private void HandleHazard(HazardKind kind, float playerKmh, Vector3 position)
        {
            streak.ResetStreak();

            switch (kind)
            {
                case HazardKind.Pothole:
                    if (config.deductPotholes)
                        Deduct(SpeedScaled(config.potholeDeduction), "POPUP_DEDUCT", position, true);
                    break;
                case HazardKind.Rock:
                    if (config.deductRocks)
                        Deduct(config.rockDeduction, "POPUP_DEDUCT", position, true);
                    break;
                case HazardKind.SpeedBumpUnmarked:
                    if (config.deductSpeedBumps)
                        Deduct(SpeedScaled(config.speedBumpUnmarkedDeduction), "POPUP_DEDUCT", position, true);
                    break;
                case HazardKind.SpeedBumpPainted:
                    if (config.deductSpeedBumps)
                        Deduct(SpeedScaled(config.speedBumpPaintedDeduction), "POPUP_DEDUCT", position, true);
                    break;
            }
        }

        private void HandleWrongLaneTick()
        {
            streak.ResetStreak();
            if (config.deductWrongLane)
                Deduct(config.wrongLanePerSecond, null, Vector3.zero, false); // WarningSystem carries the message
        }

        private void HandleSpeedingTick(int tier)
        {
            speedingThisSequence = true;
            streak.ResetStreak();
            if (config.deductSpeeding)
                Deduct(config.speedingPerSecond * (tier >= 3 ? 2 : 1), null, Vector3.zero, false);
        }

        private void HandleRewindCompleted()
        {
            streak.ResetStreak();
            Deduct(config.rewindPenalty, "POPUP_REWIND", Vector3.zero, false);
        }

        private void HandleSequenceChanged(RoadSequence sequence)
        {
            if (config.cleanZoneBonusEnabled && sequenceSeen && !speedingThisSequence)
                Add(config.cleanZoneBonus, null, Vector3.zero, false);
            sequenceSeen = true;
            speedingThisSequence = false;
        }

        // ---- Score writes --------------------------------------------------------------

        private void Add(int points, string popupKey, Vector3 position, bool worldSpace)
        {
            if (points <= 0)
                return;
            Score += points;
            GameEvents.RaiseScoreChanged(Score, points);
            if (popupKey != null && points >= config.popupMinPoints)
                GameEvents.RaisePopupRequested(points, popupKey, position, worldSpace);
        }

        private void Deduct(int points, string popupKey, Vector3 position, bool worldSpace)
        {
            if (points <= 0)
                return;
            Score = Mathf.Max(config.scoreFloor, Score - points);
            GameEvents.RaiseScoreChanged(Score, -points);
            if (popupKey != null && points >= config.popupMinPoints)
                GameEvents.RaisePopupRequested(-points, popupKey, position, worldSpace);
        }

        /// <summary>Zero below base speed, full at max — the mechanic that rewards braking (Req §6.1).</summary>
        private int SpeedScaled(int baseDeduction)
            => Mathf.RoundToInt(baseDeduction * Mathf.Clamp01(config.hazardSpeedScale.Evaluate(WorldSpeed.Instance.SpeedRatio)));

        private void HandleSessionReset()
        {
            Score = 0;
            trickleAccumulator = 0f;
            correctLaneTimer = 0f;
            speedingThisSequence = false;
            sequenceSeen = false;
            GameEvents.RaiseScoreChanged(0, 0);
        }
    }
}
