using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// One large flat ground surface under the road, kept SEPARATE from the road tiles. This is the piece that
    /// makes turns reliable: because the ground is a single flat plane (not baked into 200x200 m square tiles),
    /// it never has to bend or re-tile, so it can never overlap or fold when the road curves. The thin road
    /// strips (CurvedRoadMesh) sit on top and can bend any amount over it.
    ///
    /// The player is stationary and the world scrolls past, so the plane is static; motion is shown by scrolling
    /// the ground TEXTURE with the distance travelled — infinite-looking moving terrain with no moving geometry,
    /// and nothing that can break. Build it once (Tools > Kenya Scooter > Road Tiles and Seams > Create Scrolling
    /// Ground, or add this
    /// component to an empty object at the player's position), assign your savannah material, and size it bigger
    /// than the road's draw distance so it always fills the view, including around curves.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ScrollingGround : MonoBehaviour
    {
        [Tooltip("Side length of the ground plane in metres. Make it bigger than the road's draw distance (spawnHorizon) so it fills the view, including to the sides on a curve.")]
        [SerializeField] private float size = 800f;
        [Tooltip("Height of the ground, just below the road surface so the road never z-fights it.")]
        [SerializeField] private float yLevel = -0.03f;
        [Tooltip("Texture repeats per metre. Higher = smaller, more frequent texture.")]
        [SerializeField] private float textureTilesPerMetre = 0.04f;
        [Tooltip("Texture scroll speed relative to the world (1 = locked to the world; negative flips the scroll direction).")]
        [SerializeField] private float scrollMultiplier = 1f;
        [Tooltip("Optional. Your savannah ground material. Empty = a plain placeholder is built so it is visible.")]
        [SerializeField] private Material groundMaterial;
        [Tooltip("Tint the whole ground toward Kenyan laterite red (multiplies the ground material's colour/texture). Off = the material's own colour.")]
        [SerializeField] private bool applyTint = true;
        [Tooltip("The red-laterite tint applied to the ground when Apply Tint is on.")]
        [SerializeField] private Color groundTint = new Color(0.72f, 0.42f, 0.28f, 1f);

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock mpb;
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST"); // URP Lit
        private static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST"); // built-in fallback
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            BuildQuad();
            meshRenderer = GetComponent<MeshRenderer>();
            if (groundMaterial != null)
                meshRenderer.sharedMaterial = groundMaterial;
            else if (meshRenderer.sharedMaterial == null)
                meshRenderer.sharedMaterial = BuildPlaceholder();
            mpb = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (meshRenderer == null)
                return;
            float dist = WorldSpeed.Instance != null ? WorldSpeed.Instance.DistanceTravelled : 0f;
            float tile = size * textureTilesPerMetre;
            float offset = dist * textureTilesPerMetre * scrollMultiplier;
            meshRenderer.GetPropertyBlock(mpb);
            Vector4 st = new Vector4(tile, tile, 0f, -offset); // scroll along the forward (V) axis
            mpb.SetVector(BaseMapST, st);
            mpb.SetVector(MainTexST, st);
            if (applyTint)
            {
                // Multiplies the ground texture/colour toward red laterite, so the world's sand reads as Kenya.
                mpb.SetColor(BaseColorId, groundTint);
                mpb.SetColor(ColorId, groundTint);
            }
            meshRenderer.SetPropertyBlock(mpb);
        }

        private void BuildQuad()
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh != null && mf.sharedMesh.name == "ScrollingGround")
                return;
            float h = size * 0.5f;
            var mesh = new Mesh { name = "ScrollingGround" };
            mesh.vertices = new[]
            {
                new Vector3(-h, yLevel, -h), new Vector3(-h, yLevel, h),
                new Vector3( h, yLevel,  h), new Vector3( h, yLevel, -h)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
        }

        private static Material BuildPlaceholder()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = "Ground (placeholder)" };
            var c = new Color(0.72f, 0.62f, 0.44f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }
    }
}
