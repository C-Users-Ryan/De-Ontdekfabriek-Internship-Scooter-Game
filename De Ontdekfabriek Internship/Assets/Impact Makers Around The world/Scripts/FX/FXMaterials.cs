using UnityEngine;

namespace KenyaScooter.FX
{
    /// <summary>
    /// Shared runtime-built materials for the dust effects (DustAtmosphere, VehicleDustTrail). Like SpeedLines,
    /// these never reference a project material asset: the built-in particle material renders MAGENTA under URP,
    /// so we build our own on a shader that is guaranteed to exist in the active pipeline. A single soft
    /// alpha-blended sprite material plus a runtime-generated round falloff texture is cached and shared by
    /// every dust system, so motes read as soft haze rather than hard squares with zero per-system setup.
    /// </summary>
    public static class FXMaterials
    {
        private static Material _softDust;
        private static Texture2D _softDot;

        /// <summary>A cached alpha-blended unlit material with a soft round texture, tinted per-particle by the
        /// particle's own start colour (including alpha). Safe under URP and the built-in pipeline.</summary>
        public static Material SoftDustMaterial()
        {
            if (_softDust != null)
                return _softDust;

            // Sprites/Default is alpha-blended, unlit, respects the particle vertex colour, ships with every
            // pipeline and never resolves to the magenta error shader. URP's particle shader is the fallback.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            _softDust = new Material(shader) { name = "SoftDust (runtime)" };
            Texture2D dot = SoftDot();
            _softDust.mainTexture = dot;
            if (_softDust.HasProperty("_BaseMap")) _softDust.SetTexture("_BaseMap", dot);
            if (_softDust.HasProperty("_BaseColor")) _softDust.SetColor("_BaseColor", Color.white);
            if (_softDust.HasProperty("_Color")) _softDust.SetColor("_Color", Color.white);
            return _softDust;
        }

        /// <summary>A small round texture with a smooth alpha falloff from centre to edge — one soft dust mote.</summary>
        private static Texture2D SoftDot()
        {
            if (_softDot != null)
                return _softDot;

            const int size = 64;
            const float half = size * 0.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SoftDot (runtime)", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);          // 0 centre, 1 edge
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);                          // smoothstep falloff
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _softDot = tex;
            return tex;
        }
    }
}
