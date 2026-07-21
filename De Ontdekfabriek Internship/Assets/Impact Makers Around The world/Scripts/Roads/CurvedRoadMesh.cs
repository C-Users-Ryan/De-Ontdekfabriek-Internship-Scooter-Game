using System.Collections.Generic;
using UnityEngine;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// Generates a tile's road surface (and an optional ground/verge strip) along the EXACT line the tile
    /// drives — it samples the sibling <see cref="RoadTile.EvaluateRun"/>, so the painted road and the driven
    /// road cannot disagree. Straight tiles get a straight strip, tiles with turns get the bend, multiple
    /// turns per tile just work. Optional: a tile with a hand-modelled road mesh simply doesn't add this.
    ///
    /// The optional ground strip (set Ground Width > 0) follows the same line, so a corner's ground is a strip
    /// ALONG the road rather than a big square. Road is sub-mesh 0, ground is sub-mesh 1, so put two materials
    /// on the MeshRenderer: element 0 = road, element 1 = ground.
    ///
    /// Setup: put this on a tile with a MeshFilter + MeshRenderer + RoadTile; set Road Width (and Ground Width)
    /// to match your straight tiles so the seams line up. It rebuilds live in the editor as you edit the tile,
    /// and the sequencer rebuilds it at every spawn.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class CurvedRoadMesh : MonoBehaviour
    {
        [Header("Road strip")]
        [Tooltip("Width of the drivable road strip in metres. Match this to your straight road tiles.")]
        [SerializeField] private float roadWidth = 8f;
        [Tooltip("Samples along the tile. More = smoother bends. 16 is plenty for a 90 degree turn.")]
        [SerializeField] private int segments = 16;
        [Tooltip("Metres of road per texture repeat along the length (V axis). Match your straight road material.")]
        [SerializeField] private float uvLengthPerTile = 8f;

        [Header("Ground / verge strip (sub-mesh 1, second material)")]
        [Tooltip("Total width of the ground/verge strip in metres (a bit wider than the road). 0 = no ground, road only. It is automatically capped near a bend's radius so a tight turn can never fold its inner edge through the centre.")]
        [SerializeField] private float groundWidth = 16f;
        [Tooltip("How far the ground sits below the road surface, to avoid z-fighting.")]
        [SerializeField] private float groundDrop = 0.06f;

        [Header("Options")]
        [Tooltip("If a surface renders dark/invisible from above, tick this to reverse the triangle winding.")]
        [SerializeField] private bool flipFaces = false;

        private Mesh mesh;
        private MeshFilter meshFilter;
        private RoadTile activeTile; // set during BuildFromTile so AddStrip can convert metres → tile local
        private bool builtAtRuntime; // build ONCE per pooled instance in play mode — see BuildFromTile
        private readonly List<Vector3> verts = new List<Vector3>(160);
        private readonly List<Vector3> normals = new List<Vector3>(160);
        private readonly List<Vector2> uvs = new List<Vector2>(160);
        private readonly List<int> roadTris = new List<int>(240);
        private readonly List<int> groundTris = new List<int>(240);
        private readonly List<Vector3> centres = new List<Vector3>(40);
        private readonly List<Vector3> rights = new List<Vector3>(40);
        private readonly List<float> uList = new List<float>(40);

        private void Awake() => Preview();
        private void OnEnable() => Preview();
#if UNITY_EDITOR
        private void OnValidate() { if (!Application.isPlaying) Preview(); }
#endif

        [ContextMenu("Rebuild Mesh")]
        private void Preview()
        {
            var tile = GetComponent<RoadTile>();
            if (tile != null) BuildFromTile(tile);
            else EnsureMesh();
        }

        /// <summary>
        /// (Re)builds the strip along the tile's driven line, sampled through the same
        /// <see cref="RoadTile.EvaluateRun"/> the sequencer rides. Samples are uniform along the tile PLUS
        /// the exact green/red ball positions of every turn, so the tangent breaks sit precisely on the
        /// authored begin/end points. The sequencer calls this at every spawn; the editor calls it live.
        /// </summary>
        public void BuildFromTile(RoadTile tile)
        {
            // In play the strip is DETERMINISTIC per instance — it samples only the tile's authored geometry,
            // which never changes at runtime — yet every pooled respawn rebuilt it TWICE (OnEnable's Preview +
            // the sequencer's explicit call). On tile-heavy stretches those redundant rebuilds landed on the
            // same frame as the tile activation itself and fed the load hitch, so: build once, then keep it.
            // The editor path stays live (OnValidate/Preview rebuild while authoring).
            if (Application.isPlaying)
            {
                if (builtAtRuntime)
                    return;
                builtAtRuntime = true;
            }
            EnsureMesh();
            float length = tile != null ? Mathf.Max(0f, tile.length) : 0f;
            if (length <= 0.0001f) { mesh.Clear(); return; }

            int count = Mathf.Max(2, segments * 2);
            uList.Clear();
            for (int i = 0; i <= count; i++)
                uList.Add((float)i / count * length);
            int uniform = uList.Count;
            tile.AppendTurnBoundaries(uList);
            for (int i = uniform; i < uList.Count; i++)
                uList[i] = Mathf.Clamp(uList[i], 0f, length);
            uList.Sort();
            int keep = 0; // drop near-duplicate samples (a ball that landed on a uniform step)
            for (int i = 0; i < uList.Count; i++)
                if (keep == 0 || uList[i] - uList[keep - 1] > 0.001f)
                    uList[keep++] = uList[i];
            uList.RemoveRange(keep, uList.Count - keep);

            // Sample the driven line in real METRES (widths and UVs are metres), then convert each vertex
            // into the tile's local space at emit time — the mesh lives on the tile's (possibly scaled)
            // transform, so a ×20 root must not scale the road strip twice.
            Quaternion facing = tile.ArtFacing;
            activeTile = tile;
            centres.Clear(); rights.Clear();
            for (int i = 0; i < uList.Count; i++)
            {
                tile.EvaluateRun(uList[i], out Vector3 runPos, out Quaternion runRot);
                centres.Add(tile.RunToMetres(runPos));
                rights.Add(facing * runRot * Vector3.right);
            }

            // The tightest bend's radius caps the half-width so a sharp turn never folds its inner edge.
            float minRadius = tile.MinBendRadius();
            EmitStrips(minRadius < float.MaxValue ? minRadius * 0.92f : float.MaxValue);
            activeTile = null;
        }

        /// <summary>Emits the road (and optional ground) strip from the already-sampled centres/rights/uList,
        /// capping each half-width at <paramref name="maxHalf"/> so a tight bend never folds its inner edge.</summary>
        private void EmitStrips(float maxHalf)
        {
            verts.Clear(); normals.Clear(); uvs.Clear(); roadTris.Clear(); groundTris.Clear();
            bool hasGround = groundWidth > 0.01f;
            if (hasGround) AddStrip(Mathf.Min(groundWidth * 0.5f, maxHalf), -groundDrop, groundTris); // ground, below
            AddStrip(Mathf.Min(roadWidth * 0.5f, maxHalf), 0f, roadTris);                            // road, on top

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = hasGround ? 2 : 1;
            mesh.SetTriangles(roadTris, 0);
            if (hasGround) mesh.SetTriangles(groundTris, 1);
            mesh.RecalculateBounds();
        }

        private void AddStrip(float half, float y, List<int> outTris)
        {
            int start = verts.Count;
            float vScale = 1f / Mathf.Max(0.01f, uvLengthPerTile);
            int sampleCount = centres.Count;
            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 c = centres[i], r = rights[i];
                Vector3 left = new Vector3(c.x - r.x * half, c.y + y, c.z - r.z * half);
                Vector3 right = new Vector3(c.x + r.x * half, c.y + y, c.z + r.z * half);
                if (activeTile != null)
                {
                    left = activeTile.MetresToTileLocal(left);   // metres → the tile's (possibly scaled) local space
                    right = activeTile.MetresToTileLocal(right);
                }
                verts.Add(left);
                verts.Add(right);
                normals.Add(Vector3.up); normals.Add(Vector3.up);
                float v = uList[i] * vScale;
                uvs.Add(new Vector2(0f, v)); uvs.Add(new Vector2(1f, v));

                if (i > 0)
                {
                    int b = start + (i - 1) * 2;
                    if (!flipFaces)
                    {
                        outTris.Add(b); outTris.Add(b + 2); outTris.Add(b + 1);
                        outTris.Add(b + 1); outTris.Add(b + 2); outTris.Add(b + 3);
                    }
                    else
                    {
                        outTris.Add(b); outTris.Add(b + 1); outTris.Add(b + 2);
                        outTris.Add(b + 1); outTris.Add(b + 3); outTris.Add(b + 2);
                    }
                }
            }
        }

        private void EnsureMesh()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (mesh == null)
            {
                mesh = new Mesh { name = "CurvedRoad (generated)" };
                mesh.MarkDynamic();
            }
            if (meshFilter != null && meshFilter.sharedMesh != mesh)
                meshFilter.sharedMesh = mesh;
        }
    }
}
