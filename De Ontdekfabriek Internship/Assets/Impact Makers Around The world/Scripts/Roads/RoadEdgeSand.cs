using UnityEngine;
using UnityEngine.Rendering;
using KenyaScooter.Core;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// Red-laterite SAND VERGES that make the road edges read as mixed with sand (Oplevering 25 Jun 2026: "there
    /// is a lot of sand on and beside the road; the edges should transition from road to sand rather than a hard
    /// line"). Two long flat strips run along both shoulders, on a lit, ALPHA-CLIPPED material with a procedurally
    /// generated warm sand-grain texture whose alpha is a DENSITY mask: full sand in the strip, ramping out over a
    /// speckled band at both edges, with tongues biting onto the asphalt and loose freckles landing past them. The
    /// KenyaScooter/RoadSand shader (Shaders/Resources) dissolves the clip edge into individual grains, so the
    /// sand genuinely MIXES with the road instead of meeting it in a wavering line. The strips are static (the
    /// player is fixed at the origin facing +Z) and the sand SCROLLS its texture with the world, exactly like
    /// ScrollingGround, so it never bends or pops and reads as moving ground.
    ///
    /// Why alpha-CLIP (cutout) not alpha-blend: cutout sand is opaque, so it lights and fogs like real ground and
    /// has no transparency sort order to fight the road/ground; the mixed edge comes from the mask + grain dissolve.
    ///
    /// Self-activates after scene load (mirrors ScrollingGround being built once), additive and reversible: with
    /// <see cref="sandEnabled"/> off it builds nothing, and it never touches gameplay (no collider, pure scenery).
    /// </summary>
    public sealed class RoadEdgeSand : MonoBehaviour
    {
        [Header("Master")]
        [SerializeField] private bool sandEnabled = true;

        [Header("Placement (metres from road centre)")]
        [Tooltip("Inner edge of the sand, near the road. Slightly inside the road edge (~3.25 m lane + 1.5 m shoulder) so sand bites onto the asphalt.")]
        [SerializeField] private float innerEdge = 2.8f;
        [Tooltip("Outer edge of the sand, where it frays into the open ground.")]
        [SerializeField] private float outerEdge = 14f;
        [Tooltip("How far behind / ahead of the player the strips extend (metres). Cover the road draw distance.")]
        [SerializeField] private float zBehind = 80f;
        [SerializeField] private float zAhead = 420f;
        [Tooltip("Height above the road surface (road sits at y = 0). A few cm so the sand draws over the road edge without z-fighting.")]
        [SerializeField] private float yLevel = 0.02f;

        [Header("Look")]
        [Tooltip("Red-laterite sand colour. The grain texture is multiplied by this, so this is the dial for how red the sand is.")]
        [SerializeField] private Color sandColour = new Color(0.66f, 0.40f, 0.26f, 1f);
        [Tooltip("Texture repeats per metre along the road (grain density + how fast the ragged edge pattern repeats).")]
        [SerializeField] private float tilesPerMetre = 0.08f;
        [Tooltip("How strongly the sand scrolls with the world (1 = locked to the road).")]
        [SerializeField] private float scrollMultiplier = 1f;

        [Header("Edge dissolve (KenyaScooter/RoadSand shader)")]
        [Tooltip("How strongly the sand boundary dissolves into loose grains. 0 = smooth cutout line; higher = wider speckle band.")]
        [SerializeField, Range(0f, 1f)] private float edgeNoiseStrength = 0.38f;
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

        private MeshRenderer leftRenderer;
        private MeshRenderer rightRenderer;
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
            leftRenderer = BuildStrip("RoadEdgeSand_Left", -1f, vMax, mat);
            rightRenderer = BuildStrip("RoadEdgeSand_Right", 1f, vMax, mat);
            built = true;
        }

        private void Update()
        {
            if (!built)
                return;

            // Scroll the sand grain (and its ragged edge) with the world, the way ScrollingGround scrolls its
            // texture. The geometry stays put; only the texture offset moves, so it can never bend or overlap.
            float dist = WorldSpeed.Instance != null ? WorldSpeed.Instance.DistanceTravelled : 0f;
            float offset = dist * tilesPerMetre * scrollMultiplier;
            var st = new Vector4(1f, 1f, 0f, -offset);

            if (leftRenderer != null)
            {
                leftRenderer.GetPropertyBlock(mpb);
                mpb.SetVector(BaseMapStId, st);
                mpb.SetVector(MainTexStId, st);
                leftRenderer.SetPropertyBlock(mpb);
            }
            if (rightRenderer != null)
            {
                rightRenderer.GetPropertyBlock(mpb);
                mpb.SetVector(BaseMapStId, st);
                mpb.SetVector(MainTexStId, st);
                rightRenderer.SetPropertyBlock(mpb);
            }
        }

        /// <summary>One shoulder strip: a flat quad from innerEdge to outerEdge on the given side, lying just above
        /// the road. UV.x = 0 at the ROAD side (so the ragged inner edge always faces the road on both sides), UV.y
        /// runs along the road for the scroll.</summary>
        private MeshRenderer BuildStrip(string stripName, float side, float vMax, Material mat)
        {
            var go = new GameObject(stripName);
            go.transform.SetParent(transform, false);

            float innerX = side * innerEdge;
            float outerX = side * outerEdge;
            float zMin = -zBehind;
            float zMax = zAhead;

            var mesh = new Mesh { name = stripName };
            mesh.vertices = new[]
            {
                new Vector3(innerX, yLevel, zMin),
                new Vector3(innerX, yLevel, zMax),
                new Vector3(outerX, yLevel, zMax),
                new Vector3(outerX, yLevel, zMin),
            };
            // UV.x: 0 at the road (inner) side, 1 at the outer side. UV.y tiles along Z for the scroll.
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
            mr.sharedMaterial = mat; // shared; the material is double-sided so winding never hides a strip
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true; // it is ground, let the warm light read on it
            return mr;
        }

        /// <summary>A lit, alpha-clipped sand material. Prefers the custom KenyaScooter/RoadSand shader (in
        /// Shaders/Resources so builds keep it): same cutout, but the clip edge dissolves into individual grains,
        /// so the sand MIXES into the asphalt instead of ending in a line. Falls back to plain URP/Lit cutout,
        /// which still works (the density-gradient mask alone already softens the edge). Double-sided as a safety
        /// net for the quad winding.</summary>
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

            // Alpha cutout: opaque sand with a clipped edge (the texture's alpha is the shoulder DENSITY mask).
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

        /// <summary>Procedural warm sand grain with a DENSITY mask (not a binary in/out): alpha ramps from 0 to
        /// full over a speckled transition band at both edges, tongues of sand reach in toward the road at the
        /// inner edge (UV.x small) and loose freckles land past it on the tarmac itself, thinning toward the road
        /// centre. The RoadSand shader turns that density into a per-grain dissolve, so the boundary reads as sand
        /// MIXING into the asphalt instead of a wavering line. Grayscale-warm grain, tinted red by the material's
        /// BaseColor, so the redness is one dial. Tiles along V for the scroll.</summary>
        private static Texture2D BuildSandTexture()
        {
            const int w = 128;  // across the strip (UV.x: 0 road side .. 1 outer)
            const int h = 192;  // along the road (UV.y), tiled
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "RoadEdgeSand (runtime)",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                // Ragged inner edge (toward the road): a wavering threshold, with occasional deep tongues that
                // reach right onto the asphalt. Lower threshold = sand reaches further toward the road centre.
                float inner = Mathf.Lerp(0.03f, 0.24f, Mathf.PerlinNoise(v * 6f, 0.37f));
                if (Mathf.PerlinNoise(v * 2.3f, 4.1f) > 0.74f)
                    inner = 0f; // a tongue of sand spilling onto the road
                // Ragged outer edge: frays into the open ground so there is no hard line on the savanna side.
                float outer = Mathf.Lerp(0.80f, 1.0f, Mathf.PerlinNoise(v * 5f, 9.2f));
                // Width of the density ramps. The inner band wavers so the mix zone itself varies along the road.
                float innerBand = Mathf.Lerp(0.10f, 0.26f, Mathf.PerlinNoise(v * 7.7f, 2.2f));
                const float outerBand = 0.12f;

                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;

                    // Density: 0 on the asphalt, ramping to 1 inside the strip, ramping out again at the far side.
                    float tIn = Mathf.Clamp01((u - inner) / innerBand);
                    float tOut = Mathf.Clamp01((outer - u) / outerBand);
                    float a01 = Mathf.Min(tIn, tOut);

                    if (a01 > 0f && a01 < 1f)
                    {
                        // Speckle the ramps: with the shader's grain dissolve this breaks the boundary into
                        // scattered sand instead of a soft-but-straight gradient.
                        float clump = Mathf.PerlinNoise(u * 46f, v * 120f);
                        a01 = Mathf.Clamp01(a01 * Mathf.Lerp(0.55f, 1.45f, clump));
                    }
                    else if (a01 <= 0f && u < inner)
                    {
                        // Loose freckles past the inner edge: stray sand sitting ON the tarmac, denser near the
                        // shoulder and thinning toward the road centre so it never buries the lane.
                        float toward = u / Mathf.Max(inner, 0.001f); // 0 = road centre .. 1 = at the sand edge
                        float freckle = Mathf.PerlinNoise(u * 34f + 7f, v * 88f + 3f);
                        if (freckle > Mathf.Lerp(0.88f, 0.70f, toward))
                            a01 = 0.3f + 0.45f * toward;
                    }

                    byte a = (byte)(Mathf.Clamp01(a01) * 255f);

                    // Warm grain: a broad lightness variation plus a finer speckle, kept grayscale-warm so the
                    // material's red BaseColor sets the actual sand colour.
                    float grain = Mathf.PerlinNoise(u * 9f, v * 22f);
                    float speck = Mathf.PerlinNoise(u * 40f, v * 90f);
                    float light = Mathf.Clamp01(Mathf.Lerp(0.78f, 1.12f, grain) * Mathf.Lerp(0.94f, 1.06f, speck));
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
