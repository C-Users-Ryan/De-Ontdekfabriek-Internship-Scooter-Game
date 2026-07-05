using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KenyaScooter.Session;

namespace KenyaScooter.Sky
{
    /// <summary>
    /// Sky atmosphere: a scatter of slow, soft CLOUD bands drifting high across the warm sky, plus one big warm
    /// GLOW disc sitting on the real sun so the low African sun blooms into the haze. Together they give the upper
    /// sky some life and a focal point, complementing the distant <see cref="HorizonRange"/> ridges below.
    ///
    /// Clouds are soft, lumpy alpha billboards (no recognisable silhouette, so the "flat cardboard" read that sank
    /// the old horizon billboards can't happen here — a cloud looks the same from any angle). They are camera-
    /// anchored, drift sideways and recycle, and tint + fade with the day cycle (fullest by day, warm at dusk, gone
    /// at night). The glow is a single soft disc placed in the sun's own direction (read from the scene's directional
    /// light), warm-tinted, brightening at dawn and dusk and fading out at night — it augments the skybox sun rather
    /// than adding a second one.
    ///
    /// Render-pipeline-safe (Sprites/Default, never the magenta built-in default under URP), depth-tested but not
    /// depth-written, drawn just before the dust so haze veils it. Self-bootstraps after scene load (mirrors
    /// NightSky / SkyLife), additive, reversible via the facilitator toggle, and never touches gameplay.
    /// </summary>
    public sealed class SkyAtmosphere : MonoBehaviour
    {
        [Header("Master")]
        [SerializeField] private bool atmosphereEnabled = true;

        [Header("Clouds")]
        [Tooltip("How many soft cloud bands drift across the sky at once.")]
        [SerializeField] private int cloudCount = 8;
        [Tooltip("Distance of the cloud layer from the camera (metres). Far, so they read as sky, not props.")]
        [SerializeField] private float cloudDistance = 620f;
        [Tooltip("Elevation band above the horizon the clouds occupy, in degrees (low band, high band). Kept above the ridgeline so clouds sit in open sky.")]
        [SerializeField] private Vector2 cloudElevation = new Vector2(19f, 44f);
        [Tooltip("Half-width of the forward arc the clouds spread across, in degrees to either side of straight ahead.")]
        [SerializeField] private float cloudArc = 78f;
        [Tooltip("On-sky size of a cloud band at that distance (metres): min, max.")]
        [SerializeField] private Vector2 cloudSize = new Vector2(150f, 300f);
        [Tooltip("Sideways drift speed of a cloud, in metres/second (min, max).")]
        [SerializeField] private Vector2 cloudDrift = new Vector2(3.5f, 9f);
        [Tooltip("Peak opacity of a cloud (before the day-phase fade). Kept low so clouds stay soft and hazy.")]
        [SerializeField, Range(0f, 1f)] private float cloudOpacity = 0.34f;

        [Header("Sun glow")]
        [Tooltip("Add a warm glow disc on the sun. Off = clouds only.")]
        [SerializeField] private bool sunGlow = true;
        [Tooltip("On-sky size of the sun glow (metres).")]
        [SerializeField] private float glowSize = 240f;
        [Tooltip("Warm colour the glow tends toward (blended over the live fog colour).")]
        [SerializeField] private Color glowWarm = new Color(1f, 0.82f, 0.55f, 1f);
        [Tooltip("Peak opacity of the glow at dawn/dusk (before the phase curve).")]
        [SerializeField, Range(0f, 1f)] private float glowOpacity = 0.55f;

        [Header("Fade")]
        [SerializeField] private float fadeSeconds = 3f;

        public const string PrefKey = "ksg.sky";
        private static SkyAtmosphere instance;

        /// <summary>Facilitator on/off for the clouds + sun glow. On by default; persisted in PlayerPrefs.</summary>
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

