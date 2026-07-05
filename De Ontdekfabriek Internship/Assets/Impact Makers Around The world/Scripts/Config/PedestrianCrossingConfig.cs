using UnityEngine;
using KenyaScooter.Hazards;

namespace KenyaScooter.Config
{
    /// <summary>
    /// One asset that fully describes the pedestrian-crossing feature (M28 — yield to vulnerable road users).
    /// It carries how a crossing spawns (frequency ramp, walk speed, how far ahead it appears), how the
    /// player is warned, how a clean yield is rewarded, and — through <see cref="hitConfig"/> — how a hit
    /// is punished. Mirrors HazardSpawnConfig so the system is data-driven (SC4): tuning a crossing, or
    /// adding a second kind of crosser, is an asset change, not a code change.
    ///
    /// Design philosophy (the game's safe-driving rule): REWARD safe behaviour, do not over-punish. A clean
    /// yield gives a positive bonus + cue; hitting a pedestrian is a real mistake routed through the normal
    /// hazard pipeline (deduction, streak reset, strong feedback) but the emphasis stays on the yield reward.
    /// Every crossing is telegraphed ahead of time so yielding is a fair, anticipated choice, never a gotcha.
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Pedestrian Crossing Config", fileName = "PedestrianCrossingConfig")]
    public sealed class PedestrianCrossingConfig : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("The pedestrian crosser prefab (needs the Pedestrian component + a trigger Collider). " +
                 "Use the placeholder capsule until a low-poly walking person, flat-shaded to match the art, is provided.")]
        public Pedestrian prefab;
        [Tooltip("Pooled instances. One crossing uses 1; a small pool covers overlap as the density ramps.")]
        public int poolSize = 4;

        [Header("On hit (routes through the hazard pipeline — see hitConfig)")]
        [Tooltip("The HazardSpawnConfig whose deduction / speed scrub / shake / wobble / warn+popup keys are " +
                 "applied when a pedestrian is struck. Reuses the whole hazard feedback stack with zero new wiring. " +
                 "Author it as a StaticObstacle with a firm (non-speed-scaled) deduction and warnKey WARN_PEDESTRIAN.")]
        public HazardSpawnConfig hitConfig;

        [Header("Telegraph (fairness — anticipation, not a gotcha)")]
        [Tooltip("Metres ahead of the player the pedestrian first appears AND the warning fires. Must be large " +
                 "enough that a player can read it and brake in time — keep >= the longest comfortable stopping distance.")]
        public float warningLeadDistance = 90f;
        [Tooltip("SwahiliUI key for the calm caution banner that telegraphs the crossing. Empty = no banner.")]
        public string crossingWarnKey = "WARN_CROSSING_AHEAD";

        [Header("Walk")]
        [Tooltip("Metres per second the pedestrian walks across the road. Slow enough that a yielding player " +
                 "clears the lesson, slow enough to steer around reactively if they do not brake.")]
        public float walkSpeed = 2.2f;
        [Tooltip("Lateral start/end of the walk, in metres from the road centre. The pedestrian crosses from " +
                 "one shoulder to the other; sign is randomised per crossing so they come from either side.")]
        public float walkFromLateral = 7f;
        [Tooltip("How long (s) the pedestrian lingers at the far shoulder before despawning, so a slow yielder still clears.")]
        public float despawnAfterCrossSeconds = 1.5f;

        [Header("Yield reward (the core teaching reward)")]
        [Tooltip("Base bonus for a clean yield, awarded once per crossing with the current streak multiplier (like an overtake).")]
        public int yieldReward = 150;
        [Tooltip("The player counts as yielding when the world speed drops to this fraction of base speed or below " +
                 "while the pedestrian is in the danger window. 0.6 = clearly easing off, not a full stop.")]
        [Range(0f, 1f)] public float yieldSpeedRatio = 0.6f;
        [Tooltip("SwahiliUI key for the floating 'good yield' popup. Empty = no popup.")]
        public string yieldPopupKey = "POPUP_YIELD";
        [Tooltip("Longitudinal window around the crossing (metres before/after the pedestrian's arc) inside which " +
                 "slowing counts as a yield and a contact counts as a hit. Keeps the reward tied to THIS crossing.")]
        public float dangerWindow = 18f;

        [Header("Density (ramps over the session, like hazards — the play test said the game is too easy)")]
        [Tooltip("Expected crossings per 100 m at curve value 1.")]
        public float crossingsPer100m = 0.6f;
        [Tooltip("Multiplier over normalised session time (0 = start, 1 = end) — the difficulty ramp.")]
        public AnimationCurve densityOverSession = new AnimationCurve(
            new Keyframe(0f, 0.2f), new Keyframe(0.4f, 0.6f), new Keyframe(1f, 1f));
        [Tooltip("Minimum clear road between crossings — also the fairness floor so two crossings never stack.")]
        public float minCrossingGap = 70f;
        [Tooltip("Hard cap per session. 0 = unlimited.")]
        public int maxPerSession = 0;
        [Tooltip("Only spawn crossings while the active RoadSequence carries one of these context tags " +
                 "(e.g. town/village zones). Empty = everywhere.")]
        public string[] requiredContextTags;
    }
}
