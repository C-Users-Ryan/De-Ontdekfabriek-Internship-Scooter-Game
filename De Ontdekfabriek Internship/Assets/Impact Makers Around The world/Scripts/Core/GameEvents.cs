using System;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Roads;
using KenyaScooter.Traffic;

namespace KenyaScooter.Core
{
    /// <summary>
    /// The central event hub. Systems talk through these events instead of holding direct references
    /// to each other, so any one of them can be removed from the scene without breaking the rest.
    /// Each event has exactly one system that raises it (noted per event); everyone else only listens.
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
        /// <summary>The bike has pulled into the charge station and started charging (cinematic relay) — for the charge visuals (battery refill, glow). Raised by ChargeStationSequence before CheckpointReached.</summary>
        public static event Action ChargingStarted;
        /// <summary>A new class group is starting — group-scoped state (group total, shared streak) resets. Raised by GroupScoreManager.</summary>
        public static event Action GroupReset;
        /// <summary>Endless mode only: the player's remaining lives changed (parameter = lives left, 0 = game over). Raised by GameManager.</summary>
        public static event Action<int> LivesChanged;

        /// <summary>A full-screen framing/menu screen became visible (true) or was dismissed back to the HUD (false).
        /// Raised by KenyaMenuScreens as the single source of truth for "a UI screen is covering the world". The
        /// audio layer uses it two ways: the UI sound plays an open cue on it, and the game-world beds (ambience,
        /// soundscape) duck to silence while it is up so nothing leaks from the game behind a menu.</summary>
        public static event Action<bool> MenuScreenChanged;
        /// <summary>Live convenience mirror of the last <see cref="MenuScreenChanged"/> value, so a system can gate on
        /// "is a menu screen up?" without subscribing. True while any framing screen (title/setup/relay/eindstand/
        /// game-over) is visible.</summary>
        public static bool MenuScreenVisible { get; private set; }

        // ---- Driving events ---------------------------------------------------------

        /// <summary>Confirmed pass of a same-direction vehicle on the correct side (M13). Raised by OvertakeDetector.</summary>
        public static event Action<TrafficVehicle> OvertakeCompleted;
        /// <summary>A pass completed on the wrong (illegal) side — earns no points, only a corrective warning. Raised by OvertakeDetector.</summary>
        public static event Action<TrafficVehicle> IllegalOvertake;
        /// <summary>Survived close pass with an oncoming vehicle (M18). Raised by NearMissDetector.</summary>
        public static event Action<TrafficVehicle> NearMiss;
        /// <summary>(severity, relative km/h, world position, absorbed by grace). Raised by PlayerCollisionHandler (M15).</summary>
        public static event Action<CollisionSeverity, float, Vector3, bool> CollisionOccurred;
        /// <summary>(hazard config, player km/h, world position). Raised by Hazard on player contact (M21, M22).</summary>
        public static event Action<HazardSpawnConfig, float, Vector3> HazardHit;
        /// <summary>Player entered (true) or left (false) the oncoming lane. Raised by WrongLaneDetector.</summary>
        public static event Action<bool> WrongLaneChanged;
        /// <summary>One full second in the oncoming lane beyond the overtake allowance (M14).</summary>
        public static event Action WrongLaneTick;
        /// <summary>Speeding tier entered: 0 = none, 1 = warning, 2 = deduction, 3 = double rate (Req §7.3).</summary>
        public static event Action<int> SpeedingTierChanged;
        /// <summary>One full second spent in speeding tier 2 or 3 (parameter = tier).</summary>
        public static event Action<int> SpeedingTick;

        // ---- Pedestrian crossing (M28 — yield to vulnerable road users) -------------
        // Pedestrian HITS reuse the HazardHit event above (the crosser carries a HazardSpawnConfig),
        // so the deduction, streak reset, speed scrub, wobble, shake, haptics and warning all fire
        // through the existing hazard pipeline — no new wiring. These two events add only what the
        // hazard model has no notion of: telegraphing a crossing ahead, and REWARDING a clean yield.

