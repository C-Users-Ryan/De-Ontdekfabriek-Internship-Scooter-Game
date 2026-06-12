using UnityEngine;
using UnityEngine.Rendering;

namespace OvertakeGame
{
    /// <summary>
    /// Drives a one-way dawn-to-sunset light arc across the session duration.
    ///
    /// KEY DESIGN DECISION (see R16_Research_DayCycle_Temporal_Perception):
    ///   A full day arc compressed into 120 seconds creates "temporal compression" —
    ///   the player perceives the session as longer and more complete. The light arc
    ///   also aligns emotionally with the gameplay arc: harsh midday light at peak
    ///   difficulty (Tsavo), warm golden sunset as the session resolves.
    ///
    /// HOW IT WORKS:
    ///   Progress is driven by GameManager.Instance.SessionProgress (0–1).
    ///   Five named keyframes sit along that 0–1 curve. Every Update, the system
    ///   finds which two keyframes bracket the current progress and smoothly
    ///   interpolates between them. There are NO discrete phase jumps and NO
    ///   transition coroutines — the light changes continuously, every frame.
    ///
    /// SETUP:
    ///   1. Window > Rendering > Lighting:
    ///      - Skybox Material: assign your procedural or HDRI sky material
    ///      - Sun Source: assign your Directional Light
    ///      - Ambient Mode: Color (this script drives ambientLight directly)
    ///   2. URP Volumes (optional — enhance post-processing per phase):
    ///      - Three Volume GameObjects: morningVolume, middayVolume, sunsetVolume
    ///      - Each needs Is Global = TRUE and a Profile with color-grading overrides
    ///      - This script blends their weights; leave Skybox overrides OFF in the profiles
    ///      - If you leave all three empty, the script still works via direct light control
    ///   3. Assign directionalLight in Inspector. If empty, light is not driven.
    ///   4. The script reads GameManager.Instance.SessionProgress. If GameManager is
    ///      absent (e.g. in a test scene), it falls back to its own elapsed timer.
    /// </summary>
    public class DayCycleManager : MonoBehaviour
    {
        public static DayCycleManager Instance { get; private set; }

        // ── Keyframe definition ───────────────────────────────────────────────

        [System.Serializable]
        public struct DayKeyframe
        {
            [Tooltip("Where along the session (0 = start, 1 = end) this keyframe sits.")]
            [Range(0f, 1f)] public float sessionProgress;

            [Tooltip("Swahili time-of-day label shown in the HUD. Leave empty to suppress label change.")]
            public string swahiliLabel;

            [Tooltip("Directional light (sun) colour at this keyframe.")]
            public Color sunColor;

            [Tooltip("Directional light intensity at this keyframe. Midday ~1.2, dawn/sunset ~0.5.")]
            [Range(0f, 2f)] public float sunIntensity;

            [Tooltip("Sun angle: X = elevation (90 = overhead, 10 = near horizon), Y = heading (negative = west).")]
            public Vector3 sunEuler;

            [Tooltip("RenderSettings.ambientLight at this keyframe. This is the main scene fill light — "+
                     "if this is dark, the scene is dark. Dawn: warm orange-brown. Midday: near white. Sunset: deep orange.")]
            public Color ambientColor;

            [Tooltip("Overall ambient intensity multiplier at this keyframe.")]
            [Range(0f, 1f)] public float ambientIntensity;

            [Tooltip("Exponential fog density. 0 = no fog. ~0.003 for midday haze, ~0.008 for dusty sunset.")]
            [Range(0f, 0.02f)] public float fogDensity;

            [Tooltip("Fog colour at this keyframe.")]
            public Color fogColor;
        }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Light Source")]
        [Tooltip("The scene's Directional Light (the sun). Required for sun angle and colour.")]
        [SerializeField] private Light directionalLight;

