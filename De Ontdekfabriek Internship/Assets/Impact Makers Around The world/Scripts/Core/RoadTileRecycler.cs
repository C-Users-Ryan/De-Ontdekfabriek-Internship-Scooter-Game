using System.Collections.Generic;
using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Endless road — tiles move toward the fixed player at WorldSpeed along RoadDirection.Current.
    /// The rearmost tile is recycled to the front when it passes behind the player.
    ///
    /// WHAT CHANGED FROM THE ORIGINAL:
    ///   - Movement uses RoadDirection.Current instead of Vector3.back → tiles follow turns.
    ///   - Recycling uses a dot-product distance check along the travel axis → works on any axis.
    ///   - Tile placement uses the forward axis → tiles lay correctly after a turn.
    ///   - If a RoadSequencer component exists on this GameObject, tile selection delegates to it.
    ///     Otherwise falls back to weighted random from tilePrefabs (original behaviour).
    ///
    /// TILE PIVOT RULE (unchanged):
    ///   Root pivot at back edge (local Z=0). Front edge at local Z=tileLength.
    ///   Every tile prefab for every country must follow this rule.
    ///
    /// TURN BEHAVIOUR:
    ///   When TurnTrigger fires, RoadDirection.Current changes. From that frame on,
    ///   all tiles move in the new direction. Old tiles (now to the side/behind) are
    ///   recycled quickly; new tiles are placed ahead along the new axis. No extra
    ///   code needed here — the dot-product math handles it automatically.
    /// </summary>
    public class RoadTileRecycler : MonoBehaviour
    {
        [Header("Tile Prefabs (fallback if no RoadSequencer)")]
        [Tooltip("Used when no RoadSequencer is present — original weighted-random behaviour.")]
        public List<GameObject> tilePrefabs;
        public List<float>      tileWeights;

        [Header("Settings")]
        public float tileLength   = 100f;
        public int   poolSize     = 6;
        public float recycleOffset = 10f;

        [Header("Starting Layout")]
        [Tooltip("Z of the first tile's back edge at startup. Player is at Z=0.")]
        public float firstTileStartZ = -20f;

        [Header("Player Reference")]
        public Transform playerTransform;

        // ── Private ───────────────────────────────────────────────────────────

        private struct TileInstance
        {
            public GameObject go;
            public int        prefabIndex; // –1 when driven by sequencer
        }

        private List<TileInstance> _pool = new();
        private RoadSequencer      _sequencer;
        private float              _weightTotal;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        void Start()
        {
            _sequencer = GetComponent<RoadSequencer>();

            if (playerTransform == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }

            if (!_sequencer && (tilePrefabs == null || tilePrefabs.Count == 0))
            {
                Debug.LogError("[RoadTileRecycler] No tile prefabs and no RoadSequencer — nothing to spawn.");
                return;
            }

            BuildWeightTable();
            InitPool();
        }

        void Update()
        {
            if (playerTransform == null || _pool.Count == 0) return;
            if (WorldSpeed.Instance == null || RoadDirection.Instance == null) return;

            float speed         = WorldSpeed.Instance.Current;
            Vector3 travelDir   = RoadDirection.Current;           // world moves in this direction
            Vector3 forwardDir  = -travelDir;                      // "ahead of player" = opposite

            // ── Move all tiles along road direction ──
            for (int i = 0; i < _pool.Count; i++)
                _pool[i].go?.transform.Translate(travelDir * speed * Time.deltaTime, Space.World);

            // ── Recycle tiles that have passed behind the player ──
            float playerAhead  = Vector3.Dot(playerTransform.position, forwardDir);
            float recycleLine  = playerAhead - recycleOffset;  // behind this point = recycle

            float furthestAhead = GetFurthestAheadPos(forwardDir);

            for (int i = 0; i < _pool.Count; i++)
            {
                var inst = _pool[i];
                if (inst.go == null) continue;

                // Front edge of this tile along travel axis
                float tileFront = Vector3.Dot(inst.go.transform.position, forwardDir) + tileLength;
                if (tileFront >= recycleLine) continue; // still in play

                // ── Recycle ──
                furthestAhead += tileLength;
                inst.go.transform.position = forwardDir * furthestAhead;
                inst.go.transform.position = new Vector3(
                    inst.go.transform.position.x,
                    0f,
                    inst.go.transform.position.z);

                // ── Pick new tile ──
                SwapTileIfNeeded(ref inst);

                _pool[i] = inst;
            }
        }

        // ── Pool initialisation ───────────────────────────────────────────────

        private void InitPool()
        {
            Vector3 forwardDir = -RoadDirection.Current;
            float   z          = firstTileStartZ;

            for (int i = 0; i < poolSize; i++)
            {
                var entry = GetNextEntry(out int prefabIdx);
                if (entry == null && tilePrefabs.Count == 0) break;

                GameObject prefab = entry?.prefab ?? tilePrefabs[prefabIdx];
                var go = Instantiate(prefab, transform);
                go.transform.position = forwardDir * z;
                go.transform.position = new Vector3(
                    go.transform.position.x, 0f, go.transform.position.z);

                _pool.Add(new TileInstance { go = go, prefabIndex = entry != null ? -1 : prefabIdx });
                z += tileLength;
            }
        }

        // ── Tile selection ────────────────────────────────────────────────────

        private RoadSequence.TileEntry GetNextEntry(out int fallbackIdx)
        {
            fallbackIdx = -1;
            if (_sequencer != null)
                return _sequencer.GetNextTileEntry();

            // Fallback: weighted random from tilePrefabs
            fallbackIdx = PickWeightedRandom();
            return null;
        }

        private void SwapTileIfNeeded(ref TileInstance inst)
        {
            var entry = GetNextEntry(out int fallbackIdx);
            GameObject newPrefab = entry?.prefab ?? (tilePrefabs.Count > 0 ? tilePrefabs[fallbackIdx] : null);
            if (newPrefab == null) return;

            // Only swap if prefab changed
            bool isNew = (entry != null) ? (inst.go.name != newPrefab.name + "(Clone)") : (inst.prefabIndex != fallbackIdx);
            if (isNew)
            {
                var savedPos = inst.go.transform.position;
                Destroy(inst.go);
                inst.go = Instantiate(newPrefab, transform);
                inst.go.transform.position = savedPos;
                inst.prefabIndex = fallbackIdx;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private float GetFurthestAheadPos(Vector3 forwardDir)
        {
            float max = float.MinValue;
            foreach (var inst in _pool)
            {
                if (inst.go == null) continue;
                float v = Vector3.Dot(inst.go.transform.position, forwardDir);
                if (v > max) max = v;
            }
            return max;
        }

        private void BuildWeightTable()
        {
            _weightTotal = 0f;
            if (tilePrefabs == null) return;
            if (tileWeights == null || tileWeights.Count != tilePrefabs.Count)
            {
                tileWeights = new List<float>();
                for (int i = 0; i < tilePrefabs.Count; i++) tileWeights.Add(1f);
            }
            foreach (var w in tileWeights) _weightTotal += Mathf.Max(0f, w);
        }

        private int PickWeightedRandom()
        {
            if (_weightTotal <= 0f) return 0;
            float r = Random.Range(0f, _weightTotal), c = 0f;
            for (int i = 0; i < tileWeights.Count; i++)
            {
                c += tileWeights[i];
                if (r <= c) return i;
            }
            return tilePrefabs.Count - 1;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (_pool == null) return;
            UnityEditor.Handles.color = new Color(1f, 0.6f, 0f, 0.4f);
            foreach (var inst in _pool)
            {
                if (inst.go == null) continue;
                Vector3 forwardDir = -RoadDirection.Current;
                Vector3 centre = inst.go.transform.position + forwardDir * tileLength * 0.5f;
                centre.y += 0.1f;
                UnityEditor.Handles.DrawWireCube(centre, new Vector3(10f, 0.1f, tileLength));
            }
        }
#endif
    }
}
