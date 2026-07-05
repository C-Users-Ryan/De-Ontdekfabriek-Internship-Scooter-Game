using UnityEngine;
using KenyaScooter.Config;

namespace KenyaScooter.FX
{
    /// <summary>
    /// The single shared "what is the wind doing" signal — the weather twin of <see cref="SpeedFeel"/>
    /// (FX Design Spec "Levend Kenia" §01, 3 Jul 2026). Smoke, laundry, grass, leaves, gust fronts and
    /// dust devils all read the SAME strength and the SAME gust-front schedule, so the whole roadside
    /// breathes together instead of every system rolling its own dice (the old "uniform, rhythm-less" read).
    ///
    /// The signal has three parts: a slow BREATH (always on, never still), scheduled GUST FRONTS (one per
    /// <see cref="gustPeriod"/> window at a hashed offset inside it, with an attack/hold/release envelope,
    /// so gaps vary ~8..20 s with zero stored state), and a DIRECTION that wanders over minutes.
    ///
    /// Stateless by design (a pure function of Time.time, like SpeedFeel's properties): no Update, no
    /// allocation, and deterministic — every subscriber sampling the same frame sees the same wind.
    /// Wind is not an effect; it is a CLOCK other effects subscribe to.
    /// </summary>
    public static class WindField
    {
        // One front per window; the hash jitters WHERE in the window it lands, so gaps vary without any
        // stored state. Overridden from WeatherConfig at DustAtmosphere's bootstrap (ApplyConfig below).
        public static float gustPeriod = 13f;
        public static float attack = 1.6f, hold = 1.1f, release = 3.4f;

        /// <summary>Adopts the facilitator-tunable wind timing from the shared WeatherConfig (called by
        /// DustAtmosphere on Awake, so the asset stays the single tuning surface). Ignores nonsense values
        /// so a zeroed asset field can never freeze the schedule maths.</summary>
        public static void ApplyConfig(WeatherConfig config)
        {
            if (config != null && config.windGustPeriod > 2f)
                gustPeriod = config.windGustPeriod;
        }

        private static float Hash(int k)
        {
            float x = Mathf.Sin(k * 127.1f + 311.7f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        /// <summary>0 in a lull, 1 at the heart of a gust front. The EVENT signal — the lull before and
        /// after is what makes a front legible, so most of the time this is exactly 0.</summary>
        public static float Gust01
        {
            get
            {
                float t = Time.time;
                int k = Mathf.FloorToInt(t / gustPeriod);
                // The front lands at a hashed point inside its window, kept clear of the window edges so
                // the envelope never straddles two windows (Max guards a very short tuned period).
                float slack = Mathf.Max(0f, gustPeriod - attack - hold - release - 2f);
                float front = k * gustPeriod + 1.5f + Hash(k) * slack;
                float d = t - front;
                float g = 0f;
                if (d > -attack && d < hold + release)
                    g = d < 0f ? Mathf.SmoothStep(0f, 1f, (d + attack) / attack)
                               : d < hold ? 1f : Mathf.SmoothStep(1f, 0f, (d - hold) / release);
                return g * (0.55f + 0.45f * Hash(k + 7)); // per-front strength varies too
            }
        }

        /// <summary>The slow always-on sway (~0.05..0.35): two incommensurate sines — never still, never a front.</summary>
        public static float Breath01 =>
            Mathf.Clamp01(0.16f + 0.12f * Mathf.Sin(Time.time * 0.21f) + 0.07f * Mathf.Sin(Time.time * 0.53f + 2f));

        /// <summary>The master 0..1 every subscriber multiplies into its own peak amplitude.</summary>
        public static float Strength01 => Mathf.Clamp01(Breath01 + Gust01 * 0.9f);

        /// <summary>True while a front is above half strength — the cheap edge for event subscribers.</summary>
        public static bool FrontLive => Gust01 > 0.5f;

        /// <summary>True in a front's dying release (dust devils spawn here, so they BELONG to the weather
        /// instead of appearing at random — the DustDevil gate).</summary>
        public static bool InReleaseTail => !FrontLive && Gust01 > 0.05f;

        /// <summary>Which way the wind blows across the road (+1 = left→right in world X). Wanders over minutes.</summary>
        public static float DirectionX => Mathf.Sign(Mathf.Sin(Time.time * 0.011f + 0.8f));

        /// <summary>World-space wind velocity for particle velocity modules (peakSpeed ≈ groundGustSpeed).
        /// Mostly across the road, with a touch of down-road push so fronts sweep rather than curtain.</summary>
        public static Vector3 Velocity(float peakSpeed) =>
            new Vector3(DirectionX * peakSpeed * Strength01, 0f, -peakSpeed * 0.25f * Strength01);
    }
}
