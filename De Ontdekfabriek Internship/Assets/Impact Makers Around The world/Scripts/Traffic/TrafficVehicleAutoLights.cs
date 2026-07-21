using UnityEngine;
using UnityEngine.Rendering;
using KenyaScooter.Session;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// Gives EVERY traffic vehicle "lights on" with zero prefab work. The car prefabs carry no wired light meshes
    /// (TrafficVehicleLights' slots are empty), so instead of hand-authoring lamps on each prefab this builds a
    /// warm headlight bar on the nose and two red tail lights on the rear corners, sized from the vehicle's own
    /// <see cref="TrafficVehicle.length"/>/<see cref="TrafficVehicle.width"/>, PLUS a visible additive headlight
    /// BEAM from the nose that fades in at night (so oncoming cars beam their headlights toward the player). They
    /// are EMISSIVE glows / translucent meshes on shared materials — NOT real lights — so any number of cars stays
    /// cheap, keeping the scene at two real lights (the sun/moon and the scooter spot). The glows are ON day and
    /// night; the two tail lights brighten at night so the back reads in the dark, and flare on braking; the beam
    /// is night-only (a shaft in daylight would look wrong).
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
        private const float TailEdgeFactor = 0.34f;    // how far toward the car's L/R edges the two tail lights sit (matches the beams)
        private const float TailDayLevel = 0.5f;       // subtle red by day
        private const float TailNightLevel = 1.6f;     // bright HDR red at night so the back reads clearly in the dark
        private const float BrakeDayLevel = 1.2f;      // brake flare by day
        private const float BrakeNightLevel = 2.2f;    // brake flare at night
        private const float BrakeDecel = 1.5f;         // m/s² slowing that counts as braking
        private const float BrakeCrawlSpeed = 0.5f;    // below this speed the car reads as braking/stopped
        // Headlight BEAM: a visible additive cone from the nose (oncoming cars beam toward the player). Night-only —
        // a visible shaft in daylight would look wrong, so it fades in with the day cycle while the glow bars stay on.
        private const float BeamLength = 12f;
        private const float BeamStartRadius = 0.2f;
        private const float BeamEndRadius = 2.6f;        // wide diagonal spread (the two beams share the road ahead)
        private const float BeamHeight = 0.6f;
        private const float BeamEdgeFactor = 0.34f;      // how far toward the car's L/R edges the two headlights sit
        private const float BeamPitchDeg = 12f;          // angled ~12° down so oncoming beams hit the road, not the player's face
        private const float BeamAlpha = 0.4f;            // warm light each beam adds (additive; the two beams overlap)
        private const float BeamNightThreshold = 0.2f;   // NightFactor below which the beams are off (no daytime shafts)
        private static readonly Color HeadColour = new Color(1f, 0.94f, 0.78f);
        private static readonly Color TailColour = new Color(1f, 0.10f, 0.04f);
        private static readonly Color BrakeColour = new Color(1f, 0.16f, 0.10f);
        private static readonly Color BeamColour = new Color(1.35f, 1.28f, 1.08f); // warm, slightly HDR so the shaft reads bright + blooms

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Mesh quadMesh, beamMesh;
        private static Material glowMat, beamMat;

        private TrafficVehicle vehicle;
        private Renderer head, tailL, tailR, tailHaloL, tailHaloR, beamL, beamR;
        private MaterialPropertyBlock mpb;
        private float prevSpeed;
        private float lastHead = -1f, lastTail = -1f, lastBeam = -1f;

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

            // Place the lamps on the vehicle's REAL silhouette, measured from its renderers, not from the
            // configured TrafficVehicle.length/width. Those config values drive spacing/AI and routinely run
            // SHORTER than the visual mesh — which buried the nose/tail glow quads INSIDE the body, so the rear
            // showed no lights at all ("the backs have no lights visible in the dark"). The 12 m beams escaped
            // the mesh regardless, which is why the fronts looked fine while the small quads vanished.
            float front = halfLen + 0.03f, rear = -(halfLen + 0.03f), tailY = TailHeight, headY = HeadHeight;
            if (TryGetLocalBounds(out Bounds body))
            {
                front = body.max.z + 0.06f;
                rear = body.min.z - 0.06f;
                w = Mathf.Max(w, body.size.x);
                // Keep the lamps on the body: clamp their height into the lower half of the real silhouette.
                tailY = Mathf.Clamp(TailHeight, body.min.y + 0.2f, body.min.y + body.size.y * 0.55f);
                headY = Mathf.Clamp(HeadHeight, body.min.y + 0.2f, body.min.y + body.size.y * 0.55f);
            }

            // A wide, short glow bar reads as "lights on" without pretending to be two separate lamps.
            head = BuildBar("HeadlightGlow", new Vector3(0f, headY, front), faceBack: false, new Vector2(w * 0.72f, 0.34f));
            // Two tail lights at the rear corners (mirroring the two front beams) so the back reads as real tail
            // lights, not one central blob; brightened at night in Update so the car is clearly visible in the dark.
            tailL = BuildBar("TaillightGlowL", new Vector3(-w * TailEdgeFactor, tailY, rear), faceBack: true, new Vector2(w * 0.34f, 0.34f));
            tailR = BuildBar("TaillightGlowR", new Vector3(w * TailEdgeFactor, tailY, rear), faceBack: true, new Vector2(w * 0.34f, 0.34f));
            // A soft red HALO behind each tail lamp: alpha-blended quads can't over-brighten the way the old
            // additive material did, so SIZE does the work — the big low-alpha aura is what makes the rear read
            // from a distance in the dark, the small core above reads as the lamp itself.
            tailHaloL = BuildBar("TaillightHaloL", new Vector3(-w * TailEdgeFactor, tailY, rear - 0.03f), faceBack: true, new Vector2(w * 0.6f, 0.9f));
            tailHaloR = BuildBar("TaillightHaloR", new Vector3(w * TailEdgeFactor, tailY, rear - 0.03f), faceBack: true, new Vector2(w * 0.6f, 0.9f));
            // Two headlight beams, out toward the car's left/right edges (like real headlights), each tilted down a touch.
            beamL = BuildBeam(new Vector3(-w * BeamEdgeFactor, BeamHeight, front - 0.02f));
            beamR = BuildBeam(new Vector3(w * BeamEdgeFactor, BeamHeight, front - 0.02f));
            prevSpeed = vehicle != null ? vehicle.CurrentSpeed : 0f;
        }

        /// <summary>Combined bounds of the vehicle's mesh renderers in ROOT-local space, so the lamps sit on the
        /// real body instead of the configured length. Built from each renderer's LOCAL bounds mapped renderer→root
        /// (never through a world AABB, which is already inflated while the car sits at a mid-bend yaw when the
        /// Provisioner attaches this — and the built flag would bake that error in for the car's pooled lifetime).
        /// False if there are no renderers yet.</summary>
        private bool TryGetLocalBounds(out Bounds local)
        {
            local = new Bounds();
            var renderers = GetComponentsInChildren<MeshRenderer>(false);
            bool has = false;
            Matrix4x4 toRoot = transform.worldToLocalMatrix;
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds lb = renderers[i].localBounds;
                Matrix4x4 toRootLocal = toRoot * renderers[i].transform.localToWorldMatrix;
                Vector3 min = lb.min, max = lb.max;
                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3((c & 1) == 0 ? min.x : max.x,
                                             (c & 2) == 0 ? min.y : max.y,
                                             (c & 4) == 0 ? min.z : max.z);
                    Vector3 lp = toRootLocal.MultiplyPoint3x4(corner);
                    if (!has) { local = new Bounds(lp, Vector3.zero); has = true; }
                    else local.Encapsulate(lp);
                }
            }
            return has;
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

            // Tail lights: subtle by day, bright HDR red at night so the back of the car is clearly visible in
            // the dark (night-aware like the beams, via DayCycleManager.NightFactor01); brake/crawl flares brighter.
            float speed = vehicle != null ? vehicle.CurrentSpeed : 0f;
            float decel = (prevSpeed - speed) / dt;
            prevSpeed = speed;
            bool braking = decel > BrakeDecel || speed < BrakeCrawlSpeed;
            float night = DayCycleManager.NightFactor01;
            float tailLevel = braking ? Mathf.Lerp(BrakeDayLevel, BrakeNightLevel, night)
                                      : Mathf.Lerp(TailDayLevel, TailNightLevel, night);
            // Sign of the key encodes braking, so a day↔night ramp OR a brake edge both refresh the two lights.
            float tailKey = braking ? -tailLevel : tailLevel;
            if (tailKey != lastTail)
            {
                lastTail = tailKey;
                Color tailC = (braking ? BrakeColour : TailColour) * tailLevel;
                Apply(tailL, tailC);
                Apply(tailR, tailC);
                // The halo carries the same red at a soft alpha — its size (not brightness) makes the rear read
                // in the dark; slightly stronger at night / while braking, subtle by day.
                var haloC = new Color(tailC.r, tailC.g, tailC.b, Mathf.Lerp(0.25f, 0.55f, night) * (braking ? 1.25f : 1f));
                Apply(tailHaloL, haloC);
                Apply(tailHaloR, haloC);
            }

            // Headlight beam: a visible additive shaft from the nose, fading in at night (off by day). The rgb carries
            // the night level so Apply disables the renderer by day; the additive material + gradient do the rest.
            float beamOn = Mathf.InverseLerp(BeamNightThreshold, 1f, DayCycleManager.NightFactor01);
            if (!Mathf.Approximately(beamOn, lastBeam))
            {
                lastBeam = beamOn;
                Color bc = new Color(BeamColour.r * beamOn, BeamColour.g * beamOn, BeamColour.b * beamOn, BeamAlpha);
                Apply(beamL, bc);
                Apply(beamR, bc);
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
            // White vertex colours: Sprites/Default multiplies the texture by the vertex colour, and an ABSENT
            // colour channel can read as BLACK on mobile GPUs — which leaves every car glow / tail light invisible
            // on the tablet while they show in the Editor. This is the fix for "the vehicle lights don't show".
            quadMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
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

        // ---- Beam (additive shaft) ----------------------------------------------------------------------

        private Renderer BuildBeam(Vector3 localPos)
        {
            var go = new GameObject("HeadlightBeam");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(BeamPitchDeg, 0f, 0f); // faces the car's nose (+Z), tilted down a touch
            go.AddComponent<MeshFilter>().sharedMesh = BeamMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = BeamMaterial();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.enabled = false;
            return mr;
        }

        private static Mesh BeamMesh()
        {
            if (beamMesh != null)
                return beamMesh;
            const int seg = 16;
            beamMesh = new Mesh { name = "CarHeadlightBeam" };
            var verts = new Vector3[(seg + 1) * 2];
            var uvs = new Vector2[(seg + 1) * 2];
            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                int s = i * 2;
                verts[s] = new Vector3(cx * BeamStartRadius, cy * BeamStartRadius, 0f);
                verts[s + 1] = new Vector3(cx * BeamEndRadius, cy * BeamEndRadius, BeamLength);
                uvs[s] = new Vector2((float)i / seg, 0f);
                uvs[s + 1] = new Vector2((float)i / seg, 1f);
            }
            var tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                int s = i * 2, t = i * 6;
                tris[t] = s; tris[t + 1] = s + 1; tris[t + 2] = s + 3;
                tris[t + 3] = s; tris[t + 4] = s + 3; tris[t + 5] = s + 2;
            }
            beamMesh.vertices = verts;
            beamMesh.uv = uvs;
            var cols = new Color[verts.Length];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white; // Sprites/Default needs vertex colours (mobile: absent = black)
            beamMesh.colors = cols;
            beamMesh.triangles = tris;
            beamMesh.RecalculateBounds();
            return beamMesh;
        }

        /// <summary>Shared material for every car beam (a translucent warm shaft). Uses the built-in "Sprites/Default"
        /// shader — Always-Included, needs no runtime-set keywords — so it renders RELIABLY on the Android/URP build.
        /// The previous version reconfigured "Universal Render Pipeline/Unlit" into a transparent-additive material at
        /// runtime, but URP strips that unused variant from a mobile build, leaving the beams invisible on the tablet
        /// while they showed in the Editor. Alpha-blended rather than additive; the beams are night-only and warm, so
        /// overlapping shafts still read fine.</summary>
        private static Material BeamMaterial()
        {
            if (beamMat != null)
                return beamMat;
            Texture2D grad = BeamGradient();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit"); // last-ditch; Sprites/Default is Always-Included
            beamMat = new Material(shader) { name = "CarHeadlightBeam", mainTexture = grad };
            beamMat.enableInstancing = true;
            return beamMat;
        }

        private static Texture2D BeamGradient()
        {
            const int h = 48;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "CarBeamGrad" };
            for (int y = 0; y < h; y++)
            {
                float alpha = Mathf.Pow(1f - y / (float)(h - 1), 1.1f); // slower fade = the shaft stays visible further
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
                tex.SetPixel(1, y, new Color(1f, 1f, 1f, alpha));
            }
            tex.Apply();
            return tex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { quadMesh = null; glowMat = null; beamMesh = null; beamMat = null; }

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
