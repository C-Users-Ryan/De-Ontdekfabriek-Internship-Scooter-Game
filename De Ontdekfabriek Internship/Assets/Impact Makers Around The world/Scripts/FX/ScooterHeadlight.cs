using UnityEngine;
using UnityEngine.Rendering;
using KenyaScooter.Core;
using KenyaScooter.Session;

namespace KenyaScooter.FX
{
    /// <summary>
    /// The scooter's headlight — the ONE real-time light in the scene besides the sun/moon. A single spot with
    /// NO shadows, plus a lamp glow and an additive light-pool on the road, all fading in with the night.
    ///
    /// PLACEABLE: drop this on an empty GameObject, parent it under the bike at the headlamp and point it forward;
    /// the whole light (spot + glow + pool) builds as children of THAT object, so it sits where you place it and
    /// moves with the bike. Left unplaced, it self-bootstraps and mounts itself on the player.
    ///
    /// The spot does the real lighting; a warm ADDITIVE light-POOL laid flat on the road ahead shows the headlight's
    /// throw where a real one would fall — on the tarmac, not floating in the air — so it reads as "the road ahead is
    /// lit" instead of a confusing forward shaft. Everything but the spot is a cheap mesh, so the scene stays at two
    /// real lights whatever the traffic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScooterHeadlight : MonoBehaviour
    {
        [Header("Spot (the real light — reaches as far as the pool)")]
        [SerializeField] private float maxIntensity = 13f;
        [Tooltip("How far the real light reaches (metres) — how far ahead things are actually lit. Keep long so the far road the pool covers is really lit.")]
        [SerializeField] private float range = 260f;
        [SerializeField] private float spotAngle = 72f;
        [SerializeField] private Color colour = new Color(1f, 0.95f, 0.82f);
        [Tooltip("Pitch of the spot beam in degrees. POSITIVE aims UP (lifts the throw onto the road/cars further " +
                 "ahead); negative aims DOWN toward the tarmac right in front. ~9 lights well ahead while the wide " +
                 "cone still covers the near road, and the flat road light-pool below stays put.")]
        [SerializeField] private float aimPitchDegrees = 9f;

        [Header("Auto-mount (only when self-bootstrapped, not when you place it)")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.72f, 1.35f);

        [Header("Visible lamp glow")]
        [SerializeField] private float glowSize = 0.34f;
        [SerializeField] private float glowStrength = 1f;

        [Header("Visible light pool on the road (additive — the headlight's throw)")]
        [Tooltip("Pool colour — tint the light on the road independently of the real spot.")]
        [SerializeField] private Color poolColour = new Color(1f, 0.94f, 0.78f);
        [Tooltip("Where the pool starts / ends ahead of the bike (metres).")]
        [SerializeField] private float poolNear = 2f;
        [SerializeField] private float poolFar = 32f;
        [Tooltip("Pool width at the near / far end (metres) — fans out like a real headlight throw.")]
        [SerializeField] private float poolNearWidth = 1.6f;
        [SerializeField] private float poolFarWidth = 9f;
        [Tooltip("Metres to drop the pool below the lamp so it lies ON the road. Raise if it floats, lower if it sinks.")]
        [SerializeField] private float poolDrop = 0.8f;
        [Tooltip("Base opacity of the pool (before the night fade). Alpha-blended, so this is the on-road glow strength.")]
        [SerializeField, Range(0f, 1f)] private float poolAlpha = 0.6f;
        [Tooltip("Brightness multiplier on the additive pool — push above 1 for a stronger glow.")]
        [SerializeField] private float poolIntensity = 1.3f;

        [Header("Fade")]
        [SerializeField] private float fadeSpeed = 2.2f;
        [Tooltip("NightFactor below which the light stays fully off, so it never shows by day.")]
        [SerializeField, Range(0f, 1f)] private float onThreshold = 0.18f;

        /// <summary>Set by the self-bootstrap so the auto instance mounts itself on the player.</summary>
        [System.NonSerialized] public bool autoMode;

        private Light spot;
        private Renderer glow, pool;
        private MaterialPropertyBlock mpb;
        private Transform player;
        private bool built;
        private float current;
        private float lastGlow = -1f, lastPool = -1f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Mesh quadMesh;
        private static Material glowMat;

        private static readonly string[] LampNames = { "koplamp", "headlight", "headlamp", "front light", "frontlight" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { quadMesh = null; glowMat = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<ScooterHeadlight>() != null)
                return; // a hand-placed one wins
            new GameObject("ScooterHeadlight (auto)").AddComponent<ScooterHeadlight>().autoMode = true;
        }

        private void Awake() => mpb = new MaterialPropertyBlock();

        private void Update()
        {
            if (!built)
            {
                if (autoMode)
                {
                    if (player == null)
                    {
                        player = GameManager.Player;
                        if (player == null)
                            return;
                    }
                    Transform anchor = FindHeadlamp(player);
                    transform.SetParent(anchor != null ? anchor : player, false);
                    transform.localPosition = anchor != null ? Vector3.zero : localOffset;
                    transform.localRotation = Quaternion.identity;
                }
                BuildRig(transform);
                built = true;
            }

            float night = DayCycleManager.NightFactor01;
            float target = night <= onThreshold ? 0f : Mathf.InverseLerp(onThreshold, 1f, night);
            current = Mathf.MoveTowards(current, target, fadeSpeed * Time.deltaTime);
            bool lit = current > 0.001f;

            if (spot != null)
            {
                if (spot.enabled != lit)
                    spot.enabled = lit;
                if (lit)
                    spot.intensity = current * maxIntensity;
            }
            if (glow != null && !Mathf.Approximately(current, lastGlow))
            {
                lastGlow = current;
                Tint(glow, colour * (current * glowStrength), lit);
            }
            if (pool != null && !Mathf.Approximately(current, lastPool))
            {
                lastPool = current;
                // Alpha-blended (Sprites/Default): rgb is the warm pool colour, alpha is the strength gated by the
                // night fade — kept fairly opaque so it reads as a clearly lit patch of road at night.
                var c = new Color(poolColour.r, poolColour.g, poolColour.b, Mathf.Clamp01(current * poolAlpha * poolIntensity));
                Tint(pool, c, lit);
            }
        }

        private void Tint(Renderer r, Color c, bool lit)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(mpb);
            if (r.enabled != lit)
                r.enabled = lit;
        }

