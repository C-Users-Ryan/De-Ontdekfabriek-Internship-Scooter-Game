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
    /// The shake is layered Perlin noise, not a sine — washboard ripple with the odd bigger stone, so it
    /// reads as SURFACE, not vibration. It scales with the eased RoadSurfaceFeel.DirtBlend01 (so it grabs at
    /// the dirt tile's seam and settles back on tarmac), with world speed (a stopped scooter sits still) and
    /// with the motion-sensitivity dial (the Prikkelarm players get a calmer ride here too, exactly like the
    /// lean and the speed FX). It moves ONLY the scooter model, never the camera or the lens — the speed-FX
    /// saga's core lesson — so the handlebars judder against a steady view, like a real bike on bad road.
    /// </summary>
    public sealed class DirtRumble : MonoBehaviour
    {
        [Tooltip("Peak roll jitter in degrees at full speed on full dirt. Keep small — the read is 'rough road', not 'crash wobble'.")]
        [SerializeField] private float rollAmplitude = 1.6f;
        [Tooltip("Peak vertical shake of the model in metres. A couple of centimetres reads as suspension chatter.")]
        [SerializeField] private float bobAmplitude = 0.022f;
        [Tooltip("Base shake frequency (noise scrolls per second) at cruise. Rises with speed, like real washboard.")]
        [SerializeField] private float baseFrequency = 7f;

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

            // Two noise octaves: a slow washboard base plus a faster sparkle — the odd 'bigger stone' falls
            // out of the octaves lining up, so the shake never turns into a metronome.
            CurrentRoll = Noise(noiseTime, 0.37f) * rollAmplitude * strength;
            CurrentBob = Noise(noiseTime * 1.7f, 5.11f) * bobAmplitude * strength;
        }

        /// <summary>Signed layered Perlin noise, roughly -1..1.</summary>
        private static float Noise(float t, float seed)
        {
            float slow = Mathf.PerlinNoise(t, seed) * 2f - 1f;
            float fast = Mathf.PerlinNoise(t * 2.7f, seed + 11.3f) * 2f - 1f;
            return slow * 0.65f + fast * 0.35f;
        }

        private void HandleSessionReset()
        {
            CurrentRoll = 0f;
            CurrentBob = 0f;
            noiseTime = 0f;
        }
    }
}
