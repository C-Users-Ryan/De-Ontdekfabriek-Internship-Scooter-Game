using System.Collections.Generic;
using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Endless road — tiles move toward the fixed player at WorldSpeed.
    /// Rearmost tile is recycled to the front when its front edge passes behind the player.
    /// Supports any number of tile prefab variants with weighted random selection.
    ///
    /// TILE PIVOT RULE: root pivot at Z=0 (back edge), front edge at Z=tileLength.
    /// </summary>
    public class RoadTileRecycler : MonoBehaviour
    {
        [Header("Tile Prefabs")]
        public List<GameObject> tilePrefabs;
        [Tooltip("Relative weight per prefab — leave empty for equal weighting.")]
        public List<float> tileWeights;

        [Header("Settings")]
        public float tileLength = 100f;
        public int poolSize = 5;
        public float recycleOffset = 10f;

        [Header("Starting Layout")]
        [Tooltip("Z of first tile back edge. Player is at Z=0. Default -20 = road starts behind player.")]
        public float firstTileStartZ = -20f;

        [Header("Player Reference (auto-found if null)")]
        public Transform playerTransform;

        [Header("Sequencer (optional)")]
        [Tooltip("When assigned, tile selection is delegated to the RoadSequencer instead of the weighted list above.")]
        public RoadSequencer sequencer;

        private List<TileInstance> _pool = new();

        /// <summary>
        /// Cumulative world heading in degrees. 0 = forward (+Z).
        /// Updated when tiles with a TileDefinition curveAngle are recycled.
        /// </summary>
        public float CurrentHeadingDeg { get; private set; }

        private float _weightTotal;

        private struct TileInstance { public GameObject go; public GameObject sourcePrefab; }

        void Start()
        {
            if (playerTransform == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }
            if (sequencer == null && (tilePrefabs == null || tilePrefabs.Count == 0)) { Debug.LogError("[RoadTileRecycler] No tile prefabs and no sequencer!"); return; }
            BuildWeightTable();
            InitPool();
        }

        void Update()
        {
            if (playerTransform == null || _pool.Count == 0 || WorldSpeed.Instance == null) return;

            float speed = WorldSpeed.Instance.Current;
            Vector3 aheadAxis   = -RoadDirection.Current;
            float playerAhead   = Vector3.Dot(playerTransform.position, aheadAxis);
            float recycleLine   = playerAhead - recycleOffset;

            // Move all tiles toward player
            for (int i = 0; i < _pool.Count; i++)
                _pool[i].go?.transform.Translate(RoadDirection.Current * speed * Time.deltaTime, Space.World);

            // Find furthest back edge before recycling
            float furthestBackEdge = GetFurthestBackEdge(aheadAxis);

            for (int i = 0; i < _pool.Count; i++)
            {
                var inst = _pool[i];
                if (inst.go == null) continue;
                float frontEdge = Vector3.Dot(inst.go.transform.position, aheadAxis) + tileLength;
                if (frontEdge >= recycleLine) continue;

                GameObject nextPrefab = sequencer != null
                    ? sequencer.NextTile()
                    : tilePrefabs[PickWeightedRandom()];

                if (nextPrefab == null) continue;

                // Accumulate heading from TileDefinition if present
                var def = nextPrefab.GetComponent<TileDefinition>();
                if (def != null) CurrentHeadingDeg += def.curveAngle;

                if (nextPrefab != inst.sourcePrefab)
                {
                    Destroy(inst.go);
                    inst.go = Instantiate(nextPrefab, transform);
                    inst.sourcePrefab = nextPrefab;
                }
                furthestBackEdge += tileLength;
                inst.go.transform.position = aheadAxis * furthestBackEdge;
                _pool[i] = inst;
            }
        }

        private void InitPool()
        {
            Vector3 aheadAxis = -RoadDirection.Current;
            float   dist      = firstTileStartZ;   // reused as "distance ahead" at start (Z value)
            for (int i = 0; i < poolSize; i++)
            {
                GameObject prefab = sequencer != null
                    ? sequencer.NextTile()
                    : tilePrefabs[PickWeightedRandom()];

                if (prefab == null && tilePrefabs.Count > 0) prefab = tilePrefabs[0];
                if (prefab == null) continue;

                var go = Instantiate(prefab, transform);
                go.transform.position = aheadAxis * dist;
                _pool.Add(new TileInstance { go = go, sourcePrefab = prefab });
                dist += tileLength;
            }
        }

        private float GetFurthestBackEdge(Vector3 aheadAxis)
        {
            float f = float.MinValue;
            foreach (var inst in _pool)
            {
                if (inst.go == null) continue;
                float d = Vector3.Dot(inst.go.transform.position, aheadAxis);
                if (d > f) f = d;
            }
            return f;
        }

        private void BuildWeightTable()
        {
            _weightTotal = 0f;
            if (tileWeights == null || tileWeights.Count != tilePrefabs.Count)
            {
                tileWeights = new List<float>();
                for (int i = 0; i < tilePrefabs.Count; i++) tileWeights.Add(1f);
            }
            foreach (var w in tileWeights) _weightTotal += Mathf.Max(0f, w);
        }

        private int PickWeightedRandom()
        {
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
                UnityEditor.Handles.DrawWireCube(
                    inst.go.transform.position + new Vector3(0f, 0.1f, tileLength * 0.5f),
                    new Vector3(10f, 0.1f, tileLength));
            }
        }
#endif
    }
}