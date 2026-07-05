using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.Config;

namespace KenyaScooter.FX
{
    /// <summary>
    /// Speed-line particle emission scaled by world speed (MDA A1 — sensation).
    ///
    /// Because the camera is fixed and the world scrolls past, the particles need their own motion to read as
    /// streaks. The default <see cref="wiiRadialPreset"/> builds the recognisable cartoon/"Wii" speed read:
    /// long, thin streaks spawned through a CONE OF DEPTH ahead of the camera, so they appear near a vanishing
    /// point and fan outward to the screen edges as they rush past — a radial speed tunnel, not a flat sheet of
    /// dots at one distance (which did not read as speed). Turn the preset off to fall back to the older flat-box
    /// look driven by the legacy fields. A render-pipeline-safe white material is built either way (the built-in
    /// particle material renders magenta under URP).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SpeedLines : MonoBehaviour
    {
        [SerializeField] private float maxEmissionRate = 180f;
        [Tooltip("Legacy look only. Over-cruise fraction (0 = cruising speed, 1 = max) to emission fraction.")]
        [SerializeField] private AnimationCurve emissionBySpeed = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.12f, 0f), new Keyframe(1f, 1f));

        [Header("Auto-setup")]
        [Tooltip("Build a complete speed-line effect at runtime. Turn off only to hand-author the ParticleSystem (the safe material is still applied).")]
        [SerializeField] private bool autoConfigure = true;

        [Header("Wii-style radial preset")]
        [Tooltip("Build the speed lines as long, thin streaks that fan out from a vanishing point ahead — the recognisable 'whoosh'. " +
                 "Overrides the legacy flat-box look. A new field, so it switches an existing setup to the new look automatically.")]
        [SerializeField] private bool wiiRadialPreset = true;
        [Tooltip("How fast streaks rush past the camera. Faster + longer reads as more speed.")]
        [SerializeField] private float streakSpeed = 36f;
        [Tooltip("Renderer stretch for the preset: higher = longer streaks.")]
        [SerializeField] private float streakLength = 2.6f;
        [Tooltip("Peak streaks per second at top speed (preset). Kept modest so the slipstream reads as a light haze, not a wall of streaks. Capped at runtime too, so an old scene value can never make it harsh again.")]
        [SerializeField] private float streakDensity = 90f;
        [Tooltip("Depth of the streak tunnel ahead of the camera (metres). Deeper = continuous streaks at all distances, not a flat wall.")]
        [SerializeField] private float tunnelDepth = 30f;
        [Tooltip("Spread of the streak tunnel in degrees. Wider = streaks fan out toward the screen edges more.")]
        [SerializeField] private float tunnelAngle = 16f;

        [Header("Look")]
        [Tooltip("Optional. Assign your own URP-compatible particle material to take full control. Empty = an unlit near-white material is built at runtime so particles can never go magenta.")]
        [SerializeField] private Material overrideMaterial;
        [SerializeField] private Color lineColor = new Color(1f, 0.99f, 0.96f, 0.55f);

        // Speed dust moved out (Levend Kenia §02, 3 Jul 2026): the warm dust is now the three-layer
        // SlipstreamDust (kick + wake + grain), which self-bootstraps — nothing to wire here.

        [Header("Legacy motion (used only when the Wii preset is OFF)")]
        [Tooltip("Streak length from the particle's own size.")]
        [SerializeField] private float lengthScale = 1.5f;
        [Tooltip("Extra streak length from the particle's speed.")]
        [SerializeField] private float velocityScale = 0.06f;
        [SerializeField] private Vector2 streakSizeRange = new Vector2(0.04f, 0.09f);
        [Tooltip("Local distance ahead of the camera where streaks spawn.")]
        [SerializeField] private float spawnAheadDistance = 14f;
        [Tooltip("Width x height of the spawn area, in metres.")]
        [SerializeField] private Vector2 spawnAreaSize = new Vector2(10f, 6f);
        [Tooltip("How fast streaks rush toward and past the camera.")]
        [SerializeField] private float rushSpeed = 22f;

        private ParticleSystem.EmissionModule emission;
        private Transform anchor;

        // Self-bootstrap like DustRush so the streaks exist with zero wiring in any scene. The guard means the
        // hand-placed scene instance (a camera child) is left untouched — this only fills in a missing one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<SpeedLines>() != null)
                return;
            var go = new GameObject("Speed Lines (auto)", typeof(ParticleSystem));
            go.AddComponent<SpeedLines>();
        }

        private void Awake()
        {
            ParticleSystem ps = GetComponent<ParticleSystem>();
            emission = ps.emission;

            ApplySafeMaterial(ps);
            if (autoConfigure)
            {
                if (wiiRadialPreset)
                    ConfigureRadial(ps);
                else
                    ConfigureLegacy(ps);
            }
        }

        private void Update()
        {
            // A scene-placed instance already rides the camera (leave it); a bootstrapped one has no parent, so
            // ride Camera.main like DustRush does — otherwise its local-space streak cone would sit at the origin.
            if (anchor == null)
            {
                if (transform.parent != null)
                {
                    anchor = transform.parent;
                }
                else
                {
                    Camera cam = Camera.main;
                    if (cam == null)
                        return;
                    anchor = cam.transform;
                    transform.SetParent(anchor, false);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                }
            }

            float overCruise = -1f;
            if ((GameManager.State == GameState.Playing || GameManager.State == GameState.AtCheckpoint)
                && WorldSpeed.Instance != null)
            {
                WorldSpeed ws = WorldSpeed.Instance;
                // 0 at or below cruising (base) speed, 1 at max — the effect builds the faster you go than cruise.
                overCruise = Mathf.InverseLerp(ws.BaseSpeed, ws.MaxSpeed, ws.Current);
            }

            // White speed lines: pure speed feedback. THE high-frequency streak read.
            float lineRate = 0f;
            if (overCruise >= 0f)
                lineRate = wiiRadialPreset
                    // The dense streaks are what actually sell speed (dust alone was too sparse). A small dead
                    // zone keeps cruising calm, then it climbs fast. Mathf.Max floors the peak so an old, low
                    // serialized streakDensity from the scene instance can never quietly cap it back to a trickle.
                    ? Mathf.Max(streakDensity, 160f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 1f, overCruise))
                    : emissionBySpeed.Evaluate(overCruise) * maxEmissionRate;

            // UN-RETIRED (user direction, best-of-both): the white streaks run EVERYWHERE again, alongside the
            // warm DustRush motes — the lines carry the fast, frequent speed read; the dust carries the
            // atmosphere. (Was hard-zeroed on 2026-07-05 when dust briefly replaced them.)
            emission.rateOverTime = lineRate;
        }

        /// <summary>
        /// The Wii-style radial tunnel: long thin streaks spawned through a deep cone ahead of the camera,
        /// rushing back past it. Off-axis streaks elongate radially from the screen centre and sweep to the
        /// edges as they pass — the classic forward-speed read — while the depth keeps them continuous.
        /// </summary>
        private void ConfigureRadial(ParticleSystem ps)
        {
            const float near = 3f;

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            if (r != null)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = Mathf.Min(streakLength, 2.6f); // clean slipstream streaks, not long thick scratches
                r.velocityScale = 0.12f;
                r.alignment = ParticleSystemRenderSpace.View;
            }

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f; // motion comes from velocity-over-lifetime, so direction is exact
            main.startLifetime = Mathf.Max(0.15f, (near + tunnelDepth + 8f) / Mathf.Max(1f, streakSpeed));
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f); // thin, so they read as clean lines not dots
            Color startTint = lineColor;
            startTint.a = Mathf.Min(lineColor.a <= 0f ? 1f : lineColor.a, 0.55f); // never fully opaque; the fade below carries the rest
            main.startColor = startTint;
            main.gravityModifier = 0f;
            main.maxParticles = 400;
            main.playOnAwake = true;

            // A hollow cone of spawn positions stretching deep ahead: streaks appear near the vanishing point
            // and fan out to the screen edges as they rush toward the camera. The apex radius is the PROTECTED
            // SIGHTLINE (Levend Kenia §02): no streak crosses the centre where the road information lives.
            WeatherConfig weather = DustAtmosphere.ResolveConfig();
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.ConeVolume;
            shape.angle = tunnelAngle;
            shape.radius = Mathf.Max(0.3f, weather != null ? weather.centreClearRadius : 0.8f);
            shape.length = tunnelDepth;
            shape.position = new Vector3(0f, 0f, near);
            shape.rotation = Vector3.zero;

            // Each streak's speed EASES IN over its life (0.7 → 2.1 × streakSpeed): the texture accelerates
            // past you, which reads as rush rather than a constant drizzle (Levend Kenia §02, L3).
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            var ease = new AnimationCurve(new Keyframe(0f, 0.333f), new Keyframe(1f, 1f));
            var flat = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
            // x/y/z must share one MinMaxCurve mode (Unity: "Particle Velocity curves must all be in the same mode").
            velocity.x = new ParticleSystem.MinMaxCurve(1f, flat);
            velocity.y = new ParticleSystem.MinMaxCurve(1f, flat);
            velocity.z = new ParticleSystem.MinMaxCurve(-streakSpeed * 2.1f, ease); // rush back toward (and past) the camera

            // Feather each streak in and out over its short life so they never pop as hard scratches: they swell out
            // of the vanishing point and dissolve as they sweep past, which reads as soft wind rather than clutter.
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            if (!ps.isPlaying)
                ps.Play();
        }

        /// <summary>The older flat-box look: a wide sheet of streaks at one distance ahead, rushing back.</summary>
        private void ConfigureLegacy(ParticleSystem ps)
        {
            ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                psRenderer.lengthScale = lengthScale;
                psRenderer.velocityScale = velocityScale;
                psRenderer.alignment = ParticleSystemRenderSpace.View;
            }

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            main.startLifetime = Mathf.Max(0.1f, (spawnAheadDistance + 6f) / Mathf.Max(1f, rushSpeed));
            main.startSize = new ParticleSystem.MinMaxCurve(streakSizeRange.x, streakSizeRange.y);
            main.startColor = lineColor;
            main.gravityModifier = 0f;
            main.maxParticles = 600;
            main.playOnAwake = true;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(spawnAreaSize.x, spawnAreaSize.y, 1f);
            shape.position = new Vector3(0f, 0f, spawnAheadDistance);
            shape.rotation = Vector3.zero;

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(-rushSpeed);

            if (!ps.isPlaying)
                ps.Play();
        }

        /// <summary>
        /// Guarantees a material on a shader that exists in the active pipeline, so the
        /// particles can never fall back to the magenta error shader.
        /// </summary>
        private void ApplySafeMaterial(ParticleSystem ps)
        {
            ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            if (psRenderer == null)
                return;

            psRenderer.material = overrideMaterial != null ? overrideMaterial : BuildSafeMaterial();
        }

        private Material BuildSafeMaterial()
        {
            // Sprites/Default first: it is unlit, ALWAYS alpha-blends, respects the particle's vertex colour and a
            // texture, ships with every pipeline and never resolves to the magenta error shader. The old order
            // picked the URP particle shader first, which — with NO base texture assigned — sampled an empty (black)
            // map and drew the streaks DARK instead of white (the "lines are not white" bug). We now always feed a
            // soft white streak texture, and alpha-blend it, so the lines read as soft light whichever shader wins.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");

            Material mat = new Material(shader) { name = "SpeedLines (runtime)" };
            Texture2D tex = SoftStreakTexture();
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", tex);
            // White tint; the per-particle start alpha and the colour-over-lifetime fade carry the opacity.
            Color tint = new Color(lineColor.r, lineColor.g, lineColor.b, 1f);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", tint);
            return mat;
        }

        // A soft round white sprite (alpha feathers out to the edge) so the stretched streaks have soft tips and
        // read as wind, not hard white rectangles. Built once and shared across instances.
        private static Texture2D softStreakTex;
        private static Texture2D SoftStreakTexture()
        {
            if (softStreakTex != null)
                return softStreakTex;

            const int size = 32;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SpeedLineStreak (runtime)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a *= a; // soft falloff toward the edge
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            softStreakTex = t;
            return t;
        }
    }
}
