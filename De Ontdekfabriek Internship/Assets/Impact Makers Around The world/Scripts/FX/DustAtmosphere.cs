using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;
using KenyaScooter.Settings;

namespace KenyaScooter.FX
{
    /// <summary>
    /// The warm dry-air HAZE of a Kenyan road (Oplevering 25 Jun 2026), driven by <see cref="WeatherConfig"/>.
    /// Two parts, both gated by the facilitator <c>dustEnabled</c> toggle:
    ///   1. FOG — enables exponential fog and drives its DENSITY only. It never sets the fog COLOUR, which the
    ///      DayCycleManager owns (warmed per phase), so the two systems do not fight over RenderSettings.
    ///   2. A drifting warm-dust particle VEIL anchored to the camera, built at runtime on a render-pipeline-safe
    ///      soft-sprite material (mirrors SpeedLines: the built-in particle material renders magenta under URP).
    ///
    /// ON BY DEFAULT, BUT REVERSIBLE. It self-bootstraps with a runtime-default config (no asset needed) and
    /// captures the scene's original fog state on Awake; with the toggle OFF it eases the fog back to exactly
    /// that and stops the veil, so the liked straight-road build is one facilitator switch ("Stof en haze") away.
    /// </summary>
    public sealed class DustAtmosphere : MonoBehaviour
    {
        [SerializeField] private WeatherConfig config;

        // Self-bootstrap after scene load (mirroring PedestrianCrossingSpawner / HapticFeedback), and ON BY
        // DEFAULT: if no WeatherConfig asset is present it uses a runtime-default one, so the Kenyan dust shows
        // in the build with zero wiring. A facilitator can still switch it off ("Stof en haze"); create the
        // asset (the editor tool) only if you want the choice to PERSIST across restarts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<DustAtmosphere>() != null)
                return;
            var go = new GameObject("DustAtmosphere (auto)");
            go.AddComponent<DustAtmosphere>().config = ResolveConfig();
        }

        /// <summary>The one WeatherConfig everything dust-related shares: the project asset if one exists, otherwise
        /// a runtime-default instance created once (so the dust, vehicle trails and speed dust all read the same
        /// toggle). Found by the facilitator menu too, since runtime ScriptableObjects are discoverable.</summary>
        public static WeatherConfig ActiveConfig { get; private set; }

        public static WeatherConfig ResolveConfig()
        {
            if (ActiveConfig != null)
                return ActiveConfig;
            ActiveConfig = ConfigLocator.Find<WeatherConfig>();
            if (ActiveConfig == null)
            {
                ActiveConfig = ScriptableObject.CreateInstance<WeatherConfig>();
                ActiveConfig.name = "WeatherConfig (runtime default)";
            }
            return ActiveConfig;
        }

        // Captured scene fog state, restored whenever dust is off.
        private bool origFog;
        private FogMode origFogMode;
        private float origFogDensity;

        private static DustAtmosphere instance;   // for the cheap static PulseVeil hook (TruckDustWake)

        private ParticleSystem veil;
        private ParticleSystem.EmissionModule veilEmission;
        private ParticleSystem.VelocityOverLifetimeModule veilVelocity;
        private float veilPulse;                  // seconds left of a wash pulse (truck pass); eases out

        /// <summary>True when the active RoadSequence is a town/city zone, so the blowing dust CLOUDS (gusts here,
        /// and the dust devils) can be suppressed there when WeatherConfig.dustCloudsCountryOnly is on. Updated from
        /// GameEvents.SequenceChanged; read by DustDevil too.</summary>
        public static bool InTownZone { get; private set; }

        private void Awake()
        {
            instance = this;
            if (config == null)
                config = ResolveConfig();

            WindField.ApplyConfig(config); // the wind clock adopts the asset's gust timing once, up front

            origFog = RenderSettings.fog;
            origFogMode = RenderSettings.fogMode;
            origFogDensity = RenderSettings.fogDensity;

            BuildVeil();

            // Ground dust is a gust-front EVENT now (Levend Kenia §03): the GustFront child replaces the old
            // constant BuildGusts faucet, driven by the same WindField clock the rest of the roadside reads.
            var gustGo = new GameObject("GustFront");
            gustGo.transform.SetParent(transform, false);
            gustGo.AddComponent<GustFront>();
        }

        /// <summary>Briefly thickens the drifting veil (a warm pulse at the screen bottom edge of the air
        /// itself) — the truck wash's "its dust arrives" beat. Safe to call any time; no-ops without a veil.</summary>
        public static void PulseVeil(float seconds)
        {
            if (instance != null)
                instance.veilPulse = Mathf.Max(instance.veilPulse, seconds);
        }