        private void BuildRig(Transform mount)
        {
            var rig = new GameObject("Headlight Rig").transform;
            rig.SetParent(mount, false);
            rig.localPosition = Vector3.zero;
            rig.localRotation = Quaternion.Euler(-aimPitchDegrees, 0f, 0f); // negative X-euler pitches the forward beam UP

            spot = rig.gameObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.shadows = LightShadows.None;
            spot.renderMode = LightRenderMode.ForcePixel;
            spot.range = range;
            spot.spotAngle = spotAngle;
            spot.innerSpotAngle = spotAngle * 0.3f; // small full-bright core, long falloff = strong centre, soft edges
            spot.color = colour;
            spot.intensity = 0f;
            spot.enabled = false;

            glow = BuildGlow(rig);
            pool = BuildPool(mount); // on the LEVEL mount, not the tilted rig, so it lies flat on the road
        }

        private static Transform FindHeadlamp(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                for (int i = 0; i < LampNames.Length; i++)
                    if (n.Contains(LampNames[i]))
                        return t;
            }
            return null;
        }

        private Renderer BuildGlow(Transform parent)
        {
            var go = new GameObject("Lamp Glow");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * glowSize;
            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GlowMaterial();
            StripProbes(mr);
            mr.enabled = false;
            return mr;
        }

        private Renderer BuildPool(Transform parent)
        {
            var go = new GameObject("Light Pool");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -poolDrop, 0f); // dropped to road level
            go.transform.localRotation = Quaternion.identity;            // lies flat, facing straight up
            go.AddComponent<MeshFilter>().sharedMesh = BuildPoolMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MakePoolMaterial(); // per-instance so the gradient is its own
            StripProbes(mr);
            mr.enabled = false;
            return mr;
        }

        // A flat trapezoid on the ground (XZ plane): narrow at the bike, fanning out down the road ahead.
        private Mesh BuildPoolMesh()
        {
            float hn = poolNearWidth * 0.5f, hf = poolFarWidth * 0.5f;
            var mesh = new Mesh { name = "HeadlightPool" };
            mesh.vertices = new[]
            {
                new Vector3(-hn, 0f, poolNear), // 0 near-left
                new Vector3( hn, 0f, poolNear), // 1 near-right
                new Vector3( hf, 0f, poolFar),  // 2 far-right
                new Vector3(-hf, 0f, poolFar),  // 3 far-left
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), // v = 0 at the bike
                new Vector2(1f, 1f), new Vector2(0f, 1f), // v = 1 far down the road
            };
            // White vertex colours: Sprites/Default multiplies the texture by the vertex colour, and an ABSENT
            // colour channel can read as black on mobile GPUs (→ an invisible pool). QuadMesh does the same.
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; // material is double-sided, so winding is cosmetic
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void StripProbes(Renderer mr)
        {
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Mesh QuadMesh()
        {
            if (quadMesh != null)
                return quadMesh;
            quadMesh = new Mesh { name = "HeadlampGlowQuad" };
            quadMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            quadMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            quadMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quadMesh.RecalculateBounds();
            return quadMesh;
        }

        private static Material GlowMaterial()
        {
            if (glowMat != null)
                return glowMat;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            glowMat = new Material(shader) { name = "HeadlampGlow", mainTexture = SoftDot() };
            return glowMat;
        }

        /// <summary>Translucent material for the road light-pool (the visible headlight throw). Uses the built-in
        /// "Sprites/Default" shader — which is in the project's Always-Included-Shaders list and needs NO
        /// runtime-set keywords — so it renders RELIABLY in an Android/URP build. The previous version reconfigured
        /// "Universal Render Pipeline/Unlit" into a transparent-additive material at runtime, but URP strips that
        /// unused transparent/additive VARIANT from a mobile build, which left the pool invisible on the tablet
        /// while it showed fine in the Editor (the reported "no light beam on the tablet"). Sprites/Default is
        /// alpha-blended, so the pool is boosted in Update() to read as a strong warm glow on the dark night road.</summary>
        private Material MakePoolMaterial()
        {
            Texture2D grad = PoolGradient();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit"); // last-ditch; Sprites/Default is Always-Included
            return new Material(shader) { name = "HeadlightPool", mainTexture = grad };
        }

        // 2-D soft-edged gradient: bright down the centre, fading at the sides (u) and fading in near / out far (v),
        // so the pool has no hard rectangle edge — it reads as a soft glow the headlight casts on the tarmac.
        private static Texture2D PoolGradient()
        {
            const int w = 48, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "PoolGrad" };
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                // Builds fast to a HIGH peak just ahead of the bike, then fades slowly to the far end. THE old
                // curve was SmoothStep(0f, 0.28f, v) — but Unity's Mathf.SmoothStep(from, to, t) is a smoothed
                // LERP (result ranges from..to), NOT GLSL smoothstep(edge0, edge1, x), so it returned at most
                // 0.28: the whole pool texture peaked around ~0.13 alpha. THAT is why the player's light barely
                // read at dusk. Here t = v/0.16 (SmoothStep clamps it), so the curve genuinely ramps 0→1 over
                // the first 16% and then fades (1-v)^0.9 — peak ≈ 0.86. Alpha-blended glows have no HDR
                // headroom, so the texture must carry the brightness.
                float lengthA = Mathf.SmoothStep(0f, 1f, v / 0.16f) * Mathf.Pow(1f - v, 0.9f);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    // Max() guards the edges: float Sin(π) dips fractionally NEGATIVE, and Pow(neg, 1.4) is NaN.
                    float widthA = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)), 1.4f); // wide bright core, soft edges
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, lengthA * widthA));
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D SoftDot()
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "HeadlampDot" };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f;
                float dy = (y + 0.5f) / s * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();
            return tex;
        }
    }
}
