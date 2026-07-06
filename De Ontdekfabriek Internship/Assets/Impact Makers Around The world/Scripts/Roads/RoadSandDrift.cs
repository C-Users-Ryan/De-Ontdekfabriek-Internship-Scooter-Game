using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KenyaScooter.Core;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// Drifts of red sand blown ACROSS the tarmac (Oplevering 25 Jun 2026: "a lot of sand ON and beside the
    /// road"). Where <see cref="RoadEdgeSand"/> handles the shoulders, this scatters soft, low-opacity sand
    /// patches over the road surface itself, biased toward the edges but with the odd tongue reaching across a
    /// lane, so the road reads as a dry, sand-strewn Kenyan highway rather than clean asphalt.
    ///
    /// They are a thin DUSTING, not solid patches: soft-edged, semi-transparent and warm, so they never read as
    /// a hazard or hide what is on the road. Pooled flat quads lying just over the tarmac; they scroll toward the
    /// stationary player in lockstep with the ground (by the delta of WorldSpeed.DistanceTravelled, exactly like
    /// ScrollingGround / RoadEdgeSand, so they only move while driving) and recycle to the far end with a fresh
    /// size, angle and position.
    ///
    /// Self-activates after scene load, additive and reversible (<see cref="driftEnabled"/>), no collider, never
    /// touches gameplay. Soft-sprite material so it can never render magenta under URP.
    /// </summary>
    public sealed class RoadSandDrift : MonoBehaviour
    {
        [Header("Master")]
        // Off by default since 2026-07-06: the road itself is now murram (sandy) and the dust is meant to sit only
        // OUTSIDE the road (RoadEdgeSand's bed), so on-road drift patches are redundant. Flip on to dust the road too.
        [SerializeField] private bool driftEnabled = false;

        [Header("Scatter")]
        [Tooltip("How many sand drifts are pooled and on the road at once. Sparse, so it dusts the road, not paves it.")]
        [SerializeField] private int count = 14;
        [Tooltip("Half-width of the scatter across the road (metres). Around the lane + shoulder so most sand hugs the edges.")]
        [SerializeField] private float acrossHalfWidth = 4.6f;
        [Tooltip("Nearest / farthest distance ahead a drift is seeded (metres). Covers the visible road.")]
        [SerializeField] private Vector2 zRange = new Vector2(-25f, 200f);
        [Tooltip("Recycle a drift once it has scrolled this far behind the player (metres).")]
        [SerializeField] private float recycleBehind = 30f;
        [Tooltip("Height above the road surface (road sits at y = 0). A touch lower than the shoulder sand.")]
        [SerializeField] private float yLevel = 0.015f;

        [Header("Look")]
        [Tooltip("Red-laterite sand colour of the drifts. Alpha here is the PEAK opacity; each patch jitters under it.")]
        [SerializeField] private Color sandColour = new Color(0.66f, 0.40f, 0.26f, 0.45f);
        [Tooltip("Patch size range across (metres).")]
        [SerializeField] private Vector2 sizeRange = new Vector2(0.8f, 2.6f);
        [Tooltip("How much longer a patch is along the road than across (wind-streaked).")]
        [SerializeField] private Vector2 stretchRange = new Vector2(1.1f, 2.4f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<RoadSandDrift>() != null)
                return;
            var go = new GameObject("RoadSandDrift (auto)");
            go.AddComponent<RoadSandDrift>();
        }

        private sealed class Drift
        {
            public Transform tr;
            public MeshRenderer renderer;
            public float worldZ;
            public float worldX;
            public float yaw;
            public Vector3 scale;
            public float alpha;
        }

        private readonly List<Drift> drifts = new List<Drift>(24);
        private Mesh quad;
        private Material material;
        private MaterialPropertyBlock mpb;
        private float lastDistance;
        private bool built;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            if (!driftEnabled)
                return;
            Build();
        }

        private void Build()
        {
            if (built)
                return;
            quad = BuildQuad();
            material = BuildMaterial(BuildSandSplat());
            mpb = new MaterialPropertyBlock();
            lastDistance = WorldSpeed.Instance != null ? WorldSpeed.Instance.DistanceTravelled : 0f;

            for (int i = 0; i < Mathf.Max(0, count); i++)
            {
                var go = new GameObject("SandDrift");
                go.transform.SetParent(transform, false);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

                var d = new Drift { tr = go.transform, renderer = mr };
                // Spread the initial set across the whole visible length so the road is dusted immediately.
                float z = Mathf.Lerp(zRange.x, zRange.y, i / Mathf.Max(1f, count - 1f));
                Seed(d, z);
                drifts.Add(d);
            }
            built = true;
        }

        private void Update()
        {
            if (!built)
                return;

            // Move with the ground: the delta of DistanceTravelled only advances while driving, so the drifts
            // scroll in lockstep with ScrollingGround / RoadEdgeSand and hold still on the menu / hand-off.
            float dist = WorldSpeed.Instance != null ? WorldSpeed.Instance.DistanceTravelled : lastDistance;
            float delta = dist - lastDistance;
            lastDistance = dist;

            for (int i = 0; i < drifts.Count; i++)
            {
                Drift d = drifts[i];
                d.worldZ -= delta;
                if (d.worldZ < -recycleBehind)
                    Seed(d, Random.Range(zRange.y * 0.8f, zRange.y));

                d.tr.SetPositionAndRotation(new Vector3(d.worldX, yLevel, d.worldZ), Quaternion.Euler(90f, d.yaw, 0f));
                d.tr.localScale = d.scale;
            }
        }

        /// <summary>(Re)places a drift: edge-biased across the road, random size, road-aligned stretch, random yaw
        /// and a per-patch opacity, then pushes its warm tint via the property block.</summary>
        private void Seed(Drift d, float z)
        {
            d.worldZ = z;

            // Edge-biased lateral: more sand hugs the shoulders, some tongues reach across a lane.
            float side = Random.value < 0.5f ? -1f : 1f;
            float across = Mathf.Pow(Random.value, 0.65f); // skew toward the outer edge
            d.worldX = side * across * acrossHalfWidth;

            float size = Random.Range(sizeRange.x, sizeRange.y);
            float stretch = Random.Range(stretchRange.x, stretchRange.y);
            // Quad is rotated flat (X +90), so its local Y maps to world Z: stretch along the road on local Y.
            d.scale = new Vector3(size, size * stretch, 1f);
            d.yaw = Random.Range(-18f, 18f); // mostly road-aligned, slight wander
            d.alpha = sandColour.a * Random.Range(0.6f, 1f);

            Color c = sandColour;
            c.a = d.alpha;
            d.renderer.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            mpb.SetColor(BaseColorId, c);
            d.renderer.SetPropertyBlock(mpb);
        }

        /// <summary>A flat, centred quad in the XZ plane (rotated +90 on X at use so it lies on the road and its
        /// local Y runs along the road for the stretch).</summary>
        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "SandDriftQuad" };
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

        /// <summary>Sprites/Default: alpha-blended, unlit, double-sided, respects the per-patch _Color from the
        /// property block, ships with every pipeline and never resolves to the magenta URP error shader.</summary>
        private static Material BuildMaterial(Texture2D tex)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            var m = new Material(shader) { name = "RoadSandDrift (runtime)" };
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// <summary>A soft, irregular sand splat: a blobby radial falloff (so edges are soft, not a disc) with warm
        /// grain inside. White-warm RGB so the per-patch red tint sets the colour; alpha is the soft blob.</summary>
        private static Texture2D BuildSandSplat()
        {
            const int s = 64;
            const float half = s * 0.5f;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "SandSplat (runtime)", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy); // 0 centre .. ~1.4 corner
                    // Wobble the radius so the blob is irregular, not a clean disc.
                    float wobble = (Mathf.PerlinNoise(x * 0.12f, y * 0.12f) - 0.5f) * 0.5f;
                    float edge = Mathf.Clamp01(1f - (r + wobble));
                    float a = edge * edge * (3f - 2f * edge); // smoothstep soft edge

                    float grain = Mathf.Lerp(0.82f, 1.1f, Mathf.PerlinNoise(x * 0.35f, y * 0.35f));
                    byte rr = (byte)(Mathf.Clamp01(grain) * 255f);
                    byte gg = (byte)(Mathf.Clamp01(grain * 0.96f) * 255f);
                    byte bb = (byte)(Mathf.Clamp01(grain * 0.9f) * 255f);
                    px[y * s + x] = new Color32(rr, gg, bb, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
