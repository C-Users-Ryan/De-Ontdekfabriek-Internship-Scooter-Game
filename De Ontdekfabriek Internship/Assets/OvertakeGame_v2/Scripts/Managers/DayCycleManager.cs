using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Cycles through Morning → Midday → Sunset → Morning automatically.
    ///
    /// SETUP TO FIX BLACK SKYBOX:
    ///   1. In Lighting window (Window > Rendering > Lighting):
    ///      - Set Environment > Skybox Material to your skybox material.
    ///      - Set Sun Source to your Directional Light.
    ///   2. Each Volume GameObject needs:
    ///      - Volume component with Is Global = TRUE and a Profile assigned.
    ///      - Weight starts at 0; this script drives it.
    ///   3. Drag all three Volumes and the Directional Light into this script's
    ///      Inspector slots. If a slot is empty the script skips it safely.
    /// </summary>
    public class DayCycleManager : MonoBehaviour
    {
        public static DayCycleManager Instance { get; private set; }

        public enum TimeOfDay { Morning, Midday, Sunset }

        [Header("Volume Presets")]
        public Volume morningVolume;
        public Volume middayVolume;
        public Volume sunsetVolume;

        [Header("Directional Light")]
        public Light directionalLight;

        [Header("Morning — #FFD28A")]
        public Color morningLightColour = new Color(1f, 0.82f, 0.54f);
        public float morningIntensity = 0.7f;

        [Header("Midday — #FFFDE8")]
        public Color middayLightColour = new Color(1f, 0.99f, 0.91f);
        public float middayIntensity = 1.2f;

        [Header("Sunset — #FF6B2B")]
        public Color sunsetLightColour = new Color(1f, 0.42f, 0.17f);
        public float sunsetIntensity = 0.6f;

        [Header("Day Cycle")]
        [Tooltip("Total real-time seconds for one full day (Morning → Midday → Sunset → back to Morning).")]
        public float dayDuration = 120f;

        [Tooltip("Seconds to blend between two times of day. Should be less than dayDuration / 3.")]
        public float transitionDuration = 5f;

        [Tooltip("Which time of day the game starts on.")]
        public TimeOfDay startTime = TimeOfDay.Morning;

        [Tooltip("Automatically cycle through times of day. Uncheck to stay on startTime.")]
        public bool autoCycle = true;

        // ── Public state ──────────────────────────────────────────────────────

        public static TimeOfDay Current { get; private set; }

        /// <summary>0–1 progress through the current day phase (Morning/Midday/Sunset).</summary>
        public float PhaseProgress { get; private set; }

        // ── Internals ─────────────────────────────────────────────────────────

        private float _phaseTimer;       // time spent in current phase
        private float _phaseDuration;    // how long each phase lasts = dayDuration / 3
        private bool _transitioning;
        private Coroutine _transitionCoroutine;
        private int _cycleIndex;         // cached index to avoid Array.IndexOf each advance

        private static readonly TimeOfDay[] _cycle =
            { TimeOfDay.Morning, TimeOfDay.Midday, TimeOfDay.Sunset };

        // ── Unity lifecycle ───────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            _phaseDuration = dayDuration / 3f;
            _phaseTimer    = 0f;
            _cycleIndex    = System.Array.IndexOf(_cycle, startTime);
            if (_cycleIndex < 0) _cycleIndex = 0;
            Current = startTime;
            ApplyInstant(Current);
        }

        void Update()
        {
            if (!autoCycle || _transitioning) return;

            _phaseTimer += Time.deltaTime;
            PhaseProgress = Mathf.Clamp01(_phaseTimer / _phaseDuration);

            if (_phaseTimer >= _phaseDuration)
            {
                _phaseTimer = 0f;
                AdvanceCycle();
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Manually jump to a time of day with a smooth blend.</summary>
        public void SetTime(TimeOfDay t)
        {
            if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = StartCoroutine(TransitionTo(t));
        }

        /// <summary>Skip to the next phase immediately — useful for a debug button.</summary>
        public void CycleNext()
        {
            _phaseTimer = 0f;
            AdvanceCycle();
        }

        // ── Cycle logic ───────────────────────────────────────────────────────

        private void AdvanceCycle()
        {
            _cycleIndex = (_cycleIndex + 1) % _cycle.Length;
            SetTime(_cycle[_cycleIndex]);
        }

        // ── Instant apply ─────────────────────────────────────────────────────

        private void ApplyInstant(TimeOfDay t)
        {
            // Enable only the target volume at full weight
            SetVolumeWeight(morningVolume, t == TimeOfDay.Morning ? 1f : 0f);
            SetVolumeWeight(middayVolume, t == TimeOfDay.Midday ? 1f : 0f);
            SetVolumeWeight(sunsetVolume, t == TimeOfDay.Sunset ? 1f : 0f);

            ApplyLight(PresetFor(t));
        }

        // ── Smooth transition ─────────────────────────────────────────────────

        private IEnumerator TransitionTo(TimeOfDay next)
        {
            _transitioning = true;

            TimeOfDay prev = Current;
            Current = next;

            LightPreset fromP = PresetFor(prev);
            LightPreset toP = PresetFor(next);

            // Make sure both are active for blending
            GetVolume(prev)?.gameObject.SetActive(true);
            GetVolume(next)?.gameObject.SetActive(true);

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, transitionDuration);

            while (elapsed < duration)
            {
                float s = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                SetVolumeWeight(GetVolume(prev), 1f - s);
                SetVolumeWeight(GetVolume(next), s);
                ApplyLight(LerpPreset(fromP, toP, s));

                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyInstant(next);
            _transitioning = false;
            _transitionCoroutine = null;
        }

        // ── Light helpers ─────────────────────────────────────────────────────

        private void ApplyLight(LightPreset p)
        {
            if (directionalLight == null) return;
            directionalLight.color = p.colour;
            directionalLight.intensity = p.intensity;
            directionalLight.transform.rotation = p.rotation;
        }

        private LightPreset LerpPreset(LightPreset a, LightPreset b, float t) => new LightPreset
        {
            colour = Color.Lerp(a.colour, b.colour, t),
            intensity = Mathf.Lerp(a.intensity, b.intensity, t),
            rotation = Quaternion.Slerp(a.rotation, b.rotation, t)
        };

        private LightPreset PresetFor(TimeOfDay t) => t switch
        {
            TimeOfDay.Morning => new LightPreset
            {
                colour = morningLightColour,
                intensity = morningIntensity,
                rotation = Quaternion.Euler(-15f, 30f, 0f)
            },
            TimeOfDay.Midday => new LightPreset
            {
                colour = middayLightColour,
                intensity = middayIntensity,
                rotation = Quaternion.Euler(-75f, 0f, 0f)
            },
            _ => new LightPreset
            {
                colour = sunsetLightColour,
                intensity = sunsetIntensity,
                rotation = Quaternion.Euler(-5f, -60f, 0f)
            }
        };

        // ── Volume helpers ────────────────────────────────────────────────────

        private Volume GetVolume(TimeOfDay t) => t switch
        {
            TimeOfDay.Morning => morningVolume,
            TimeOfDay.Midday => middayVolume,
            _ => sunsetVolume
        };

        private void SetVolumeWeight(Volume vol, float weight)
        {
            if (vol == null) return;
            vol.gameObject.SetActive(weight > 0.001f);
            vol.weight = weight;
        }

        // ── Display name ──────────────────────────────────────────────────────

        public static string DisplayName() => Current switch
        {
            TimeOfDay.Morning => "ASUBUHI",
            TimeOfDay.Midday => "MCHANA",
            TimeOfDay.Sunset => "JIONI",
            _ => ""
        };

        // ── Structs ───────────────────────────────────────────────────────────

        private struct LightPreset
        {
            public Color colour;
            public float intensity;
            public Quaternion rotation;
        }
    }
}