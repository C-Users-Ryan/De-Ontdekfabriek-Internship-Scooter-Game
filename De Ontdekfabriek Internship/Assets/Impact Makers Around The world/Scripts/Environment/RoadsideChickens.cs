using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.Environment
{
    /// <summary>
    /// A small flock pecking on the verge that SCATTERS AWAY FROM THE ROAD as the player nears — the
    /// classic reactive-world beat, ground edition (FX Design Spec "Levend Kenia" §4.5, 3 Jul 2026): the
    /// world not only exists, it NOTICES you. The ground companion to <see cref="RoadsideWaver"/>'s wave.
    ///
    /// Spawned as a prop by <see cref="RoadsidePropSpawner"/> (so it rides tiles, the curve and the rewind).
    /// Birds are pooled child pivots with a two-frame procedural flap — the SkyLife trick, grounded: a
    /// vertical squash sine while running reads as flapping at verge distance. No Animator, no navigation,
    /// no gameplay contact; little dust poofs at their feet mark the startle.
    ///
    /// SAFETY RULE (on-message for a game that teaches safe driving): scatter direction is strictly
    /// OUTWARD, away from the road — the scenery must never rehearse "animals jump in front of you".
    /// Only CHILD transforms are animated; the spawner owns the prop root (the RoadsideWaver contract).
    /// </summary>
    public sealed class RoadsideChickens : MonoBehaviour
    {
        [Tooltip("Planar distance to the player (the origin) at which the flock startles.")]
        [SerializeField] private float triggerDistance = 11f;
        [Tooltip("Initial scatter speed, m/s; decays, then the birds settle and resume pecking.")]
        [SerializeField] private float scatterSpeed = 3f;
        [Tooltip("How far a bird runs before settling, metres.")]
        [SerializeField] private float runDistance = 2.2f;
        [Tooltip("Optional: one soft cluck on the startle (pitch-jittered, once per pass). Empty = silent.")]
        [SerializeField] private AudioClip cluck;

        private enum BirdState { Peck, Run, Settle }

        private sealed class Bird
        {
            public Transform tr;
            public Vector3 rest;        // authored local position (the pecking spot)
            public Vector3 baseScale;
            public BirdState state;
            public float speed;
            public float ran;           // metres run so far
            public float seed;          // desyncs the peck bob
        }

        private Bird[] birds;
        private RoadsideProp prop;
        private ParticleSystem poof;
        private AudioSource voice;
        private bool startled;

        private void Awake()
        {
            prop = GetComponent<RoadsideProp>();

            // The flock is whatever 'Bird*' children the prefab carries (the factory builds four).
            var found = new System.Collections.Generic.List<Bird>(6);
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Bird"))
                    continue;
                found.Add(new Bird
                {
                    tr = child,
                    rest = child.localPosition,
                    baseScale = child.localScale,
                    seed = Random.value * 17f,
                });
            }
            birds = found.ToArray();

            poof = BuildPoof();
            if (cluck != null)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.maxDistance = 30f;
            }
        }

        private void OnEnable()
        {
            // Fresh from the pool: everyone back on their pecking spot, re-armed for one startle.
            startled = false;
            if (birds == null)
                return;
            foreach (Bird b in birds)
            {
                b.state = BirdState.Peck;
                b.ran = 0f;
                b.tr.localPosition = b.rest;
                b.tr.localScale = b.baseScale;
            }
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (birds == null || birds.Length == 0 || (state != GameState.Playing && state != GameState.AtCheckpoint))
                return;

            // The player sits at the origin (the same planar test RoadsideWaver uses).
            Vector3 here = transform.position;
            float planar = new Vector2(here.x, here.z).magnitude;

            if (!startled && planar < triggerDistance)
                Startle();
            if (startled && planar > triggerDistance * 2f)
                startled = false; // the player is long gone — re-arm (matters only if the prop lingers)

            float dt = Time.deltaTime;
            float t = Time.time;
            // Outward in prop-local space: local +X is the road's right, and RoadLateral's sign says which
            // shoulder this flock stands on — so away-from-road is simply the lateral's own sign.
            float outward = prop != null && prop.RoadLateral < 0f ? -1f : 1f;

            foreach (Bird b in birds)
                TickBird(b, dt, t, outward);
        }

        private void Startle()
        {
            startled = true;
            foreach (Bird b in birds)
            {
                if (b.state != BirdState.Peck)
                    continue;
                b.state = BirdState.Run;
                b.speed = scatterSpeed * Random.Range(0.8f, 1.2f);
                b.ran = 0f;
                if (poof != null)
                {
                    poof.transform.position = b.tr.position;
                    poof.Emit(2);
                }
            }
            if (voice != null && cluck != null)
            {
                voice.pitch = Random.Range(0.9f, 1.1f);
                voice.PlayOneShot(cluck); // once per startle — the flock clucks as one, not four times
            }
        }

        private void TickBird(Bird b, float dt, float t, float outward)
        {
            switch (b.state)
            {
                case BirdState.Peck:
                    // Head-down pecking with hashed pauses: a bob that stalls, so the flock never metronomes.
                    float bob = Mathf.Max(0f, Mathf.Sin(t * 3.1f + b.seed)) * (Mathf.PerlinNoise(t * 0.3f, b.seed) > 0.4f ? 1f : 0f);
                    b.tr.localPosition = b.rest + new Vector3(0f, -0.04f * bob, 0f);
                    b.tr.localScale = b.baseScale;
                    break;

                case BirdState.Run:
                    float step = b.speed * dt;
                    b.tr.localPosition += new Vector3(outward * step, 0f, Random.Range(-0.3f, 0.3f) * dt);
                    b.ran += step;
                    // The two-frame flap, grounded: a fast vertical squash reads as beating wings.
                    float flap = 1f - 0.35f * Mathf.Abs(Mathf.Sin(t * 18f + b.seed));
                    b.tr.localScale = new Vector3(b.baseScale.x, b.baseScale.y * flap, b.baseScale.z);
                    if (b.ran >= runDistance)
                        b.state = BirdState.Settle;
                    break;

                case BirdState.Settle:
                    b.speed = Mathf.MoveTowards(b.speed, 0f, 4f * dt);
                    b.tr.localPosition += new Vector3(outward * b.speed * dt, 0f, 0f);
                    b.tr.localScale = Vector3.MoveTowards(b.tr.localScale, b.baseScale, dt);
                    if (b.speed <= 0.01f)
                    {
                        b.rest = b.tr.localPosition; // pecking resumes where the run ended, behind the player
                        b.state = BirdState.Peck;
                    }
                    break;
            }
        }

        /// <summary>Tiny feet-dust: two puffs per startled bird, on the shared soft-dust material.</summary>
        private ParticleSystem BuildPoof()
        {
            var go = new GameObject("FeetPoof");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startColor = new Color(0.8f, 0.64f, 0.45f, 0.4f);
            main.maxParticles = 12;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.08f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = FX.FXMaterials.SoftDustMaterial();

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f; // Emit()-only
            ps.Play();
            return ps;
        }
    }
}
