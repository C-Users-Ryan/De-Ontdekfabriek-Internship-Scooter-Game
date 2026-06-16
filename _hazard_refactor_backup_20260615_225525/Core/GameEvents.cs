using System;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Roads;
using KenyaScooter.Traffic;

namespace KenyaScooter.Core
{
    /// <summary>
    /// Central event hub. Systems communicate through these events instead of holding
    /// direct references, so any subsystem can be removed from the scene without
    /// breaking the rest. Each event is raised by exactly one owning system (noted
    /// per event); everything else only subscribes.
    /// </summary>
    public static class GameEvents
    {
        // ---- Session flow (raised by GameManager) ----------------------------------

        /// <summary>A new turn is about to start — every system returns to its initial state.</summary>
        public static event Action SessionReset;
        /// <summary>Gameplay has begun for the current student.</summary>
        public static event Action SessionStarted;
        /// <summary>(from, to) — the single source of truth for state transitions.</summary>
        public static event Action<GameState, GameState> StateChanged;
        /// <summary>The player has stopped at the relay checkpoint (Req §9.2).</summary>
        public static event Action CheckpointReached;

        // ---- Driving events ---------------------------------------------------------

        /// <summary>Confirmed pass of a same-direction vehicle (M13). Raised by OvertakeDetector.</summary>
        public static event Action<TrafficVehicle> OvertakeCompleted;
        /// <summary>Survived close pass with an oncoming vehicle (M18). Raised by NearMissDetector.</summary>
        public static event Action<TrafficVehicle> NearMiss;
        /// <summary>(severity, relative km/h, world position, absorbed by grace). Raised by PlayerCollisionHandler (M15).</summary>
        public static event Action<CollisionSeverity, float, Vector3, bool> CollisionOccurred;
        /// <summary>(hazard kind, player km/h, world position). Raised by Hazard on player contact (M21, M22).</summary>
        public static event Action<HazardKind, float, Vector3> HazardHit;
        /// <summary>Player entered (true) or left (false) the oncoming lane. Raised by WrongLaneDetector.</summary>
        public static event Action<bool> WrongLaneChanged;
        /// <summary>One full second in the oncoming lane beyond the overtake allowance (M14).</summary>
        public static event Action WrongLaneTick;
        /// <summary>Speeding tier entered: 0 = none, 1 = warning, 2 = deduction, 3 = double rate (Req §7.3).</summary>
        public static event Action<int> SpeedingTierChanged;
        /// <summary>One full second spent in speeding tier 2 or 3 (parameter = tier).</summary>
        public static event Action<int> SpeedingTick;

        // ---- Grace and rewind (M16, M17) --------------------------------------------

        public static event Action GraceAbsorbed;   // raised by GraceSystem
        public static event Action GraceRecharged;  // raised by GraceSystem
        public static event Action RewindStarted;   // raised by RewindSystem
        public static event Action RewindCompleted; // raised by RewindSystem — penalty is applied on this

        // ---- Score and streak (M19, M20 — raised by ScoreManager / StreakSystem) ----

        /// <summary>(new total, delta).</summary>
        public static event Action<int, int> ScoreChanged;
        /// <summary>(streak count, current multiplier).</summary>
        public static event Action<int, float> StreakChanged;
        /// <summary>(delta, SwahiliUI label key, anchor position, anchor is world space). Raised by ScoreManager.</summary>
        public static event Action<int, string, Vector3, bool> PopupRequested;

        // ---- World progression (M23–M25) ---------------------------------------------

        /// <summary>A new RoadSequence became active. Carries zone tags, speed limit and traffic profile. Raised by RoadSequencer.</summary>
        public static event Action<RoadSequence> SequenceChanged;
        /// <summary>(phase index, Swahili label). Raised by DayCycleManager.</summary>
        public static event Action<int, string> DayPhaseChanged;

        // ---- Raise methods ------------------------------------------------------------

        public static void RaiseSessionReset() => SafeInvoke(SessionReset);
        public static void RaiseSessionStarted() => SafeInvoke(SessionStarted);
        public static void RaiseStateChanged(GameState from, GameState to) => StateChanged?.Invoke(from, to);
        public static void RaiseCheckpointReached() => CheckpointReached?.Invoke();

        public static void RaiseOvertakeCompleted(TrafficVehicle vehicle) => OvertakeCompleted?.Invoke(vehicle);
        public static void RaiseNearMiss(TrafficVehicle vehicle) => NearMiss?.Invoke(vehicle);
        public static void RaiseCollisionOccurred(CollisionSeverity severity, float relativeKmh, Vector3 position, bool absorbed)
            => CollisionOccurred?.Invoke(severity, relativeKmh, position, absorbed);
        public static void RaiseHazardHit(HazardKind kind, float playerKmh, Vector3 position)
            => HazardHit?.Invoke(kind, playerKmh, position);
        public static void RaiseWrongLaneChanged(bool inWrongLane) => WrongLaneChanged?.Invoke(inWrongLane);
        public static void RaiseWrongLaneTick() => WrongLaneTick?.Invoke();
        public static void RaiseSpeedingTierChanged(int tier) => SpeedingTierChanged?.Invoke(tier);
        public static void RaiseSpeedingTick(int tier) => SpeedingTick?.Invoke(tier);

        public static void RaiseGraceAbsorbed() => GraceAbsorbed?.Invoke();
        public static void RaiseGraceRecharged() => GraceRecharged?.Invoke();
        public static void RaiseRewindStarted() => RewindStarted?.Invoke();
        public static void RaiseRewindCompleted() => RewindCompleted?.Invoke();

        public static void RaiseScoreChanged(int total, int delta) => ScoreChanged?.Invoke(total, delta);
        public static void RaiseStreakChanged(int streak, float multiplier) => StreakChanged?.Invoke(streak, multiplier);
        public static void RaisePopupRequested(int delta, string labelKey, Vector3 position, bool worldSpace)
            => PopupRequested?.Invoke(delta, labelKey, position, worldSpace);

        public static void RaiseSequenceChanged(RoadSequence sequence) => SequenceChanged?.Invoke(sequence);
        public static void RaiseDayPhaseChanged(int index, string label) => DayPhaseChanged?.Invoke(index, label);

        /// <summary>
        /// Invokes each subscriber independently so one throwing handler cannot abort the
        /// rest of the chain. Critical for the session-flow events: otherwise a single bad
        /// subscriber stops GameManager reaching the Playing state and the world freezes.
        /// Exceptions are logged, never silently swallowed.
        /// </summary>
        private static void SafeInvoke(Action handlers)
        {
            if (handlers == null)
                return;
            Delegate[] list = handlers.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                try { ((Action)list[i]).Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        // Static events survive an editor play session when domain reload is disabled.
        // Clearing here guarantees a clean slate on every enter-play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearAllSubscriptions()
        {
            SessionReset = null; SessionStarted = null; StateChanged = null; CheckpointReached = null;
            OvertakeCompleted = null; NearMiss = null; CollisionOccurred = null; HazardHit = null;
            WrongLaneChanged = null; WrongLaneTick = null; SpeedingTierChanged = null; SpeedingTick = null;
            GraceAbsorbed = null; GraceRecharged = null; RewindStarted = null; RewindCompleted = null;
            ScoreChanged = null; StreakChanged = null; PopupRequested = null;
            SequenceChanged = null; DayPhaseChanged = null;
        }
    }
}
