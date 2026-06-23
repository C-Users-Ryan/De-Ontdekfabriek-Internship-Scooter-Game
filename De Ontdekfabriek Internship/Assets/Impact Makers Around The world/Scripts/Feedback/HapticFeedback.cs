using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.Feedback
{
    /// <summary>
    /// Impact haptics (Req §6 / safety-feel): vibrates the tablet when the player hits a hazard or a vehicle,
    /// with the STRENGTH of the buzz scaled by how hard the hit was — a gentle pothole tap versus a hard crash.
    /// It listens to the very same GameEvents that drive the camera shake and scooter wobble, so the hand, the
    /// screen and the bike all react together, and adding it needs no other wiring.
    ///
    /// Strength mapping (both clamped to a felt minimum so even a glancing hit registers):
    ///   - Vehicle collision: Light / Medium / Hard base level, scaled up with the relative closing speed; a
    ///     hit fully absorbed by the safety-net grace gives a softened tap, not the full crash buzz.
    ///   - Hazard: the hazard's own bite (its speedPenalty — a rock harder than a pothole), scaled with speed.
    ///
    /// Self-bootstraps at runtime, so it works with zero scene setup. Cross-platform variable strength is in
    /// <see cref="HapticDriver"/>; this class only decides HOW HARD to buzz. Silent no-op in the editor.
    /// </summary>
    public sealed class HapticFeedback : MonoBehaviour
    {
        [Header("Master")]
        [Tooltip("Master on/off for impact haptics. Can also be toggled at runtime via the PlayerPrefs key 'ksg.haptics' (1 = on, 0 = off) for an accessibility/facilitator switch with no rebuild.")]
        [SerializeField] private bool enableHaptics = true;

        [Header("Vehicle-collision strength (0..1 before speed scaling)")]
        [Range(0f, 1f)] [SerializeField] private float lightHitIntensity = 0.35f;
        [Range(0f, 1f)] [SerializeField] private float mediumHitIntensity = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float hardHitIntensity = 1f;

        [Header("Scaling")]
        [Tooltip("Relative km/h that already counts as a full-strength impact for the speed boost.")]
        [SerializeField] private float referenceKmh = 45f;
        [Tooltip("Floor so even the gentlest hit is still felt in the hand.")]
        [Range(0f, 1f)] [SerializeField] private float minIntensity = 0.15f;

        [Header("Buzz length (ms), mapped from final intensity")]
        [SerializeField] private float minDurationMs = 18f;
        [SerializeField] private float maxDurationMs = 90f;
        [Tooltip("Ignore repeat buzzes closer together than this, so a cluster of hits stays crisp instead of blurring into one long rumble.")]
        [SerializeField] private float minInterval = 0.04f;

        private float lastFireTime = -10f;
        private HapticDriver driver;

        // Self-bootstrap: spawn one instance after the scene loads if none was placed by hand, so impact
        // haptics work with no scene wiring (same approach as the scoring managers).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<HapticFeedback>() != null)
                return;
            var go = new GameObject("HapticFeedback (auto)");
            go.AddComponent<HapticFeedback>();
            DontDestroyOnLoad(go);
        }

        private bool Enabled => enableHaptics && PlayerPrefs.GetInt("ksg.haptics", 1) == 1;

        private void Awake() => driver = HapticDriver.Create();

        private void OnEnable()
        {
            GameEvents.CollisionOccurred += HandleCollision;
            GameEvents.HazardHit += HandleHazard;
        }

        private void OnDisable()
        {
            GameEvents.CollisionOccurred -= HandleCollision;
            GameEvents.HazardHit -= HandleHazard;
        }

        private void HandleCollision(CollisionSeverity severity, float relativeKmh, Vector3 position, bool absorbed)
        {
            float baseIntensity =
                severity == CollisionSeverity.Hard ? hardHitIntensity :
                severity == CollisionSeverity.Medium ? mediumHitIntensity :
                lightHitIntensity;

            float speedScale = Mathf.Clamp01(relativeKmh / Mathf.Max(1f, referenceKmh));
            float intensity = baseIntensity * Mathf.Lerp(0.6f, 1f, speedScale);
            if (absorbed)
                intensity *= 0.5f; // grace forgave the points; the player still felt a clip, so soften, do not silence

            Fire(intensity);
        }

        private void HandleHazard(HazardSpawnConfig definition, float playerKmh, Vector3 position)
        {
            // Per-hazard bite: speedPenalty is "how much momentum this scrubs off" — a rock scrubs more than a
            // pothole — so it doubles as a hardness value for the buzz. Scaled with the speed of the hit.
            float hardness = definition != null ? Mathf.Clamp01(0.3f + definition.speedPenalty) : 0.5f;
            float speedScale = Mathf.Clamp01(playerKmh / Mathf.Max(1f, referenceKmh));
            float intensity = hardness * Mathf.Lerp(0.5f, 1f, speedScale);

            Fire(intensity);
        }

        private void Fire(float intensity)
        {
            if (!Enabled || driver == null)
                return;
            if (Time.unscaledTime - lastFireTime < minInterval)
                return;
            lastFireTime = Time.unscaledTime;

            intensity = Mathf.Clamp01(Mathf.Max(minIntensity, intensity));
            int durationMs = Mathf.RoundToInt(Mathf.Lerp(minDurationMs, maxDurationMs, intensity));
            driver.Vibrate(intensity, durationMs);
        }
    }
}
