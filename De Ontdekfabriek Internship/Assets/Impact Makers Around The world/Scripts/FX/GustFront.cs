using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.FX
{
    /// <summary>
    /// Ground dust as a gust-front EVENT — nothing in a lull, one coherent rolling wave when
    /// <see cref="WindField"/> says a front is live (FX Design Spec "Levend Kenia" §03, 3 Jul 2026 —
    /// replaces DustAtmosphere.BuildGusts, whose constant 10/s faucet read as floating blobs).
    ///
    /// Two pooled particle systems with a shared anatomy:
    ///   ROLLER  — one volley of large tumbling puffs in a ragged line abreast, ground-hugging with a
    ///             rolling hop (gravity + a small upward start speed, never floating off), angled a few
    ///             degrees off square so the front SWEEPS past instead of curtaining.
    ///   SKITTER — hard bright grains sprinting ON the tarmac at ~2× the roller's speed (stretch-rendered).
    ///             This is the contact layer that kills the floating-blob read: the eye anchors the whole
    ///             front to the surface.
    ///
    /// Obeys dustEnabled + groundGustEnabled + dustCloudsCountryOnly exactly like the system it replaces,
    /// and runs only while playing. DustAtmosphere creates one as a child on Awake (zero scene wiring);
    /// colours and wind speed come from the shared WeatherConfig so the facilitator asset stays the single
    /// tuning surface.
    /// </summary>
    public sealed class GustFront : MonoBehaviour
    {
        [Tooltip("Roller puffs per front — one volley, a ragged line across ~25 m of road depth.")]
        [SerializeField] private int rollerCount = 14;
        [Tooltip("Skitter grains per second at the heart of a front (follows the WindField envelope).")]
        [SerializeField] private float skitterRate = 34f;
        [Tooltip("Degrees the front line is angled off square, so it sweeps rather than curtains.")]
        [SerializeField] private float frontAngle = 15f;
        [Tooltip("PERMANENT wind presence (2026-07-05): fraction of the skitter rate that keeps running " +
                 "between fronts, plus a slow trickle of roller puffs — wind-blown dust is always crossing " +
                 "the road, and the fronts peak on top of it. 0 = the old events-only behaviour.")]
        [Range(0f, 1f)]
        [SerializeField] private float baselinePresence = 0.3f;

        private ParticleSystem roller, skitter;
        private ParticleSystem.EmissionModule skitterEmission;
        private Transform emitter;      // the upwind anchor both systems hang off; re-placed per front
        private bool frontSeen;
        private float rollerTrickleIn;  // seconds until the next baseline roller puff

        private void Awake()
        {
            emitter = transform;
            BuildRoller();
            BuildSkitter();
        }

        private void Update()
        {
            WeatherConfig wc = DustAtmosphere.ResolveConfig();
            bool cloudsHere = wc.dustEnabled && wc.groundGustEnabled
                              && !(wc.dustCloudsCountryOnly && DustAtmosphere.InTownZone);
            bool playing = GameManager.State == GameState.Playing;
            if (!cloudsHere || !playing)
            {
                skitterEmission.rateOverTime = 0f;
                return;
            }

            // One roller volley per front, on the rising edge; the skitter follows the whole envelope.
            if (WindField.FrontLive && !frontSeen)
            {
                frontSeen = true;
                PlaceUpwind(wc);
                roller.Emit(rollerCount);
            }
            if (!WindField.FrontLive && WindField.Gust01 < 0.1f)
                frontSeen = false;

            // PERMANENT presence (2026-07-05, design direction: "the dust and wind coming in from the side
            // should be permanent"): between fronts, a slow trickle of single roller puffs keeps wind-blown
            // dust crossing the road — each re-anchored upwind, so the drift direction stays honest — and
            // the skitter grains hold a gentle floor instead of dying to zero.
            if (baselinePresence > 0f)
            {
                rollerTrickleIn -= Time.deltaTime;
                if (rollerTrickleIn <= 0f)
                {
                    PlaceUpwind(wc);
                    roller.Emit(1);
                    rollerTrickleIn = Random.Range(0.9f, 1.8f) / Mathf.Max(0.1f, baselinePresence);
                }
            }

            float envelope = Mathf.Max(baselinePresence * 0.5f, WindField.Gust01 - 0.15f);
            skitterEmission.rateOverTime = skitterRate * envelope;
        }

        /// <summary>Moves the emitter to the upwind shoulder, yawed off square so the front sweeps, and points
        /// both systems' velocities along the live wind. Runs once per front, so the cost is nothing.</summary>
        private void PlaceUpwind(WeatherConfig wc)
        {
            float dir = WindField.DirectionX;
            emitter.position = new Vector3(-dir * 16f, 0.15f, 18f + Random.Range(-4f, 6f));
            emitter.rotation = Quaternion.Euler(0f, dir * frontAngle, 0f);

            float speed = wc.groundGustSpeed;
            SetWindVelocity(roller, dir * speed, -speed * 0.25f);
            SetWindVelocity(skitter, dir * speed * 2f, -speed * 0.4f);

            ParticleSystem.MainModule rmain = roller.main;
            rmain.startColor = wc.groundGustColour;
            ParticleSystem.MainModule smain = skitter.main;
            Color bright = wc.groundGustColour;                     // the sunlit grains: brighter, denser
            bright.r = Mathf.Clamp01(bright.r * 1.15f);
            bright.g = Mathf.Clamp01(bright.g * 1.12f);
            bright.a = Mathf.Clamp01(bright.a * 1.6f);
            smain.startColor = bright;
        }

        private static void SetWindVelocity(ParticleSystem ps, float x, float z)
        {
            var vel = ps.velocityOverLifetime;
            // x/y/z must share one MinMaxCurve mode (Unity: "Particle Velocity curves must all be in the same mode").
            vel.x = new ParticleSystem.MinMaxCurve(x * 0.8f, x * 1.2f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(z * 0.8f, z * 1.2f);
        }

        /// <summary>The front's body: big slow tumbling puffs that HOP along the ground (a touch of gravity
        /// against a small upward birth speed) instead of hovering — dust is born on surfaces and dies in the air.</summary>
        private void BuildRoller()
        {
            WeatherConfig wc = DustAtmosphere.ResolveConfig();
            ParticleSystem ps = NewSystem("GustRoller");

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // the wave crosses the road on its own
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f); // small upward pop off the ground...
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.0f, 1.9f);
            main.startColor = wc.groundGustColour;
            main.gravityModifier = 0.15f;                                // ...that gravity keeps pulling back: the hop
            main.maxParticles = 60;
            main.playOnAwake = false;

            // A ragged line abreast: shallow in the wind direction, deep along the road.
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4f, 0.4f, 25f);
            shape.rotation = new Vector3(-90f, 0f, 0f); // startSpeed fires along +Z of the shape — point it up

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            SetWindVelocity(ps, wc.groundGustSpeed, -wc.groundGustSpeed * 0.25f);

            // The tumble: each puff slowly rotates as it rolls, which is what sells "a wave", not "fog".
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f); // rad/s, both directions

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.7f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0.85f)));

            FeatherAlpha(ps);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f; // volley-only (Emit per front)
            ps.Play();
            roller = ps;
        }

        /// <summary>The front's contact layer: fast bright grains stretched along their sprint, hugging the
        /// tarmac at y ≈ 0.05 — the piece that anchors the whole event to the road surface.</summary>
        private void BuildSkitter()
        {
            WeatherConfig wc = DustAtmosphere.ResolveConfig();
            ParticleSystem ps = NewSystem("GustSkitter");

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2f;
            r.velocityScale = 0.06f;

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = wc.groundGustColour;
            main.gravityModifier = 0f;
            main.maxParticles = 60;
            main.playOnAwake = false;

            // A wide, paper-thin sheet ON the road: grains are born touching the surface they sprint across.
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 0.08f, 45f);
            shape.position = new Vector3(0f, -0.1f, 0f); // emitter sits at 0.15: the sheet lands at y ≈ 0.05

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            SetWindVelocity(ps, wc.groundGustSpeed * 2f, -wc.groundGustSpeed * 0.4f);

            FeatherAlpha(ps);

            skitterEmission = ps.emission;
            skitterEmission.rateOverTime = 0f; // Update drives it from the WindField envelope
            ps.Play();
            skitter = ps;
        }

        private ParticleSystem NewSystem(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(emitter, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = FXMaterials.SoftDustMaterial();
            return ps;
        }

        private static void FeatherAlpha(ParticleSystem ps)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }
    }
}
