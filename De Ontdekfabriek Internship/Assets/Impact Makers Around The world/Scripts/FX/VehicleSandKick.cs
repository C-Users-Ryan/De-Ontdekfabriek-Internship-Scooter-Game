using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;
using KenyaScooter.Traffic;

namespace KenyaScooter.FX
{
    /// <summary>
    /// Grainy SAND flung up off a vehicle's REAR WHEELS while it drives on an unpaved (sand/murram) tile — the
    /// "kicking up sand" beat, distinct from the airborne dust plume (<see cref="VehicleDustTrail"/>). One emitter
    /// per rear wheel throws heavier, faster grains that arc up-and-back and fall under gravity, so it reads as
    /// thrown sand, not floating haze. Self-contained and URP-safe (its own ParticleSystem on
    /// <see cref="FXMaterials.SoftDustMaterial"/>, so it can never go magenta). Auto-added to traffic by
    /// <see cref="TrafficVehicle"/>; also works on the player scooter.
    ///
    /// SURFACE- AND SPEED-GATED. Emits only while the road under the player is sand
    /// (<see cref="RoadSurfaceFeel.DirtBlend01"/>) AND the vehicle is moving, scaled by the facilitator
    /// "Onverharde wegen" intensity, and off entirely unless <see cref="WeatherConfig.dustEnabled"/> is on.
    /// On tarmac it emits nothing.
    /// </summary>
    public sealed class VehicleSandKick : MonoBehaviour
    {
        [Tooltip("Rear-axle reference point in local space (low, behind). A grain emitter is built at each rear " +
                 "wheel, offset sideways from here.")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.12f, -1.8f);
        [Tooltip("Optional. Leave empty to auto-find the shared WeatherConfig at runtime.")]
        [SerializeField] private WeatherConfig config;

        private readonly List<ParticleSystem> emitters = new List<ParticleSystem>(2);
        private TrafficVehicle vehicle;

        /// <summary>Own-speed (m/s) at which the sand throw peaks (matches <see cref="VehicleDustTrail"/>).</summary>
        private const float FullSpeed = 8f;
        private const float MinSpeedRatio = 0.05f;
        /// <summary>Grains/sec on full sand at full speed, split across the two wheels.</summary>
        private const float PeakRate = 60f;
        /// <summary>Below this surface blend the tile is effectively tarmac, so no sand is thrown.</summary>
        private const float MinSandBlend = 0.02f;
        /// <summary>Sideways distance from the centre line to each rear wheel (approx; models vary).</summary>
        private const float RearWheelHalfWidth = 0.7f;

        private void Awake()
        {
            if (config == null)
                config = DustAtmosphere.ResolveConfig();
            vehicle = GetComponent<TrafficVehicle>(); // present on traffic; null on the player scooter
            Build(localOffset + new Vector3(-RearWheelHalfWidth, 0f, 0f));
            Build(localOffset + new Vector3(RearWheelHalfWidth, 0f, 0f));
        }

        private void Update()
        {
            if (emitters.Count == 0)
                return;

            float rate = 0f;
            if (config != null && config.dustEnabled && WorldSpeed.Instance != null
                && (GameManager.State == GameState.Playing || GameManager.State == GameState.AtCheckpoint))
            {
                // Sand only on an unpaved tile, damped by the facilitator's dirt-road intensity (UIT = none), and
                // scaled by this vehicle's own motion (the player scooter falls back to world-scroll).
                float sand = Mathf.Clamp01(RoadSurfaceFeel.DirtBlend01 * RoadSurfaceFeel.IntensityScale);
                float speed01 = vehicle != null
                    ? Mathf.Clamp01(vehicle.CurrentSpeed / FullSpeed)
                    : WorldSpeed.Instance.SpeedRatio;
                if (sand > MinSandBlend && speed01 > MinSpeedRatio)
                    rate = sand * speed01 * PeakRate;
            }

            float perWheel = rate * 0.5f; // split across the two rear-wheel emitters
            for (int i = 0; i < emitters.Count; i++)
            {
                ParticleSystem.EmissionModule em = emitters[i].emission;
                em.rateOverTime = perWheel;
            }
        }

        private void Build(Vector3 offset)
        {
            var go = new GameObject("SandKick");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            go.transform.localRotation = Quaternion.identity;

            var grains = go.AddComponent<ParticleSystem>();
            grains.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = grains.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;               // grains are flung and LEFT BEHIND
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);             // thrown, not drifting
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.5f);             // small grains, some clumps
            // Layered sand: each grain a random blend of a pale and a warmer tone.
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.72f, 0.58f, 0.40f, 0.9f), new Color(0.90f, 0.78f, 0.58f, 0.9f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);       // heavy: arc up, then fall back
            main.maxParticles = 90;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = grains.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;                                                        // a wide fan off the wheel
            shape.radius = 0.18f;
            shape.rotation = new Vector3(120f, 0f, 0f);                              // fling back-and-up vs travel

            ParticleSystem.SizeOverLifetimeModule sol = grains.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(1f, 0.4f)));                       // grains scatter and shrink

            ParticleSystem.ColorOverLifetimeModule col = grains.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = FXMaterials.SoftDustMaterial();

            ParticleSystem.EmissionModule emission = grains.emission;
            emission.rateOverTime = 0f;
            grains.Play();

            emitters.Add(grains);
        }
    }
}
