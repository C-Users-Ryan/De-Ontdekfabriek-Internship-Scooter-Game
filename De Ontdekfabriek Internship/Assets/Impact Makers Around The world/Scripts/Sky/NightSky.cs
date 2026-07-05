using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KenyaScooter.Session;

namespace KenyaScooter.Sky
{
    /// <summary>
    /// Stars and a moon for the new USIKU night phase, so the day cycle actually closes into a Kenyan night rather
    /// than just going dark. A dome of small procedural star points plus one soft moon disc are anchored to the
    /// camera and fade IN through dusk (JIONI) and full at night (USIKU), fading out again by morning. Driven by
    /// <see cref="DayCycleManager.CurrentPhase"/>, so it tracks the same cycle as the sun, fog and sky.
    ///
    /// Built at runtime on a render-pipeline-safe soft-sprite material (never the built-in default, which goes
    /// magenta under URP). The whole dome is far out and behind the horizon/birds, with depth-write off, so it
    /// sits in the sky. Self-activates after scene load, additive and reversible (<see cref="nightSkyEnabled"/>),
    /// and never touches gameplay.
    /// </summary>
    public sealed class NightSky : MonoBehaviour
    {
        [Header("Master")]
        [SerializeField] private bool nightSkyEnabled = true;

        [Header("Stars")]
        [SerializeField] private int starCount = 260;
        [Tooltip("Distance of the star dome (metres). Large, so it sits behind the horizon and birds.")]
        [SerializeField] private float domeRadius = 900f;
        [Tooltip("On-screen star size range at that distance (metres).")]
        [SerializeField] private Vector2 starSize = new Vector2(2.5f, 6f);
        [Tooltip("Star tint (a touch warm-white).")]
        [SerializeField] private Color starColour = new Color(1f, 0.97f, 0.9f, 1f);
        [Tooltip("Very slow drift of the star dome (degrees/second) so the night is not dead still. 0 = static.")]
        [SerializeField] private float driftDegPerSec = 0.25f;

        [Header("Moon")]
        [SerializeField] private float moonSize = 42f;
        [Tooltip("Direction to the moon from the camera (need not be normalised).")]
        [SerializeField] private Vector3 moonDirection = new Vector3(0.5f, 0.7f, 1f);
        [SerializeField] private Color moonColour = new Color(0.95f, 0.96f, 1f, 1f);

        [Header("Fade")]
        [Tooltip("Seconds the stars/moon take to ease in or out when the phase changes.")]
        [SerializeField] private float fadeSeconds = 3f;

