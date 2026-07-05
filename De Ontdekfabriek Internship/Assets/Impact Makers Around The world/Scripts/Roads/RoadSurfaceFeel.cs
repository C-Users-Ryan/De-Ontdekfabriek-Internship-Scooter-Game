using UnityEngine;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// The single shared "what is under the tyres" signal (2026-07-05), mirroring how RoadDirection publishes
    /// the live bend and SpeedFeel publishes the speed drive. RoadSequencer calls <see cref="Publish"/> every
    /// frame with the surface of the tile under the player; the dirt-road consumers (DirtRumble on the
    /// scooter, the dust layers via <see cref="DustBoost"/>) all read the SAME eased blend, so the rumble and
    /// the extra dust arrive and leave together instead of each layer snapping at the tile seam.
    ///
    /// The blend eases IN faster than OUT: dirt grabs the wheels almost immediately when the tyres hit
    /// murram, but the shake settles gently once back on tarmac — a hard cut at a tile boundary reads as a
    /// glitch, a short settle reads as suspension.
    /// </summary>
    public static class RoadSurfaceFeel
    {
        /// <summary>The surface of the tile under the player right now (raw, unblended).</summary>
        public static RoadSurfaceType Current { get; private set; }

        /// <summary>0 = fully on tarmac, 1 = fully on dirt, eased across surface changes. Every dirt-road
        /// effect scales by this one number so they all move together.</summary>
        public static float DirtBlend01 { get; private set; }

        private const float BlendInSeconds = 0.45f;  // tyres hit the murram: the rumble grabs quickly
        private const float BlendOutSeconds = 0.9f;  // back on tarmac: the shake settles, not snaps

        /// <summary>Called by RoadSequencer each rendered frame with the surface of the tile under the
        /// player. Eases <see cref="DirtBlend01"/> toward the new surface.</summary>
        public static void Publish(RoadSurfaceType surface, float deltaTime)
        {
            Current = surface;
            float target = surface == RoadSurfaceType.Dirt ? 1f : 0f;
            float seconds = target > DirtBlend01 ? BlendInSeconds : BlendOutSeconds;
            DirtBlend01 = Mathf.MoveTowards(DirtBlend01, target, deltaTime / Mathf.Max(0.0001f, seconds));
        }

        /// <summary>A dust layer's emission multiplier right now: 1 on tarmac, <paramref name="dirtMultiplier"/>
        /// on dirt, eased between the two and damped by the facilitator's "Onverharde wegen" setting. Pass
        /// WeatherConfig.dirtDustMultiplier.</summary>
        public static float DustBoost(float dirtMultiplier) =>
            Mathf.Lerp(1f, dirtMultiplier, DirtBlend01 * IntensityScale);

        // ---- Facilitator intensity ("Onverharde wegen", OMGEVING) ------------------------------------------

        /// <summary>PlayerPrefs key for the facilitator's dirt-road feel: 0 = UIT, 1 = SUBTIEL, 2 = VOL.
        /// Written by the SettingsCatalog "env.dirtFeel" stepper, read live here — the same lazy-cache
        /// pattern SpeedFeel uses for the speed-FX toggle.</summary>
        public const string IntensityPrefKey = "ksg.dirtFeel";

        private static int _intensity = -1; // lazy PlayerPrefs cache (-1 = not yet read)

        /// <summary>The facilitator's chosen step, 0 (UIT) .. 2 (VOL). VOL by default; persisted so the
        /// choice survives an app restart. UIT keeps the murram LOOK of a dirt tile (that is what the road
        /// is) but makes it ride like asphalt: no rumble, no extra dust.</summary>
        public static int IntensityStep
        {
            get
            {
                if (_intensity < 0)
                    _intensity = Mathf.Clamp(PlayerPrefs.GetInt(IntensityPrefKey, 2), 0, 2);
                return _intensity;
            }
            set
            {
                _intensity = Mathf.Clamp(value, 0, 2);
                PlayerPrefs.SetInt(IntensityPrefKey, _intensity);
                PlayerPrefs.Save();
            }
        }

        /// <summary>The step as a 0..1 scale (UIT = 0, SUBTIEL = 0.5, VOL = 1) that the rumble and the dust
        /// boost multiply in, so one setting calms every dirt-road effect together.</summary>
        public static float IntensityScale => IntensityStep * 0.5f;

        /// <summary>Back to tarmac instantly — called on session reset so a new turn never inherits the
        /// previous group's blend.</summary>
        public static void Reset()
        {
            Current = RoadSurfaceType.Paved;
            DirtBlend01 = 0f;
        }

        // Static state survives an editor play session when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
