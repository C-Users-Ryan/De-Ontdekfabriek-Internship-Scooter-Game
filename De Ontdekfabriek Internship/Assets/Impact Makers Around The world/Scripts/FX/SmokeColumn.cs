using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.Session;

namespace KenyaScooter.FX
{
    /// <summary>
    /// A thin cooking-fire smoke column on a homestead/market prop (FX Design Spec "Levend Kenia" §4.3,
    /// 3 Jul 2026). What sells it is not the smoke — it is that the column BENDS with <see cref="WindField"/>
    /// (the wind made visible at 60 m) and that fires are morning/evening things: density follows the day
    /// phase, fullest at dawn and dusk — breakfast and dinner fires — mirroring SkyLife's curve.
    ///
    /// Drop it on a roadside-prop prefab (the factory adds one to the Duka); it builds its own six-particle
    /// column on the shared soft-dust material at an optional 'SmokePoint' child (else just above the prop).
    /// Grey-brown, low alpha, never black — cooking fires, not burning ones. A static registry caps how many
    /// columns smoke at once, so a market row never becomes a wall of chimneys. Because it rides a pooled
    /// RoadsideProp, the existing "Leven langs de weg" facilitator toggle gates it for free.
    /// </summary>
    public sealed class SmokeColumn : MonoBehaviour
    {
        [Tooltip("Puffs per second at full day-phase density (a thin column needs very few).")]
        [SerializeField] private float rate = 7f;
        [Tooltip("Smoke colour — grey-brown cooking smoke, alpha low, never black.")]
        [SerializeField] private Color smoke = new Color(0.45f, 0.42f, 0.40f, 0.30f);
        [Tooltip("Where the column rises from, relative to the prop root, when no 'SmokePoint' child exists.")]
        [SerializeField] private Vector3 fallbackPoint = new Vector3(0.4f, 2.0f, -0.3f);

        /// <summary>Most columns smoking at once, across all live props — 3-4 reads as life, more reads as fire.</summary>
        private const int MaxLiveColumns = 3;
        private static readonly List<SmokeColumn> live = new List<SmokeColumn>(8);

        private ParticleSystem column;
        private ParticleSystem.EmissionModule emission;
        private ParticleSystem.VelocityOverLifetimeModule velocity;
        private static DayCycleManager dayCycle; // shared cache; re-found if it goes away (HeatShimmer pattern)

        private void Awake() => BuildColumn();

        private void OnEnable() => live.Add(this);

        private void OnDisable()
        {
            live.Remove(this);
            if (column != null)
                column.Clear(true); // fresh from the pool next time — no stale puffs mid-air
        }

        private void Update()
        {
            GameState state = GameManager.State;
            bool playing = state == GameState.Playing || state == GameState.AtCheckpoint;
            // Only the first few registered columns smoke; the rest hold cold until a slot frees up.
            bool slot = live.IndexOf(this) < MaxLiveColumns;

            emission.rateOverTime = (playing && slot) ? rate * DayCurve() : 0f;
            if (!playing || !slot)
                return;

            // The bend: lateral push grows with the wind while buoyancy fights it; a front beats the column
            // down and tears it up (shorter lifetime). Velocity space is WORLD so the bend direction is the
            // wind's, whatever way this prop happens to face on the shoulder.
            float w = WindField.Strength01;
            velocity.x = new ParticleSystem.MinMaxCurve(WindField.DirectionX * (0.4f + 2.0f * w));
            velocity.y = new ParticleSystem.MinMaxCurve(1.25f - 0.5f * WindField.Gust01);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            ParticleSystem.MainModule main = column.main;
            main.startLifetime = WindField.FrontLive ? 2.3f : 4.2f;
        }

        /// <summary>Day-phase density: ~1 at dawn/dusk (breakfast and dinner fires), thinner at midday, out at
        /// night. Reads DayCycleManager.CurrentPhase (0 ASUBUHI, 1 MCHANA, 2 ALASIRI, 3 JIONI, 4 MAGHARIBI,
        /// 5 USIKU), like SkyLife.</summary>
        private static float DayCurve()
        {
            if (dayCycle == null)
                dayCycle = Object.FindObjectOfType<DayCycleManager>();
            int phase = dayCycle != null ? dayCycle.CurrentPhase : 0;
            switch (phase)
            {
                case 0: return 1f;    // ASUBUHI — breakfast fires
                case 1: return 0.4f;  // MCHANA — a thin midday trickle
                case 2: return 1f;    // ALASIRI — dinner fires starting
                case 3: return 0.15f; // JIONI — dying embers
                case 4: return 0.05f; // MAGHARIBI — blue hour, fires almost out
                case 5: return 0f;    // USIKU — night, fires out
                default: return 0.5f;
            }
        }

        private void BuildColumn()
        {
            Transform point = transform.Find("SmokePoint");
            var go = new GameObject("Smoke");
            go.transform.SetParent(point != null ? point : transform, false);
            if (point == null)
                go.transform.localPosition = fallbackPoint;

            column = go.AddComponent<ParticleSystem>();
            column.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = column.main;
            // Local space: the column rides its prop as the spawner re-places it along the curve each frame
            // (world space would smear the smoke ahead of the "moving" house under the world-scroll illusion).
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            main.startLifetime = 4.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startColor = smoke;
            main.gravityModifier = 0f;
            main.maxParticles = 30;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = column.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f; // a hearth, not a chimney stack

            velocity = column.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World; // the wind blows in world axes
            velocity.x = new ParticleSystem.MinMaxCurve(0.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.25f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            // Puffs swell and thin as they rise, fading at both ends so the column has soft tips.
            ParticleSystem.SizeOverLifetimeModule size = column.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(1f, 2.2f)));

            ParticleSystem.ColorOverLifetimeModule col = column.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = FXMaterials.SoftDustMaterial();

            emission = column.emission;
            emission.rateOverTime = 0f; // Update gates it on state + slot + the day phase
            column.Play();
        }
    }
}
