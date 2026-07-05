using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Session;

namespace KenyaScooter.FX
{
    /// <summary>
    /// A faint MIRAGE over the DISTANT road on the hot phases of the day. A low, transparent, warm shimmer band
    /// is built far ahead near the horizon line over the road; its mesh vertices and UVs WOBBLE on a sine over
    /// time, so the band ripples subtly the way hot air does, and its alpha is low so it never reads as solid.
    ///
    /// CHEAP AND SUBTLE BY DESIGN: true refraction needs a distortion/grab-pass shader, which is out of scope on
    /// this URP build. Instead this is a soft warm semi-transparent strip whose own geometry ripples — no extra
    /// passes, no render texture. It shows mainly on the hot midday/afternoon phases (MCHANA / ALASIRI), read from
    /// <see cref="DayCycleManager.CurrentPhase"/>, and fades to nothing on the cool morning/evening phases.
    ///
    /// Pure scenery, never touches gameplay, additive and safe. It also follows the facilitator dust toggle
    /// (<see cref="WeatherConfig.dustEnabled"/>, via <see cref="DustAtmosphere.ResolveConfig"/>) since it is a
    /// heat/dust-air effect, so when the dust is switched off the shimmer goes with it. Self-bootstraps after
    /// scene load with a URP-safe transparent unlit material (built like SpeedLines.BuildSafeMaterial), so it
    /// needs zero wiring and can never render magenta.
    /// </summary>
    public sealed class HeatShimmer : MonoBehaviour
    {
        [Header("Master")]
        [Tooltip("Off: no shimmer ever shows (independent of the dust toggle, which can also hide it).")]
        [SerializeField] private bool enabledEffect = true;

        [Header("Placement")]
        [Tooltip("How far ahead of the player the shimmer band sits, near the horizon line over the road (metres).")]
        [SerializeField] private float distance = 90f;
        [Tooltip("Width of the band across the road (metres). A little wider than the road so it hugs the horizon.")]
        [SerializeField] private float width = 26f;
        [Tooltip("Vertical height of the shimmer band above the road surface (metres). Low and flat.")]
        [SerializeField] private float bandHeight = 2.2f;
        [Tooltip("Height of the band's base above the ground (metres), so it floats just over the distant road.")]
        [SerializeField] private float groundOffset = 0.15f;

        [Header("Wobble")]
        [Tooltip("How far the band's vertices ripple (metres). Small — this is a heat-haze waver, not a wave.")]
        [SerializeField] private float wobbleAmplitude = 0.18f;
        [Tooltip("How fast the ripple travels (cycles/second-ish). Slow and lazy reads as heat.")]
        [SerializeField] private float wobbleSpeed = 1.6f;
        [Tooltip("How many ripples span the band's width. More = finer shimmer.")]
        [SerializeField] private float wobbleWaves = 3f;
        [Tooltip("Horizontal columns across the band. More = smoother ripple, slightly more cost.")]
        [SerializeField] private int columns = 24;

        [Header("Look")]
        [Tooltip("Warm shimmer colour. Alpha is the PEAK opacity on the hottest phase — keep it low and subtle.")]
        [SerializeField] private Color shimmerColour = new Color(0.95f, 0.86f, 0.66f, 0.14f);
        [Tooltip("Seconds the shimmer takes to ease in/out when the phase changes, so it never pops.")]
        [SerializeField] private float fadeSeconds = 2.5f;

        // Self-bootstrap after scene load (mirrors DustAtmosphere), so it is present in the build with no wiring.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<HeatShimmer>() != null)
                return;
            var go = new GameObject("HeatShimmer (auto)");
            go.AddComponent<HeatShimmer>();
        }

        // Day phases (DayCycleManager.CurrentPhase, 0..3): 0 ASUBUHI, 1 MCHANA, 2 ALASIRI, 3 JIONI.
        private const int MCHANA = 1;   // hot midday — full shimmer
        private const int ALASIRI = 2;  // hot afternoon — full shimmer

