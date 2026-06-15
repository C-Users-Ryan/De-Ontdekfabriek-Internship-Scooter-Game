using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.Cameras
{
    /// <summary>
    /// Computes a Perlin-noise shake offset for collisions, near misses (M18) and
    /// hazard hits. It exposes CurrentOffset only — CameraRigController applies it,
    /// keeping the single-camera-writer rule intact.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        [SerializeField] private float frequency = 14f;
        [SerializeField] private float nearMissStrength = 0.3f;
        [SerializeField] private float hazardStrength = 0.2f;
        [SerializeField] private float lightHitStrength = 0.25f;
        [SerializeField] private float hardHitStrength = 0.7f;
        [SerializeField] private float defaultDuration = 0.45f;

        public Vector3 CurrentOffset { get; private set; }

        private float strength;
        private float duration;
        private float elapsed;

        private void OnEnable()
        {
            GameEvents.CollisionOccurred += HandleCollision;
            GameEvents.NearMiss += HandleNearMiss;
            GameEvents.HazardHit += HandleHazard;
            GameEvents.SessionReset += HandleSessionReset;
        }

        private void OnDisable()
        {
            GameEvents.CollisionOccurred -= HandleCollision;
            GameEvents.NearMiss -= HandleNearMiss;
            GameEvents.HazardHit -= HandleHazard;
            GameEvents.SessionReset -= HandleSessionReset;
        }

        private void Update()
        {
            if (elapsed >= duration)
            {
                CurrentOffset = Vector3.zero;
                return;
            }

            elapsed += Time.deltaTime;
            float falloff = 1f - Mathf.Clamp01(elapsed / duration);
            float t = Time.time * frequency;
            CurrentOffset = new Vector3(
                (Mathf.PerlinNoise(t, 0.5f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0.5f, t) - 0.5f) * 2f,
                0f) * (strength * falloff * falloff);
        }

        /// <summary>Starts (or restarts, if stronger) a shake.</summary>
        public void Impulse(float newStrength, float newDuration)
        {
            float remaining = strength * (1f - Mathf.Clamp01(elapsed / Mathf.Max(duration, 0.01f)));
            if (newStrength < remaining)
                return;

            strength = newStrength;
            duration = newDuration;
            elapsed = 0f;
        }

        private void HandleCollision(CollisionSeverity severity, float relativeKmh, Vector3 position, bool absorbed)
        {
            float impulseStrength = severity == CollisionSeverity.Hard ? hardHitStrength : lightHitStrength;
            Impulse(impulseStrength, defaultDuration);
        }

        private void HandleNearMiss(Traffic.TrafficVehicle vehicle) => Impulse(nearMissStrength, defaultDuration * 0.7f);

        private void HandleHazard(HazardKind kind, float playerKmh, Vector3 position)
            => Impulse(hazardStrength * Mathf.Max(0.5f, WorldSpeed.Instance.SpeedRatio), defaultDuration * 0.6f);

        private void HandleSessionReset()
        {
            strength = 0f;
            elapsed = duration;
            CurrentOffset = Vector3.zero;
        }
    }
}