        [Header("URP Post-Processing Volumes (optional)")]
        [Tooltip("Volume active during morning phase (dawn → pre-midday). Blend weight driven by this script.")]
        [SerializeField] private Volume morningVolume;
        [Tooltip("Volume active during midday phase.")]
        [SerializeField] private Volume middayVolume;
        [Tooltip("Volume active during sunset phase.")]
        [SerializeField] private Volume sunsetVolume;

        [Header("Day Arc Keyframes")]
        [Tooltip("Five keyframes defining the light arc from dawn to sunset. " +
                 "Must be sorted by sessionProgress 0→1. " +
                 "The system always interpolates between the two that bracket the current progress.")]
        [SerializeField] private DayKeyframe[] keyframes = DefaultKeyframes();

        [Header("Debug / Fallback")]
        [Tooltip("If GameManager is absent, the cycle uses this duration (seconds) as a standalone timer. " +
                 "Matches the default session length.")]
        [SerializeField] private float fallbackSessionDuration = 120f;

        [Tooltip("Pause the cycle at a fixed progress for testing. 0 = session start, 1 = session end.")]
        [SerializeField] [Range(0f, 1f)] private float debugProgress = 0f;
        [Tooltip("When ON, debugProgress overrides the live session progress. Use to preview each phase.")]
        [SerializeField] private bool useDebugProgress = false;

        // ── Public state ──────────────────────────────────────────────────────

        /// <summary>Current session progress 0–1 being used this frame.</summary>
        public float CurrentProgress { get; private set; }

        /// <summary>Swahili label for the currently active named phase.</summary>
        public static string CurrentLabel { get; private set; } = "ASUBUHI";

        // ── Internals ─────────────────────────────────────────────────────────

        private float _fallbackTimer;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void Start()
        {
            EnsureKeyframesSorted();
            ApplyProgress(0f);
        }

        private void Update()
        {
            float progress;

            if (useDebugProgress)
            {
                progress = debugProgress;
            }
            else if (GameManager.Instance != null)
            {
                progress = GameManager.Instance.SessionProgress;
            }
            else
            {
                // Fallback: drive by own timer when GameManager is absent (e.g. isolated test scene)
                _fallbackTimer += Time.deltaTime;
                progress = Mathf.Clamp01(_fallbackTimer / fallbackSessionDuration);
            }

            CurrentProgress = progress;
            ApplyProgress(progress);
        }

        // ── Core evaluation ───────────────────────────────────────────────────

        /// <summary>
        /// Evaluates the day arc at the given 0–1 progress and applies all light values.
        /// Called every frame — all operations must be cheap.
        /// </summary>
        private void ApplyProgress(float p)
        {
            if (keyframes == null || keyframes.Length < 2) return;

            // Find the two keyframes that bracket p
            int lo = 0;
            for (int i = 0; i < keyframes.Length - 1; i++)
            {
                if (keyframes[i].sessionProgress <= p) lo = i;
            }
            int hi = Mathf.Min(lo + 1, keyframes.Length - 1);

            DayKeyframe a = keyframes[lo];
            DayKeyframe b = keyframes[hi];

            // Local t between the two keyframes (0 at a, 1 at b)
            float span = b.sessionProgress - a.sessionProgress;
            float localT = span > 0.0001f
                ? Mathf.Clamp01((p - a.sessionProgress) / span)
                : 1f;

            // SmoothStep so transitions ease in and out rather than being linear
            float s = Mathf.SmoothStep(0f, 1f, localT);

            // ── Directional light (sun) ───────────────────────────────────────
            if (directionalLight != null)
            {
                directionalLight.color     = Color.Lerp(a.sunColor, b.sunColor, s);
                directionalLight.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, s);
                directionalLight.transform.rotation = Quaternion.Slerp(
                    Quaternion.Euler(a.sunEuler),
                    Quaternion.Euler(b.sunEuler),
                    s);
            }