        private void OnEnable() => GameEvents.SequenceChanged += HandleSequenceChanged;

        private void HandleSequenceChanged(RoadSequence sequence)
        {
            InTownZone = sequence != null && config != null && config.townContextTags != null
                         && sequence.HasAnyTag(config.townContextTags);
        }

        private void Update()
        {
            if (config == null)
                return;

            bool wantDust = config.dustEnabled;

            // --- Fog density (colour stays the day cycle's) ---
            float targetDensity = wantDust ? config.hazeFogDensity : origFogDensity;
            float rate = config.hazeFadeSeconds > 0.01f
                ? Mathf.Abs(config.hazeFogDensity - origFogDensity) / config.hazeFadeSeconds
                : 1f;
            if (wantDust)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
            }
            RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, targetDensity, rate * Time.deltaTime);
            // Only hand fog back once we have fully eased down to the original density, so it never pops off.
            if (!wantDust && Mathf.Approximately(RenderSettings.fogDensity, origFogDensity))
            {
                RenderSettings.fog = origFog;
                RenderSettings.fogMode = origFogMode;
            }

            // --- Drifting dust veil ---
            if (veil != null)
            {
                // A truck wash briefly thickens the air (PulseVeil), easing back out on its own.
                float pulse = 1f;
                if (veilPulse > 0f)
                {
                    veilPulse -= Time.deltaTime;
                    pulse = 1f + 2.5f * Mathf.Clamp01(veilPulse / 0.4f);
                }
                veilEmission.rateOverTime = wantDust ? config.hazeEmissionRate * pulse : 0f;
                ParticleSystem.MainModule main = veil.main;
                main.startColor = config.hazeParticleColour;

                // The veil breathes with the shared wind: gusts push the haze across the view, so even the
                // ambient air acknowledges a front (struct writes only — no allocation).
                Vector3 wind = WindField.Velocity(config.groundGustSpeed * 0.35f);
                veilVelocity.x = new ParticleSystem.MinMaxCurve(-0.4f + wind.x, 0.4f + wind.x);
            }
        }

        private void OnDisable()
        {
            GameEvents.SequenceChanged -= HandleSequenceChanged;
            // Leave the scene as we found it if this object is torn down (e.g. scene unload).
            RenderSettings.fog = origFog;
            RenderSettings.fogMode = origFogMode;
            RenderSettings.fogDensity = origFogDensity;
        }

        /// <summary>Builds the camera-anchored drifting dust veil: soft warm motes spawned across a wide box in
        /// front of the camera, drifting slowly past it. Soft round sprite + safe material, so it can never go magenta.</summary>
        private void BuildVeil()
        {
            Transform anchor = Camera.main != null ? Camera.main.transform : transform;
            var go = new GameObject("DustVeil");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            veil = go.AddComponent<ParticleSystem>();
            veil.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = veil.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f; // motion comes from velocity-over-lifetime, kept exact
            main.startLifetime = 6f;
            main.startSize = new ParticleSystem.MinMaxCurve(config.hazeParticleSize.x, config.hazeParticleSize.y);
            main.startColor = config.hazeParticleColour;
            main.gravityModifier = 0f;
            main.maxParticles = 220;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = veil.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 32f, 90f);
            shape.position = new Vector3(0f, 0f, 28f); // a slab of air ahead of the camera

            // Drift slowly toward and past the camera, with a touch of lateral and sink so it isn't a dead grid.
            ParticleSystem.VelocityOverLifetimeModule velocity = veil.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // x/y/z must share one MinMaxCurve mode (Unity: "Particle Velocity curves must all be in the same mode").
            velocity.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(-config.hazeDriftSpeed, -config.hazeDriftSpeed);
            veilVelocity = velocity; // Update nudges x with the shared WindField so the haze rides the gusts

            // Fade each mote in and out so they never pop at the box edges.
            ParticleSystem.ColorOverLifetimeModule col = veil.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = FXMaterials.SoftDustMaterial();

            veilEmission = veil.emission;
            veilEmission.rateOverTime = 0f; // Update gates it on the toggle
            veil.Play();
        }

        // The old BuildGusts (a constant low slab of streaks) was replaced by the GustFront child in Awake:
        // ground dust is now an EVENT with an anatomy — a roller volley plus a tarmac skitter per WindField
        // front, with a lull before and after. The lull is the effect (Levend Kenia §03).
    }
}
