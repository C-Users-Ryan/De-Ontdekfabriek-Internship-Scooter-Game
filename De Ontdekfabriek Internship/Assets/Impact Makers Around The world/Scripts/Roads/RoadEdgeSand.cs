using UnityEngine;
using UnityEngine.Rendering;
using KenyaScooter.Core;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// Red-laterite SAND that the road sits in (Oplevering 25 Jun 2026: "there is a lot of sand on and beside the
    /// road; the edges should transition from road to sand rather than a hard line").
    ///
    /// 2026-07-06 rework — ONE CONTINUOUS BED instead of two shoulder strips. The bed is a single flat sheet the
    /// full width of the verges that runs UNDER the road (at <see cref="yLevel"/>, just below the road surface, so
    /// the opaque road draws over the middle). Two things fall out of that:
    ///   • there is never a bare-tile gap at the road edges — sand is continuous beneath the tarmac, so the road
    ///     genuinely sits IN the sand;
    ///   • the ragged DUST fray is only on the OUTSIDE — the sheet is solid across the whole middle (under and
    ///     beside the road) and only fingers out into loose grains at the two far edges where it meets the open
    ///     ground. No raggedness faces the road any more.
    ///
    /// Still lit + alpha-clipped on KenyaScooter/RoadSand (Shaders/Resources) so the outer edge dissolves into
    /// individual grains, and the sheet SCROLLS its texture with the world like ScrollingGround (geometry static,
    /// only the texture offset moves) so it never bends or pops. Self-activates after scene load, additive and
    /// reversible (<see cref="sandEnabled"/>), no collider — pure scenery.
    /// </summary>
    public sealed class RoadEdgeSand : MonoBehaviour
    {
        [Header("Master")]
        [SerializeField] private bool sandEnabled = true;

        [Header("Sand bed (metres from road centre)")]
        [Tooltip("Half-width of the sand bed each side of the road centre. The bed is CONTINUOUS across the middle " +
                 "and runs UNDER the road, so there is never a bare-tile gap at the road edges.")]
        [SerializeField] private float halfWidth = 30f;
        [Tooltip("Width of the ragged DUST fray at each OUTER edge (metres), where the sand fingers out into the " +
                 "open ground. Everything inside this — beside AND under the road — stays solid, so the dust " +
                 "pattern is only on the outside of the road shape.")]
        [SerializeField] private float frayMetres = 12f;
        [Tooltip("How far behind / ahead of the player the bed extends (metres). Cover the road draw distance.")]
        [SerializeField] private float zBehind = 80f;
        [SerializeField] private float zAhead = 420f;
        [Tooltip("Height of the bed. Just BELOW the road surface (road sits at y = 0) so the opaque road draws over " +
                 "the middle and the sand shows only beside/under it. Raise toward 0 if the bed is hidden; lower if " +
                 "it pokes up through the road.")]
        [SerializeField] private float yLevel = -0.02f;

        [Header("Look")]
        [Tooltip("Red-laterite sand colour. The grain texture is multiplied by this, so this is the dial for how red the sand is.")]
        [SerializeField] private Color sandColour = new Color(0.58f, 0.36f, 0.22f, 1f);
        [Tooltip("Texture repeats per metre along the road (grain density + how fast the ragged edge pattern repeats).")]
        [SerializeField] private float tilesPerMetre = 0.08f;
        [Tooltip("How strongly the sand scrolls with the world (1 = locked to the road).")]
        [SerializeField] private float scrollMultiplier = 1f;

        [Header("Outer dust dissolve (KenyaScooter/RoadSand shader)")]
        [Tooltip("How strongly the OUTER sand boundary dissolves into loose grains. 0 = smooth cutout line; higher = wider speckle band.")]
        [SerializeField, Range(0f, 1f)] private float edgeNoiseStrength = 0.4f;
        [Tooltip("Dissolve grain cells across (x) and along (y) one texture tile. Higher = finer grains.")]
        [SerializeField] private Vector2 edgeNoiseCells = new Vector2(110f, 240f);

        // ---- Self-bootstrap (mirrors the other runtime-built scenery) ---------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<RoadEdgeSand>() != null)
                return;
            var go = new GameObject("RoadEdgeSand (auto)");
            go.AddComponent<RoadEdgeSand>();
        }

        private MeshRenderer bedRenderer;
        private MaterialPropertyBlock mpb;
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST"); // URP Lit
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST"); // built-in fallback
        private bool built;

        private void Awake()
        {
            if (!sandEnabled)
                return;
            Build();
        }

        private void Build()
        {
            if (built)
                return;

            Texture2D tex = BuildSandTexture();
            Material mat = BuildSandMaterial(tex);
            mpb = new MaterialPropertyBlock();

            float vMax = Mathf.Max(1f, (zAhead + zBehind) * tilesPerMetre);
            bedRenderer = BuildBed("RoadEdgeSand_Bed", vMax, mat);
            built = true;
        }

        private void Update()
        {
            if (!built || bedRenderer == null)
                return;

            // Scroll the sand grain (and its ragged outer edge) with the world, the way ScrollingGround scrolls its
            // texture. The geometry stays put; only the texture offset moves, so it can never bend or overlap.
            float dist = WorldSpeed.Instance != null ? WorldSpeed.Instance.DistanceTravelled : 0f;
            float offset = dist * tilesPerMetre * scrollMultiplier;
            var st = new Vector4(1f, 1f, 0f, -offset);

            bedRenderer.GetPropertyBlock(mpb);
            mpb.SetVector(BaseMapStId, st);
            mpb.SetVector(MainTexStId, st);
            bedRenderer.SetPropertyBlock(mpb);
        }

        /// <summary>The single sand bed: one flat quad the full width (-halfWidth .. +halfWidth), lying just under
        /// the road so the road draws over the middle. UV.x = 0 at the left outer edge, 1 at the right outer edge
        /// (so the fray is symmetric on both outsides); UV.y runs along the road for the scroll.</summary>
        private MeshRenderer BuildBed(string bedName, float vMax, Material mat)
        {
            var go = new GameObject(bedName);
            go.transform.SetParent(transform, false);

            float zMin = -zBehind;
            float zMax = zAhead;

            var mesh = new Mesh { name = bedName };
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, yLevel, zMin),
                new Vector3(-halfWidth, yLevel, zMax),
                new Vector3( halfWidth, yLevel, zMax),
                new Vector3( halfWidth, yLevel, zMin),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, vMax),
                new Vector2(1f, vMax),
                new Vector2(1f, 0f),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat; // shared; the material is double-sided so winding never hides the bed
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true; // it is ground, let the warm light read on it
            return mr;
        }

        /// <summary>A lit, alpha-clipped sand material. Prefers the custom KenyaScooter/RoadSand shader (in
        /// Shaders/Resources so builds keep it): the clip edge dissolves into individual grains, so the outer
        /// boundary reads as sand fingering into the ground instead of ending in a line. Falls back to plain
        /// URP/Lit cutout. Double-sided as a safety net for the quad winding.</summary>
        private Material BuildSandMaterial(Texture2D tex)
        {
            Shader shader = Shader.Find("KenyaScooter/RoadSand");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var m = new Material(shader) { name = "RoadEdgeSand (runtime)" };

            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex; // _MainTex / built-in fallback
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", sandColour);
            if (m.HasProperty("_Color")) m.SetColor("_Color", sandColour);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f); // dry, matte sand
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);

            // Alpha cutout: opaque sand with a clipped edge (the texture's alpha is the DENSITY mask — solid in the
            // middle, fraying only at the outer edges).
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off); // double-sided safety net
            // Granular dissolve (custom shader only; the HasProperty guards keep the URP/Lit fallback safe).
            if (m.HasProperty("_NoiseStrength")) m.SetFloat("_NoiseStrength", edgeNoiseStrength);
            if (m.HasProperty("_NoiseCells")) m.SetVector("_NoiseCells", new Vector4(edgeNoiseCells.x, edgeNoiseCells.y, 0f, 0f));
            m.renderQueue = (int)RenderQueue.AlphaTest;
            return m;
        }

        /// <summary>Procedural warm sand with a DENSITY mask that is SOLID across the whole middle (under and beside
        /// the road) and only frays at the two OUTER edges. The fray threshold varies in BOTH u and v so the sand
        /// fingers out into the ground in irregular tongues instead of stripey bands. Grayscale-warm grain tinted
        /// red by the material's BaseColor, so the redness is one dial. Tiles along V for the scroll.</summary>
        private Texture2D BuildSandTexture()
        {
            const int w = 192;  // across the bed (UV.x: 0 left outer .. 1 right outer)
            const int h = 192;  // along the road (UV.y), tiled
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "RoadEdgeSand (runtime)",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[w * h];

            // The fray band as a fraction of the (full-width) UV: frayMetres out of the whole 2*halfWidth sheet.
            float frayFrac = Mathf.Clamp(frayMetres / Mathf.Max(1f, 2f * halfWidth), 0.02f, 0.45f);

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float edgeDist = Mathf.Min(u, 1f - u); // 0 at the two outer edges, 0.5 at the centre

                    float a01;
                    if (edgeDist >= frayFrac)
                    {
                        a01 = 1f; // solid: the whole middle, under and beside the road
                    }
                    else
                    {
                        // Inside the outer fray band: dissolve from 0 (very edge) to 1 (fray inner boundary), with
                        // the threshold wandering in BOTH u and v so the sand reaches out in irregular fingers
                        // rather than horizontal stripes.
                        float t = edgeDist / frayFrac;
                        float broad = Mathf.PerlinNoise(u * 7f + 0.5f, v * 11f);
                        float thresh = Mathf.Lerp(0.08f, 0.82f, broad);
                        a01 = Mathf.Clamp01((t - thresh) / 0.35f);
                        if (a01 > 0f && a01 < 1f)
                        {
                            // Speckle the transition so the shader's grain dissolve scatters it into loose grains.
                            float fine = Mathf.PerlinNoise(u * 33f, v * 41f);
                            a01 = Mathf.Clamp01(a01 * Mathf.Lerp(0.5f, 1.5f, fine));
                        }
                    }

                    byte a = (byte)(Mathf.Clamp01(a01) * 255f);

                    // Warm grain: a broad lightness variation plus a finer speckle, kept grayscale-warm so the
                    // material's red BaseColor sets the actual sand colour.
                    float grain = Mathf.PerlinNoise(u * 9f, v * 22f);
                    float speck = Mathf.PerlinNoise(u * 40f, v * 90f);
                    float light = Mathf.Clamp01(Mathf.Lerp(0.80f, 1.10f, grain) * Mathf.Lerp(0.95f, 1.05f, speck));
                    byte r = (byte)(light * 255f);
                    byte g = (byte)(light * 0.96f * 255f);
                    byte b = (byte)(light * 0.90f * 255f);
                    px[y * w + x] = new Color32(r, g, b, a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
