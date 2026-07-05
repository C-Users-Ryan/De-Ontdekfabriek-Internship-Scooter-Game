using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.Session;

namespace KenyaScooter.Sky
{
    /// <summary>
    /// "Sky life" — occasional dark BIRD silhouettes drifting and slowly circling high across the warm sky, so the
    /// world reads as alive rather than empty (Oplevering "roadside life" backlog, the airborne half). Authentic
    /// reads from the research brief: a lone MARABOU STORK or a RAPTOR wheeling slowly high up, and an occasional
    /// small scatter of STARLINGS/WEAVERS crossing lower. Silhouettes only, dark-warm against the sky.
    ///
    /// Everything is built at runtime and is render-pipeline-safe: the bird shapes are small RGBA alpha textures
    /// generated procedurally (a shallow gull "M" and a fatter stork), drawn on billboard quads on a Sprites/Default
    /// material — never the built-in default material, which renders MAGENTA under URP (mirrors FXMaterials / SpeedLines).
    /// Birds are POOLED quads anchored to the camera; one manager Update ticks them all (flap, drift, circle, billboard),
    /// so there is no per-frame allocation and no per-bird MonoBehaviour. Density follows the day phase — more at dawn
    /// (ASUBUHI) and dusk (ALASIRI/JIONI), near-none at night (JIONI deep).
    ///
    /// Self-bootstraps after scene load if absent (mirrors PedestrianCrossingSpawner / DustAtmosphere). Purely additive
    /// and never touches gameplay: if disabled it does nothing, and it owns only its own quads high above the horizon.
    /// </summary>
    // NOTE: this class is split across two files for readability (2026-06-27, no behaviour change):
    //   SkyLife.cs        — runtime: lifecycle, spawning, the per-frame bird tick/billboard, pooling.
    //   SkyLife.Assets.cs — construction: shared material, quad meshes, procedural silhouette textures.
    public sealed partial class SkyLife : MonoBehaviour
    {
        [Header("Enable")]
        [Tooltip("Master switch. Off = no birds are spawned and any live ones recycle; gameplay is unaffected either way.")]
        [SerializeField] private bool enabledSkyLife = true;

        [Header("Population")]
        [Tooltip("Most birds visible at once across both kinds (the day-phase curve scales the live target below this).")]
        [SerializeField] private int maxBirds = 7;
        [Tooltip("Seconds between spawn attempts. Each attempt adds one bird (or a small flock) up to the live target.")]
        [SerializeField] private float spawnInterval = 4f;
        [Tooltip("Chance a low-crossing spawn is a small SCATTER (starlings/weavers) rather than a single bird.")]
        [Range(0f, 1f)] [SerializeField] private float flockChance = 0.35f;
        [Tooltip("How many birds in a small scatter flock (inclusive range).")]
        [SerializeField] private Vector2Int flockSize = new Vector2Int(3, 6);

        [Header("Placement (camera-relative, high above the horizon)")]
        [Tooltip("Metres ahead of the camera the birds fly (near, far). Far enough to read as birds, not props.")]
        [SerializeField] private Vector2 distanceRange = new Vector2(55f, 120f);
        [Tooltip("Height band above the camera the birds occupy, in metres (low scatter min, high wheeler max).")]
        [SerializeField] private Vector2 heightRange = new Vector2(22f, 60f);
        [Tooltip("Half-width of the lateral spread to either side of the view centre, in metres.")]
        [SerializeField] private float lateralSpread = 70f;

        [Header("Look")]
        [Tooltip("Silhouette tint. Dark-warm reads as a bird against the bright sky. Alpha sets overall opacity.")]
        [SerializeField] private Color silhouetteColour = new Color(0.16f, 0.12f, 0.10f, 0.92f);
        [Tooltip("On-screen size of a small crossing bird, in metres (it is far away, so this is small).")]
        [SerializeField] private float smallBirdSize = 1.6f;
        [Tooltip("On-screen size of the lone wheeling stork/raptor (bigger, slower, higher).")]
        [SerializeField] private float largeBirdSize = 3.2f;

        [Header("Motion")]
        [Tooltip("Drift speed of a crossing bird across the sky, in metres/second.")]
        [SerializeField] private float driftSpeed = 6f;
        [Tooltip("Wing-flap frequency (flaps/second) for the small crossing birds. The wheeler barely flaps.")]
        [SerializeField] private float flapHz = 3.2f;
        [Tooltip("Radius of the lone wheeler's slow circling arc, in metres.")]
        [SerializeField] private float circleRadius = 14f;
        [Tooltip("Angular speed of the wheeler's circle, in degrees/second (slow = majestic).")]
        [SerializeField] private float circleDegPerSec = 14f;