        // Day phases (DayCycleManager.CurrentPhase): 0 ASUBUHI, 1 MCHANA, 2 ALASIRI, 3 JIONI, 4 MAGHARIBI, 5 USIKU.
        private const int JIONI = 3;
        private const int MAGHARIBI = 4;
        private const int USIKU = 5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<NightSky>() != null)
                return;
            var go = new GameObject("NightSky (auto)");
            go.AddComponent<NightSky>();
        }

        private Transform anchor;       // the camera, so the dome surrounds the view
        private Transform starsTr;
        private MeshRenderer starsRenderer;
        private MeshRenderer moonRenderer;
        private Material material;       // shared soft-sprite material (white); per-renderer tint via MPB
        private MaterialPropertyBlock mpb;
        private DayCycleManager dayCycle;
        private float nightness;        // 0 day .. 1 full night
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private bool built;

        private void Awake()
        {
            if (!nightSkyEnabled)
                return;
            mpb = new MaterialPropertyBlock();
            material = BuildMaterial(BuildSoftDot());
            BuildStars();
            BuildMoon();
            built = true;
        }

        private void Start()
        {
            ResolveAnchor();
            dayCycle = FindObjectOfType<DayCycleManager>();
        }

        private void Update()
        {
            if (!built)
                return;
            if (anchor == null)
                ResolveAnchor();

            int phase = dayCycle != null ? dayCycle.CurrentPhase : -1;
            // Stars/moon reveal gradually: a hint at the JIONI dusk, most of the way in through the MAGHARIBI
            // blue hour, full at USIKU night — so the sky the player loves eases in instead of popping on.
            float target = phase == USIKU ? 1f : phase == MAGHARIBI ? 0.8f : phase == JIONI ? 0.45f : 0f;
            nightness = Mathf.MoveTowards(nightness, target, Time.deltaTime / Mathf.Max(0.1f, fadeSeconds));

            ApplyAlpha(starsRenderer, starColour, nightness);
            ApplyAlpha(moonRenderer, moonColour, nightness);

            if (starsTr != null && driftDegPerSec != 0f && nightness > 0.001f)
                starsTr.Rotate(Vector3.up, driftDegPerSec * Time.deltaTime, Space.Self);
        }

        private void ApplyAlpha(MeshRenderer r, Color baseTint, float a)
        {
            if (r == null)
                return;
            r.enabled = a > 0.002f; // skip drawing entirely in daylight
            if (!r.enabled)
                return;
            Color c = baseTint;
            c.a = a;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(mpb);
        }

        private void ResolveAnchor()
        {
            Camera cam = Camera.main;
            anchor = cam != null ? cam.transform : transform;
            if (starsTr != null && starsTr.parent != anchor) starsTr.SetParent(anchor, false);
            if (moonRenderer != null && moonRenderer.transform.parent != anchor) moonRenderer.transform.SetParent(anchor, false);
        }

        // ---- Build -------------------------------------------------------------------------------

        private void BuildStars()
        {
            var go = new GameObject("Stars");
            starsTr = go.transform;
            starsTr.SetParent(transform, false);

            var verts = new List<Vector3>(starCount * 4);
            var uvs = new List<Vector2>(starCount * 4);
            var cols = new List<Color>(starCount * 4);
            var tris = new List<int>(starCount * 6);

            for (int i = 0; i < Mathf.Max(0, starCount); i++)
            {
                // Random direction on the UPPER hemisphere (stars only above the horizon).
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.94f + 0.06f;
                dir.Normalize();
                Vector3 pos = dir * domeRadius;

                // A small quad facing the dome centre (the camera).
                Vector3 right = Vector3.Cross(Vector3.up, dir);
                if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
                right.Normalize();
                Vector3 up = Vector3.Cross(dir, right);
                float h = Random.Range(starSize.x, starSize.y) * 0.5f;
                float bright = Random.Range(0.45f, 1f);
                var c = new Color(bright, bright, bright, 1f);

                int b = verts.Count;
                verts.Add(pos - right * h - up * h);
                verts.Add(pos - right * h + up * h);
                verts.Add(pos + right * h + up * h);
                verts.Add(pos + right * h - up * h);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
                cols.Add(c); cols.Add(c); cols.Add(c); cols.Add(c);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }

            var mesh = new Mesh { name = "NightStars" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // plenty of verts
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (domeRadius * 2.2f));

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            starsRenderer = go.AddComponent<MeshRenderer>();
            ConfigureRenderer(starsRenderer);
        }

        private void BuildMoon()
        {
            var go = new GameObject("Moon");
            go.transform.SetParent(transform, false);
            Vector3 dir = moonDirection.sqrMagnitude > 0.0001f ? moonDirection.normalized : Vector3.up;
            Vector3 pos = dir * (domeRadius * 0.96f); // just inside the star dome

            Vector3 right = Vector3.Cross(Vector3.up, dir);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(dir, right);
            float h = moonSize * 0.5f;

            var mesh = new Mesh { name = "Moon" };
            mesh.vertices = new[]
            {
                pos - right * h - up * h, pos - right * h + up * h,
                pos + right * h + up * h, pos + right * h - up * h
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.bounds = new Bounds(pos, Vector3.one * moonSize * 2f);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            moonRenderer = go.AddComponent<MeshRenderer>();
            ConfigureRenderer(moonRenderer);
        }

        private void ConfigureRenderer(MeshRenderer r)
        {
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false; // hidden until night fades in
        }

        /// <summary>Sprites/Default: alpha-blended, unlit, double-sided, respects vertex colour and the per-renderer
        /// _Color from the property block, ships everywhere and never resolves to the magenta URP error shader.</summary>
        private static Material BuildMaterial(Texture2D tex)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            var m = new Material(shader) { name = "NightSky (runtime)" };
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            // Sky dome: draw with the transparent crowd, never write depth, so it sits behind everything in the sky.
            m.renderQueue = (int)RenderQueue.Transparent;
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            return m;
        }

        private static Texture2D BuildSoftDot()
        {
            const int s = 32;
            const float half = s * 0.5f;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "NightDot (runtime)", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a); // soft round falloff
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
