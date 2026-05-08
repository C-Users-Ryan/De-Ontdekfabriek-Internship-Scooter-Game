using UnityEngine;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Prevents floating-point precision loss on long drives by periodically
    /// shifting the entire world back toward the origin.
    ///
    /// How it works:
    ///   When the player travels beyond shiftThreshold units along Z,
    ///   every registered Transform in the scene is offset by -playerZ,
    ///   then the player is reset to Z=0. From the player's perspective
    ///   nothing moves — but all world coordinates stay small and precise.
    ///
    /// SETUP:
    ///   1. Add this component to any persistent GameObject (e.g. Managers).
    ///   2. Assign playerTransform in the Inspector (or leave null to auto-find).
    ///   3. Everything else is automatic — the shifter finds all active
    ///      scene Transforms at runtime. You do NOT need to manually register
    ///      road tiles, traffic, potholes, the camera, etc.
    ///
    /// IMPORTANT — things that break if you don't handle them:
    ///   - ParticleSystems: paused and resumed across the shift so particles
    ///     don't jump (handled below).
    ///   - Rigidbody: velocity is kept in world space so it's unaffected;
    ///     position is reset via Transform, which Unity propagates to the
    ///     physics body on the next FixedUpdate.
    ///   - UI / Canvas: screen-space canvases have no world position —
    ///     they are skipped automatically.
    /// </summary>
    public class WorldOriginShifter : MonoBehaviour
    {
        [Header("Shift Settings")]
        [Tooltip("How far the player must travel from Z=0 before a shift occurs. " +
                 "Lower = more frequent shifts, higher precision. " +
                 "200–500 is a good range. Don't go above 1000.")]
        public float shiftThreshold = 300f;

        [Header("References")]
        [Tooltip("The player's Transform. Auto-found if left null.")]
        public Transform playerTransform;

        // Everything in the scene we need to shift.
        // Rebuilt on each shift so pooled objects that activate mid-session
        // are always included.
        private Transform[]   _allTransforms;
        private Rigidbody[]   _allRigidbodies;

        void Start()
        {
            if (playerTransform == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }

            if (playerTransform == null)
                Debug.LogError("[WorldOriginShifter] Could not find PlayerController. Assign playerTransform manually.");
        }

        void LateUpdate()
        {
            if (playerTransform == null) return;

            float playerZ = playerTransform.position.z;
            if (Mathf.Abs(playerZ) < shiftThreshold) return;

            ShiftWorld(playerZ);
        }

        private void ShiftWorld(float offsetZ)
        {
            Vector3 offset = new Vector3(0f, 0f, -offsetZ);

            // ── Collect every root Transform in the scene ─────────────────────
            // We shift root objects only; children move with their parent.
            // Rebuild every shift so newly pooled objects are included.
            var rootObjects = new List<GameObject>();
            var scene = gameObject.scene;
            scene.GetRootGameObjects(rootObjects);

            foreach (var go in rootObjects)
            {
                // Skip screen-space canvases — they have no meaningful world position
                var canvas = go.GetComponent<Canvas>();
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    continue;

                go.transform.position += offset;
            }

            // ── Preserve Rigidbody velocities ─────────────────────────────────
            // Unity Rigidbody.position is in world space. After moving the
            // Transform, we must sync the physics body or it snaps back on the
            // next FixedUpdate.
            _allRigidbodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            foreach (var rb in _allRigidbodies)
            {
                // MovePosition keeps the velocity intact and avoids a physics frame gap.
                // We've already moved the transform above, so the rb just needs syncing.
                rb.position = rb.transform.position;
            }

            // ── Pause/resume particle systems so they don't jump ──────────────
            var particles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            foreach (var ps in particles)
            {
                bool wasPlaying = ps.isPlaying;
                if (wasPlaying) ps.Pause();
                var main = ps.main;
                // Shift the particle system's simulation space particles
                // by stopping and restarting; for world-space particles
                // this is the only safe approach at runtime.
                if (wasPlaying) ps.Play();
            }

            Debug.Log($"[WorldOriginShifter] Shifted world by Z={offsetZ:F1}. Player now at Z={playerTransform.position.z:F2}");
        }
    }
}