        private MeshFilter filter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Vector3[] baseVerts;   // flat rest positions
        private Vector3[] verts;       // wobbled positions written each frame (reused, no per-frame alloc)
        private Vector2[] baseUv;      // flat rest UVs
        private Vector2[] uv;          // scrolled UVs written each frame (reused)
        private MaterialPropertyBlock mpb;
        private Color tint;            // current eased colour (alpha rises/falls with the phase)
        private DayCycleManager dayCycle;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            BuildBand();
            tint = shimmerColour;
            tint.a = 0f; // start invisible; Update eases it up only on a hot phase
            mpb = new MaterialPropertyBlock();
            ApplyTint();
        }

        private void Update()
        {
            if (mesh == null)
                return;

            bool dustOn = DustAtmosphere.ResolveConfig().dustEnabled;
            bool playing = GameManager.State == GameState.Playing || GameManager.State == GameState.AtCheckpoint;

            // Show only on the hot phases, only while playing, only when the dust toggle is on, only when enabled.
            int phase = CurrentPhase();
            bool hot = phase == MCHANA || phase == ALASIRI;
            float targetAlpha = (enabledEffect && dustOn && playing && hot) ? shimmerColour.a : 0f;

            float rate = fadeSeconds > 0.01f ? shimmerColour.a / fadeSeconds : 1f;
            tint.a = Mathf.MoveTowards(tint.a, targetAlpha, rate * Time.deltaTime);
            ApplyTint();

            // Skip the vertex work entirely when fully invisible (idle phases cost almost nothing).
            if (tint.a <= 0.0001f)
                return;

            WobbleVertices();
        }

        /// <summary>Reads the live day phase from the DayCycleManager (cached, re-found if it goes away).
        /// Returns the hot MCHANA phase as a safe default if no day cycle is present, so the shimmer still
        /// shows on a scene without the full day system — it is a hot-savanna effect.</summary>
        private int CurrentPhase()
        {
            if (dayCycle == null)
                dayCycle = FindObjectOfType<DayCycleManager>();
            return dayCycle != null ? dayCycle.CurrentPhase : MCHANA;
        }

        private void ApplyTint()
        {
            if (meshRenderer == null)
                return;
            meshRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, tint);
            mpb.SetColor(ColorId, tint);
            meshRenderer.SetPropertyBlock(mpb);
        }

        /// <summary>Ripples the band's vertices and scrolls its UVs on a sine over time — the cheap shimmer.
        /// Writes into reused arrays so there is no per-frame allocation.</summary>
        private void WobbleVertices()
        {
            float t = Time.time * wobbleSpeed;
            float uScroll = Time.time * wobbleSpeed * 0.05f;
            for (int i = 0; i < baseVerts.Length; i++)
            {
                Vector3 b = baseVerts[i];
                // Phase the ripple along the band's width so the wobble travels sideways like rising air.
                float phase = (b.x / Mathf.Max(0.01f, width)) * wobbleWaves * Mathf.PI * 2f;
                float wob = Mathf.Sin(t + phase);
                // Top row ripples most, base row stays anchored to the road, so it shimmers upward.
                float heightFactor = baseUv[i].y; // 0 at base, 1 at top
                verts[i].x = b.x + wob * wobbleAmplitude * 0.5f * heightFactor;
                verts[i].y = b.y + Mathf.Sin(t * 0.8f + phase * 1.3f) * wobbleAmplitude * heightFactor;
                verts[i].z = b.z;

                uv[i].x = baseUv[i].x + wob * 0.02f;
                uv[i].y = baseUv[i].y + uScroll;
            }
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.RecalculateBounds();
        }

        /// <summary>Builds the flat shimmer strip: a low horizontal band of quads far ahead over the road, with
        /// a URP-safe transparent unlit material (built like SpeedLines so it can never go magenta). The band is
        /// 'columns' quads wide and one quad tall; UV.y encodes height (0 base, 1 top) for the wobble.</summary>
        private void BuildBand()
        {
            var go = new GameObject("HeatShimmerBand");
            go.transform.SetParent(transform, false);
            // Fixed in world space ahead of the stationary player, over the distant road near the horizon.
            go.transform.position = new Vector3(0f, groundOffset, distance);
            go.transform.rotation = Quaternion.identity;

            filter = go.AddComponent<MeshFilter>();
            meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.material = BuildSafeMaterial();

            int cols = Mathf.Max(2, columns);
            int vcount = (cols + 1) * 2; // two rows (base, top) of cols+1 verts
            baseVerts = new Vector3[vcount];
            verts = new Vector3[vcount];
            baseUv = new Vector2[vcount];
            uv = new Vector2[vcount];
            var tris = new int[cols * 6];

            float half = width * 0.5f;
            for (int c = 0; c <= cols; c++)
            {
                float fx = (float)c / cols;            // 0..1 across the band
                float x = -half + fx * width;
                int b = c * 2;                          // base-row vertex index
                int top = b + 1;                        // top-row vertex index
                baseVerts[b] = new Vector3(x, 0f, 0f);
                baseVerts[top] = new Vector3(x, bandHeight, 0f);
                baseUv[b] = new Vector2(fx, 0f);
                baseUv[top] = new Vector2(fx, 1f);
            }
            System.Array.Copy(baseVerts, verts, vcount);
            System.Array.Copy(baseUv, uv, vcount);

            for (int c = 0; c < cols; c++)
            {
                int b = c * 2;
                int ti = c * 6;
                // Two triangles per column quad (b, b+1 base/top of this column; b+2, b+3 of the next).
                tris[ti + 0] = b;
                tris[ti + 1] = b + 1;
                tris[ti + 2] = b + 2;
                tris[ti + 3] = b + 1;
                tris[ti + 4] = b + 3;
                tris[ti + 5] = b + 2;
            }

            mesh = new Mesh { name = "HeatShimmerBand" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            // Generous bounds so the wobble never culls the band when the camera looks along the road.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(width + 4f, bandHeight + 4f, 4f));
            filter.sharedMesh = mesh;
        }

        /// <summary>A URP-safe transparent unlit material (mirrors SpeedLines.BuildSafeMaterial). Sprites/Default
        /// is alpha-blended, unlit, respects the tint we push via the property block, ships with every pipeline
        /// and never resolves to the magenta error shader. The soft-dust texture gives the band a soft falloff.</summary>
        private static Material BuildSafeMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            var mat = new Material(shader) { name = "HeatShimmer (runtime)" };
            // Reuse the shared soft-dust dot so the band has a soft, non-hard-edged falloff.
            Texture2D tex = FXMaterials.SoftDustMaterial().mainTexture as Texture2D;
            if (tex != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            return mat;
        }
    }
}
