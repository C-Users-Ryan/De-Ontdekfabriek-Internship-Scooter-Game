using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Player;
using KenyaScooter.Roads;

namespace KenyaScooter.FX
{
    /// <summary>
    /// The player's own contact patch — the diegetic anchor the screen-space Slipstream implies (FX Design
    /// Spec "Levend Kenia" §4.2, 3 Jul 2026). Screen dust says "you feel fast"; wheel dust says "your tyre
    /// is on THIS dirt". Three triggers, all world-space so the dust is LEFT BEHIND like VehicleDustTrail:
    ///
    ///   ROOST  — a burst at the rear wheel on the same throttle-open edge as the Slipstream kick, so the
    ///            cause (wheel) and the sensation (screen edge) share one beat.
    ///   SCRAPE — puffs off the tyre side while leaning hard (reads ScooterLean.Current01); doubles as
    ///            gentle feedback that you are at the steering limit.
    ///   POOF   — a puff at the wheel on the pothole/rock hit, reusing the exact GameEvents.HazardHit the
    ///            haptics already listen to. Zero new wiring.
    ///
    /// Self-bootstraps after scene load (mirrors DustAtmosphere) and finds the player + rear wheel on its
    /// own; everything obeys the facilitator dust toggle and Drive folds in the speed-FX toggle + motion dial.
    /// </summary>
    public sealed class ScooterContactDust : MonoBehaviour
    {
        [Tooltip("The rear wheel / contact point the bursts spawn from. Empty = auto-find a child of the player " +
                 "whose name contains 'wheel' (preferring the rearmost), falling back to just behind the player.")]
        [SerializeField] private Transform rearWheel;
        [Tooltip("Absolute lean (0..1) above which the tyre side scrapes dust.")]
        [SerializeField, Range(0f, 1f)] private float leanThreshold = 0.62f;
        [Tooltip("Rising Drive per second that counts as 'the throttle opened' (matches the Slipstream kick).")]
        [SerializeField] private float kickEdge = 0.5f;

        // Self-bootstrap after scene load (mirrors DustAtmosphere / SlipstreamDust): zero scene wiring.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<ScooterContactDust>() != null)
                return;
            var go = new GameObject("ScooterContactDust (auto)");
            go.AddComponent<ScooterContactDust>();
        }

        private ParticleSystem roost, scrape;
        private ParticleSystem.EmissionModule scrapeEmission;
        private Transform player;
        private float prevDrive;
        private bool kicked;

        private void Awake()
        {
            roost = BuildBurst("WheelRoost");
            scrape = BuildScrape();
            scrapeEmission = scrape.emission;
        }

        private void OnEnable() => GameEvents.HazardHit += OnHazardHit;
        private void OnDisable() => GameEvents.HazardHit -= OnHazardHit;

        private void OnHazardHit(HazardSpawnConfig definition, float kmh, Vector3 position)
        {
            if (!DustAtmosphere.ResolveConfig().dustEnabled)
                return;
            roost.transform.position = WheelPosition();
            roost.Emit(10); // the pothole poof, at the wheel — the suspension dip is the haptics' job
        }

        private void Update()
        {
            if (player == null)
            {
                player = GameManager.Player;
                if (player == null)
                    return; // no player yet (bare test scene) — try again next frame
            }

            WeatherConfig weather = DustAtmosphere.ResolveConfig();
            bool dustOn = weather.dustEnabled;
            float drive = SpeedFeel.Drive;
            float dDrive = (drive - prevDrive) / Mathf.Max(Time.deltaTime, 1e-4f);
            prevDrive = drive;

            // The roost: same edge trigger + latch as the Slipstream kick, so both fire as one beat.
            if (dustOn && dDrive > kickEdge && !kicked)
            {
                roost.transform.position = WheelPosition();
                roost.Emit(12);
                kicked = true;
            }
            if (dDrive < 0.1f)
                kicked = false;

            // The scrape: the tyre side brushes the dirt while leaning hard. Positioned on the lean side
            // (Current01 is + when leaning left), just behind the contact patch.
            float lean = ScooterLean.Current01;
            bool scraping = dustOn && Mathf.Abs(lean) > leanThreshold && drive > 0.05f;
            // The tyre scrapes more loose material off a murram surface — same shared dirt boost as the plumes.
            scrapeEmission.rateOverTime = scraping ? 14f * drive * RoadSurfaceFeel.DustBoost(weather.dirtDustMultiplier) : 0f;
            if (scraping)
            {
                Vector3 wheel = WheelPosition();
                scrape.transform.position = wheel + new Vector3(-Mathf.Sign(lean) * 0.35f, 0f, -0.25f);
            }
        }

        /// <summary>The rear wheel's world position: the assigned/auto-found wheel child if there is one,
        /// otherwise a point low and just behind the player root.</summary>
        private Vector3 WheelPosition()
        {
            if (rearWheel == null && player != null)
                rearWheel = FindRearWheel(player);
            if (rearWheel != null)
                return rearWheel.position;
            return player != null ? player.position + new Vector3(0f, 0.05f, -0.7f) : new Vector3(0f, 0.05f, -0.7f);
        }

        private static Transform FindRearWheel(Transform root)
        {
            Transform best = null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant();
                if (!n.Contains("wheel") && !n.Contains("wiel") && !n.Contains("tyre") && !n.Contains("band"))
                    continue;
                if (best == null || t.position.z < best.position.z) // the rearmost wheel-ish child wins
                    best = t;
            }
            return best;
        }

        /// <summary>An Emit()-only world-space burst on the shared dust material — the roost/poof system.</summary>
        private ParticleSystem BuildBurst(string name)
        {
            WeatherConfig wc = DustAtmosphere.ResolveConfig();
            ParticleSystem ps = NewSystem(name);

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // left behind, like VehicleDustTrail
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startColor = wc != null ? wc.vehicleDustColour : new Color(0.8f, 0.64f, 0.45f, 0.45f);
            main.gravityModifier = -0.02f;
            main.maxParticles = 60;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(115f, 0f, 0f); // sprayed back-and-up off the contact patch

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f)));

            FadeOut(ps);
            ps.Play();
            return ps;
        }

        /// <summary>The lean scrape: a small continuous side-spray whose rate Update drives from the lean.</summary>
        private ParticleSystem BuildScrape()
        {
            WeatherConfig wc = DustAtmosphere.ResolveConfig();
            ParticleSystem ps = NewSystem("LeanScrape");

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startColor = wc != null ? wc.vehicleDustColour : new Color(0.8f, 0.64f, 0.45f, 0.45f);
            main.gravityModifier = 0.02f;
            main.maxParticles = 20;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.1f;

            FadeOut(ps);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f; // Update drives it from the lean
            ps.Play();
            return ps;
        }

        private ParticleSystem NewSystem(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = FXMaterials.SoftDustMaterial();
            return ps;
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
