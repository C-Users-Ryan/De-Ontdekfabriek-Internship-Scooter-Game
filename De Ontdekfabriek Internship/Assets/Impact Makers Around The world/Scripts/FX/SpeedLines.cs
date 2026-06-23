using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.FX
{
    /// <summary>
    /// Speed-line particle emission scaled by world speed (MDA A1 — sensation).
    /// Silent below base speed, ramping to full at max. The emission module is cached
    /// once; no per-frame allocation.
    ///
    /// Because the camera is fixed and the world scrolls past, the particles need their
    /// own motion to read as streaks. With <see cref="autoConfigure"/> on (default) this
    /// builds the whole effect at runtime: a render-pipeline-safe white material (the
    /// built-in particle material renders magenta under URP), stretched billboards, a
    /// wide spawn area ahead of the camera and a velocity that rushes the particles back
    /// toward and past the camera — i.e. white speed streaks — regardless of how the
    /// ParticleSystem was set up in the editor. Turn it off only to hand-author the
    /// system yourself; the safe material is still applied either way.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SpeedLines : MonoBehaviour
    {
        [SerializeField] private float maxEmissionRate = 180f;
        [Tooltip("Over-cruise fraction (0 = cruising speed, 1 = max) to emission fraction. Stays 0 at cruise; drag the middle key to change how soon streaks ramp in above it.")]
        [SerializeField] private AnimationCurve emissionBySpeed = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.12f, 0f), new Keyframe(1f, 1f));

        [Header("Auto-setup")]
        [Tooltip("Build a complete white speed-line effect at runtime. Turn off only if you want to author the ParticleSystem by hand in the editor (the safe material is still applied).")]
        [SerializeField] private bool autoConfigure = true;

        [Header("Look")]
        [Tooltip("Optional. Assign your own URP-compatible particle material to take full control. Empty = an unlit white material is built at runtime so particles can never go magenta.")]
        [SerializeField] private Material overrideMaterial;
        [SerializeField] private Color lineColor = Color.white;
        [Tooltip("Streak length from the particle's own size. Higher = longer base streak.")]
        [SerializeField] private float lengthScale = 1.5f;
        [Tooltip("Extra streak length from the particle's speed. Higher = streaks grow more with velocity.")]
        [SerializeField] private float velocityScale = 0.06f;
        [SerializeField] private Vector2 streakSizeRange = new Vector2(0.04f, 0.09f);

        [Header("Motion (auto-setup only)")]
        [Tooltip("Local distance ahead of the camera where streaks spawn.")]
        [SerializeField] private float spawnAheadDistance = 14f;
        [Tooltip("Width × height of the spawn area, in metres, so streaks fill the view.")]
        [SerializeField] private Vector2 spawnAreaSize = new Vector2(10f, 6f);
        [Tooltip("How fast streaks rush toward and past the camera.")]
        [SerializeField] private float rushSpeed = 22f;

        private ParticleSystem.EmissionModule emission;

        private void Awake()
        {
            ParticleSystem ps = GetComponent<ParticleSystem>();
            emission = ps.emission;

            ApplySafeMaterial(ps);
            if (autoConfigure)
                ConfigureSpeedLines(ps);
        }

        private void Update()
        {
            float rate = 0f;
            if ((GameManager.State == GameState.Playing || GameManager.State == GameState.AtCheckpoint)
                && WorldSpeed.Instance != null)
            {
                WorldSpeed ws = WorldSpeed.Instance;
                // 0 at or below cruising (base) speed, 1 at max — so streaks only show when driving FASTER than cruise.
                float overCruise = Mathf.InverseLerp(ws.BaseSpeed, ws.MaxSpeed, ws.Current);
                rate = emissionBySpeed.Evaluate(overCruise) * maxEmissionRate;
            }
            emission.rateOverTime = rate;
        }

        /// <summary>
        /// Guarantees a material on a shader that exists in the active pipeline, so the
        /// particles can never fall back to the magenta error shader.
        /// </summary>
        private void ApplySafeMaterial(ParticleSystem ps)
        {
            ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            if (psRenderer == null)
                return;

            psRenderer.material = overrideMaterial != null ? overrideMaterial : BuildSafeMaterial();
        }

        private Material BuildSafeMaterial()
        {
            // URP particle shader first; Sprites/Default is the fallback — it ships with
            // every pipeline, is unlit, respects the particle's vertex colour, and never
            // resolves to the magenta error shader. With no texture it draws solid white.
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            Material mat = new Material(shader) { name = "SpeedLines (runtime)" };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", lineColor);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", lineColor);
            return mat;
        }

        /// <summary>
        /// Drives every module needed for white speed streaks: stretched billboards,
        /// thin white particles spawned across a wide area ahead of the camera, and a
        /// local velocity that carries them back past the camera. Off-centre particles
        /// then stretch radially from the screen centre — the speed-line read.
        /// </summary>
        private void ConfigureSpeedLines(ParticleSystem ps)
        {
            ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                psRenderer.lengthScale = lengthScale;
                psRenderer.velocityScale = velocityScale;
                psRenderer.alignment = ParticleSystemRenderSpace.View;
            }

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f; // motion comes from velocity-over-lifetime, so direction is exact
            main.startLifetime = Mathf.Max(0.1f, (spawnAheadDistance + 6f) / Mathf.Max(1f, rushSpeed));
            main.startSize = new ParticleSystem.MinMaxCurve(streakSizeRange.x, streakSizeRange.y);
            main.startColor = lineColor;
            main.gravityModifier = 0f;
            main.maxParticles = 600;
            main.playOnAwake = true;

            // Spawn across a wide, flat box in front of the camera so streaks fill the view.
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(spawnAreaSize.x, spawnAreaSize.y, 1f);
            shape.position = new Vector3(0f, 0f, spawnAheadDistance);
            shape.rotation = Vector3.zero;

            // Rush every particle straight back toward (and past) the camera.
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(-rushSpeed);

            if (!ps.isPlaying)
                ps.Play();
        }
    }
}
