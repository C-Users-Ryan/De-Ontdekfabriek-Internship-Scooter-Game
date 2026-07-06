using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;
using KenyaScooter.Settings;
using KenyaScooter.Traffic;

namespace KenyaScooter.FX
{
    /// <summary>
    /// Dust kicked up behind a moving vehicle on a dry Kenyan road. Builds one soft world-space plume PER REAR
    /// WHEEL, so the dust rises off the tyres instead of from a single puff at the bumper, on a render-safe
    /// soft-sprite material (never magenta under URP). Auto-added to traffic by <see cref="TrafficVehicle"/>;
    /// also works on the player scooter.
    ///
    /// SPEED-DRIVEN AND GATED. Emission rises with the vehicle's OWN speed SQUARED — a crawling car barely
    /// wisps, a fast one throws a full plume — boosted ×<see cref="WeatherConfig.dirtDustMultiplier"/> on a
    /// Dirt tile, and off entirely unless <see cref="WeatherConfig.dustEnabled"/> is on. Each puff is a random
    /// blend of a darker and a lighter murram tone, so it reads as billowing dust rather than a flat sheet.
    /// </summary>
    public sealed class VehicleDustTrail : MonoBehaviour
    {
        [Tooltip("Rear-axle reference point in local space (behind, low). A plume is built at each rear wheel, " +
                 "offset sideways from here, so the dust rises off the tyres.")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.2f, -2f);
        [Tooltip("Optional. Leave empty to auto-find the shared WeatherConfig at runtime.")]
        [SerializeField] private WeatherConfig config;

        private readonly List<ParticleSystem> plumes = new List<ParticleSystem>(2);
        private TrafficVehicle vehicle;

        /// <summary>Own-speed (m/s) at which a traffic vehicle throws its FULL dust.</summary>
        private const float DustFullSpeed = 8f;
        private const float MinSpeedRatio = 0.05f;
        /// <summary>Minimum start-alpha so puffs read against the warm dusty background.</summary>
        private const float MinAlpha = 0.7f;
        /// <summary>Plume size boost over the config's authored size.</summary>
        private const float VisibilityScale = 1.8f;
        /// <summary>Sideways distance from the centre line to each rear wheel (approx; models vary).</summary>
        private const float RearWheelHalfWidth = 0.7f;

        private void Awake()
        {
            if (config == null)
                config = DustAtmosphere.ResolveConfig();
            vehicle = GetComponent<TrafficVehicle>(); // present on traffic; null on the player scooter
            BuildPlume(localOffset + new Vector3(-RearWheelHalfWidth, 0f, 0f));
            BuildPlume(localOffset + new Vector3(RearWheelHalfWidth, 0f, 0f));
        }

        private void Update()
        {
            if (plumes.Count == 0)
                return;

            float rate = 0f;
            if (config != null && config.dustEnabled && WorldSpeed.Instance != null
                && (GameManager.State == GameState.Playing || GameManager.State == GameState.AtCheckpoint))
            {
                // Dust is thrown by THIS vehicle's own motion over the road (the player scooter falls back to
                // world-scroll). Speed drives it SQUARED, so a crawling car barely wisps and a fast one plumes.
                float ratio = vehicle != null
                    ? Mathf.Clamp01(vehicle.CurrentSpeed / DustFullSpeed)
                    : WorldSpeed.Instance.SpeedRatio;
                if (ratio > MinSpeedRatio)
                {
                    float t = Mathf.InverseLerp(MinSpeedRatio, 1f, ratio);
                    rate = t * t * config.vehicleDustEmission * RoadSurfaceFeel.DustBoost(config.dirtDustMultiplier);
                }

                // Layered warm dust: each puff is a random blend of a darker and lighter murram tone.
                Color hi = config.vehicleDustColour;
                hi.a = Mathf.Max(hi.a, MinAlpha);
                Color lo = hi * 0.82f; lo.a = hi.a;
                var grad = new ParticleSystem.MinMaxGradient(lo, hi);
                for (int i = 0; i < plumes.Count; i++)
                {
                    ParticleSystem.MainModule m = plumes[i].main;
                    m.startColor = grad;
                    m.startLifetime = config.vehicleDustLifetime;
                }
            }

            float perWheel = rate * 0.5f; // split the emission across the two wheel plumes
            for (int i = 0; i < plumes.Count; i++)
            {
                ParticleSystem.EmissionModule em = plumes[i].emission;
                em.rateOverTime = perWheel;
            }
        }

        private void BuildPlume(Vector3 offset)
        {
            var go = new GameObject("DustTrail");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            go.transform.localRotation = Quaternion.identity;

            var plume = go.AddComponent<ParticleSystem>();
            plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float startSize = (config != null ? config.vehicleDustSize.x : 0.5f) * VisibilityScale;
            float endSize = (config != null ? config.vehicleDustSize.y : 1.4f) * VisibilityScale;
            float life = config != null ? config.vehicleDustLifetime : 0.9f;

            ParticleSystem.MainModule main = plume.main;
            // World space so each puff is LEFT BEHIND as the vehicle moves, reading as a trailing dust cloud.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f); // a touch more spread near the ground
            main.startLifetime = life;
            main.startSize = endSize; // full size; the size curve below scales each puff up to this as it disperses
            Color initHi = config != null ? config.vehicleDustColour : new Color(0.8f, 0.64f, 0.45f, 0.45f);
            initHi.a = Mathf.Max(initHi.a, MinAlpha);
            Color initLo = initHi * 0.82f; initLo.a = initHi.a;
            main.startColor = new ParticleSystem.MinMaxGradient(initLo, initHi);
            main.gravityModifier = -0.02f; // a touch of lift, so dust billows up before settling
            main.maxParticles = 120;
            main.playOnAwake = false;

            // Spray low and to the rear in a wide fan, so the dust spreads along the ground off the wheel.
            ParticleSystem.ShapeModule shape = plume.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(110f, 0f, 0f); // tilt the cone back-and-up relative to forward

            // Grow each puff as it expands and disperses.
            ParticleSystem.SizeOverLifetimeModule size = plume.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, Mathf.Max(0.01f, startSize / Mathf.Max(0.01f, endSize))),
                new Keyframe(1f, 1f)));

            // Fade in a touch then out, so the trail dissolves instead of cutting off.
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

            ParticleSystem.EmissionModule emission = plume.emission;
            emission.rateOverTime = 0f;
            plume.Play();

            plumes.Add(plume);
        }
    }
}
