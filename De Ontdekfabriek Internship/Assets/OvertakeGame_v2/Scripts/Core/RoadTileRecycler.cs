using Mono.Cecil;
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

        private List<TileInstance> _pool = new();

        /// <summary>
        /// Cumulative world heading in degrees. 0 = forward (+Z).
        /// Updated when tiles with a TileDefinition curveAngle are recycled.
        /// </summary>
        public float CurrentHeadingDeg { get; private set; }

        private float _weightTotal;

        private struct TileInstance { public GameObject go; public int prefabIndex; }

        void Start()
        {
            if (playerTransform == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }
            if (tilePrefabs == null || tilePrefabs.Count == 0) { Debug.LogError("[RoadTileRecycler] No tile prefabs!"); return; }
            BuildWeightTable();
            InitPool();
        }

        void Update()
        {
            if (playerTransform == null || _pool.Count == 0 || WorldSpeed.Instance == null) return;

            float speed = WorldSpeed.Instance.Current;
            float recycleLine = playerTransform.position.z - recycleOffset;

            // Move all tiles toward player
            for (int i = 0; i < _pool.Count; i++)
                _pool[i].go?.transform.Translate(Vector3.back * speed * Time.deltaTime, Space.World);

            // Find furthest back edge before recycling
            float furthestBackEdge = GetFurthestBackEdge();

            for (int i = 0; i < _pool.Count; i++)
            {
                var inst = _pool[i];
                if (inst.go == null) continue;
                float frontEdge = inst.go.transform.position.z + tileLength;
                if (frontEdge >= recycleLine) continue;

                int newIdx = PickWeightedRandom();

                // Accumulate heading from TileDefinition if present
                var def = tilePrefabs[newIdx].GetComponent<TileDefinition>();
                if (def != null) CurrentHeadingDeg += def.curveAngle;

                if (newIdx != inst.prefabIndex)
                {
                    Destroy(inst.go);
                    inst.go = Instantiate(tilePrefabs[newIdx], transform);
                    inst.prefabIndex = newIdx;
                }
                furthestBackEdge += tileLength;
                inst.go.transform.position = new Vector3(0f, 0f, furthestBackEdge);
                _pool[i] = inst;
            }
        }

        private void InitPool()
        {
            float z = firstTileStartZ;
            for (int i = 0; i < poolSize; i++)
            {
                int idx = PickWeightedRandom();
                var go = Instantiate(tilePrefabs[idx], transform);
                go.transform.position = new Vector3(0f, 0f, z);
                _pool.Add(new TileInstance { go = go, prefabIndex = idx });
                z += tileLength;
            }
        }

        private float GetFurthestBackEdge()
        {
            float f = float.MinValue;
            foreach (var inst in _pool)
                if (inst.go != null && inst.go.transform.position.z > f)
                    f = inst.go.transform.position.z;
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