using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.FX
{
    /// <summary>
    /// An occasional small dry-savanna whirlwind crossing the plain BESIDE the road (a warm-dust "dust devil").
    /// A tall, thin, slowly-rotating column of warm dust built on the shared soft-dust material (mirrors
    /// DustAtmosphere / VehicleDustTrail: the built-in particle material renders magenta under URP). Particles
    /// rise and spiral as the column scrolls past the stationary player with the world (-Z, like every other
    /// world object) and dissipates; it then respawns off one shoulder after a random interval.
    ///
    /// Pure scenery, never touches gameplay. A SINGLE recycled column (no per-frame allocation) whose emission
    /// and respawning are gated on <see cref="GameState.Playing"/> and on the facilitator dust toggle
    /// (<see cref="WeatherConfig.dustEnabled"/>, via <see cref="DustAtmosphere.ResolveConfig"/>), so when dust is
    /// switched off the devil goes with it. Self-bootstraps after scene load, so it needs zero wiring.
    /// </summary>
    public sealed class DustDevil : MonoBehaviour
    {
        [Header("Master")]
        [Tooltip("Off: no dust devil ever spawns (independent of the dust toggle, which can also hide it).")]
        [SerializeField] private bool enabledEffect = true;

        [Header("Timing")]
        [Tooltip("Random gap (seconds) between one devil dissipating and the next spawning. Occasional, not constant.")]
        [SerializeField] private Vector2 spawnIntervalRange = new Vector2(14f, 30f);
        [Tooltip("Seconds a devil lives once spawned before it dissipates and the next gap begins.")]
        [SerializeField] private float lifetime = 9f;

        [Header("Shape")]
        [Tooltip("Height of the dust column in metres (tall and thin).")]
        [SerializeField] private float height = 9f;
        [Tooltip("Radius of the spiralling column base in metres (keep small so it reads as thin).")]
        [SerializeField] private float baseRadius = 0.9f;
        [Tooltip("How far off the road shoulder the devil spawns (metres along +X / -X). Well beyond the road lateral limit.")]
        [SerializeField] private float sideDistance = 22f;
        [Tooltip("How far ahead of the player the devil first appears (metres). It then scrolls back past the player.")]
        [SerializeField] private float spawnAhead = 55f;

        [Header("Intensity")]
        [Tooltip("Dust particles per second while a devil is active. Scaled by the world soft-dust look.")]
        [SerializeField] private float intensity = 60f;
        [Tooltip("Warm dust colour of the column (alpha = how visible each mote is). Kept low and warm.")]
        [SerializeField] private Color dustColour = new Color(0.80f, 0.64f, 0.45f, 0.32f);
        [Tooltip("How fast dust spirals around the column (degrees/second).")]
        [SerializeField] private float spinSpeed = 220f;
        [Tooltip("How fast dust rises up the column (metres/second).")]
        [SerializeField] private float riseSpeed = 2.2f;

        // Self-bootstrap after scene load (mirrors DustAtmosphere / PedestrianCrossingSpawner), so the effect
        // is present in the build with no scene wiring. A facilitator switches it off with the dust toggle.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<DustDevil>() != null)
                return;
            var go = new GameObject("DustDevil (auto)");
            go.AddComponent<DustDevil>();
        }

        private ParticleSystem column;
        private ParticleSystem.EmissionModule columnEmission;
        private Transform columnTransform;

        // null = idle (waiting for the next spawn); a value = a devil is live and this is its remaining life.
        private bool active;
        private float timer;     // counts down the idle gap, then the active lifetime
        private float worldZ;    // current world-Z of the column (scrolls toward the player at world speed)

        private void Awake()
        {
            BuildColumn();
            // Start in the idle gap so the first devil appears a little after the session begins.
            active = false;
            timer = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
        }

        private void Update()
        {
            // Only run during live play; while not playing the devil holds (no spawn, no emission).
            bool playing = GameManager.State == GameState.Playing;
            // A dust devil is a dust CLOUD, so it obeys the country-only rule: off in town/city zones when set.
            WeatherConfig wc = DustAtmosphere.ResolveConfig();
            bool dustOn = wc.dustEnabled && !(wc.dustCloudsCountryOnly && DustAtmosphere.InTownZone);

            if (!enabledEffect || !playing || !dustOn)
            {
                if (active)
                    StopDevil();
                return;
            }

            timer -= Time.deltaTime;

            if (active)
            {
                // Scroll the live column back past the player with the world, the same way ground/props do.
                float worldSpeed = WorldSpeed.Instance != null ? WorldSpeed.Instance.Current : 0f;
                worldZ -= worldSpeed * Time.deltaTime;
                if (columnTransform != null)
                    columnTransform.position = new Vector3(columnTransform.position.x, 0f, worldZ);

                if (timer <= 0f)
                    StopDevil();
            }
            else if (timer <= 0f && WindField.InReleaseTail)
            {
                // Devils only rise out of a gust front's dying release (Levend Kenia §01/§03): the whirlwind
                // then BELONGS to the weather the rest of the roadside just answered, instead of appearing at
                // random. The timer arms the devil; the next front's tail actually releases it.
                SpawnDevil();
            }
        }

        private void OnDisable()
        {
            if (active)
                StopDevil();
        }

        private void SpawnDevil()
        {
            if (column == null)
                return;

            // Pick a shoulder (left or right of the road, well beyond the lateral limit) and a forward start.
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = side * (sideDistance + Random.Range(-4f, 4f));
            worldZ = spawnAhead + Random.Range(-8f, 8f);
            columnTransform.position = new Vector3(x, 0f, worldZ);

            ParticleSystem.MainModule main = column.main;
            main.startColor = dustColour;

            columnEmission.rateOverTime = intensity;
            column.Clear(true);
            column.Play(true);

            active = true;
            timer = lifetime;
        }

        private void StopDevil()
        {
            active = false;
            // Stop emitting but let the airborne motes fade out naturally (no hard pop).
            if (column != null)
            {
                columnEmission.rateOverTime = 0f;
                column.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            timer = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
        }

        /// <summary>Builds the single recycled dust column: a tall thin emission volume whose motes spiral
        /// (rotational velocity) and rise on the shared soft-dust material, so it reads as a warm whirlwind.</summary>
        private void BuildColumn()
        {
            var go = new GameObject("DustDevilColumn");
            columnTransform = go.transform;
            columnTransform.SetParent(transform, false);
            columnTransform.position = new Vector3(0f, 0f, spawnAhead);
            columnTransform.rotation = Quaternion.identity;

            column = go.AddComponent<ParticleSystem>();
            column.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = column.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // moved as a whole by columnTransform
            main.startSpeed = 0f;            // motion is from velocity/rotation-over-lifetime, kept exact
            main.startLifetime = Mathf.Max(0.5f, height / Mathf.Max(0.5f, riseSpeed));
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
            main.startColor = dustColour;
            main.gravityModifier = 0f;
            main.maxParticles = 260;
            main.playOnAwake = false;

            // A tall, thin vertical column of spawn positions (a slim cylinder approximated by a Box).
            ParticleSystem.ShapeModule shape = column.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(baseRadius * 2f, height, baseRadius * 2f);
            shape.position = new Vector3(0f, height * 0.5f, 0f);

            // Rise straight up; the spiral comes from the orbital (rotational) velocity below.
            ParticleSystem.VelocityOverLifetimeModule velocity = column.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // x/y/z must share one MinMaxCurve mode (Unity: "Particle Velocity curves must all be in the same mode").
            // y is a range, so x and z are written as (v, v) — identical to a constant, but in the same TwoConstants mode.
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(riseSpeed * 0.7f, riseSpeed * 1.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            // Orbital spin around the column's vertical (Y) axis — this is what makes the dust spiral.
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(spinSpeed * Mathf.Deg2Rad);
            velocity.radial = new ParticleSystem.MinMaxCurve(0.1f, 0.4f); // a slight outward flare as it rises

            // Fade each mote in at birth and out at the top, so the column has soft ends.
            ParticleSystem.ColorOverLifetimeModule col = column.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            // Motes shrink a touch as they rise and thin out at the top.
            ParticleSystem.SizeOverLifetimeModule size = column.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0.5f)));

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = FXMaterials.SoftDustMaterial();

            columnEmission = column.emission;
            columnEmission.rateOverTime = 0f; // gated in Update / set on spawn
        }
    }
}
