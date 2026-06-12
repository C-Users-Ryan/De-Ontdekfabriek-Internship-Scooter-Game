using System.Collections.Generic;
using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Endless road — tiles scroll toward the fixed player at WorldSpeed along RoadDirection.Current.
    /// The rearmost tile is recycled to the front when it passes behind the player.
    ///
    /// POOL ARCHITECTURE — no Instantiate/Destroy during play
    ///   At Start(), every prefab in allTilePrefabs is pre-instantiated poolSize times.
    ///   Recycling deactivates the current tile (returning it to its prefab's queue)
    ///   and activates a fresh tile from the next prefab's queue.
    ///
    /// TILE PIVOT RULE
    ///   Root pivot at back edge (local Z=0). Front edge at local Z=tileLength.
    ///   All tile prefabs must follow this convention.
    ///
    /// SEQUENCER INTEGRATION
    ///   If a RoadSequencer component is on this GameObject, tile selection delegates to it.
    ///   Otherwise falls back to weighted random from tilePrefabs.
    ///   The Designer must list ALL prefabs used by any sequence in allTilePrefabs
    ///   (or tilePrefabs) so they are pooled at start.
    ///
    /// TURN BEHAVIOUR
    ///   RoadDirection.Current changes when TurnTrigger fires. From that frame all tiles
    ///   automatically scroll in the new direction — the dot-product math handles it.
    /// </summary>
    public class RoadTileRecycler : MonoBehaviour
    {
        [Header("Tile Prefabs")]
        [Tooltip("All tile prefabs that may appear during a session. " +
                 "Include prefabs from RoadSequencer sequences. All are pooled at start.")]
        public List<GameObject> allTilePrefabs;

        [Header("Weighted Random Fallback (no RoadSequencer)")]
        [Tooltip("Per-prefab spawn weights for allTilePrefabs when no sequencer is present.")]
        public List<float> tileWeights;

        [Header("Settings")]
        public float tileLength    = 100f;
        [Tooltip("Number of active tiles visible at once. Also the per-prefab pool size.")]
        public int   poolSize      = 6;
        [Tooltip("How far behind the player (world units) before a tile is recycled.")]
        public float recycleOffset = 10f;

        [Header("Starting Layout")]
        [Tooltip("Travel-axis offset of the first tile's back edge at startup. Player is at 0.")]
        public float firstTileOffset = -20f;

        [Header("Player Reference")]
        public Transform playerTransform;

        // ── Private ────────────────────────────────────────────────────────────────

        private struct ActiveTile
        {
            public GameObject go;
            public GameObject prefab;
        }

        private List<ActiveTile>                        _active  = new();
        private Dictionary<GameObject, Queue<GameObject>> _pools  = new();
        private RoadSequencer                            _sequencer;
        private float                                    _weightTotal;

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        void Start()
        {
            _sequencer = GetComponent<RoadSequencer>();

            if (playerTransform == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }

            if (allTilePrefabs == null || allTilePrefabs.Count == 0)
            {
                Debug.LogError("[RoadTileRecycler] allTilePrefabs is empty — nothing to spawn.");
                return;
            }

            BuildWeightTable();
            PrewarmPools();
            InitialLayout();
        }

        void Update()
        {
            if (playerTransform == null || _active.Count == 0) return;
            if (WorldSpeed.Instance == null || RoadDirection.Instance == null) return;

            float   speed      = WorldSpeed.Instance.Current;
            Vector3 travelDir  = RoadDirection.Current;
            Vector3 forwardDir = -travelDir;

            for (int i = 0; i < _active.Count; i++)
            {
                var t = _active[i];
                if (t.go != null)
                    t.go.transform.Translate(travelDir * speed * Time.deltaTime, Space.World);
            }

            float playerAhead  = Vector3.Dot(playerTransform.position, forwardDir);
            float recycleLine  = playerAhead - recycleOffset;

            for (int i = 0; i < _active.Count; i++)
            {
                var inst = _active[i];
                if (inst.go == null) continue;

                float tileFront = Vector3.Dot(inst.go.transform.position, forwardDir) + tileLength;
                if (tileFront >= recycleLine) continue;

                float ahead = GetFurthestAheadPos(forwardDir) + tileLength;

                // Return old tile to pool
                ReturnToPool(inst.prefab, inst.go);

                // Pick next tile
                GameObject nextPrefab = PickNextPrefab();
                GameObject nextGo     = TakeFromPool(nextPrefab);
                if (nextGo == null) continue;

                nextGo.transform.position = SetAtAhead(forwardDir, ahead);
                nextGo.SetActive(true);
                _active[i] = new ActiveTile { go = nextGo, prefab = nextPrefab };
            }
        }

        // ── Pool operations ────────────────────────────────────────────────────────

        private void PrewarmPools()
        {
            foreach (var prefab in allTilePrefabs)
            {
                if (prefab == null || _pools.ContainsKey(prefab)) continue;
                var queue = new Queue<GameObject>();
                for (int i = 0; i < poolSize; i++)
                {
                    var go = Instantiate(prefab, transform);
                    go.SetActive(false);
                    queue.Enqueue(go);
                }
                _pools[prefab] = queue;
            }
        }

        private GameObject TakeFromPool(GameObject prefab)
        {
            if (prefab == null) return null;
            if (_pools.TryGetValue(prefab, out var queue) && queue.Count > 0)
                return queue.Dequeue();
            Debug.LogWarning($"[RoadTileRecycler] Pool exhausted for {prefab.name}. " +
                              "Increase poolSize or add more entries to allTilePrefabs.");
            return null;
        }

        private void ReturnToPool(GameObject prefab, GameObject go)
        {
            go.SetActive(false);
            if (prefab != null && _pools.TryGetValue(prefab, out var queue))
                queue.Enqueue(go);
        }

        // ── Initial layout ────────────────────────────────────────────────────────

        private void InitialLayout()
        {
            Vector3 forwardDir = -RoadDirection.Current;
            float   offset     = firstTileOffset;

            for (int i = 0; i < poolSize; i++)
            {
                GameObject prefab = PickNextPrefab();
                GameObject go     = TakeFromPool(prefab);
                if (go == null) break;

                go.transform.position = SetAtAhead(forwardDir, offset);
                go.SetActive(true);
                _active.Add(new ActiveTile { go = go, prefab = prefab });
                offset += tileLength;
            }
        }

        // ── Tile selection ─────────────────────────────────────────────────────────

        private GameObject PickNextPrefab()
        {
            if (_sequencer != null)
            {
                var entry = _sequencer.GetNextTileEntry();
                if (entry?.prefab != null) return entry.prefab;
            }
            return PickWeightedRandom();
        }

        private float GetFurthestAheadPos(Vector3 forwardDir)
        {
            float max = float.MinValue;
            foreach (var inst in _active)
            {
                if (inst.go == null) continue;
                float v = Vector3.Dot(inst.go.transform.position, forwardDir);
                if (v > max) max = v;
            }
            return max;
        }

        private static Vector3 SetAtAhead(Vector3 forwardDir, float ahead)
        {
            var pos = forwardDir * ahead;
            pos.y = 0f;
            return pos;
        }

        private void BuildWeightTable()
        {
            _weightTotal = 0f;
            if (allTilePrefabs == null) return;
            if (tileWeights == null || tileWeights.Count != allTilePrefabs.Count)
            {
                tileWeights = new List<float>();
                for (int i = 0; i < allTilePrefabs.Count; i++) tileWeights.Add(1f);
            }
            foreach (var w in tileWeights) _weightTotal += Mathf.Max(0f, w);
        }

        private GameObject PickWeightedRandom()
        {
            if (_weightTotal <= 0f || allTilePrefabs.Count == 0) return allTilePrefabs[0];
            float r = Random.Range(0f, _weightTotal), c = 0f;
            for (int i = 0; i < tileWeights.Count; i++)
            {
                c += tileWeights[i];
                if (r <= c) return allTilePrefabs[i];
            }
            return allTilePrefabs[^1];
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (_active == null) return;
            UnityEditor.Handles.color = new Color(1f, 0.6f, 0f, 0.4f);
            Vector3 forwardDir = RoadDirection.Instance != null ? -RoadDirection.Current : Vector3.forward;
            foreach (var inst in _active)
            {
                if (inst.go == null) continue;
                Vector3 centre = inst.go.transform.position + forwardDir * tileLength * 0.5f;
                centre.y += 0.1f;
                UnityEditor.Handles.DrawWireCube(centre, new Vector3(10f, 0.1f, tileLength));
            }
        }
#endif
    }
}
