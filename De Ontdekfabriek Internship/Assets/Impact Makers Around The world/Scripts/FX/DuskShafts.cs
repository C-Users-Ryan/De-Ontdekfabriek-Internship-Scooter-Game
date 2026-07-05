using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Session;

namespace KenyaScooter.FX
{
    /// <summary>
    /// The ALASIRI beauty pass (FX Design Spec "Levend Kenia" §4.8, 3 Jul 2026): three to five translucent
    /// warm quads angled from the low sun across the backdrop, breathing slowly, plus a sparse sparkle
    /// system that emits ONLY inside the shaft fan — so the haze motes twinkle in the beams. It is the
    /// payoff of the whole dust story: the air the player has been driving through all session becomes
    /// visible and beautiful for a few minutes each cycle, and the day cycle itself reads as authored.
    ///
    /// Day-phase gated exactly like SkyLife/HeatShimmer (full only on ALASIRI, the low-sun afternoon), and
    /// it follows the facilitator dust toggle since it IS dust made visible. Alpha eases over the weather
    /// config's hazeFadeSeconds so phase changes never pop. Purely a backdrop-layer effect far ahead and to
    /// the sun side — never over the road's information zone. Self-bootstraps; URP-safe unlit transparent
    /// material (the HeatShimmer pattern), so it can never render magenta. Pure scenery, zero gameplay.
    /// </summary>
    public sealed class DuskShafts : MonoBehaviour
    {
        [Header("Master")]
        [Tooltip("Off: no shafts ever show (independent of the dust toggle, which can also hide them).")]
        [SerializeField] private bool enabledEffect = true;

        [Header("Fan")]
        [Tooltip("How many shafts fan from the sun (3-5; more reads as venetian blinds).")]
        [SerializeField] private int shaftCount = 4;
        [Tooltip("Shaft tint — warm sun through dust; alpha is the MAX, before breathing and the phase fade.")]
        [SerializeField] private Color shaftColour = new Color(1f, 0.84f, 0.59f, 0.10f);
        [Tooltip("How far ahead the fan sits (m) — backdrop distance, behind the road information.")]
        [SerializeField] private float distance = 85f;
        [Tooltip("Which side of the road the low sun sits (-1 = left, +1 = right) and how far out the fan starts.")]
        [SerializeField] private float sunSide = -1f;
        [SerializeField] private float sunOffset = 26f;

        // Day phases (DayCycleManager.CurrentPhase, 0..3): 2 = ALASIRI, the low-sun afternoon.
        private const int ALASIRI = 2;

        // Self-bootstrap after scene load (mirrors HeatShimmer): present in the build with no wiring.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<DuskShafts>() != null)
                return;
            var go = new GameObject("DuskShafts (auto)");
            go.AddComponent<DuskShafts>();
        }

