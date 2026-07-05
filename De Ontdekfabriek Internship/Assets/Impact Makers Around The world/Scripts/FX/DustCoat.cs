using System.Collections.Generic;
using UnityEngine;

namespace KenyaScooter.FX
{
    /// <summary>
    /// A non-destructive DUST COAT for any object: it tints the object's renderers toward warm Kenyan laterite so a
    /// prop (a rock, a building, a sign) reads as dusted and sits in the red-dust palette instead of clashing as a
    /// clean bright model. It does NOT replace materials or textures and does NOT instance shared materials - it
    /// pushes a tinted colour through a MaterialPropertyBlock, lerping each renderer's own base colour toward the
    /// dust colour by Amount, so the source assets are untouched and the tint is fully reversible.
    ///
    /// ADJUST + APPLY: drop it on an object (or select objects and use Tools > Kenya Scooter > Weather and FX >
    /// Apply Dust Coat to Selection), then drag Amount. It previews live in the editor ([ExecuteAlways]) and multi-object editing
    /// adjusts every selected coat at once. Remove the component (or set Amount to 0) to restore the original look.
    /// For a TALL object you want dusty only at the FOOT, use the KenyaScooter/BaseDust shader instead (height tint).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class DustCoat : MonoBehaviour
    {
        [Tooltip("Warm laterite dust colour the renderers are tinted toward.")]
        [SerializeField] private Color dustColour = new Color(0.62f, 0.40f, 0.27f, 1f);
        [Tooltip("How strongly to dust: 0 = original look, 1 = fully the dust colour.")]
        [Range(0f, 1f)] [SerializeField] private float amount = 0.45f;
        [Tooltip("Also tint child renderers (props are usually a parent with mesh children).")]
        [SerializeField] private bool affectChildren = true;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock mpb;
        private readonly List<Renderer> targets = new List<Renderer>(8);
        private readonly List<Color> originals = new List<Color>(8); // each target's own base colour, read from its shared material

        private void OnEnable()
        {
            Collect();
            Apply();
        }

        private void OnDisable() => Restore();

        private void OnValidate()
        {
            // Live preview while dragging Amount / colour in the Inspector.
            if (!isActiveAndEnabled)
                return;
            Collect();
            Apply();
        }

        /// <summary>Re-read the renderers and their ORIGINAL base colours (always from the shared material, never the
        /// property block, so re-collecting is idempotent and the true source colour is preserved).</summary>
        private void Collect()
        {
            targets.Clear();
            originals.Clear();

            if (affectChildren)
                GetComponentsInChildren(true, targets);
            else
                GetComponents(targets);

            // Only solid mesh renderers; skip particles, trails, sprites, UI (they have their own colour pipelines).
            for (int i = targets.Count - 1; i >= 0; i--)
                if (!(targets[i] is MeshRenderer || targets[i] is SkinnedMeshRenderer))
                    targets.RemoveAt(i);

            for (int i = 0; i < targets.Count; i++)
            {
                Material m = targets[i].sharedMaterial;
                Color baseCol = Color.white;
                if (m != null)
                {
                    if (m.HasProperty(BaseColorId)) baseCol = m.GetColor(BaseColorId);
                    else if (m.HasProperty(ColorId)) baseCol = m.GetColor(ColorId);
                }
                originals.Add(baseCol);
            }
        }

        private void Apply()
        {
            mpb ??= new MaterialPropertyBlock();
            for (int i = 0; i < targets.Count; i++)
            {
                Renderer r = targets[i];
                if (r == null)
                    continue;
                Color tinted = Color.Lerp(originals[i], dustColour, amount);
                tinted.a = originals[i].a; // keep the original alpha (transparency untouched)
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, tinted);
                mpb.SetColor(ColorId, tinted);
                r.SetPropertyBlock(mpb);
            }
        }

        private void Restore()
        {
            mpb ??= new MaterialPropertyBlock();
            for (int i = 0; i < targets.Count && i < originals.Count; i++)
            {
                Renderer r = targets[i];
                if (r == null)
                    continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, originals[i]);
                mpb.SetColor(ColorId, originals[i]);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
