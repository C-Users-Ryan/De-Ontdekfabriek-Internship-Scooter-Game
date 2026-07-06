using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.FX;
using KenyaScooter.Roads;

namespace KenyaScooter.Player
{
    /// <summary>
    /// The rocky ride of a dirt (murram) road (2026-07-05): a continuous low-amplitude shake of the scooter
    /// VISUAL while the tile under the tyres is <see cref="RoadSurfaceType.Dirt"/>. Same contract as
    /// ScooterWobble — this component only computes <see cref="CurrentRoll"/> and <see cref="CurrentBob"/>;
    /// ScooterLean adds them onto the model, so nothing here ever touches a transform and the two shakes
    /// compose instead of fighting.
    ///
    /// The shake is layered Perlin noise, not a sine — a fine washboard ripple, so it reads as a loose dusty
    /// SURFACE, not a vibration and not jolting over rocks. It scales with the eased RoadSurfaceFeel.DirtBlend01 (so it grabs at
    /// the dirt tile's seam and settles back on tarmac), with world speed (a stopped scooter sits still) and
    /// with the motion-sensitivity dial (the Prikkelarm players get a calmer ride here too, exactly like the
    /// lean and the speed FX). It moves ONLY the scooter model, never the camera or the lens — the speed-FX
    /// saga's core lesson — so the handlebars judder against a steady view, like a real bike on bad road.
    /// </summary>
    public sealed class DirtRumble : MonoBehaviour
    {
        [Tooltip("Peak roll jitter in degrees at full speed on full dirt. Kept FAINT on purpose — the dirt road " +
                 "is communicated by the DUST (ScooterDirtDust), not by shaking the bike; this is only a whisper " +
                 "of tactile texture under it. ~0.25 is a hint; much above 0.5 starts to read as a rough ride.")]
        [SerializeField] private float rollAmplitude = 0.25f;
        [Tooltip("Peak vertical shake of the model in metres. A few millimetres is a subtle settle; more than " +
                 "that and the bike starts to feel like it is bouncing, which we no longer want.")]
        [SerializeField] private float bobAmplitude = 0.004f;
        [Tooltip("Base shake frequency (noise scrolls per second) at cruise. Rises with speed. Lowish = a slow, " +
                 "soft sway rather than a buzz.")]
        [SerializeField] private float baseFrequency = 6.5f;

        /// <summary>Roll (degrees) ScooterLean adds on top of steer lean + turn lean + hazard wobble.</summary>
        public float CurrentRoll { get; private set; }

        /// <summary>Vertical offset (metres) ScooterLean adds to the visual, shaking the model on its wheels.</summary>
        public float CurrentBob { get; private set; }

        private float noiseTime;

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        private void Update()
        {
            float blend = RoadSurfaceFeel.DirtBlend01;
            float intensity = RoadSurfaceFeel.IntensityScale; // the "Onverharde wegen" facilitator setting
            GameState state = GameManager.State;
            if (blend <= 0.001f || intensity <= 0.001f || WorldSpeed.Instance == null
                || (state != GameState.Playing && state != GameState.AtCheckpoint))
            {
                CurrentRoll = 0f;
                CurrentBob = 0f;
                return;
            }

            // Strength: the surface blend, gated to zero at standstill (full above ~30% speed), tempered by
            // the motion dial and the facilitator's dirt-feel step. Frequency also rises with speed — faster
            // over the same washboard = busier shake.
            float ratio = WorldSpeed.Instance.SpeedRatio;
            float speedGate = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ratio / 0.3f));
            float strength = blend * speedGate * Mathf.Lerp(0.6f, 1f, ratio) * SpeedFeel.MotionScale * intensity;

            noiseTime += Time.deltaTime * baseFrequency * Mathf.Lerp(0.7f, 1.5f, ratio);

            // Two noise octaves, weighted toward the FINER one: a loose-dirt shimmy with only a hint of slow
            // undulation, so the ride reads as sand/murram — never a metronome, never a boulder field.
            CurrentRoll = Noise(noiseTime, 0.37f) * rollAmplitude * strength;
            CurrentBob = Noise(noiseTime * 1.7f, 5.11f) * bobAmplitude * strength;
        }

        /// <summary>Signed layered Perlin noise, roughly -1..1. Weighted toward the faster octave so the shake is
        /// a fine loose-surface buzz rather than slow heaves that read as driving over rocks.</summary>
        private static float Noise(float t, float seed)
        {
            float slow = Mathf.PerlinNoise(t, seed) * 2f - 1f;
            float fast = Mathf.PerlinNoise(t * 3.3f, seed + 11.3f) * 2f - 1f;
            return slow * 0.4f + fast * 0.6f;
        }

        private void HandleSessionReset()
        {
            CurrentRoll = 0f;
            CurrentBob = 0f;
            noiseTime = 0f;
        }
    }
}
