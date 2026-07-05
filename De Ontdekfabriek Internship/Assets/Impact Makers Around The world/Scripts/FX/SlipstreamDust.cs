using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Roads;

namespace KenyaScooter.FX
{
    /// <summary>
    /// The player's speed dust as a three-layer SLIPSTREAM that marks the MOMENT of acceleration instead
    /// of hissing at a constant rate (FX Design Spec "Levend Kenia" §02, 3 Jul 2026 — replaces
    /// SpeedLines.BuildSpeedDust; the white line layer stays in <see cref="SpeedLines"/>, re-tuned there
    /// to the outer annulus).
    ///
    ///   L1 KICK  — one burst of chunky puffs from the LOW SCREEN CORNERS the frame the throttle opens
    ///              (edge-triggered on rising Drive and latched, so it can never become a hiss), plus one
    ///              soft settle puff on release that closes the phrase.
    ///   L2 WAKE  — curling puffs born AT THE ROAD EDGES near the ground (never mid-air), drifting outward
    ///              as they sweep past. Density ∝ Drive², so cruising stays clean and top speed blooms.
    ///   L3 GRAIN — warm dust streaks in the outer annulus only (the centre stays clear — that is where
    ///              the road information lives), each streak's speed EASING IN over its life so the
    ///              texture accelerates past you: rush, not drizzle.
    ///
    /// Every layer multiplies <see cref="SpeedFeel.Drive"/> (the facilitator "Snelheidsbeleving" toggle and
    /// the motion dial come along for free) and the whole thing obeys WeatherConfig.dustEnabled exactly like
    /// the system it replaces. Tuning lives on the shared WeatherConfig (kickBurstCount, kickEdgePerSecond,
    /// wakePeakRate, grainPeakRate, centreClearRadius), read live so the facilitator asset stays the single
    /// tuning surface. Built at runtime on FXMaterials.SoftDustMaterial (never magenta under URP), pooled by
    /// Unity's particle systems (no per-frame allocation), self-bootstrapping (zero scene wiring).
    /// </summary>
    public sealed class SlipstreamDust : MonoBehaviour
    {
        [Tooltip("Warm dust colour for the wake and grain layers (alpha = opacity at birth). The kick reads the " +
                 "WeatherConfig vehicle-dust colour so the beat matches the world's dust.")]
        [SerializeField] private Color dustColour = new Color(0.82f, 0.66f, 0.47f, 0.5f);
        [Tooltip("How fast grain streaks rush past at full ease-in, m/s (matches the old speed-dust rush).")]
        [SerializeField] private float grainSpeed = 30f;
        [Tooltip("Depth of the grain cone ahead of the camera (m) and its spread (deg) — mirrors SpeedLines' tunnel.")]
        [SerializeField] private float tunnelDepth = 30f;
        [SerializeField] private float tunnelAngle = 22f;

        // Self-bootstrap after scene load (mirrors DustAtmosphere): the slipstream is part of the
        // default speed read, so it should exist with zero wiring. SpeedLines no longer builds a dust layer.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<SlipstreamDust>() != null)
                return;
            var go = new GameObject("SlipstreamDust (auto)");
            go.AddComponent<SlipstreamDust>();
        }

        private ParticleSystem kick;
        private ParticleSystem wakeLeft, wakeRight;
        private ParticleSystem.EmissionModule wakeLeftEmission, wakeRightEmission;
        private ParticleSystem grain;
        private ParticleSystem.EmissionModule grainEmission;

        private Transform anchor;      // the camera; systems live in its local space like the old speed dust
        private float prevDrive;
        private float wildBlend = 1f;  // 1 = dirt/wild (this dust reads the speed), 0 = city (the speed lines do)
        private bool kicked, settled;