            // ── Ambient light (THE main fix for the dark scene) ───────────────
            // RenderSettings.ambientLight is the scene fill light.
            // URP Volumes only affect post-processing — they do NOT light the scene.
            // This direct assignment is what actually prevents the scene going dark.
            Color ambientTarget = Color.Lerp(a.ambientColor, b.ambientColor, s);
            float ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, s);
            RenderSettings.ambientLight = ambientTarget * ambientIntensity;

            // ── Fog ──────────────────────────────────────────────────────────
            RenderSettings.fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, s);
            RenderSettings.fogColor   = Color.Lerp(a.fogColor, b.fogColor, s);
            RenderSettings.fog        = RenderSettings.fogDensity > 0.0001f;

            // ── URP Volume weights ────────────────────────────────────────────
            // Map progress to a 0-2 float: 0=morning, 1=midday, 2=sunset
            // Volume weights are derived from distance to their "centre" on this scale.
            float volumePos = p * 2f;  // 0–2 range
            SetVolume(morningVolume, VolumeWeight(volumePos, 0f));
            SetVolume(middayVolume,  VolumeWeight(volumePos, 1f));
            SetVolume(sunsetVolume,  VolumeWeight(volumePos, 2f));

            // ── HUD label update ──────────────────────────────────────────────
            // Only update the label when crossing a keyframe with a non-empty label
            if (!string.IsNullOrEmpty(b.swahiliLabel) && localT >= 0.5f)
                CurrentLabel = b.swahiliLabel;
            else if (!string.IsNullOrEmpty(a.swahiliLabel) && localT < 0.5f)
                CurrentLabel = a.swahiliLabel;
        }

        // ── Volume helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns 0-1 weight for a volume centred at <paramref name="centre"/> on a 0–2 scale.
        /// Uses a triangle falloff: full weight at centre, zero at ±1.
        /// </summary>
        private static float VolumeWeight(float pos, float centre)
            => Mathf.Clamp01(1f - Mathf.Abs(pos - centre));

        private static void SetVolume(Volume vol, float weight)
        {
            if (vol == null) return;
            bool active = weight > 0.001f;
            if (vol.gameObject.activeSelf != active) vol.gameObject.SetActive(active);
            vol.weight = weight;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Legacy compatibility: returns the current Swahili label.
        /// Prefer <see cref="CurrentLabel"/> for direct access.
        /// </summary>
        public static string DisplayName() => CurrentLabel;

        /// <summary>Jump immediately to a specific progress (0–1). Useful for facilitator debug.</summary>
        public void SetProgress(float p)
        {
            _fallbackTimer = p * fallbackSessionDuration;
            ApplyProgress(Mathf.Clamp01(p));
        }

        // ── Default keyframes ─────────────────────────────────────────────────

        /// <summary>
        /// Factory for the five default Kenya day arc keyframes.
        /// These values are set as the Inspector defaults so the script works out-of-the-box.
        /// All colours grounded in East African field photography analysis (R15, R16).
        ///
        /// Keyframe positions (SessionProgress):
        ///   0.00  MAPAMBAZUKO — pre-dawn, 12° sun, deep warm gold fill
        ///   0.15  ASUBUHI     — morning, 30° sun, golden light
        ///   0.50  MCHANA      — midday, 78° near-overhead, near-white harsh light
        ///   0.75  ALASIRI     — late afternoon, 35° sinking west, warm amber gold
        ///   1.00  JIONI       — sunset, 8° west, deep orange-red
        ///
        /// Sun rotation convention:
        ///   X = elevation angle (negative = above horizon, positive = below)
        ///   Y = heading (negative Y = sun coming from east moving west)
        ///   The sun rotates from east-low (morning) through overhead (midday) to west-low (sunset).
        /// </summary>
        private static DayKeyframe[] DefaultKeyframes() => new DayKeyframe[]
        {
            // ── 0: MAPAMBAZUKO — Pre-dawn ─────────────────────────────────────
            new DayKeyframe
            {
                sessionProgress = 0f,
                swahiliLabel    = "ASUBUHI",
                sunColor        = new Color(1.00f, 0.55f, 0.20f),   // deep orange-gold
                sunIntensity    = 0.50f,
                sunEuler        = new Vector3(-12f, 60f, 0f),        // low, east
                ambientColor    = new Color(0.55f, 0.38f, 0.22f),   // warm brown fill
                ambientIntensity = 0.70f,
                fogDensity      = 0.004f,
                fogColor        = new Color(0.80f, 0.55f, 0.30f),   // warm dawn haze
            },

            // ── 1: ASUBUHI — Golden morning ──────────────────────────────────
            new DayKeyframe
            {
                sessionProgress = 0.15f,
                swahiliLabel    = "ASUBUHI",
                sunColor        = new Color(1.00f, 0.82f, 0.54f),   // #FFD28A
                sunIntensity    = 0.85f,
                sunEuler        = new Vector3(-30f, 40f, 0f),        // 30° elevation, east-ish
                ambientColor    = new Color(0.72f, 0.58f, 0.38f),   // warm golden ambient
                ambientIntensity = 0.80f,
                fogDensity      = 0.002f,
                fogColor        = new Color(0.85f, 0.72f, 0.50f),   // golden morning haze
            },

            // ── 2: MCHANA — Harsh midday ─────────────────────────────────────
            new DayKeyframe
            {
                sessionProgress = 0.50f,
                swahiliLabel    = "MCHANA",
                sunColor        = new Color(1.00f, 0.99f, 0.91f),   // #FFFDE8 — near-white
                sunIntensity    = 1.25f,
                sunEuler        = new Vector3(-78f, 0f, 0f),         // near-overhead, slight south
                ambientColor    = new Color(0.65f, 0.65f, 0.60f),   // cool bright ambient
                ambientIntensity = 0.90f,
                fogDensity      = 0.003f,
                fogColor        = new Color(0.85f, 0.85f, 0.78f),   // midday dust haze (slightly warm)
            },

            // ── 3: ALASIRI — Late afternoon ───────────────────────────────────
            new DayKeyframe
            {
                sessionProgress = 0.75f,
                swahiliLabel    = "ALASIRI",
                sunColor        = new Color(1.00f, 0.75f, 0.35f),   // warm amber-gold
                sunIntensity    = 0.90f,
                sunEuler        = new Vector3(-35f, -40f, 0f),       // 35° elevation, sinking west
                ambientColor    = new Color(0.68f, 0.50f, 0.28f),   // warm amber fill
                ambientIntensity = 0.75f,
                fogDensity      = 0.005f,
                fogColor        = new Color(0.82f, 0.60f, 0.32f),   // dusty late-afternoon haze
            },

            // ── 4: JIONI — Sunset ─────────────────────────────────────────────
            new DayKeyframe
            {
                sessionProgress = 1.00f,
                swahiliLabel    = "JIONI",
                sunColor        = new Color(1.00f, 0.42f, 0.17f),   // #FF6B2B — deep orange-red
                sunIntensity    = 0.45f,
                sunEuler        = new Vector3(-8f, -60f, 0f),        // very low, far west
                ambientColor    = new Color(0.52f, 0.30f, 0.14f),   // deep warm orange fill
                ambientIntensity = 0.65f,
                fogDensity      = 0.008f,
                fogColor        = new Color(0.70f, 0.40f, 0.20f),   // heavy sunset dust haze
            },
        };

        // ── Utility ───────────────────────────────────────────────────────────

        /// <summary>
        /// Ensures keyframes are sorted ascending by sessionProgress.
        /// Called once at Start — not on every frame.
        /// </summary>
        private void EnsureKeyframesSorted()
        {
            if (keyframes == null) return;
            System.Array.Sort(keyframes, (a, b) => a.sessionProgress.CompareTo(b.sessionProgress));
        }
    }
}
