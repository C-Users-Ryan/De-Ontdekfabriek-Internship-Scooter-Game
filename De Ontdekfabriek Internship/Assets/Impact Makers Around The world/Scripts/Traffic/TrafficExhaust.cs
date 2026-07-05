using UnityEngine;
using KenyaScooter.FX;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// A small exhaust puff out the back when a car guns it (plus a faint trickle while driving), so a vehicle reads
    /// as a running engine rather than a silent slab. Self-contained companion to TrafficEngine/Lights/Animator: it
    /// reads only the sibling <see cref="TrafficVehicle"/>'s public speed and builds its own URP-safe ParticleSystem
    /// (via <see cref="FXMaterials"/>, so it never resolves to the magenta error shader). No traffic-core edits.
    ///
    /// Deliberately avoids the velocity-over-lifetime module (the puffs are world-simulated, so the car simply drives
    /// away from them — leaving a trail — and there are no x/y/z curve-mode pitfalls).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrafficExhaust : MonoBehaviour
    {
        [Tooltip("Tailpipe position relative to the vehicle root (behind and low).")]
        [SerializeField] private Vector3 tailpipe = new Vector3(0f, 0.25f, -2.2f);
        [SerializeField] private Color smokeColour = new Color(0.25f, 0.24f, 0.22f, 0.5f);
        [Tooltip("Acceleration (m/s²) above which a visible puff bursts out.")]
        [SerializeField] private float puffAccel = 2.5f;
        [Tooltip("Faint puffs per second while cruising. 0 = only on hard acceleration.")]
        [SerializeField] private float idleRate = 4f;

        private TrafficVehicle vehicle;
        private ParticleSystem ps;
        private float prevSpeed;
        private float idleCooldown;

        private void Awake()
        {
            vehicle = GetComponent<TrafficVehicle>();
            Build();
        }

        private void Build()
        {
            var go = new GameObject("Exhaust");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = tailpipe;

            ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.7f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startColor = smokeColour;
            main.gravityModifier = -0.05f;                                  // drift up a touch
            main.simulationSpace = ParticleSystemSimulationSpace.World;     // puffs hang as the car drives on (a trail)
            main.maxParticles = 24;

            var emission = ps.emission;
            emission.rateOverTime = 0f;                                     // driven manually from speed in Update

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.05f;

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = grad;

            var sizeOl = ps.sizeOverLifetime;
            sizeOl.enabled = true;
            sizeOl.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.4f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = FXMaterials.SoftDustMaterial();
            renderer.sortingOrder = -1;

            ps.Stop();
        }

        private void OnEnable()
        {
            prevSpeed = vehicle != null ? vehicle.CurrentSpeed : 0f;
            if (ps != null) ps.Play();
        }

        private void OnDisable()
        {
            if (ps != null) { ps.Clear(); ps.Stop(); }
        }

        private void Update()
        {
            if (ps == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float speed = vehicle != null ? vehicle.CurrentSpeed : 0f;
            float accel = (speed - prevSpeed) / dt;
            prevSpeed = speed;

            if (accel > puffAccel)
                ps.Emit(Random.Range(2, 5)); // a burst when it accelerates hard

            if (idleRate > 0f && speed > 0.5f)
            {
                idleCooldown -= dt;
                if (idleCooldown <= 0f)
                {
                    ps.Emit(1);
                    idleCooldown = 1f / idleRate;
                }
            }
        }
    }
}