        // Day phases (DayCycleManager.CurrentPhase): 0 ASUBUHI, 1 MCHANA, 2 ALASIRI, 3 JIONI, 4 MAGHARIBI, 5 USIKU.
        private const int ASUBUHI = 0, MCHANA = 1, ALASIRI = 2, JIONI = 3, MAGHARIBI = 4, USIKU = 5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<SkyAtmosphere>() != null)
                return;
            new GameObject("SkyAtmosphere (auto)").AddComponent<SkyAtmosphere>();
        }

        private sealed class Cloud
        {
            public Transform tr;
            public MeshRenderer renderer;
            public Vector3 localPos;   // relative to the camera anchor
            public float drift;        // sideways m/s
            public float size;
            public float baseAlpha;
        }

        private readonly List<Cloud> clouds = new List<Cloud>(16);
        private Transform anchor;      // the camera
        private Transform glowTr;
        private MeshRenderer glowRenderer;
        private Material cloudMaterial; // Sprites/Default + soft cloud texture
        private Material glowMaterial;  // Sprites/Default + soft round texture
        private Mesh quad;
        private MaterialPropertyBlock mpb;
        private DayCycleManager dayCycle;
        private Light sun;
        private float cloudFade, glowFade;
        private bool built;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            instance = this;
            atmosphereEnabled = PlayerPrefs.GetInt(PrefKey, atmosphereEnabled ? 1 : 0) == 1;
            if (!atmosphereEnabled)
                return;
            mpb = new MaterialPropertyBlock();
            quad = BuildQuad();
            cloudMaterial = BuildMaterial("SkyClouds", BuildCloudTexture(), 2602);
            glowMaterial = BuildMaterial("SunGlow", BuildSoftDot(), 2602);
            built = true;
        }

        private void Start()
        {
            if (!built)
                return;
            ResolveAnchor();
            dayCycle = FindObjectOfType<DayCycleManager>();
            for (int i = 0; i < Mathf.Max(0, cloudCount); i++)
                SpawnCloud(initial: true);
            if (sunGlow)
                BuildGlow();
        }

        private void SetActiveState(bool on)
        {
            atmosphereEnabled = on;
            for (int i = 0; i < clouds.Count; i++)
                if (clouds[i].tr != null) clouds[i].tr.gameObject.SetActive(on);
            if (glowTr != null) glowTr.gameObject.SetActive(on);
        }

        private void Update()
        {
            if (!atmosphereEnabled || !built)
                return;
            if (anchor == null)
                ResolveAnchor();
            if (anchor == null)
                return;

            int phase = dayCycle != null ? dayCycle.CurrentPhase : MCHANA;
            float dt = Time.deltaTime;
            Color fog = RenderSettings.fogColor;

            // Clouds: fullest by day, dimmer and warm at dusk, thinning through the blue hour, gone at night.
            float cloudTarget = phase == USIKU ? 0.05f : phase == MAGHARIBI ? 0.18f : phase == JIONI ? 0.5f : 1f;
            cloudFade = Mathf.MoveTowards(cloudFade, cloudTarget, dt / Mathf.Max(0.1f, fadeSeconds));
            Color cloudTint = Color.Lerp(fog, Color.white, 0.42f);

            float bound = cloudDistance * Mathf.Sin(Mathf.Deg2Rad * (cloudArc + 6f));
            for (int i = 0; i < clouds.Count; i++)
            {
                Cloud c = clouds[i];
                c.localPos.x += c.drift * dt;
                if (c.localPos.x > bound)
                    ReseedCloud(c, -bound);          // wrapped off the right edge — re-enter from the left
                c.tr.localPosition = c.localPos;
                FaceCamera(c.tr);
                ApplyTint(c.renderer, cloudTint, c.baseAlpha * cloudFade);
            }

            // Sun glow: brightest at dawn/dusk, medium midday, out at night. Placed on the real sun's direction.
            if (glowRenderer != null)
            {
                if (sun == null) sun = ResolveSun();
                float glowTarget =
                    phase == ASUBUHI || phase == ALASIRI ? 1f :
                    phase == MCHANA ? 0.55f :
                    phase == JIONI ? 0.4f : 0f;
                glowFade = Mathf.MoveTowards(glowFade, glowTarget, dt / Mathf.Max(0.1f, fadeSeconds));

                if (sun != null && glowFade > 0.002f)
                {
                    Vector3 toSun = -sun.transform.forward;            // direction from the camera toward the sun
                    if (toSun.y < 0.02f) toSun.y = 0.02f;              // keep the glow at or just above the horizon
                    glowTr.localPosition = anchor.InverseTransformDirection(toSun.normalized) * (cloudDistance * 1.05f);
                    FaceCamera(glowTr);
                }
                glowRenderer.enabled = glowFade > 0.002f;
                if (glowRenderer.enabled)
                {
                    Color glow = Color.Lerp(fog, glowWarm, 0.7f);
                    ApplyTint(glowRenderer, glow, glowOpacity * glowFade);
                }
            }
        }

        // ---- Clouds ------------------------------------------------------------------------------------

        private void SpawnCloud(bool initial)
        {
            var go = new GameObject("Cloud");
            go.transform.SetParent(anchor != null ? anchor : transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cloudMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var c = new Cloud { tr = go.transform, renderer = mr };
            // Spread the initial set anywhere across the arc; later re-seeds enter from the left edge.
            float startX = initial ? Random.Range(-1f, 1f) : -1f;
            ReseedCloud(c, startX * cloudDistance * Mathf.Sin(Mathf.Deg2Rad * cloudArc));
            clouds.Add(c);
        }

        /// <summary>Re-place a cloud at a given camera-local X, with fresh elevation, size, drift and opacity.</summary>
        private void ReseedCloud(Cloud c, float localX)
        {
            float elevDeg = Random.Range(cloudElevation.x, cloudElevation.y);
            float y = Mathf.Sin(Mathf.Deg2Rad * elevDeg) * cloudDistance;
            float horiz = Mathf.Cos(Mathf.Deg2Rad * elevDeg) * cloudDistance;
            // z from the remaining horizontal reach after x; keep it positive (ahead) so clouds sit in the forward sky.
            float z = Mathf.Sqrt(Mathf.Max(1f, horiz * horiz - localX * localX));

            c.localPos = new Vector3(localX, y, z);
            c.drift = Random.Range(cloudDrift.x, cloudDrift.y);
            c.size = Random.Range(cloudSize.x, cloudSize.y);
            c.baseAlpha = cloudOpacity * Random.Range(0.7f, 1f);
            c.tr.localPosition = c.localPos;
            c.tr.localScale = new Vector3(c.size, c.size * 0.5f, 1f); // clouds are wider than tall
        }

        private void BuildGlow()
        {
            var go = new GameObject("SunGlow");
            glowTr = go.transform;
            glowTr.SetParent(anchor != null ? anchor : transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            glowRenderer = go.AddComponent<MeshRenderer>();
            glowRenderer.sharedMaterial = glowMaterial;
            glowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;
            glowRenderer.lightProbeUsage = LightProbeUsage.Off;
            glowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            glowTr.localScale = new Vector3(glowSize, glowSize, 1f);
            glowRenderer.enabled = false;
            sun = ResolveSun();
        }

        // ---- Helpers -----------------------------------------------------------------------------------

        private void FaceCamera(Transform tr)
        {
            // The anchor IS the camera, so face back toward its local origin (the lens).
            Vector3 toCam = -tr.localPosition;
            if (toCam.sqrMagnitude < 0.0001f)
                return;
            tr.localRotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
        }

        private void ApplyTint(MeshRenderer r, Color tint, float alpha)
        {
            if (r == null)
                return;
            r.enabled = alpha > 0.003f;
            if (!r.enabled)
                return;
            Color c = tint;
            c.a = Mathf.Clamp01(alpha);
            r.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(mpb);
        }

        private void ResolveAnchor()
        {
            Camera cam = Camera.main;
            anchor = cam != null ? cam.transform : transform;
            for (int i = 0; i < clouds.Count; i++)
                if (clouds[i].tr != null && clouds[i].tr.parent != anchor)
                    clouds[i].tr.SetParent(anchor, false);
            if (glowTr != null && glowTr.parent != anchor)
                glowTr.SetParent(anchor, false);
        }

        private static Light ResolveSun()
        {
            if (RenderSettings.sun != null)
                return RenderSettings.sun;
            Light[] lights = FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional)
                    return lights[i];
            return null;
        }

        // ---- Runtime assets ----------------------------------------------------------------------------

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "SkyQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f), new Vector3( 0.5f, -0.5f, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material BuildMaterial(string name, Texture2D tex, int renderQueue)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            var m = new Material(shader) { name = name + " (runtime)" };
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            m.renderQueue = renderQueue;
            return m;
        }

        /// <summary>A wide, lumpy, soft cloud alpha: several overlapping soft blobs with a flatter base, so it reads
        /// as a hazy cloud band rather than a disc. White RGB; the warm/day tint is applied at runtime.</summary>
        private static Texture2D BuildCloudTexture()
        {
            const int w = 128, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Cloud (runtime)", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];

            // A handful of soft lumps along the middle, biased to a flat-ish underside.
            var lumps = new[]
            {
                new Vector3(0.24f, 0.50f, 0.20f), new Vector3(0.42f, 0.60f, 0.26f),
                new Vector3(0.58f, 0.56f, 0.24f), new Vector3(0.74f, 0.50f, 0.20f),
                new Vector3(0.50f, 0.46f, 0.30f)
            };

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float a = 0f;
                    for (int l = 0; l < lumps.Length; l++)
                    {
                        float dx = (u - lumps[l].x);
                        float dy = (v - lumps[l].y) * 1.6f;         // squash vertically → wide, flat cloud
                        float r = Mathf.Sqrt(dx * dx + dy * dy) / lumps[l].z;
                        a += Mathf.Clamp01(1f - r);
                    }
                    a = Mathf.Clamp01(a);
                    a = a * a * (3f - 2f * a);                      // smooth falloff
                    a *= Mathf.SmoothStep(0f, 0.35f, v);            // fade the very bottom so the base is soft
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D BuildSoftDot()
        {
            const int s = 64, half = s / 2;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "SunGlow (runtime)", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = 1f - r;
                    a = a * a;                                      // tighter core, long soft halo
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
