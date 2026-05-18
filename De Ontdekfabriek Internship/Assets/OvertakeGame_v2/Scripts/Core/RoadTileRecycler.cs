using UnityEngine;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Endless road via tile recycling — supports any number of tile prefab variants.
    ///
    /// HOW IT WORKS:
    ///   A pool of tile INSTANCES moves toward the fixed player at WorldSpeed.
    ///   When a tile's back edge passes the player, it is recycled to the front
    ///   of the queue and replaced with a randomly chosen tile variant.
    ///   The player's Z is fixed — tiles come to the player, not the other way.
    ///
    /// TILE PREFAB RULES:
    ///   - Root pivot must be at Z = 0 (back edge of the tile).
    ///   - Front edge at Z = tileLength.
    ///   - All props are children of the root.
    ///   - Each prefab type is a separate entry in tilePrefabs.
    ///
    /// SETUP:
    ///   1. Create your tile prefabs following the pivot rule above.
    ///   2. Add this component to any persistent scene GameObject.
    ///   3. Set tileLength to match your prefab Z size (e.g. 100).
    ///   4. Set poolSize to at least 4 (3 minimum; more = smoother on slow devices).
    ///   5. Add all tile prefab variants to the tilePrefabs list.
    ///   6. Optionally weight certain tiles to appear more often via tileWeights.
    /// </summary>
    public class RoadTileRecycler : MonoBehaviour
    {
        [Header("Tile Prefabs")]
        [Tooltip("All tile prefab variants. Add as many as you like. " +
                 "Each entry's pivot must sit at Z=0 (back edge).")]
        public List<GameObject> tilePrefabs;

        [Tooltip("Relative spawn weight per prefab. Index matches tilePrefabs. " +
                 "Leave empty to weight all tiles equally. " +
                 "Example: [3,1,1] makes the first tile 3x more likely than the others.")]
        public List<float> tileWeights;

        [Header("Tile Settings")]
        [Tooltip("Z length of each tile in world units. Must match your prefab exactly.")]
        public float tileLength = 100f;

        [Tooltip("How many tile instances to keep in the pool. " +
                 "Must be enough to always cover the player's visible range. " +
                 "At least 4 recommended; increase if you see gaps.")]
        public int poolSize = 5;

        [Tooltip("How far behind the player before a tile is recycled. " +
                 "Keep small (5-15) so recycled tiles aren't visible ahead yet.")]
        public float recycleOffset = 10f;

        [Header("Starting Layout")]
        [Tooltip("World Z position where the first tile's back edge sits at game start. " +
                 "Set this so the player's start position is covered. " +
                 "Example: if player is at Z=0 and tileLength=100, set to -50 so the " +
                 "player starts mid-tile. Or set to player Z - tileLength for a full tile behind.")]
        public float firstTileStartZ = -50f;

        [Header("Player Reference")]
        public Transform playerTransform;

        // ── Internal pool ──────────────────────────────────────────────────────
        private List<TileInstance> _pool = new List<TileInstance>();
        private float              _weightTotal;

        private struct TileInstance
        {
            public GameObject go;
            public int        prefabIndex; // which variant this instance currently is
        }

        void Start()
        {
            if (playerTransform == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }

            if (tilePrefabs == null || tilePrefabs.Count == 0)
            {
                Debug.LogError("[RoadTileRecycler] No tile prefabs assigned!");
                return;
            }

            BuildWeightTable();
            InitPool();
        }

        void Update()
        {
            if (playerTransform == null || _pool.Count == 0) return;
            if (WorldSpeed.Instance == null) return;

            float speed = WorldSpeed.Instance.Current;

            // Move all tiles toward the player
            for (int i = 0; i < _pool.Count; i++)
            {
                var inst = _pool[i];
                if (inst.go == null) continue;
                inst.go.transform.position += Vector3.back * speed * Time.deltaTime;
            }

            // Recycle any tile whose back edge has passed the player
            float furthestZ = GetFurthestFrontZ();

            for (int i = 0; i < _pool.Count; i++)
            {
                var inst = _pool[i];
                if (inst.go == null) continue;

                // Back edge = tile root Z (pivot is at back edge)
                // Front edge = tile root Z + tileLength
                float backEdge = inst.go.transform.position.z;
                if (backEdge + tileLength < playerTransform.position.z - recycleOffset)
                {
                    // Pick a new random variant
                    int newIdx = PickWeightedRandom();

                    // Swap the GameObject if it's a different variant
                    if (newIdx != inst.prefabIndex)
                    {
                        Destroy(inst.go);
                        inst.go         = Instantiate(tilePrefabs[newIdx], transform);
                        inst.prefabIndex = newIdx;
                        _pool[i]        = inst;
                    }

                    // Place at front of queue
                    furthestZ += tileLength;
                    inst.go.transform.position = new Vector3(
                        inst.go.transform.position.x,
                        inst.go.transform.position.y,
                        furthestZ);
                    _pool[i] = inst;
                }
            }
        }

        // ── Init ──────────────────────────────────────────────────────────────
        private void InitPool()
        {
            float z = firstTileStartZ;

            for (int i = 0; i < poolSize; i++)
            {
                int prefabIdx = PickWeightedRandom();
                var go        = Instantiate(tilePrefabs[prefabIdx], transform);
                go.transform.position = new Vector3(0f, 0f, z);
                z += tileLength;

                _pool.Add(new TileInstance { go = go, prefabIndex = prefabIdx });
            }
        }

        // ── Weighted random pick ──────────────────────────────────────────────
        private void BuildWeightTable()
        {
            _weightTotal = 0f;

            // If weights not configured, equal weight for all
            if (tileWeights == null || tileWeights.Count != tilePrefabs.Count)
            {
                tileWeights = new List<float>();
                for (int i = 0; i < tilePrefabs.Count; i++)
                    tileWeights.Add(1f);
            }

            foreach (var w in tileWeights) _weightTotal += Mathf.Max(0f, w);
        }

        private int PickWeightedRandom()
        {
            float r = Random.Range(0f, _weightTotal);
            float cumulative = 0f;
            for (int i = 0; i < tileWeights.Count; i++)
            {
                cumulative += tileWeights[i];
                if (r <= cumulative) return i;
            }
            return tilePrefabs.Count - 1;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private float GetFurthestFrontZ()
        {
            float furthest = float.MinValue;
            foreach (var inst in _pool)
            {
                if (inst.go == null) continue;
                float frontZ = inst.go.transform.position.z + tileLength;
                if (frontZ > furthest) furthest = frontZ;
            }
            return furthest - tileLength; // return front of furthest tile as base
        }

        // ── Editor helper: visualise tile boundaries in Scene view ────────────
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (_pool == null) return;
            UnityEditor.Handles.color = new Color(1f, 0.6f, 0f, 0.4f);
            foreach (var inst in _pool)
            {
                if (inst.go == null) continue;
                Vector3 pos = inst.go.transform.position;
                UnityEditor.Handles.DrawWireCube(
                    pos + new Vector3(0f, 0f, tileLength * 0.5f),
                    new Vector3(10f, 0.1f, tileLength));
            }
        }
#endif
    }
}
