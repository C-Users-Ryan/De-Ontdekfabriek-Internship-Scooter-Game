using UnityEngine;
using UnityEngine.Rendering;

namespace KenyaScooter.Sky
{
    /// <summary>
    /// The far Rift Valley SKYLINE, done as real geometry: two continuous low-poly mountain RIDGES that encircle
    /// the far distance as warm-dark silhouettes whose feet dissolve into the horizon haze. It replaces the old
    /// <c>HorizonBackdrop</c> billboards, which were removed because flat quads that rotate to face the camera read
    /// as confusing cardboard cut-outs (2026-07-04). This version can never do that: each ridge is ONE closed ring
    /// of mesh, world-axis-aligned, so it is genuinely "over there" from every angle and simply sits on the horizon.
    ///
    /// HOW IT LOOKS: two concentric rings — a nearer, darker, more defined ridge and a farther, paler, taller one
    /// whose peaks poke up between the near ridge's valleys (layered atmospheric depth). The silhouette top is a
    /// seamless rolling ridgeline built from integer-harmonic sines (periodic over 2π, so the loop never shows a
    /// seam). A baked vertical vertex-colour gradient fades each ridge from the live fog colour at its foot (melts
    /// into the haze band) up to a muted warm silhouette at the peaks, so nothing cuts a hard line at the ground.
    ///
    /// HOW IT MOVES: it does not — distant mountains sit still while you drive, which is exactly right for the
    /// fixed-camera runner (the player is stationary at the origin facing +Z and the world scrolls past). The rings
    /// only follow the camera's XZ each frame so they stay centred on the view; the camera's own bank on a turn then
    /// rolls them on screen naturally, like real scenery. Zero parallax bookkeeping, zero per-frame allocation.
    ///
    /// TINT tracks the day cycle for free by reading <see cref="RenderSettings.fogColor"/> (which DayCycleManager
    /// lerps per phase) and re-baking the gradient only when it actually shifts — so dawn, noon, dusk and night all
    /// recolour the range without this system knowing anything about the cycle.
    ///
    /// Render-pipeline-safe (Sprites/Default, never the built-in default that goes MAGENTA under URP — the same
    /// choice NightSky / FXMaterials make), double-sided, drawn just before the dust so haze always veils it and
    /// nearer opaque props/traffic still occlude it. Self-bootstraps after scene load, additive, reversible via the
    /// facilitator toggle, and never touches gameplay.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class HorizonRange : MonoBehaviour
    {
        [Header("Master")]
        [Tooltip("Master switch. Off = no ridges are built and nothing ticks; the feature is invisible and free.")]
        [SerializeField] private bool rangeEnabled = true;

        [Header("Layout")]
        [Tooltip("Vertices around each ring. More = smoother ridgeline, marginally more cost. 96 reads as low-poly hills.")]
        [SerializeField] private int segments = 96;
        [Tooltip("Radius of the near ridge (metres). Kept inside the scrolling ground plane's half-size so it rests on the ground.")]
        [SerializeField] private float nearRadius = 330f;
        [Tooltip("Radius of the far ridge (metres). A little farther out, so its peaks read as a second range behind the first.")]
        [SerializeField] private float farRadius = 378f;
        [Tooltip("Ground level the ridge feet rest on (the road sits at y = 0).")]
        [SerializeField] private float groundY = 0f;

        [Header("Near ridge — darker, sharper")]
        [Tooltip("Min/max height of the near ridge (metres).")]
        [SerializeField] private Vector2 nearHeight = new Vector2(24f, 66f);
        [Tooltip("Warm-dark silhouette colour the near peaks tend toward (before the day-cycle fog blend).")]
        [SerializeField] private Color nearTint = new Color(0.22f, 0.15f, 0.13f, 1f);

        [Header("Far ridge — paler, taller, hazier")]
        [Tooltip("Min/max height of the far ridge (metres). Taller so its peaks clear the near ridge.")]
        [SerializeField] private Vector2 farHeight = new Vector2(40f, 92f);
        [Tooltip("Paler silhouette colour the far peaks tend toward; it is washed further into the haze than the near ridge.")]
        [SerializeField] private Color farTint = new Color(0.34f, 0.26f, 0.23f, 1f);

        [Header("Haze")]
        [Tooltip("How far the peaks pull toward the live fog colour (0 = full silhouette, 1 = fully hazed away). The far ridge doubles this.")]
        [SerializeField, Range(0f, 1f)] private float peakHaze = 0.28f;
        [Tooltip("Re-bake the gradient only once the fog colour has shifted by at least this much (keeps it idle between phases).")]
        [SerializeField] private float retintThreshold = 0.004f;

        /// <summary>PlayerPrefs key the facilitator "Bergen aan de horizon" toggle writes; read on Awake so the choice
        /// applies at startup and live when toggled from the menu.</summary>
        public const string PrefKey = "ksg.range";

        private static HorizonRange instance;

        /// <summary>Facilitator on/off for the distant mountain ridges. On by default; persisted in PlayerPrefs and
        /// applied to the live range immediately when changed from the settings menu.</summary>
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (instance != null) instance.SetActiveState(value);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<HorizonRange>() != null)
                return;
            var go = new GameObject("HorizonRange (auto)");
            go.AddComponent<HorizonRange>();
        }

        private sealed class Ridge
        {
            public Transform transform;
            public MeshRenderer renderer;
            public Mesh mesh;
            public Color[] colors;      // 2 per column (foot, peak); re-baked on a fog shift
            public Color peakTint;      // this ridge's target peak colour before the fog blend
            public float haze;          // 0..1 pull toward fog for this ridge's peaks
        }

        private Ridge near, far;
        private Transform cam;
        private Color lastFog = new Color(-1f, -1f, -1f, -1f);
        private bool built;

        private void Awake()
        {
            instance = this;
            rangeEnabled = PlayerPrefs.GetInt(PrefKey, rangeEnabled ? 1 : 0) == 1;
            if (!rangeEnabled)
                return;
            Build();
        }

        private void SetActiveState(bool on)
        {
            rangeEnabled = on;
            if (on && !built)
                Build();
            if (near?.transform != null) near.transform.gameObject.SetActive(on);
            if (far?.transform != null) far.transform.gameObject.SetActive(on);
        }

        private void Build()
        {
            if (built)
                return;

            // Far ridge draws first (behind); near ridge over it. Both sit just before the dust queue (3000) so the
            // haze veil always washes over them, while nearer opaque geometry still occludes them via the depth test.
            far = BuildRidge("HorizonRidge Far", farRadius, farHeight, phase: 0.0f, ruggedness: 0.75f,
                             tint: farTint, haze: Mathf.Clamp01(peakHaze * 2f), renderQueue: 2600);
            near = BuildRidge("HorizonRidge Near", nearRadius, nearHeight, phase: 3.3f, ruggedness: 1.0f,
                             tint: nearTint, haze: peakHaze, renderQueue: 2601);

            Retint(force: true);
            built = true;
        }

        private Ridge BuildRidge(string name, float radius, Vector2 height, float phase, float ruggedness,
                                 Color tint, float haze, int renderQueue)
        {
            int cols = Mathf.Max(8, segments) + 1;   // +1 duplicate column closes the loop seamlessly
            var verts = new Vector3[cols * 2];
            var uv = new Vector2[cols * 2];
            var colors = new Color[cols * 2];
            var tris = new int[(cols - 1) * 6];

            for (int i = 0; i < cols; i++)
            {
                float t = (float)i / (cols - 1);
                float angle = t * Mathf.PI * 2f;
                float h = Mathf.Lerp(height.x, height.y, Profile(angle, phase, ruggedness));
                float cx = Mathf.Cos(angle) * radius;
                float cz = Mathf.Sin(angle) * radius;

                verts[i * 2] = new Vector3(cx, groundY, cz);            // foot
                verts[i * 2 + 1] = new Vector3(cx, groundY + h, cz);    // peak
                uv[i * 2] = new Vector2(t, 0f);
                uv[i * 2 + 1] = new Vector2(t, 1f);
                colors[i * 2] = Color.white;
                colors[i * 2 + 1] = Color.white;

                if (i < cols - 1)
                {
                    int b = i * 6;
                    int a0 = i * 2, a1 = i * 2 + 1, b1 = (i + 1) * 2 + 1, b0 = (i + 1) * 2;
                    tris[b] = a0; tris[b + 1] = a1; tris[b + 2] = b1;   // Sprites/Default is double-sided, so winding is free
                    tris[b + 3] = a0; tris[b + 4] = b1; tris[b + 5] = b0;
                }
            }

            var mesh = new Mesh { name = name + " (mesh)" };
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (radius * 2.2f + height.y * 2f));
            mesh.MarkDynamic(); // colours are re-baked when the day-cycle fog shifts

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = BuildMaterial(name, renderQueue);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            return new Ridge { transform = go.transform, renderer = mr, mesh = mesh, colors = colors, peakTint = tint, haze = haze };
        }

        /// <summary>A seamless rolling ridgeline in 0..1. Integer-harmonic sines are periodic over 2π so the ring
        /// closes with no seam; a mild power curve keeps most of it as low hills with the odd higher peak.</summary>
        private static float Profile(float angle, float phase, float rugged)
        {
            float n =
                Mathf.Sin(angle * 2f + phase * 1.30f) * 0.50f +
                Mathf.Sin(angle * 5f + phase * 2.70f) * 0.26f +
                Mathf.Sin(angle * 11f + phase * 4.10f) * 0.14f * rugged +
                Mathf.Sin(angle * 19f + phase * 5.90f) * 0.08f * rugged;
            n = Mathf.Clamp01(n * 0.5f + 0.5f);
            return Mathf.Pow(n, 1.35f);
        }

        private void Update()
        {
            if (!rangeEnabled || !built)
                return;

            // Keep the rings centred on the camera's ground point so they stay on the horizon as the camera drifts,
            // without inheriting its rotation (mountains must not spin; the camera's own roll moves them on screen).
            if (cam == null)
            {
                Camera main = Camera.main;
                cam = main != null ? main.transform : null;
            }
            if (cam != null)
            {
                Vector3 p = cam.position;
                transform.SetPositionAndRotation(new Vector3(p.x, 0f, p.z), Quaternion.identity);
            }

            Color fog = RenderSettings.fogColor;
            if (ColorShift(fog, lastFog) > retintThreshold)
                Retint(force: false, fog: fog);
        }

        /// <summary>Re-bakes both ridges' vertex-colour gradients from the current fog colour: foot = fog (melts into
        /// the haze band), peak = the ridge's silhouette tint pulled partway toward fog for atmospheric wash.</summary>
        private void Retint(bool force, Color? fog = null)
        {
            Color f = fog ?? RenderSettings.fogColor;
            lastFog = f;
            RetintRidge(near, f);
            RetintRidge(far, f);
        }

        private static void RetintRidge(Ridge r, Color fog)
        {
            if (r == null) return;
            Color foot = fog;                                   // opaque haze colour at the base
            foot.a = 1f;
            Color peak = Color.Lerp(r.peakTint, fog, r.haze);   // silhouette, washed toward the haze
            peak.a = 1f;
            for (int i = 0; i < r.colors.Length; i += 2)
            {
                r.colors[i] = foot;
                r.colors[i + 1] = peak;
            }
            r.mesh.colors = r.colors;
        }

        private static float ColorShift(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        /// <summary>Sprites/Default: unlit, double-sided, vertex-coloured, ignores scene fog (so the ridge is not
        /// double-hazed) and never resolves to the magenta URP error shader. Depth-tested but not depth-written, on a
        /// queue just below the dust so the haze veils it and nearer opaque geometry still occludes it.</summary>
        private static Material BuildMaterial(string name, int renderQueue)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            var m = new Material(shader) { name = name + " (runtime)" };
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            m.renderQueue = renderQueue;
            return m;
        }
    }
}
