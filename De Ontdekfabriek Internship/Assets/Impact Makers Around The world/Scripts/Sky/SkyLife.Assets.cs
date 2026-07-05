// Construction half of SkyLife — split out 2026-06-27 for readability (no behaviour change).
// The runtime half (lifecycle, spawning, the per-frame bird tick/billboard, pooling) lives in SkyLife.cs.
// This file holds the shared material, quad meshes and procedural silhouette textures.
using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.Session;

namespace KenyaScooter.Sky
{
    public sealed partial class SkyLife
    {
        // -------------------------------------------------------------------------------------------------------
        // Build: material, quad meshes, procedural silhouette textures
        // -------------------------------------------------------------------------------------------------------

        private void ResolveAnchor()
        {
            cam = Camera.main;
            anchor = cam != null ? cam.transform : null;
            // Re-parent any already-created birds onto the camera so they ride the view.
            if (anchor != null)
                for (int i = 0; i < birds.Count; i++)
                    if (birds[i].tr.parent != anchor)
                        birds[i].tr.SetParent(anchor, false);
        }

        /// <summary>Sprites/Default tinted dark-warm: alpha-blended, unlit, respects the per-renderer colour, ships
        /// with every pipeline and never resolves to the magenta URP error shader (mirrors FXMaterials / SpeedLines).</summary>
        private void BuildSharedMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            sharedMaterial = new Material(shader) { name = "SkyLife Bird (runtime)" };
            if (sharedMaterial.HasProperty(ColorId)) sharedMaterial.SetColor(ColorId, silhouetteColour);
            if (sharedMaterial.HasProperty(BaseColorId)) sharedMaterial.SetColor(BaseColorId, silhouetteColour);
            // Render with the transparent/sky crowd, not opaque geometry, so the silhouette blends over the skybox.
            sharedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>A unit quad (1m, centred). The mesh is shared by every bird of that kind; the per-bird transform
        /// scales it to size, and the kind's silhouette texture is pushed onto the renderer via the property block.</summary>
        private static Mesh BuildBirdQuad(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A wide, shallow gull/"M" silhouette: two swept wings meeting at a small body. Reads as a small
        /// crossing bird at distance. RGBA alpha only; the dark-warm colour comes from the renderer tint.</summary>
        private static Texture2D BuildGullTexture()
        {
            const int w = 64, h = 32;
            var tex = NewAlphaTex(w, h, "SkyLife GullTex (runtime)");
            var px = new Color32[w * h];
            float cx = (w - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x - cx) / cx;           // -1..1 across width
                    float ax = Mathf.Abs(nx);
                    // A shallow "M": each wing dips then sweeps up toward the tips. Body is a small lump at centre.
                    float wing = 0.30f + 0.55f * ax - 0.65f * Mathf.Max(0f, 0.45f - ax); // wing centre-line height (0..1)
                    float ny = (float)y / (h - 1);      // 0 bottom, 1 top
                    float thickness = Mathf.Lerp(0.20f, 0.07f, ax); // wings taper to the tips
                    float body = ax < 0.10f ? 0.12f : 0f;          // small central body lump
                    float d = Mathf.Abs(ny - (wing + body));
                    float a = Mathf.Clamp01(1f - d / Mathf.Max(0.001f, thickness));
                    a = a * a * (3f - 2f * a);          // smoothstep edge
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>A fatter stork/raptor silhouette: broad slightly-bowed wings with a thicker body and a hint of a
        /// neck/tail, for the lone high wheeler. RGBA alpha only; colour comes from the renderer tint.</summary>
        private static Texture2D BuildStorkTexture()
        {
            const int w = 64, h = 36;
            var tex = NewAlphaTex(w, h, "SkyLife StorkTex (runtime)");
            var px = new Color32[w * h];
            float cx = (w - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x - cx) / cx;
                    float ax = Mathf.Abs(nx);
                    float ny = (float)y / (h - 1);
                    // Broad, gently bowed wings (a soft arch), thicker than the gull, drooping a touch at the tips.
                    float wing = 0.46f - 0.16f * ax * ax;
                    float thickness = Mathf.Lerp(0.26f, 0.06f, ax);
                    // A thicker central body with a short neck (above) and tail (below) protruding at the centre.
                    float body = 0f;
                    if (ax < 0.16f)
                    {
                        body = 0.16f;
                        thickness = Mathf.Max(thickness, 0.30f);
                    }
                    float d = Mathf.Abs(ny - (wing + body * 0.0f)); // body widens thickness rather than offsetting the line
                    float a = Mathf.Clamp01(1f - d / Mathf.Max(0.001f, thickness));
                    a = a * a * (3f - 2f * a);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D NewAlphaTex(int w, int h, string name) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
    }
}
