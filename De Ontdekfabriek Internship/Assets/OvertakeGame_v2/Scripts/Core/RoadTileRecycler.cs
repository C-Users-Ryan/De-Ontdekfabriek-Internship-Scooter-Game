using UnityEngine;
using System.Collections.Generic;

namespace OvertakeGame
{
    /// <summary>
    /// Endless road via tile recycling.
    /// Works correctly alongside WorldOriginShifter — _furthestZ is recomputed
    /// from actual tile positions each frame rather than accumulated, so origin
    /// shifts don't corrupt the placement logic.
    /// </summary>
    public class RoadTileRecycler : MonoBehaviour
    {
        [Header("Road Tiles")]
        [Tooltip("Assign all road tile instances (minimum 3).")]
        public List<Transform> tiles;

        [Tooltip("Length of each tile in world units along Z. Must match your prefab exactly.")]
        public float tileLength = 100f;

        [Tooltip("How far behind the player a tile must be before recycling to the front.")]
        public float recycleOffset = 10f;

        [Header("Player Reference")]
        public Transform playerTransform;

        void Start()
        {
            if (playerTransform == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }

            if (tiles == null || tiles.Count == 0)
                Debug.LogError("[RoadTileRecycler] No tiles assigned!");
        }

        void Update()
        {
            if (playerTransform == null || tiles == null) return;

            // Recompute furthest Z from actual positions every frame.
            // This makes the recycler immune to WorldOriginShifter corrections
            // because it never relies on an accumulated absolute value.
            float furthestZ = float.MinValue;
            foreach (var t in tiles)
                if (t.position.z > furthestZ) furthestZ = t.position.z;

            foreach (var tile in tiles)
            {
                float tileBackEdge = tile.position.z + tileLength;
                if (tileBackEdge < playerTransform.position.z - recycleOffset)
                {
                    furthestZ += tileLength;
                    tile.position = new Vector3(tile.position.x, tile.position.y, furthestZ);
                }
            }
        }
    }
}
