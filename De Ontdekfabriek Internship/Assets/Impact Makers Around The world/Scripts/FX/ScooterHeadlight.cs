using UnityEngine;
using UnityEngine.Rendering;
using KenyaScooter.Core;
using KenyaScooter.Session;

namespace KenyaScooter.FX
{
    /// <summary>
    /// The scooter's headlight — the ONE real-time light in the scene besides the sun/moon. A single spot with
    /// NO shadows, plus a lamp glow and an additive translucent beam, all fading in with the night.
    ///
    /// PLACEABLE: drop this on an empty GameObject, parent it under the bike at the headlamp and point it forward;
    /// the whole light (spot + glow + beam) builds as children of THAT object, so it sits where you place it and
    /// moves with the bike. Left unplaced, it self-bootstraps and mounts itself on the player.
    ///
    /// The spot is aimed and ranged to light the SAME stretch the beam covers, so anything the beam falls on is
    /// really lit. The beam is ADDITIVE, so it brightens the road it covers (enhances sight) instead of fogging it.
    /// Everything but the spot is a cheap mesh, so the scene stays at two real lights whatever the traffic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScooterHeadlight : MonoBehaviour
    {
        [Header("Spot (the real light — reaches as far as the beam)")]
        [SerializeField] private float maxIntensity = 7f;
        [Tooltip("How far the real light reaches. Keep long so the far road the beam covers is actually lit.")]
        [SerializeField] private float range = 150f;
        [SerializeField] private float spotAngle = 64f;
        [SerializeField] private Color colour = new Color(1f, 0.95f, 0.82f);
        [Tooltip("Aim: metres ahead the spot points, and metres it drops below the lamp. Lower drop / bigger ahead " +
                 "= flatter throw that reaches further down the road (to match the beam).")]
        [SerializeField] private float aimAhead = 8f;
        [SerializeField] private float aimDrop = 1.5f;

        [Header("Auto-mount (only when self-bootstrapped, not when you place it)")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.72f, 1.35f);

        [Header("Visible lamp glow")]
        [SerializeField] private float glowSize = 0.28f;
        [SerializeField] private float glowStrength = 1.3f;

        [Header("Visible light beam (additive, enhances sight)")]
        [Tooltip("Beam colour — tint the shaft independently of the real light.")]
        [SerializeField] private Color beamColour = new Color(1f, 0.95f, 0.8f);
        [Tooltip("Length of the visible beam shaft (metres); it fades out over this distance.")]
        [SerializeField] private float beamLength = 12f;
        [Tooltip("The 'hole' at the headlight — beam radius right at the lamp (metres).")]
        [SerializeField] private float beamStartRadius = 0.16f;
        [Tooltip("Beam radius at the far end (metres) — how WIDE the shaft spreads.")]
        [SerializeField] private float beamEndRadius = 2.6f;
        [Tooltip("Base opacity of the beam at the lamp (before intensity).")]
        [SerializeField, Range(0f, 1f)] private float beamAlpha = 0.22f;
        [Tooltip("Brightness multiplier on the additive beam — push above 1 for a stronger shaft.")]
        [SerializeField] private float beamIntensity = 1.2f;
        [Tooltip("How quickly the beam fades along its length: 1 = linear, higher = fades sooner, lower = reaches further.")]
        [SerializeField] private float beamFadePower = 1.4f;
        [Tooltip("Downward pitch of the beam shaft (degrees) — SHALLOW so it skims the road and fades before the horizon.")]
        [SerializeField] private float beamPitchDeg = 6f;

        [Header("Fade")]
        [SerializeField] private float fadeSpeed = 2.2f;
        [Tooltip("NightFactor below which the light stays fully off, so it never shows by day.")]
        [SerializeField, Range(0f, 1f)] private float onThreshold = 0.18f;

        /// <summary>Set by the self-bootstrap so the auto instance mounts itself on the player.</summary>
        [System.NonSerialized] public bool autoMode;

        private Light spot;
        private Renderer glow, beam;
        private MaterialPropertyBlock mpb;
        private Transform player;
        private bool built;
        private float current;
        private float lastGlow = -1f, lastBeam = -1f;

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
            if (beam != null && !Mathf.Approximately(current, lastBeam))
            {
                lastBeam = current;
                // Additive: rgb (× intensity) is the added light; alpha gates it by the night fade + base opacity.
                float k = beamIntensity;
                var c = new Color(beamColour.r * k, beamColour.g * k, beamColour.b * k, current * beamAlpha);
                Tint(beam, c, lit);
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
            rig.localRotation = Quaternion.LookRotation(new Vector3(0f, -aimDrop, aimAhead).normalized, Vector3.up);

            spot = rig.gameObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.shadows = LightShadows.None;
            spot.renderMode = LightRenderMode.ForcePixel;
            spot.range = range;
            spot.spotAngle = spotAngle;
            spot.innerSpotAngle = spotAngle * 0.5f;
            spot.color = colour;
            spot.intensity = 0f;
            spot.enabled = false;

            glow = BuildGlow(rig);
            beam = BuildBeam(rig);
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

        private Renderer BuildBeam(Transform parent)
        {
            var go = new GameObject("Light Beam");
            go.transform.SetParent(parent, false);
            float rigPitch = Mathf.Atan2(aimDrop, aimAhead) * Mathf.Rad2Deg;
            go.transform.localRotation = Quaternion.Euler(beamPitchDeg - rigPitch, 0f, 0f); // own shallow pitch
            go.AddComponent<MeshFilter>().sharedMesh = BuildBeamMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MakeBeamMaterial(); // per-instance so beamFadePower can shape its own gradient
            StripProbes(mr);
            mr.enabled = false;
            return mr;
        }

        private Mesh BuildBeamMesh()
        {
            const int seg = 22;
            var mesh = new Mesh { name = "HeadlightBeam" };
            var verts = new Vector3[(seg + 1) * 2];
            var uvs = new Vector2[(seg + 1) * 2];
            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                int s = i * 2;
                verts[s] = new Vector3(cx * beamStartRadius, cy * beamStartRadius, 0f);
                verts[s + 1] = new Vector3(cx * beamEndRadius, cy * beamEndRadius, beamLength);
                uvs[s] = new Vector2((float)i / seg, 0f);   // v = 0 at the lamp
                uvs[s + 1] = new Vector2((float)i / seg, 1f); // v = 1 at the far end (gradient texture fades it)
            }
            var tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                int s = i * 2, t = i * 6;
                tris[t] = s; tris[t + 1] = s + 1; tris[t + 2] = s + 3;
                tris[t + 3] = s; tris[t + 4] = s + 3; tris[t + 5] = s + 2;
            }
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
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

        /// <summary>ADDITIVE translucent material for the beam (adds warm light = enhances sight). Per-instance so
        /// beamFadePower shapes its own length gradient. URP/Unlit set additive-transparent; alpha-sprite fallback.</summary>
        private Material MakeBeamMaterial()
        {
            Texture2D grad = BeamGradient(beamFadePower);
            Shader urp = Shader.Find("Universal Render Pipeline/Unlit");
            if (urp != null)
            {
                var m = new Material(urp) { name = "HeadlightBeam", mainTexture = grad };
                m.SetFloat("_Surface", 1f);   // transparent
                m.SetFloat("_Blend", 2f);     // additive
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                return m;
            }
            return new Material(Shader.Find("Sprites/Default")) { name = "HeadlightBeam", mainTexture = grad };
        }

        private static Texture2D BeamGradient(float power)
        {
            const int h = 64;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BeamGrad" };
            float p = Mathf.Max(0.1f, power);
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                float alpha = Mathf.Pow(1f - v, p);
                for (int x = 0; x < 2; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
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