        /// <summary>A pedestrian crossing is coming up — telegraph it so yielding is a fair, anticipated choice.
        /// (SwahiliUI warn key, metres ahead). Raised by PedestrianCrossingSpawner; the HUD shows a calm caution banner.</summary>
        public static event Action<string, float> CrossingAhead;
        /// <summary>The player slowed for a pedestrian and let them pass cleanly — the core teaching reward
        /// (base points before the streak multiplier, SwahiliUI popup key, world position). Raised by Pedestrian;
        /// ScoreManager awards it with the current multiplier, like an overtake.</summary>
        public static event Action<int, string, Vector3> PedestrianYielded;

        // ---- Road-shape telegraph (2026-07-05 play-test: a bend took a first-time player by surprise) ----
        /// <summary>A bend is coming up — telegraph it so the turn is anticipated, not a gotcha (design:
        /// anticipation, not punishment — the same principle as the crossing telegraph above). (SwahiliUI warn
        /// key encoding the direction, WARN_TURN_LEFT / WARN_TURN_RIGHT; metres ahead of the turn's start).
        /// Raised by TurnTelegraph; the HUD shows a calm caution banner on the very channel the crossing
        /// telegraph uses, so nothing new has to be wired to display it.</summary>
        public static event Action<string, float> TurnAhead;

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
        public static void RaiseChargingStarted() => SafeInvoke(ChargingStarted);
        public static void RaiseGroupReset() => GroupReset?.Invoke();
        public static void RaiseLivesChanged(int lives) => LivesChanged?.Invoke(lives);
        public static void RaiseMenuScreenChanged(bool visible)
        {
            MenuScreenVisible = visible;
            MenuScreenChanged?.Invoke(visible);
        }

        public static void RaiseOvertakeCompleted(TrafficVehicle vehicle) => OvertakeCompleted?.Invoke(vehicle);
        public static void RaiseIllegalOvertake(TrafficVehicle vehicle) => IllegalOvertake?.Invoke(vehicle);
        public static void RaiseNearMiss(TrafficVehicle vehicle) => NearMiss?.Invoke(vehicle);
        public static void RaiseCollisionOccurred(CollisionSeverity severity, float relativeKmh, Vector3 position, bool absorbed)
            => CollisionOccurred?.Invoke(severity, relativeKmh, position, absorbed);
        public static void RaiseHazardHit(HazardSpawnConfig definition, float playerKmh, Vector3 position)
            => HazardHit?.Invoke(definition, playerKmh, position);
        public static void RaiseWrongLaneChanged(bool inWrongLane) => WrongLaneChanged?.Invoke(inWrongLane);
        public static void RaiseCrossingAhead(string warnKey, float metresAhead) => CrossingAhead?.Invoke(warnKey, metresAhead);
        public static void RaisePedestrianYielded(int basePoints, string popupKey, Vector3 position)
            => PedestrianYielded?.Invoke(basePoints, popupKey, position);
        public static void RaiseTurnAhead(string warnKey, float metresAhead) => TurnAhead?.Invoke(warnKey, metresAhead);
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
            SessionReset = null; SessionStarted = null; StateChanged = null; CheckpointReached = null; ChargingStarted = null; GroupReset = null; LivesChanged = null;
            MenuScreenChanged = null; MenuScreenVisible = false;
            OvertakeCompleted = null; IllegalOvertake = null; NearMiss = null; CollisionOccurred = null; HazardHit = null;
            WrongLaneChanged = null; WrongLaneTick = null; SpeedingTierChanged = null; SpeedingTick = null;
            CrossingAhead = null; PedestrianYielded = null; TurnAhead = null;
            GraceAbsorbed = null; GraceRecharged = null; RewindStarted = null; RewindCompleted = null;
            ScoreChanged = null; StreakChanged = null; PopupRequested = null;
            SequenceChanged = null; DayPhaseChanged = null;
        }
    }
}
