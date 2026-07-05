using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;
using KenyaScooter.Settings;

namespace KenyaScooter.FX
{
    /// <summary>
    /// Dust kicked up behind a moving vehicle on a dry Kenyan road (Oplevering 25 Jun 2026). Drop this on a
    /// traffic-vehicle prefab or the player scooter; it builds its own world-space dust plume on a render-safe
    /// soft-sprite material (so it can never go magenta under URP, like SpeedLines) and needs no other wiring.
    ///
    /// ADDITIVE AND GATED. Emission scales with world speed and is silenced below a configurable speed, and the
    /// whole thing is off unless <see cref="WeatherConfig.dustEnabled"/> is on (the facilitator dust toggle).
    /// If the component is never attached, or no WeatherConfig exists, nothing is emitted and nothing changes.
    /// </summary>
    public sealed class VehicleDustTrail : MonoBehaviour
    {
        [Tooltip("Where the plume sits relative to this object, in its local space. Default is low and behind " +
                 "(−Z is the vehicle's rear, since vehicles face their travel direction).")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.05f, -2f);
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
                float ratio = WorldSpeed.Instance.SpeedRatio;
                if (ratio > config.vehicleDustMinSpeedRatio)
                {
                    float t = Mathf.InverseLerp(config.vehicleDustMinSpeedRatio, 1f, ratio);
                    // On a dirt tile every vehicle churns more — all traffic rides the same road as the
                    // player, so one shared surface blend is correct for the whole convoy.
                    rate = t * config.vehicleDustEmission * RoadSurfaceFeel.DustBoost(config.dirtDustMultiplier);
                }

                ParticleSystem.MainModule main = plume.main;
                main.startColor = config.vehicleDustColour;
                main.startLifetime = config.vehicleDustLifetime;
            }
            plumeEmission.rateOverTime = rate;
        }

        private void BuildPlume()
        {
            var go = new GameObject("DustTrail");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localOffset;
            go.transform.localRotation = Quaternion.identity;

            plume = go.AddComponent<ParticleSystem>();
            plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float startSize = config != null ? config.vehicleDustSize.x : 0.5f;
            float endSize = config != null ? config.vehicleDustSize.y : 1.4f;
            float life = config != null ? config.vehicleDustLifetime : 0.9f;

            ParticleSystem.MainModule main = plume.main;
            // World space so each puff is LEFT BEHIND as the vehicle moves, reading as a trailing dust cloud.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
            main.startLifetime = life;
            main.startSize = endSize; // full size; the size curve below scales each puff up to this as it disperses
            main.startColor = config != null ? config.vehicleDustColour : new Color(0.8f, 0.64f, 0.45f, 0.45f);
            main.gravityModifier = -0.02f; // a touch of lift, so dust billows up before settling
            main.maxParticles = 120;
            main.playOnAwake = false;

            // Spray low and to the rear: a shallow cone aimed back and slightly up off the wheels.
            ParticleSystem.ShapeModule shape = plume.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(110f, 0f, 0f); // tilt the cone back-and-up relative to forward

            // Grow each puff as it expands and disperses.
            ParticleSystem.SizeOverLifetimeModule size = plume.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, Mathf.Max(0.01f, startSize / Mathf.Max(0.01f, endSize))),
                new Keyframe(1f, 1f)));

            // Fade out so the trail dissolves instead of cutting off.
            ParticleSystem.ColorOverLifetimeModule col = plume.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
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