        private MeshRenderer[] shafts;
        private ParticleSystem sparkle;
        private ParticleSystem.EmissionModule sparkleEmission;
        private MaterialPropertyBlock mpb;
        private float alpha;                   // eased master alpha (0 outside ALASIRI)
        private DayCycleManager dayCycle;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            BuildFan();
            BuildSparkle();
        }

        private void Update()
        {
            WeatherConfig wc = DustAtmosphere.ResolveConfig();
            bool playing = GameManager.State == GameState.Playing || GameManager.State == GameState.AtCheckpoint;
            bool dusk = CurrentPhase() == ALASIRI;
            float target = (enabledEffect && wc.dustEnabled && playing && dusk) ? 1f : 0f;

            float fade = wc.hazeFadeSeconds > 0.01f ? wc.hazeFadeSeconds : 1f;
            alpha = Mathf.MoveTowards(alpha, target, Time.deltaTime / fade);

            sparkleEmission.rateOverTime = 8f * alpha;
            if (alpha <= 0.0001f)
                return; // fully out: skip the per-shaft work on every other phase

            for (int i = 0; i < shafts.Length; i++)
            {
                float breathe = 0.55f + 0.45f * Mathf.Sin(Time.time * 0.5f + i * 1.7f);
                Color c = shaftColour;
                c.a *= alpha * breathe;
                shafts[i].GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, c);
                mpb.SetColor(ColorId, c);
                shafts[i].SetPropertyBlock(mpb);
            }
        }

        /// <summary>Reads the live day phase (cached, re-found if it goes away — the HeatShimmer pattern).
        /// Without a day cycle there is no dusk, so the shafts stay dark rather than defaulting on.</summary>
        private int CurrentPhase()
        {
            if (dayCycle == null)
                dayCycle = FindObjectOfType<DayCycleManager>();
            return dayCycle != null ? dayCycle.CurrentPhase : -1;
        }

        /// <summary>Builds the fan: tall thin quads leaning away from the sun side, each a touch different in
        /// tilt, width and depth so the fan reads as light finding gaps, not as blinds.</summary>
        private void BuildFan()
        {
            Material mat = BuildSafeMaterial();
            int count = Mathf.Clamp(shaftCount, 3, 5);
            shafts = new MeshRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("DuskShaft" + i);
                go.transform.SetParent(transform, false);
                float f = (count > 1) ? i / (float)(count - 1) : 0.5f;
                go.transform.position = new Vector3(
                    sunSide * (sunOffset - f * 18f),           // fanning inward from the sun side
                    0f,
                    distance + f * 12f);
                // Leaning away from the low sun: the beam tilts over the road direction, never across it.
                go.transform.rotation = Quaternion.Euler(0f, 0f, sunSide * (22f + f * 12f));

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = ShaftQuad(2.6f + f * 2.2f, 30f);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                shafts[i] = mr;

                // Start invisible; Update eases the alpha in only on ALASIRI.
                mr.GetPropertyBlock(mpb);
                Color c = shaftColour; c.a = 0f;
                mpb.SetColor(BaseColorId, c);
                mpb.SetColor(ColorId, c);
                mr.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Sparse bright motes emitted only across the fan's volume, so the twinkle happens IN the
        /// beams — the veil's dust made precious for a few minutes a cycle.</summary>
        private void BuildSparkle()
        {
            var go = new GameObject("ShaftSparkle");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(sunSide * (sunOffset - 9f), 8f, distance + 6f);

            sparkle = go.AddComponent<ParticleSystem>();
            sparkle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = sparkle.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            Color bright = shaftColour; bright.a = 0.8f;
            main.startColor = bright;
            main.gravityModifier = -0.005f; // motes drift UP in the warm air, barely
            main.maxParticles = 40;
            main.playOnAwake = false;

            // A slab roughly covering the fan — close enough that every mote reads as "inside a beam".
            ParticleSystem.ShapeModule shape = sparkle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 16f, 14f);

            ParticleSystem.ColorOverLifetimeModule col = sparkle.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = FXMaterials.SoftDustMaterial();

            sparkleEmission = sparkle.emission;
            sparkleEmission.rateOverTime = 0f; // Update drives it from the dusk fade
            sparkle.Play();
        }

        /// <summary>A tall quad whose alpha feathers to zero at both vertical ends (via vertex colours the
        /// Sprites shader multiplies), so the beam dissolves into sky and ground instead of ending in a line.</summary>
        private static Mesh ShaftQuad(float width, float height)
        {
            var mesh = new Mesh { name = "DuskShaft (runtime)" };
            float hw = width * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-hw, 0f, 0f), new Vector3(hw, 0f, 0f),
                new Vector3(-hw * 0.6f, height, 0f), new Vector3(hw * 0.6f, height, 0f), // narrows toward the sun
            };
            mesh.uv = new[] { new Vector2(0.2f, 0.5f), new Vector2(0.8f, 0.5f), new Vector2(0.35f, 0.5f), new Vector2(0.65f, 0.5f) };
            mesh.colors = new[]
            {
                new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f),   // ground end fades out
                new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 1f),   // sky end carries the beam
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 1, 2, 0, 3, 2, 1 }; // both windings: visible from either side
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>URP-safe transparent unlit material (mirrors HeatShimmer.BuildSafeMaterial).</summary>
        private static Material BuildSafeMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            var mat = new Material(shader) { name = "DuskShaft (runtime)" };
            Texture2D tex = FXMaterials.SoftDustMaterial().mainTexture as Texture2D;
            if (tex != null)
            {
                mat.mainTexture = tex; // the soft dot gives each beam a soft lateral falloff
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            return mat;
        }
    }
}
