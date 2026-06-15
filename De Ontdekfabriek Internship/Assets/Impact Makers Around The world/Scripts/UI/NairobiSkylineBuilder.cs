using UnityEngine;
using UnityEngine.UI;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Procedural Nairobi skyline silhouette (Req §12.4): KICC's cylinder and
    /// helipad disc, Times Tower's spire, UAP Old Mutual, Britam's slanted crown and
    /// Teleposta — built once at Start from plain UI Images, no textures. Proportions
    /// are stylised for silhouette recognition, matching the low-poly art direction.
    /// </summary>
    public sealed class NairobiSkylineBuilder : MonoBehaviour
    {
        [SerializeField] private Color silhouetteColour = new Color(0.16f, 0.18f, 0.24f);
        [Tooltip("Pixel height of one 'storey' — scales the whole skyline.")]
        [SerializeField] private float unit = 6f;

        private void Start()
        {
            // x offset (units), width (units), height (units) — left to right.
            BuildTower("Teleposta", -34f, 7f, 16f);
            BuildTower("UAP_Tower", -20f, 8f, 28f);

            // KICC: the iconic cylinder with the wider helipad disc on top.
            BuildTower("KICC_Cylinder", -6f, 6f, 24f);
            BuildTower("KICC_Helipad", -7.5f, 9f, 2f, baseHeight: 24f);

            // Times Tower: tall block with a spire.
            BuildTower("TimesTower_Block", 6f, 7f, 30f);
            BuildTower("TimesTower_Spire", 8.8f, 1.4f, 6f, baseHeight: 30f);

            // Britam: Nairobi's tallest, with the slanted crown approximated by steps.
            BuildTower("Britam_Block", 20f, 8f, 34f);
            BuildTower("Britam_Crown1", 20f, 6f, 2.5f, baseHeight: 34f);
            BuildTower("Britam_Crown2", 20f, 4f, 2.5f, baseHeight: 36.5f);
            BuildTower("Britam_Crown3", 20f, 2f, 2.5f, baseHeight: 39f);
        }

        private void BuildTower(string towerName, float x, float width, float height, float baseHeight = 0f)
        {
            var go = new GameObject(towerName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x * unit, baseHeight * unit);
            rect.sizeDelta = new Vector2(width * unit, height * unit);

            var image = go.GetComponent<Image>();
            image.color = silhouetteColour;
            image.raycastTarget = false;
        }
    }
}
