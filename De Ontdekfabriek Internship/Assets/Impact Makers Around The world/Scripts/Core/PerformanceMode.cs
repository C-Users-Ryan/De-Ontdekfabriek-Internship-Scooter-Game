using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KenyaScooter.Core
{
    /// <summary>
    /// PERFORMANCE MODES (rebuilt 2026-07-05 from the 06-26 binary low-end switch): keeps the game running
    /// on OLD exhibition tablets by trading ATMOSPHERE for frame rate, never gameplay. One facilitator knob
    /// ("Prestaties" in the BEHEER section) picks a tier:
    ///
    ///  • AUTOMATISCH — detect weak hardware and use LICHT there; full quality everywhere else (default).
    ///  • VOLLEDIG    — everything exactly as authored.
    ///  • GEBALANCEERD — a slightly simpler picture: render scale 0.85, shadows capped, the three most
    ///                   GPU-hungry ambient effects off (heat shimmer, dust devils, dusk shafts), roadside
    ///                   life at 70%.
    ///  • LICHT       — maximum head-room: post-processing OFF, shadows OFF, render scale 0.7, the cosmetic
    ///                   ambient layer off (smoke, truck dust wash, gusts, leaves, laundry, chickens, grass
    ///                   sway, sky life), roadside life at 35%.
    ///
    /// Traffic, hazards, speeds and scoring are IDENTICAL in every tier — performance modes change how the
    /// world looks, never how it plays, so scores stay comparable between devices. Systems that already have
    /// their own facilitator toggle (dust/haze, clouds) are left to their own switch.
    ///
    /// Self-bootstrapping like the other managers (nothing to place); re-applies on every session reset so
    /// late-bootstrapping effects are caught, and applies live when the facilitator changes the setting.
    /// Density-driven spawners read <see cref="DensityScale"/>. Render-scale changes are skipped in the
    /// editor (they would dirty the shared URP asset); everything else previews in the editor too.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public sealed class PerformanceMode : MonoBehaviour
    {
        public enum Tier { Auto = 0, Full = 1, Balanced = 2, Light = 3 }

        /// <summary>PlayerPrefs key holding the facilitator's chosen tier (0 = Auto). Written by the
        /// "Prestaties" setting; read here at every apply.</summary>
        public const string TierPrefKey = "ksg.perfTier";

        [Tooltip("Auto engages LICHT below this much system RAM (MB).")]
        [SerializeField] private int lowRamThresholdMB = 3000;
        [Tooltip("...or below this much reported graphics memory (MB). 0 = unknown, not treated as low.")]
        [SerializeField] private int lowVramThresholdMB = 768;

        /// <summary>The tier actually in effect (Auto already resolved to Full or Light).</summary>
        public static Tier Resolved { get; private set; } = Tier.Full;

        /// <summary>Back-compat for earlier readers: true while the LIGHT tier is engaged.</summary>
        public static bool Active => Resolved == Tier.Light;

        /// <summary>Density multiplier for cosmetic spawners (roadside life). 1 on Full, less on the lower
        /// tiers. Never affects gameplay spawners (traffic, hazards).</summary>
        public static float DensityScale { get; private set; } = 1f;

        private static PerformanceMode instance;

        private bool applied;
        private readonly List<Behaviour> dimmed = new List<Behaviour>(64);

        // Authored (full-quality) values, captured once so switching back UP restores them exactly.
        private bool captured;
        private float authoredShadowDistance;
        private UnityEngine.ShadowQuality authoredShadows; // fully qualified: URP declares its own ShadowQuality
        private float authoredRenderScale = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<PerformanceMode>() == null)
                new GameObject("Performance Mode (auto)").AddComponent<PerformanceMode>();
        }

        private void Awake() => instance = this;

        private void OnEnable() => GameEvents.SessionReset += OnSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= OnSessionReset;

        private void Start() => TryApply();

        private void Update()
        {
            // Retry until there is a camera to act on (some scenes build it late); idle once applied.
            if (!applied)
                TryApply();
        }

        // A reset re-bootstraps effects and re-activates pooled objects: re-assert the tier over the fresh set.
        private void OnSessionReset() => applied = false;

        /// <summary>Re-resolves the tier from the PlayerPrefs choice and re-applies it now. The settings menu
        /// calls this when the facilitator changes "Prestaties", so the tier takes effect live.</summary>
        public static void Reapply()
        {
            if (instance == null)
                return;
            instance.applied = false;
            instance.TryApply();
        }

        private void TryApply()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return; // not ready yet; Update retries

            Tier chosen = (Tier)PlayerPrefs.GetInt(TierPrefKey, (int)Tier.Auto);
            Resolved = chosen == Tier.Auto ? (IsLowEndDevice() ? Tier.Light : Tier.Full) : chosen;

            CaptureAuthoredOnce();
            RestoreAuthored(cam); // always start from the full-quality baseline, then step down

            switch (Resolved)
            {
                case Tier.Balanced:
                    DensityScale = 0.7f;
                    QualitySettings.shadowDistance = Mathf.Min(authoredShadowDistance, 25f);
                    SetRenderScale(0.85f);
                    DimAmbient(balancedOnly: true);
                    break;

                case Tier.Light:
                    DensityScale = 0.35f;
                    QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                    SetRenderScale(0.7f);
                    var data = cam.GetUniversalAdditionalCameraData();
                    if (data != null)
                        data.renderPostProcessing = false; // the single biggest win on a mobile GPU
                    DimAmbient(balancedOnly: false);
                    break;

                default:
                    DensityScale = 1f;
                    break;
            }

            applied = true;
            Debug.Log($"[Kenya Scooter] PerformanceMode: tier {Resolved} in effect " +
                      $"(choice {chosen}, ambient effects dimmed: {dimmed.Count}, density ×{DensityScale:0.##}).");
        }

        // ---- Baseline capture / restore ---------------------------------------------------------------

        private void CaptureAuthoredOnce()
        {
            if (captured)
                return;
            captured = true;
            authoredShadowDistance = QualitySettings.shadowDistance;
            authoredShadows = QualitySettings.shadows;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
                authoredRenderScale = urp.renderScale;
        }

        private void RestoreAuthored(Camera cam)
        {
            QualitySettings.shadowDistance = authoredShadowDistance;
            QualitySettings.shadows = authoredShadows;
            if (authoredRenderScale > 0f)
                SetRenderScale(authoredRenderScale);

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
                data.renderPostProcessing = true;

            // Wake everything a lower tier put to sleep. Their own drivers resume emission; the very fullest
            // look returns at the next session reset, when the effects re-seed themselves.
            for (int i = 0; i < dimmed.Count; i++)
                if (dimmed[i] != null)
                    dimmed[i].enabled = true;
            dimmed.Clear();
        }

        private void SetRenderScale(float scale)
        {
            // In the editor this would DIRTY the shared URP asset (the change persists into the project);
            // in a build it is an in-memory tweak. Preview the rest of the tier in the editor, skip this bit.
            if (Application.isEditor)
                return;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
                urp.renderScale = scale;
        }

        // ---- The cosmetic ambient layer ----------------------------------------------------------------

        /// <summary>
        /// Puts cosmetic ambient effects to sleep for the current tier: the component is disabled and any
        /// particles it owns are stopped and cleared. GAMEPLAY systems are never in these lists, and neither
        /// is anything with its own facilitator toggle (dust/haze, clouds). Pooled instances are included, so
        /// per-vehicle effects (the truck dust wash) stay off when their vehicle respawns.
        /// </summary>
        private void DimAmbient(bool balancedOnly)
        {
            // GEBALANCEERD: only the three most GPU-hungry ambient effects (full-screen/alpha-heavy).
            Dim<KenyaScooter.FX.HeatShimmer>();
            Dim<KenyaScooter.FX.DustDevil>();
            Dim<KenyaScooter.FX.DuskShafts>();
            if (balancedOnly)
                return;

            // LICHT: the rest of the cosmetic layer. The world stays recognisably Kenya (road, traffic,
            // props at reduced density, day cycle); what goes is the garnish.
            Dim<KenyaScooter.FX.SmokeColumn>();
            Dim<KenyaScooter.FX.TruckDustWake>();
            Dim<KenyaScooter.FX.GustFront>();
            Dim<KenyaScooter.Environment.LeafSkitter>();
            Dim<KenyaScooter.Environment.ClothesLine>();
            Dim<KenyaScooter.Environment.RoadsideChickens>();
            Dim<KenyaScooter.Environment.GrassSway>();
            Dim<KenyaScooter.Sky.SkyLife>();
        }

        private void Dim<T>() where T : Behaviour
        {
            T[] found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                T behaviour = found[i];
                if (behaviour == null || !behaviour.enabled)
                    continue;
                behaviour.enabled = false;
                dimmed.Add(behaviour);

                ParticleSystem[] particles = behaviour.GetComponentsInChildren<ParticleSystem>(true);
                for (int p = 0; p < particles.Length; p++)
                    particles[p].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private bool IsLowEndDevice()
        {
            // Never auto-downgrade the editor or a desktop build: only real handheld hardware is a candidate.
            if (Application.isEditor || !Application.isMobilePlatform)
                return false;

            int ram = SystemInfo.systemMemorySize;     // MB, 0 if the platform does not report it
            int vram = SystemInfo.graphicsMemorySize;  // MB, often an estimate on Android
            bool lowRam = ram > 0 && ram < lowRamThresholdMB;
            bool lowVram = vram > 0 && vram < lowVramThresholdMB;
            return lowRam || lowVram;
        }
    }
}
