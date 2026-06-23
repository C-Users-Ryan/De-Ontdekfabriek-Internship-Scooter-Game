using UnityEngine;

namespace KenyaScooter.Hazards
{
    /// <summary>
    /// Visual variety for pooled potholes (M21). On each spawn it swaps the quad's main texture to a
    /// random one from the set and (optionally) mirrors the UVs, through a MaterialPropertyBlock — so
    /// there is no material instancing and no per-spawn allocation. Drop it on the pothole prefab (the
    /// MeshRenderer quad) and assign the Pothole_LP_* textures. Combined with the spawner's random
    /// scale, repeated potholes never read as copy-pasted.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class PotholeVariant : MonoBehaviour
    {
        [Tooltip("Assign the Pothole_LP_Round / Long / Chip / Sprawl textures.")]
        [SerializeField] private Texture2D[] textures;
        [Tooltip("Also randomly mirror the texture so the same shape reads differently.")]
        [SerializeField] private bool randomMirror = true;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

        private MeshRenderer rend;
        private MaterialPropertyBlock mpb;

        private void Awake()
        {
            rend = GetComponent<MeshRenderer>();
            mpb = new MaterialPropertyBlock();
        }

        // Pooled hazards re-enable on every spawn, so the look is re-rolled here.
        private void OnEnable()
        {
            if (rend == null || textures == null || textures.Length == 0)
                return;

            rend.GetPropertyBlock(mpb);
            mpb.SetTexture(MainTexId, textures[Random.Range(0, textures.Length)]);
            if (randomMirror)
            {
                float sx = Random.value < 0.5f ? -1f : 1f;
                float sy = Random.value < 0.5f ? -1f : 1f;
                mpb.SetVector(MainTexStId, new Vector4(sx, sy, sx < 0f ? 1f : 0f, sy < 0f ? 1f : 0f));
            }
            rend.SetPropertyBlock(mpb);
        }
    }
}
