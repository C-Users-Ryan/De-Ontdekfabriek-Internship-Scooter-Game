using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.Player
{
    /// <summary>
    /// Decaying roll wobble after a hazard hit (M21): a sine wave whose amplitude
    /// decays exponentially, scaled by how fast the player was going. ScooterLean
    /// reads CurrentRoll and adds it to the steering lean — this component never
    /// touches a transform itself.
    /// </summary>
    public sealed class ScooterWobble : MonoBehaviour
    {
        [SerializeField] private ScooterConfig config;

        public float CurrentRoll { get; private set; }

        private float amplitude;
        private float time;

        private void OnEnable()
        {
            GameEvents.HazardHit += HandleHazardHit;
            GameEvents.SessionReset += HandleSessionReset;
        }

        private void OnDisable()
        {
            GameEvents.HazardHit -= HandleHazardHit;
            GameEvents.SessionReset -= HandleSessionReset;
        }

        private void Update()
        {
            if (amplitude < 0.01f)
            {
                CurrentRoll = 0f;
                return;
            }

            time += Time.deltaTime;
            float decay = Mathf.Exp(-config.wobbleDecay * time);
            CurrentRoll = Mathf.Sin(time * config.wobbleFrequency) * amplitude * decay;

            if (decay < 0.02f)
                amplitude = 0f;
        }

        private void HandleHazardHit(HazardKind kind, float playerKmh, Vector3 position)
        {
            // Harder at speed — the wobble is the physical half of the speed-scaled penalty.
            amplitude = config.wobbleAmplitude * Mathf.Max(0.4f, WorldSpeed.Instance.SpeedRatio);
            time = 0f;
        }

        private void HandleSessionReset()
        {
            amplitude = 0f;
            CurrentRoll = 0f;
        }
    }
}
