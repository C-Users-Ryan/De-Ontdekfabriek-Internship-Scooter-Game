using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;
using KenyaScooter.Traffic;

namespace KenyaScooter.FX
{
    /// <summary>
    /// THE one dust system a vehicle kicks up (replaces the old VehicleDustTrail + VehicleSandKick pair). A single
    /// warm, layered plume behind the vehicle that ONLY appears while it drives on a Dirt (sand/murram) tile.
    ///
    /// LOCAL simulation space, on purpose: a traffic car is TELEPORTED to a fresh world pose every frame (its
    /// position is re-derived from the road curve, not translated), so world-space puffs would be left stranded
    /// at their old coordinates and — as the world scrolls fast past the fixed player — drift IN FRONT of the
    /// player. Local space keeps every puff pinned BEHIND its own car and moving with it.
    ///
    /// WHY IT EMITS VIA <see cref="ParticleSystem.Emit(int)"/> AND NEVER A CACHED MODULE: writing to a stored
    /// <c>emission.rateOverTime</c> every frame throws "NullReferenceException: Do not create your own module
    /// instances" in this project — that silently killed every previous dust system (see the Editor log for
    /// DustAtmosphere / ScooterContactDust / DuskShafts). This caches ONLY the ParticleSystem and emits directly,
    /// the same safe path <see cref="Traffic.TrafficExhaust"/> uses, so it can never hit that bug.
    ///
    /// Auto-added to traffic by <see cref="TrafficVehicle"/>; also works on the player scooter (no TrafficVehicle
    /// sibling → it reads world-scroll speed). URP-safe material, so it never renders magenta.
    /// </summary>
    public sealed class VehicleDust : MonoBehaviour
    {
        [Tooltip("Where the plume sits relative to this object, in local space: behind and a little up off the road.")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.25f, -2f);
        [Tooltip("Optional. Leave empty to auto-find the shared WeatherConfig at runtime.")]
        [SerializeField] private WeatherConfig config;

        private ParticleSystem ps;   // cache the SYSTEM only — never a module struct
        private TrafficVehicle vehicle;
        private float emitCarry;     // fractional-particle accumulator, so low rates still emit smoothly

        private const float FullSpeed = 8f;       // own-speed (m/s) at which the plume peaks
        private const float MinSpeedRatio = 0.05f;
        private const float BaseRate = 45f;        // puffs/sec at full speed on full dirt
        private const float MinDirt = 0.02f;       // below this the tile is effectively tarmac → no dust
        private const float VisibilityScale = 1.9f;
        private const float MinAlpha = 0.7f;

        private void Awake()
        {
            if (config == null)
                config = DustAtmosphere.ResolveConfig();
            vehicle = GetComponent<TrafficVehicle>(); // present on traffic; null on the player scooter
            Build();
        }

        private void Update()
        {
            if (ps == null)
                return;
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            // Gate: dust toggle on, and we're actually driving (not in a menu).
            bool on = (config == null || config.dustEnabled)
                      && (GameManager.State == GameState.Playing || GameManager.State == GameState.AtCheckpoint)
                      && WorldSpeed.Instance != null;
            if (!on)
                return;

            // Dust is thrown by THIS vehicle's own motion over the road (the player scooter falls back to the
            // world-scroll, which IS its motion). Speed drives it SQUARED, so a crawl wisps and a fast car plumes.
            float ratio = vehicle != null
                ? Mathf.Clamp01(vehicle.CurrentSpeed / FullSpeed)
                : WorldSpeed.Instance.SpeedRatio;
            if (ratio <= MinSpeedRatio)
                return;

            // ONLY on an unpaved (sand/murram) tile — no dust on tarmac. Scaled by how far onto dirt we are
            // (eased across the seam) and the facilitator's dirt-road intensity (UIT = none).
            float dirt = Mathf.Clamp01(RoadSurfaceFeel.DirtBlend01 * RoadSurfaceFeel.IntensityScale);
            if (dirt <= MinDirt)
                return;

            float perSecond = ratio * ratio * BaseRate * dirt;

            emitCarry += perSecond * dt;
            int n = Mathf.FloorToInt(emitCarry);
            if (n > 0)
            {
                emitCarry -= n;
                ps.Emit(n); // NO module access in Update — this is the safe path
            }
        }

        private void Build()
        {
            var go = new GameObject("VehicleDust");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localOffset;
            go.transform.localRotation = Quaternion.identity;

            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float startSize = (config != null ? config.vehicleDustSize.x : 0.5f) * VisibilityScale;
            float endSize = (config != null ? config.vehicleDustSize.y : 1.4f) * VisibilityScale;

            // All module writes below are ONE-TIME, on the just-created system, and used immediately — that is the
            // safe use of modules. Only the per-frame rateOverTime pattern is forbidden; we use Emit() for that.
            ParticleSystem.MainModule main = ps.main;
            // LOCAL space so each puff stays BEHIND its car and moves with it (see the class note). World space made
            // the dust hang in place while the scrolling world rushed past, so at speed it drifted in FRONT of the player.
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = config != null ? config.vehicleDustLifetime : 0.9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize, endSize);
            Color hi = config != null ? config.vehicleDustColour : new Color(0.82f, 0.66f, 0.48f, 0.7f);
            hi.a = Mathf.Max(hi.a, MinAlpha);
            Color lo = hi * 0.82f; lo.a = hi.a;                          // layered: darker + lighter murram tone
            main.startColor = new ParticleSystem.MinMaxGradient(lo, hi);
            main.gravityModifier = -0.02f;                               // a touch of lift
            main.maxParticles = 200;
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;                                  // we drive it by Emit(), not rate

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.35f;                                        // spans ~the rear axle, off both wheels
            shape.rotation = new Vector3(110f, 0f, 0f);                 // aim back-and-up relative to travel

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(1f, 1.3f)));        // grow and disperse

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.55f, 0.45f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = FXMaterials.SoftDustMaterial();

            ps.Play(); // playing with rate 0; Emit() adds the particles on demand each frame
        }
    }
}