        private void Update()
        {
            // Anchor to the camera as soon as one exists (some scenes build it late).
            if (anchor == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                    return;
                anchor = cam.transform;
                transform.SetParent(anchor, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                BuildSystems();
            }

            WeatherConfig weather = DustAtmosphere.ResolveConfig();
            bool dustOn = weather == null || weather.dustEnabled;
            float kickEdge = weather != null ? weather.kickEdgePerSecond : 0.5f;

            float drive = SpeedFeel.Drive;

            // Zone hand-over RETIRED (2026-07-05 play-test verdict): dust is the one speed read EVERYWHERE —
            // the white speed lines are off, so this dust no longer crossfades away in city zones.
            wildBlend = 1f;
            drive *= wildBlend;

            float dDrive = (drive - prevDrive) / Mathf.Max(Time.deltaTime, 1e-4f);
            prevDrive = drive;

            // L1 — the beat. Edge-triggered and latched, so it can never become a hiss.
            if (dustOn && dDrive > kickEdge && !kicked)
            {
                EmitKick(weather != null ? weather.kickBurstCount : 18, settle: false);
                kicked = true;
            }
            if (dDrive < 0.1f)
                kicked = false;

            // Release: one soft settle puff per side closes the phrase.
            if (dustOn && dDrive < -kickEdge && !settled)
            {
                EmitKick(4, settle: true);
                settled = true;
            }
            if (dDrive > -0.1f)
                settled = false;

            // L2 + L3 — the sustained layers. Wake is quadratic so cruising stays clean. On a dirt tile the
            // wake (the road-edge churn — the layer that says "THIS ground") is boosted; the grain, which is
            // a speed-feel texture rather than a surface cue, is left alone.
            float dirtBoost = RoadSurfaceFeel.DustBoost(weather != null ? weather.dirtDustMultiplier : 1f);
            float wakeRate = dustOn ? (weather != null ? weather.wakePeakRate : 26f) * drive * drive * dirtBoost : 0f;
            wakeLeftEmission.rateOverTime = wakeRate * 0.5f;
            wakeRightEmission.rateOverTime = wakeRate * 0.5f;

            // Per-frame outward push grows with Drive (struct writes, no allocation).
            float outward = 1.2f + 2.2f * drive;
            float sweep = -(8f + 22f * drive);
            SetWakeVelocity(wakeLeft, -outward, sweep);
            SetWakeVelocity(wakeRight, outward, sweep);

            float grainRate = dustOn
                ? (weather != null ? weather.grainPeakRate : 70f)
                  * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1f, drive))
                : 0f;
            grainEmission.rateOverTime = grainRate;
        }

        private static void SetWakeVelocity(ParticleSystem ps, float x, float z)
        {
            if (ps == null)
                return;
            var vel = ps.velocityOverLifetime;
            // x/y/z must share one MinMaxCurve mode (Unity: "Particle Velocity curves must all be in the same mode").
            vel.x = new ParticleSystem.MinMaxCurve(x * 0.7f, x * 1.3f);
            vel.y = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            vel.z = new ParticleSystem.MinMaxCurve(z * 0.85f, z * 1.15f);
        }

        /// <summary>One kick volley: chunky puffs pushed outward+up from BOTH low corners of the frame (the
        /// settle variant is the same shape, softer and slower — the phrase's closing breath).</summary>
        private void EmitKick(int totalCount, bool settle)
        {
            if (kick == null)
                return;

            WeatherConfig weather = DustAtmosphere.ResolveConfig();
            Color c = weather != null ? weather.vehicleDustColour : dustColour;
            c.a = Mathf.Clamp01(c.a * (settle ? 0.7f : 1.1f)); // the beat is a touch darker/denser than the trail

            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < totalCount; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f; // alternate corners so both fire together
                float speed = settle ? Random.Range(0.8f, 1.6f) : Random.Range(2.5f, 5f);
                ep.position = new Vector3(side * Random.Range(2.8f, 3.6f), Random.Range(0.1f, 0.3f), Random.Range(3.7f, 5.3f));
                ep.velocity = new Vector3(side * speed * 0.8f, speed * Random.Range(0.5f, 0.8f), Random.Range(-0.6f, 0.4f));
                ep.startSize = Random.Range(0.5f, 0.95f) * (settle ? 1.2f : 1f);
                ep.startLifetime = Random.Range(0.5f, 0.8f);
                ep.startColor = c;
                kick.Emit(ep, 1);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Construction (mirrors SpeedLines.BuildSpeedDust's safe-material, feathered-alpha setup)
        // ---------------------------------------------------------------------------------------------------

        private void BuildSystems()
        {
            WeatherConfig weather = DustAtmosphere.ResolveConfig();
            kick = BuildKick();
            wakeLeft = BuildWake("WakeLeft", -1f);
            wakeRight = BuildWake("WakeRight", 1f);
            wakeLeftEmission = wakeLeft.emission;
            wakeRightEmission = wakeRight.emission;
            grain = BuildGrain(weather != null ? weather.centreClearRadius : 0.8f);
            grainEmission = grain.emission;
        }

        /// <summary>Emit()-only burst system for the kick and settle puffs. No shape — EmitKick places every
        /// puff by hand at the two low corners, which one ShapeModule cannot do.</summary>
        private ParticleSystem BuildKick()
        {
            ParticleSystem ps = NewChildSystem("Kick");

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;                 // EmitParams carries position, velocity, size, life, colour
            main.gravityModifier = -0.02f;        // a touch of billow before the fade takes it
            main.maxParticles = 80;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            FeatherAlpha(ps, 0.2f, 0.6f);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.7f), new Keyframe(1f, 1.5f))); // each puff swells as it disperses

            ps.Play();
            return ps;
        }

        /// <summary>One wake edge: a thin ground-hugging box at x = ±(2.7..3.8) — particles are BORN AT THE
        /// GROUND, never mid-air, which is what kills the camera-glued read.</summary>
        private ParticleSystem BuildWake(string name, float side)
        {
            ParticleSystem ps = NewChildSystem(name);

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startColor = dustColour;
            main.gravityModifier = 0f;
            main.maxParticles = 80;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.1f, 0.25f, 22f);                    // thin slab hugging one road edge
            shape.position = new Vector3(side * 3.25f, 0.125f, 14f);       // low: births touch the ground

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            SetWakeVelocity(ps, side * 1.2f, -8f); // per-frame Drive scaling overwrites this each Update

            FeatherAlpha(ps, 0.25f, 0.6f);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(1f, 2.4f)));            // churned dust grows as it passes

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f; // Update drives it from Drive²

            ps.Play();
            return ps;
        }

        /// <summary>The grain: warm streaks through the same deep cone as the white lines but as an ANNULUS
        /// (the apex radius keeps the sightline centre clear) with a per-particle speed that eases IN over its
        /// life — the texture accelerates past you, which reads as rush.</summary>
        private ParticleSystem BuildGrain(float clearRadius)
        {
            const float near = 3f;
            ParticleSystem ps = NewChildSystem("Grain");

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 1.6f;
            r.velocityScale = 0.07f;
            r.alignment = ParticleSystemRenderSpace.View;

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            main.startLifetime = Mathf.Max(0.15f, (near + tunnelDepth + 8f) / Mathf.Max(1f, grainSpeed));
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
            main.startColor = dustColour;
            main.gravityModifier = 0f;
            main.maxParticles = 200;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.ConeVolume;
            shape.angle = tunnelAngle;
            shape.radius = Mathf.Max(0.3f, clearRadius); // the annulus: nothing crosses the protected centre
            shape.length = tunnelDepth;
            shape.position = new Vector3(0f, 0f, near);

            // Speed eases in over each streak's life (0.7 → 2.1 × grainSpeed): acceleration, not drizzle.
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            var ease = new AnimationCurve(new Keyframe(0f, 0.333f), new Keyframe(1f, 1f));
            // x/y/z must share one MinMaxCurve mode, so the still axes are flat zero curves.
            var flat = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
            vel.x = new ParticleSystem.MinMaxCurve(1f, flat);
            vel.y = new ParticleSystem.MinMaxCurve(1f, flat);
            vel.z = new ParticleSystem.MinMaxCurve(-grainSpeed * 2.1f, ease);

            FeatherAlpha(ps, 0.3f, 0.7f);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f; // Update drives it from Drive

            ps.Play();
            return ps;
        }

        private ParticleSystem NewChildSystem(string name)
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

        /// <summary>Fade each puff in and out over its life so nothing ever pops (the shared dust gradient).</summary>
        private static void FeatherAlpha(ParticleSystem ps, float inAt, float holdTo)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inAt), new GradientAlphaKey(1f, holdTo), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }
    }
}