        // --- One bird ----------------------------------------------------------------------------------------------
        private enum BirdKind { CrossingSmall, WheelingLarge }

        private sealed class Bird
        {
            public Transform tr;
            public MeshRenderer renderer;
            public BirdKind kind;
            public bool active;

            // Crossing birds: a straight drift across the sky in localDir from localOrigin.
            public Vector3 localPos;      // position relative to the camera anchor
            public Vector3 localDir;      // unit drift direction (camera-local)
            public float speed;

            // Wheeling bird: a slow circle around a camera-local centre.
            public Vector3 circleCentre;
            public float circleAngleDeg;
            public float circleRadius;

            public float baseSize;
            public float flapPhase;       // radians, advanced per frame
            public float flapHz;          // 0 for the near-static wheeler glide
        }

        private readonly List<Bird> birds = new List<Bird>(16);
        private float nextSpawnAt;

        private Transform anchor;          // the camera (birds live in its local space so they follow the view)
        private Camera cam;
        private Material sharedMaterial;   // Sprites/Default, tinted dark-warm, shared by every quad
        private Mesh gullMesh;             // shallow-"M" gull silhouette quad
        private Mesh storkMesh;            // fatter stork/raptor silhouette quad
        private Texture2D gullTex;         // alpha silhouette for the small crossing bird
        private Texture2D storkTex;        // alpha silhouette for the lone wheeler
        private MaterialPropertyBlock mpb;

