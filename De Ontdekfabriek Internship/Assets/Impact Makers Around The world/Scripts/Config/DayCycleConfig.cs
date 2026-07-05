using System;
using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// Defines the phases of the day (M25). Defaults follow the MDA's four-phase arc
    /// (ASUBUHI → MCHANA → ALASIRI → JIONI); switching to the three-phase version is just an asset
    /// edit, no code (decision logged in D17). DayCycleManager advances these phases automatically by
    /// time and drives the sun, fog and sky from them; it can ALSO crossfade one optional URP Volume per
    /// phase (matched by index) if a scene wires them, but that grading is optional and not required.
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Day Cycle Config", fileName = "DayCycleConfig")]
    public sealed class DayCycleConfig : ScriptableObject
    {
        [Serializable]
        public struct Phase
        {
            [Tooltip("Swahili label shown on the HUD.")]
            public string label;
            [Tooltip("Session time (seconds) at which this phase begins.")]
            public float startTime;
            public Color labelColour;
            [Tooltip("Directional light rotation for this phase.")]
            public Vector3 sunEuler;
            public Color sunColour;
            public float sunIntensity;
            public Color fogColour;

            [Tooltip("Procedural-skybox _SkyTint for this phase (warm = Kenyan). Only used when the scene skybox is " +
                     "Skybox/Procedural and driveSkybox is on; ignored otherwise, so it is safe on any skybox.")]
            public Color skyTint;
            [Tooltip("Procedural-skybox _GroundColor (the warm dusty band at the horizon) for this phase.")]
            public Color groundColour;
            [Tooltip("Procedural-skybox _AtmosphereThickness: higher = hazier, paler, warmer horizon.")]
            public float atmosphereThickness;
            [Tooltip("Procedural-skybox _Exposure (overall sky brightness) for this phase.")]
            public float skyExposure;

            [Tooltip("How much artificial light belongs in this phase: 0 = broad daylight (headlights off), " +
                     "1 = full night (headlights fully on). DayCycleManager blends this continuously into NightFactor01, " +
                     "which fades the scooter headlight in as the world darkens. Lets dawn stay lamp-free even though its " +
                     "sun is dim, so it does not track sun intensity.")]
            [Range(0f, 1f)]
            public float artificialLight;
        }

        // Phase colours + sun arc follow REAL equatorial Kenya (Oplevering 25 Jun 2026: "day-and-night cycle
        // should match the red look"; research brief: near-overhead harsh midday, low steep dawn/dusk, fast
        // saturated golden hour, then a starry night). SIX phases: a cool MAGHARIBI blue hour now bridges the
        // laterite-red dusk and the indigo USIKU night, so the sundown is a slow sweep instead of a lurch.
        // 2026-07-04 regrade for MORE Kenya: warmer rose dawn, a more overhead + heat-bleached midday, a richer
        // golden ALASIRI, a deep saturated LATERITE-RED dusk, and a deeper indigo star-friendly night — the
        // DayCycleManager glides CONTINUOUSLY through these, so the sun arcs and the sky reddens the whole way.
        // 2026-07-05 re-pace: start times spread to fill a ~120 s session (see referenceSessionSeconds) and the
        // MAGHARIBI bridge added, so the player genuinely lives morning -> midday -> dusk -> night in one run;
        // each phase also carries artificialLight (0 day .. 1 night) that drives the scooter headlight fade-in.
        // NOTE: an existing DayCycleConfig.asset serialises its OWN values, so these C# defaults only define a
        // freshly created asset. To push this palette into the shipped asset, run
        // Tools > Kenya Scooter > Weather and FX > Apply Kenya Reference Day Palette (or type the values in the Inspector).
        public Phase[] phases =
        {
            new Phase { label = "ASUBUHI",   startTime = 0f,   labelColour = new Color(1f, 0.8f, 0.7f),     sunEuler = new Vector3(8f, -86f, 0f),   sunColour = new Color(1f, 0.80f, 0.60f),   sunIntensity = 0.80f, fogColour = new Color(0.86f, 0.72f, 0.62f), skyTint = new Color(0.82f, 0.60f, 0.56f), groundColour = new Color(0.48f, 0.35f, 0.30f), atmosphereThickness = 1.7f,  skyExposure = 0.95f, artificialLight = 0f },
            new Phase { label = "MCHANA",    startTime = 22f,  labelColour = new Color(1f, 0.98f, 0.82f),   sunEuler = new Vector3(88f, -8f, 0f),   sunColour = new Color(1f, 0.97f, 0.88f),   sunIntensity = 1.30f, fogColour = new Color(0.90f, 0.85f, 0.74f), skyTint = new Color(0.68f, 0.72f, 0.70f), groundColour = new Color(0.62f, 0.48f, 0.36f), atmosphereThickness = 1.15f, skyExposure = 1.20f, artificialLight = 0f },
            new Phase { label = "ALASIRI",   startTime = 50f,  labelColour = new Color(1f, 0.85f, 0.6f),    sunEuler = new Vector3(42f, 40f, 0f),   sunColour = new Color(1f, 0.82f, 0.55f),   sunIntensity = 1.05f, fogColour = new Color(0.92f, 0.76f, 0.55f), skyTint = new Color(0.90f, 0.68f, 0.46f), groundColour = new Color(0.58f, 0.42f, 0.30f), atmosphereThickness = 1.6f,  skyExposure = 1.05f, artificialLight = 0.10f },
            new Phase { label = "JIONI",     startTime = 74f,  labelColour = new Color(1f, 0.55f, 0.35f),   sunEuler = new Vector3(4f, 84f, 0f),    sunColour = new Color(1f, 0.42f, 0.20f),   sunIntensity = 0.60f, fogColour = new Color(0.74f, 0.40f, 0.30f), skyTint = new Color(0.95f, 0.38f, 0.22f), groundColour = new Color(0.40f, 0.24f, 0.19f), atmosphereThickness = 2.1f,  skyExposure = 0.98f, artificialLight = 0.50f },
            new Phase { label = "MAGHARIBI", startTime = 92f,  labelColour = new Color(0.78f, 0.76f, 0.95f), sunEuler = new Vector3(-2f, 108f, 0f),  sunColour = new Color(0.52f, 0.54f, 0.74f), sunIntensity = 0.34f, fogColour = new Color(0.30f, 0.28f, 0.42f), skyTint = new Color(0.42f, 0.36f, 0.54f), groundColour = new Color(0.20f, 0.17f, 0.22f), atmosphereThickness = 1.85f, skyExposure = 0.72f, artificialLight = 0.85f },
            new Phase { label = "USIKU",     startTime = 106f, labelColour = new Color(0.7f, 0.78f, 1f),    sunEuler = new Vector3(20f, 150f, 0f),  sunColour = new Color(0.50f, 0.58f, 0.82f), sunIntensity = 0.16f, fogColour = new Color(0.08f, 0.11f, 0.20f), skyTint = new Color(0.10f, 0.14f, 0.26f), groundColour = new Color(0.08f, 0.09f, 0.15f), atmosphereThickness = 1.3f,  skyExposure = 0.45f, artificialLight = 1f }
        };

        [Tooltip("Legacy. The day cycle now glides CONTINUOUSLY between phases (pacing comes from each phase's " +
                 "startTime), so this fixed crossfade duration is no longer read. Kept for older assets only.")]
        public float transitionSeconds = 4f;

        [Tooltip("When on, the day cycle also drives the scene's procedural skybox (tint, ground colour, atmosphere, " +
                 "exposure) per phase, so the SKY shifts from dawn to dusk too. No effect unless the scene skybox is a " +
                 "Skybox/Procedural material, so it is safe to leave on with any skybox.")]
        public bool driveSkybox = true;

        [Header("Fit to session length")]
        [Tooltip("When on, the phase start times above are treated as authored for a referenceSessionSeconds-long run " +
                 "and STRETCHED (or squeezed) to match the facilitator's actual session length, so the full arc always " +
                 "fills the run whatever the timer is set to. Off = the start times are used as literal seconds.")]
        public bool scaleToSessionLength = true;
        [Tooltip("The session length (seconds) the phase start times above were authored against. The arc is scaled by " +
                 "actualSessionSeconds / this, so 120 here + a 120 s timer means no change; a 180 s timer stretches the " +
                 "whole day 1.5x. Match this to the default SessionConfig.sessionSeconds.")]
        public float referenceSessionSeconds = 120f;

        [Header("Facilitator control")]
        [Tooltip("When true (default) the day advances with session time, morning to sunset. When false the scene " +
                 "holds the single fixed phase below, so a facilitator can pick one look (e.g. always morning).")]
        public bool cycleEnabled = true;
        [Tooltip("Which phase is held when cycleEnabled is false (0 = first phase). Clamped to the phases array.")]
        public int fixedPhaseIndex = 0;
    }
}
