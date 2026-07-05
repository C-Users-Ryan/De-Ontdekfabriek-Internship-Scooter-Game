using UnityEngine;
using UnityEngine.Rendering;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// Gives EVERY traffic vehicle "lights on" with zero prefab work. The car prefabs carry no wired light meshes
    /// (TrafficVehicleLights' slots are empty), so instead of hand-authoring lamps on each prefab this builds a
    /// warm headlight bar on the nose and a red tail bar on the back, sized from the vehicle's own
    /// <see cref="TrafficVehicle.length"/>/<see cref="TrafficVehicle.width"/>. They are EMISSIVE glow quads on a
    /// shared unlit material (GPU-instanced) — NOT real lights — so any number of cars stays cheap, keeping the
    /// scene at two real lights (the sun/moon and the scooter spot). The lights are ON day and night; the tail bar
    /// brightens when the car brakes/crawls (a readable "the car ahead is slowing" cue).
    ///
    /// A tiny self-bootstrapping <see cref="Provisioner"/> attaches this to each pooled vehicle as it appears, so
    /// there is nothing to wire in the scene or on the prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrafficVehicleAutoLights : MonoBehaviour
    {
        // ---- Tunables (shared defaults; simple enough not to need per-car authoring) ----------------------
        private const float HeadHeight = 0.55f;       // lamp height up the nose/tail
        private const float TailHeight = 0.6f;
        private const float BrakeDecel = 1.5f;         // m/s² slowing that counts as braking
        private const float BrakeCrawlSpeed = 0.5f;    // below this speed the car reads as braking/stopped
        private static readonly Color HeadColour = new Color(1f, 0.94f, 0.78f);
        private static readonly Color TailColour = new Color(1f, 0.10f, 0.04f);
        private static readonly Color BrakeColour = new Color(1f, 0.16f, 0.10f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Mesh quadMesh;
        private static Material glowMat;

        private TrafficVehicle vehicle;
        private Renderer head, tail;
        private MaterialPropertyBlock mpb;
        private float prevSpeed;
        private float lastHead = -1f, lastTail = -1f;

        private bool built;

        // Build lazily on the first Update, NOT in Awake. The component is added at runtime by the Provisioner and
        // relying on Awake left mpb/bars uninitialised on some pooled cars — GetPropertyBlock(null) then threw every
        // frame and aborted the whole light update, so no car showed any lights. This guarantees setup before use.
        private void EnsureBuilt()
        {
            built = true;
            vehicle = GetComponent<TrafficVehicle>();
            if (mpb == null)
                mpb = new MaterialPropertyBlock();

            float halfLen = (vehicle != null ? vehicle.length : 4.5f) * 0.5f;
            float w = (vehicle != null ? vehicle.width : 1.9f);

            // A wide, short glow bar reads as "lights on" without pretending to be two separate lamps.
            head = BuildBar("HeadlightGlow", new Vector3(0f, HeadHeight, halfLen + 0.03f), faceBack: false, new Vector2(w * 0.72f, 0.34f));
            tail = BuildBar("TaillightGlow", new Vector3(0f, TailHeight, -(halfLen + 0.03f)), faceBack: true, new Vector2(w * 0.72f, 0.30f));
            prevSpeed = vehicle != null ? vehicle.CurrentSpeed : 0f;
        }

        private void Update()
        {
            if (!built)
                EnsureBuilt();

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            // Every car runs its lights ON, day and night (per request). Kept at full; the tail still flares on braking.
            const float on = 1f;

            // Headlights: steady warm glow.
            if (!Mathf.Approximately(on, lastHead))
            {
                lastHead = on;
                Apply(head, HeadColour * on);
            }

            // Tail: dim red normally, bright red when braking or crawling.
            float speed = vehicle != null ? vehicle.CurrentSpeed : 0f;
            float decel = (prevSpeed - speed) / dt;
            prevSpeed = speed;
            bool braking = decel > BrakeDecel || speed < BrakeCrawlSpeed;
            float tailLevel = braking ? 1f : 0.45f;
            if (!Mathf.Approximately(tailLevel, lastTail))
            {
                lastTail = tailLevel;
                Apply(tail, (braking ? BrakeColour : TailColour) * tailLevel);
            }
        }

        // ---- Build helpers -------------------------------------------------------------------------------

        private Renderer BuildBar(string name, Vector3 localPos, bool faceBack, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = faceBack ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GlowMaterial();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return mr;
        }

        private void Apply(Renderer r, Color colour)
        {
            if (r == null)
                return;
            if (mpb == null)
                mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, colour);      // Sprites/Default
            mpb.SetColor(BaseColorId, colour);  // URP unlit, in case the shader fell back
            r.SetPropertyBlock(mpb);
            bool lit = colour.maxColorComponent > 0.003f;
            if (r.enabled != lit)
                r.enabled = lit; // fully off by day = not drawn at all
        }

        private static Mesh QuadMesh()
        {
            if (quadMesh != null)
                return quadMesh;
            quadMesh = new Mesh { name = "AutoLightQuad" };
            quadMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            quadMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; // faces +Z
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
            glowMat = new Material(shader) { name = "TrafficAutoLightGlow", mainTexture = SoftDot() };
            glowMat.enableInstancing = true; // same mesh + material + per-instance colour -> batched, cheap at any car count
            return glowMat;
        }

        /// <summary>A soft round falloff so the bar reads as a glow, not a hard rectangle.</summary>
        private static Texture2D SoftDot()
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "AutoLightDot" };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f;
                float dy = (y + 0.5f) / s * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a; // softer core-to-edge falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return tex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { quadMesh = null; glowMat = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<Provisioner>() != null)
                return;
            new GameObject("TrafficAutoLights (auto)").AddComponent<Provisioner>();
        }

        /// <summary>Ensures every pooled traffic vehicle carries the glow lights, checked cheaply off the existing
        /// <see cref="TrafficVehicle.Active"/> registry (no scene scans) on a slow cadence — a car only needs the
        /// component added once, then it rides along through pooling.</summary>
        private sealed class Provisioner : MonoBehaviour
        {
            private float accum;

            private void Update()
            {
                accum += Time.deltaTime;
                if (accum < 0.25f)
                    return;
                accum = 0f;

                var active = TrafficVehicle.Active;
                for (int i = 0; i < active.Count; i++)
                {
                    TrafficVehicle v = active[i];
                    if (v != null && v.GetComponent<TrafficVehicleAutoLights>() == null)
                        v.gameObject.AddComponent<TrafficVehicleAutoLights>();
                }
            }
        }
    }
}