        private DayCycleManager dayCycle;  // cached; CurrentPhase drives density
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        // Self-bootstrap after scene load, mirroring PedestrianCrossingSpawner / DustAtmosphere. Additive and safe;
        // if a SkyLife is already placed by hand we leave it be.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<SkyLife>() != null)
                return;
            var go = new GameObject("SkyLife (auto)");
            go.AddComponent<SkyLife>();
        }

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            BuildSharedMaterial();
            gullTex = BuildGullTexture();
            storkTex = BuildStorkTexture();
            gullMesh = BuildBirdQuad("SkyLife Gull (runtime)");
            storkMesh = BuildBirdQuad("SkyLife Stork (runtime)");
            nextSpawnAt = spawnInterval;
        }

        private void Start()
        {
            ResolveAnchor();
            dayCycle = FindObjectOfType<DayCycleManager>();
        }

        private void Update()
        {
            if (!enabledSkyLife)
            {
                if (birds.Count > 0)
                    RecycleAll();
                return;
            }

            // Only fly while the world is actually running; freeze (but keep them placed) otherwise so they do not
            // drift away during the start overlay / checkpoint / game over.
            GameState state = GameManager.State;
            bool playing = state == GameState.Playing || state == GameState.AtCheckpoint;

            if (anchor == null)
                ResolveAnchor();
            if (anchor == null)
                return; // no camera yet — nothing to anchor to, try again next frame

            float dt = Time.deltaTime;
            float t01 = PhaseDensity01();
            int liveTarget = Mathf.RoundToInt(maxBirds * t01);

            if (playing)
            {
                TickBirds(dt);
                TrySpawn(liveTarget);
            }

            Billboard(); // always keep them facing the camera, even when paused, so a frozen bird never shows edge-on
        }

        // -------------------------------------------------------------------------------------------------------
        // Spawning
        // -------------------------------------------------------------------------------------------------------

        private void TrySpawn(int liveTarget)
        {
            if (Time.time < nextSpawnAt)
                return;
            nextSpawnAt = Time.time + spawnInterval;

            int alive = CountActive();
            if (alive >= liveTarget || liveTarget <= 0)
                return;

            // Occasionally a lone high wheeler (stork/raptor) when there is none aloft; otherwise a low crossing
            // bird or a small scatter flock of them.
            bool wheelerAloft = HasActiveKind(BirdKind.WheelingLarge);
            if (!wheelerAloft && Random.value < 0.45f)
            {
                SpawnWheeler();
                return;
            }

            if (Random.value < flockChance)
            {
                int n = Random.Range(flockSize.x, flockSize.y + 1);
                Vector3 dir = RandomCrossDirection();
                float height = Random.Range(heightRange.x, Mathf.Lerp(heightRange.x, heightRange.y, 0.5f));
                for (int i = 0; i < n && CountActive() < maxBirds; i++)
                    SpawnCrossing(dir, height, scatter: true);
            }
            else
            {
                SpawnCrossing(RandomCrossDirection(), Random.Range(heightRange.x, heightRange.y), scatter: false);
            }
        }

        private void SpawnCrossing(Vector3 localDir, float height, bool scatter)
        {
            Bird b = GetOrCreate(gullMesh);
            b.kind = BirdKind.CrossingSmall;
            b.flapHz = flapHz * Random.Range(0.85f, 1.2f);
            b.flapPhase = Random.value * Mathf.PI * 2f;
            b.baseSize = smallBirdSize * Random.Range(0.8f, 1.15f);
            b.speed = driftSpeed * Random.Range(0.8f, 1.25f);
            b.localDir = localDir;

            float depth = Random.Range(distanceRange.x, distanceRange.y);
            // Start just off the entering edge so it drifts across the view. Scatter members cluster around it.
            float startSide = -Mathf.Sign(localDir.x == 0f ? 1f : localDir.x) * lateralSpread;
            float jitter = scatter ? Random.Range(-6f, 6f) : 0f;
            b.localPos = new Vector3(startSide + jitter, height + Random.Range(-3f, 3f), depth + jitter);

            Activate(b);
        }

        private void SpawnWheeler()
        {
            Bird b = GetOrCreate(storkMesh);
            b.kind = BirdKind.WheelingLarge;
            b.flapHz = 0f; // a wheeler glides; only the faintest size shimmer, set in TickBirds
            b.flapPhase = Random.value * Mathf.PI * 2f;
            b.baseSize = largeBirdSize * Random.Range(0.9f, 1.15f);
            b.circleRadius = circleRadius * Random.Range(0.8f, 1.3f);
            b.circleAngleDeg = Random.value * 360f;

            float depth = Random.Range(Mathf.Lerp(distanceRange.x, distanceRange.y, 0.4f), distanceRange.y);
            float height = Random.Range(Mathf.Lerp(heightRange.x, heightRange.y, 0.55f), heightRange.y);
            float sideways = Random.Range(-lateralSpread * 0.5f, lateralSpread * 0.5f);
            b.circleCentre = new Vector3(sideways, height, depth);
            b.localPos = b.circleCentre + new Vector3(b.circleRadius, 0f, 0f);

            Activate(b);
        }

        private Vector3 RandomCrossDirection()
        {
            // Mostly lateral with a little vertical wander, so birds cross the sky rather than fly at the camera.
            float vx = Random.value < 0.5f ? 1f : -1f;
            return new Vector3(vx, Random.Range(-0.12f, 0.12f), Random.Range(-0.15f, 0.15f)).normalized;
        }

        // -------------------------------------------------------------------------------------------------------
        // Per-frame motion (one manager Update for all birds — no per-bird component)
        // -------------------------------------------------------------------------------------------------------

        private void TickBirds(float dt)
        {
            float recycleLateral = lateralSpread + 18f;
            for (int i = 0; i < birds.Count; i++)
            {
                Bird b = birds[i];
                if (!b.active)
                    continue;

                if (b.kind == BirdKind.CrossingSmall)
                {
                    b.localPos += b.localDir * (b.speed * dt);
                    // Recycle once it has drifted clear of the lateral band (it has crossed the sky).
                    if (Mathf.Abs(b.localPos.x) > recycleLateral)
                    {
                        Deactivate(b);
                        continue;
                    }
                }
                else // WheelingLarge — slow circle
                {
                    b.circleAngleDeg += circleDegPerSec * dt;
                    float rad = b.circleAngleDeg * Mathf.Deg2Rad;
                    // Circle in the camera's X (lateral) / Z (depth) plane so it wheels across the sky, not vertically.
                    b.localPos = b.circleCentre + new Vector3(Mathf.Cos(rad) * b.circleRadius, 0f, Mathf.Sin(rad) * b.circleRadius);
                    // Drift the whole circle slowly sideways so a wheeler eventually leaves and a new one can appear.
                    b.circleCentre.x += 0.6f * dt;
                    if (b.circleCentre.x > lateralSpread)
                    {
                        Deactivate(b);
                        continue;
                    }
                }

                // Wing-flap as a subtle sine on the quad's vertical scale (reads as flapping at distance). The wheeler
                // gets a tiny, slow shimmer instead of a flap.
                float hz = b.flapHz > 0f ? b.flapHz : 0.5f;
                b.flapPhase += dt * hz * Mathf.PI * 2f;
                float flapAmt = b.flapHz > 0f ? 0.45f : 0.08f;
                float vScale = 1f - flapAmt * Mathf.Abs(Mathf.Sin(b.flapPhase)); // wings down => shorter silhouette
                b.tr.localScale = new Vector3(b.baseSize, b.baseSize * vScale, b.baseSize);

                b.tr.localPosition = b.localPos;
            }
        }

        /// <summary>Face every active quad toward the camera each frame (cheap manual billboard in camera-local space:
        /// the quad's forward points back at the anchor origin, since the anchor IS the camera).</summary>
        private void Billboard()
        {
            if (anchor == null)
                return;
            for (int i = 0; i < birds.Count; i++)
            {
                Bird b = birds[i];
                if (!b.active)
                    continue;
                // The anchor is the camera; look back toward its origin (local 0,0,0) so the quad faces the lens.
                Vector3 toCam = -b.tr.localPosition;
                if (toCam.sqrMagnitude < 0.0001f)
                    continue;
                b.tr.localRotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Day-phase density
        // -------------------------------------------------------------------------------------------------------

        /// <summary>0..1 density multiplier by day phase: full at dawn (ASUBUHI) and late afternoon (ALASIRI),
        /// moderate at midday (MCHANA), thinning through dusk (JIONI) and gone through the blue hour into night
        /// (MAGHARIBI/USIKU) as the birds roost. Reads DayCycleManager.CurrentPhase (int 0..5).</summary>
        private float PhaseDensity01()
        {
            int phase = dayCycle != null ? dayCycle.CurrentPhase : 1; // default to a lively midday read if no cycle
            switch (phase)
            {
                case 0: return 1.0f;  // ASUBUHI — dawn, most birds
                case 1: return 0.55f; // MCHANA — midday, fewer
                case 2: return 0.9f;  // ALASIRI — late afternoon, busy again
                case 3: return 0.12f; // JIONI — dusk, thinning out
                case 4: return 0.04f; // MAGHARIBI — blue hour, all but roosted
                case 5: return 0f;    // USIKU — night, the day birds are gone
                default: return 0.5f;
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Pool
        // -------------------------------------------------------------------------------------------------------

        private Bird GetOrCreate(Mesh mesh)
        {
            // Reuse a free bird of any kind (its mesh is swapped to the requested one), else grow the pool.
            for (int i = 0; i < birds.Count; i++)
            {
                if (!birds[i].active)
                {
                    SetMesh(birds[i], mesh);
                    return birds[i];
                }
            }
            var b = CreateBird(mesh);
            birds.Add(b);
            return b;
        }

        private Bird CreateBird(Mesh mesh)
        {
            var go = new GameObject("Bird");
            go.transform.SetParent(anchor != null ? anchor : transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = sharedMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // Per-bird tint + silhouette texture via property block (no material instances), so the dark-warm
            // colour and the kind's shape apply with one shared material.
            ApplyVisual(mr, mesh);

            go.SetActive(false);
            return new Bird { tr = go.transform, renderer = mr, active = false };
        }

        private void SetMesh(Bird b, Mesh mesh)
        {
            var mf = b.tr.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != mesh)
            {
                mf.sharedMesh = mesh;
                ApplyVisual(b.renderer, mesh); // swap the silhouette texture to match the new kind
            }
        }

        /// <summary>Pushes the dark-warm tint and the kind's silhouette texture onto a renderer via the shared
        /// property block, so one Sprites/Default material draws both bird shapes with no material instances.</summary>
        private void ApplyVisual(MeshRenderer mr, Mesh mesh)
        {
            if (mr == null)
                return;
            Texture2D tex = mesh == storkMesh ? storkTex : gullTex;
            mr.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, silhouetteColour);
            mpb.SetColor(BaseColorId, silhouetteColour);
            if (tex != null)
            {
                mpb.SetTexture(MainTexId, tex);
                mpb.SetTexture(BaseMapId, tex);
            }
            mr.SetPropertyBlock(mpb);
        }

        private void Activate(Bird b)
        {
            b.active = true;
            b.tr.localPosition = b.localPos;
            b.tr.localScale = new Vector3(b.baseSize, b.baseSize, b.baseSize);
            b.tr.gameObject.SetActive(true);
        }

        private void Deactivate(Bird b)
        {
            b.active = false;
            b.tr.gameObject.SetActive(false);
        }

        private void RecycleAll()
        {
            for (int i = 0; i < birds.Count; i++)
                if (birds[i].active)
                    Deactivate(birds[i]);
        }

        private int CountActive()
        {
            int n = 0;
            for (int i = 0; i < birds.Count; i++)
                if (birds[i].active) n++;
            return n;
        }

        private bool HasActiveKind(BirdKind kind)
        {
            for (int i = 0; i < birds.Count; i++)
                if (birds[i].active && birds[i].kind == kind)
                    return true;
            return false;
        }

    }
}
