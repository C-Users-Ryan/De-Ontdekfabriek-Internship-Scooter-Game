using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.Environment
{
    /// <summary>
    /// One manager that leans every registered verge tuft (and acacia crown) with <see cref="KenyaScooter.FX.WindField"/>
    /// (FX Design Spec "Levend Kenia" §4.4, 3 Jul 2026). The verge is on screen 100% of the time, so this is
    /// the highest-coverage wind read in the game: a single child rotation per tuft, with a hashed per-tuft
    /// phase so the wave TRAVELS along the verge instead of the whole roadside nodding in unison.
    ///
    /// No per-tuft Update (the SkyLife pattern): <see cref="GrassTuft"/> markers register their lean child on
    /// spawn and unregister on recycle, and this one manager ticks them all. Big things answer slowly — a
    /// tuft's optional lag smooths its lean so an acacia canopy trails the gust by half a second. Animates
    /// CHILD transforms only, so it composes with the spawner that owns every prop root. Self-bootstraps on
    /// first registration; costs nothing while nothing is registered.
    /// </summary>
    public sealed class GrassSway : MonoBehaviour
    {
        [Tooltip("Lean at full wind strength, degrees, before a tuft's own scale. Keep gentle; the wave does the work.")]
        [SerializeField] private float maxLean = 14f;

        private sealed class Tuft
        {
            public Transform body;
            public float seed;       // hashed phase so the wave travels
            public float scale;      // per-tuft amplitude (grass 1, canopy ~0.4)
            public float lag;        // seconds of smoothing (0 = answers instantly, canopy ~0.5)
            public float current;    // smoothed lean state, degrees
            public Quaternion rest;  // authored child rotation the lean composes onto
        }

        private static readonly List<Tuft> tufts = new List<Tuft>(64);
        private static GrassSway instance;

        /// <summary>Called by GrassTuft on enable. Creates the manager on first use (zero scene wiring).</summary>
        public static void Register(Transform body, float scale, float lag)
        {
            if (body == null)
                return;
            if (instance == null && Application.isPlaying)
                instance = new GameObject("GrassSway (auto)").AddComponent<GrassSway>();
            tufts.Add(new Tuft
            {
                body = body,
                seed = Random.value * 20f,
                scale = scale,
                lag = lag,
                rest = body.localRotation,
            });
        }

        /// <summary>Called by GrassTuft on disable (pool recycle). Restores the authored rest pose.</summary>
        public static void Unregister(Transform body)
        {
            for (int i = tufts.Count - 1; i >= 0; i--)
            {
                if (tufts[i].body != body)
                    continue;
                if (body != null)
                    body.localRotation = tufts[i].rest;
                tufts.RemoveAt(i);
            }
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (state != GameState.Playing && state != GameState.AtCheckpoint)
                return;

            float w = FX.WindField.Strength01;
            float dir = -FX.WindField.DirectionX; // blades lean away from the push
            float t = Time.time;
            float dt = Time.deltaTime;

            for (int i = tufts.Count - 1; i >= 0; i--)
            {
                Tuft tuft = tufts[i];
                if (tuft.body == null) { tufts.RemoveAt(i); continue; } // its prop was destroyed under us

                float phase = Mathf.Sin(t * 5.5f + tuft.seed) * 0.15f;
                float target = dir * maxLean * tuft.scale * (w + phase);
                // Lagged tufts (canopies) trail the gust; lag 0 answers within the frame.
                tuft.current = tuft.lag > 0.01f
                    ? Mathf.Lerp(tuft.current, target, 1f - Mathf.Exp(-dt / tuft.lag))
                    : target;
                tuft.body.localRotation = tuft.rest * Quaternion.Euler(0f, 0f, tuft.current);
            }
        }

        // Static state survives an editor play session when domain reload is disabled (the GameEvents pattern).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            tufts.Clear();
            instance = null;
        }
    }
}
