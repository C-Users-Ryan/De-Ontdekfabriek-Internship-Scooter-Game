using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;

namespace KenyaScooter.FX
{
    /// <summary>
    /// The player's own murram dust (2026-07-05): a plume kicked up off the REAR WHEEL whenever the scooter is
    /// moving on a DIRT tile. THIS is how a dirt road is communicated — the surface reads as a warm dust cloud
    /// trailing the rider (authentic to murram: "a scooter on murram = a visible red dust cloud behind the
    /// rider", R15), instead of as a harsh shake. Clean on tarmac, blooming on dirt — the CONTRAST is the
    /// signal, so the player SEES the road change under them without feeling a jolt.
    ///
    /// Because this is the fixed-player, world-scrolls model, the player does NOT translate in world space, so
    /// (unlike the traffic's <see cref="VehicleDustTrail"/>, which rides moving cars) a world-space plume would
    /// hang in the air. Instead it works like <see cref="SlipstreamDust"/>: LOCAL-space particles born at the
    /// wheel and swept BACKWARD each frame at the world-scroll speed, so the dust stays glued to the road and
    /// streams off behind the bike. Built on the render-safe soft-dust material (never magenta under URP) and
    /// auto-added by PlayerController, so there is zero scene wiring.
    ///
    /// It obeys <see cref="WeatherConfig.dustEnabled"/> (the "Stof en haze" facilitator toggle) and scales with
    /// the "Onverharde wegen" step (<see cref="RoadSurfaceFeel.IntensityScale"/>) like every other dirt-road
    /// effect, so ONE knob governs how present the dirt road is: VOL = a full dust cloud, SUBTIEL = a lighter
    /// one, UIT = the road behaves and looks like tarmac (no dust, no shake).
    /// </summary>
    public sealed class ScooterDirtDust : MonoBehaviour
    {
        [Tooltip("Where the plume is born relative to the scooter root, in its local space — low and behind the " +
                 "rear wheel (−Z is the bike's rear).")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.05f, -1.1f);
        [Tooltip("Peak dust particles/second at full speed on full dirt. The rider's cloud is the hero cue — the " +
                 "main way the dirt road is communicated — so it is dense and clearly visible.")]
        [SerializeField] private float peakEmission = 90f;
        [Tooltip("Below this fraction of max world speed the bike kicks up no dust, so a parked/crawling scooter " +
                 "stays clean.")]
        [Range(0f, 1f)] [SerializeField] private float minSpeedRatio = 0.1f;
        [Tooltip("Optional. Leave empty to auto-find the shared WeatherConfig at runtime.")]
        [SerializeField] private WeatherConfig config;

        private ParticleSystem plume;
        private ParticleSystem.EmissionModule plumeEmission;

        private void Awake()
        {
            if (config == null)
                config = DustAtmosphere.ResolveConfig();
            BuildPlume();
        }

        private void Update()
        {
            if (plume == null)
                return;

            float rate = 0f;
            if (config != null && config.dustEnabled && WorldSpeed.Instance != null
                && (GameManager.State == GameState.Playing || GameManager.State == GameState.AtCheckpoint))
            {
                // Only on dirt (the eased surface blend) and only while actually rolling — the two gates that
                // make this READ as "this ground is loose", not as constant exhaust. Scaled by the facilitator's
                // dirt-feel step so UIT returns the road to a clean tarmac look, like the rest of the dirt FX.
                float blend = RoadSurfaceFeel.DirtBlend01;
                float ratio = WorldSpeed.Instance.SpeedRatio;
                if (blend > 0.001f && ratio > minSpeedRatio)
                {
                    float speed = Mathf.InverseLerp(minSpeedRatio, 1f, ratio);
                    rate = blend * speed * peakEmission * RoadSurfaceFeel.IntensityScale;
                }

                // Sweep the puffs backward at the actual scroll speed, so they stay pinned to the road as it
                // slides under the fixed bike, plus a little outward spread and a touch of lift.
                SetPlumeVelocity(WorldSpeed.Instance.Current);

                ParticleSystem.MainModule main = plume.main;
                main.startColor = config.vehicleDustColour;
                main.startLifetime = config.vehicleDustLifetime;
            }
            plumeEmission.rateOverTime = rate;
        }

        // x/y/z of a velocityOverLifetime must share one MinMaxCurve mode (Unity: "Particle Velocity curves must
        // all be in the same mode"), so all three are TwoConstants. z carries the backward scroll.
        private void SetPlumeVelocity(float scroll)
        {
            var vel = plume.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);   // outward spread
            vel.y = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);   // a little lift as the cloud billows up
            vel.z = new ParticleSystem.MinMaxCurve(-scroll * 1.1f, -scroll * 0.8f); // stream back with the road
        }

        // The plume: born in a small volume at the rear wheel, LOCAL space so the per-frame backward sweep above
        // reads as dust left on the scrolling road. Sized up a little — the rider's cloud is the hero.
        private void BuildPlume()
        {
            var go = new GameObject("DirtDust");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localOffset;
            go.transform.localRotation = Quaternion.identity;

            plume = go.AddComponent<ParticleSystem>();
            plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float startSize = config != null ? config.vehicleDustSize.x * 1.3f : 0.65f;
            float endSize = config != null ? config.vehicleDustSize.y * 1.4f : 2.0f;
            float life = config != null ? config.vehicleDustLifetime : 0.9f;

            ParticleSystem.MainModule main = plume.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // swept backward each frame (see SetPlumeVelocity)
            main.startSpeed = 0f;                 // all motion comes from velocityOverLifetime
            main.startLifetime = life;
            main.startSize = endSize;             // full size; the size curve below grows each puff up to this
            main.startColor = config != null ? config.vehicleDustColour : new Color(0.80f, 0.64f, 0.45f, 0.5f);
            main.gravityModifier = 0f;
            main.maxParticles = 220;
            main.playOnAwake = false;

            // Born in a small slab right at the rear-wheel contact (never mid-air), so the cloud rises off the ground.
            ParticleSystem.ShapeModule shape = plume.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.8f, 0.15f, 0.4f);

            ParticleSystem.VelocityOverLifetimeModule vel = plume.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            SetPlumeVelocity(0f); // Update overwrites this every frame from the world speed

            ParticleSystem.SizeOverLifetimeModule size = plume.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, Mathf.Max(0.01f, startSize / Mathf.Max(0.01f, endSize))),
                new Keyframe(1f, 1f)));

            // Fade in then out so the trail dissolves instead of popping or cutting off.
            ParticleSystem.ColorOverLifetimeModule col = plume.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = FXMaterials.SoftDustMaterial();

            plumeEmission = plume.emission;
            plumeEmission.rateOverTime = 0f;
            plume.Play();
        }
    }
}
