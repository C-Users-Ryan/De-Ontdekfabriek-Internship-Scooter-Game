using UnityEngine;
using KenyaScooter.Cameras;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Traffic;

namespace KenyaScooter.FX
{
    /// <summary>
    /// A heavy vehicle's dust doesn't just get LEFT BEHIND — it ARRIVES (FX Design Spec "Levend Kenia"
    /// §4.1, 3 Jul 2026). Attach beside <see cref="VehicleDustTrail"/> on truck/matatu prefabs (Tools >
    /// Kenya Scooter > Weather and FX > Add Truck Dust Wake to Selection): while the world is fast the vehicle tows a dust
    /// WALL (the trail's plume scaled up ~2×), and the frame it passes the player it raises ONE WASH — a
    /// burst pushed toward the player's lane, a short veil pulse (<see cref="DustAtmosphere.PulseVeil"/>)
    /// and a small camera rumble. The strongest "the world physically touches me" beat available at this
    /// cost, and it quietly rewards keeping distance from big vehicles — on-message for this game.
    ///
    /// Once per pass, latched (re-armed when the pooled vehicle recycles), so it can never spam in a jam;
    /// the rumble stays well under the collision shake and scales with the motion-sensitivity dial.
    /// Oncoming vehicles give the strongest wash; overtaken ones a mild one. Everything obeys the
    /// facilitator dust toggle, exactly like the trail it rides beside.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TruckDustWake : MonoBehaviour
    {
        [Tooltip("Below this fraction of max world speed the vehicle tows no wall and washes nothing " +
                 "(slow traffic and jams stay clean).")]
        [SerializeField, Range(0f, 1f)] private float minSpeedRatio = 0.4f;
        [Tooltip("Puffs in the one-shot wash burst as the vehicle passes the player.")]
        [SerializeField] private int washCount = 16;
        [Tooltip("Camera rumble for the pass — well under the collision shake, scaled by the motion dial.")]
        [SerializeField] private float rumble = 0.12f;

        private TrafficVehicle vehicle;
        private ParticleSystem wall, wash;
        private ParticleSystem.EmissionModule wallEmission;
        private bool passed;

        private void Awake()
        {
            vehicle = GetComponent<TrafficVehicle>();
            BuildWall();
            BuildWash();
        }

        private void OnEnable()
        {
            passed = false; // fresh from the pool: the wash is armed for this vehicle's one pass
        }

        private void Update()
        {
            WeatherConfig wc = DustAtmosphere.ResolveConfig();
            bool playing = GameManager.State == GameState.Playing || GameManager.State == GameState.AtCheckpoint;
            bool fast = WorldSpeed.Instance != null && WorldSpeed.Instance.SpeedRatio >= minSpeedRatio;
            bool on = wc.dustEnabled && playing && fast;

            // The wall: the trail scaled up, so a big vehicle visibly tows its own weather.
            wallEmission.rateOverTime = on ? wc.vehicleDustEmission * 1.6f : 0f;
            if (on)
            {
                ParticleSystem.MainModule main = wall.main;
                main.startColor = wc.vehicleDustColour;
            }

            if (!on)
                return;

            // The player sits at the origin; this vehicle's z crossing zero IS the pass.
            float z = transform.position.z;
            if (!passed && z < 3.5f && z > -2f)
            {
                passed = true;
                bool oncoming = vehicle == null || vehicle.Direction == LaneDirection.Oncoming;
                float strength = oncoming ? 1f : 0.5f; // an overtaken truck brushes you; an oncoming one slaps

                wash.transform.position = new Vector3(transform.position.x * 0.5f, 0.3f, 3f);
                ParticleSystem.MainModule wmain = wash.main;
                wmain.startColor = wc.vehicleDustColour;
                wash.Emit(Mathf.RoundToInt(washCount * strength));

                CameraShake.Rumble(rumble * strength * SpeedFeel.MotionScale, 0.35f);
                DustAtmosphere.PulseVeil(0.4f * strength);
            }
            if (z > 30f)
                passed = false; // the pooled vehicle was recycled ahead — re-arm for its next pass
        }

        /// <summary>The towed wall: VehicleDustTrail's plume recipe scaled up (~2.2× size, wider cone), built
        /// separately so the two components stay independent (either works without the other).</summary>
        private void BuildWall()
        {
            WeatherConfig wc = DustAtmosphere.ResolveConfig();
            var go = new GameObject("DustWall");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.1f, -2.5f);

            wall = go.AddComponent<ParticleSystem>();
            wall.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float endSize = (wc != null ? wc.vehicleDustSize.y : 1.4f) * 2.2f;

            ParticleSystem.MainModule main = wall.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // left hanging as the vehicle drives on
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startLifetime = (wc != null ? wc.vehicleDustLifetime : 0.9f) * 1.8f;
            main.startSize = endSize;
            main.startColor = wc != null ? wc.vehicleDustColour : new Color(0.8f, 0.64f, 0.45f, 0.45f);
            main.gravityModifier = -0.02f;
            main.maxParticles = 120;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = wall.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(110f, 0f, 0f); // back-and-up off the wheels, like the trail

            ParticleSystem.SizeOverLifetimeModule size = wall.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.4f), new Keyframe(1f, 1f)));

            FadeOut(wall);

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = FXMaterials.SoftDustMaterial();

            wallEmission = wall.emission;
            wallEmission.rateOverTime = 0f;
            wall.Play();
        }

        /// <summary>The one-shot wash: a world-space burst that gets PUSHED into the player's lane the frame
        /// the vehicle passes, so the moment lands where the player is looking.</summary>
        private void BuildWash()
        {
            var go = new GameObject("DustWash");
            go.transform.SetParent(transform, false); // repositioned in world space per pass

            wash = go.AddComponent<ParticleSystem>();
            wash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = wash.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.gravityModifier = -0.02f;
            main.maxParticles = 24;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = wash.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.8f;

            ParticleSystem.SizeOverLifetimeModule size = wash.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f)));

            FadeOut(wash);

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = FXMaterials.SoftDustMaterial();

            ParticleSystem.EmissionModule emission = wash.emission;
            emission.rateOverTime = 0f; // Emit()-only
            wash.Play();
        }

        private static void FadeOut(ParticleSystem ps)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }
    }
}
