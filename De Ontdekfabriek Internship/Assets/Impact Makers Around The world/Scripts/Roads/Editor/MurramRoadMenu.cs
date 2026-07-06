using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KenyaScooter.RoadsEditor
{
    /// <summary>
    /// One-click "make this road look like a sandy murram road". The Synty road model is a single mesh whose
    /// asphalt / curbs / yellow line all come from one shared ATLAS material, so you can't just tint it (goes
    /// flat) or texture it (the atlas UVs smear). This swaps the road pieces onto the KenyaScooter/MurramRoad
    /// shader, which paints red-laterite murram procedurally from world position + normal — no texture, no UVs,
    /// and the un-removable raised edges become graded sandy banks.
    ///
    /// A single shared material (Generated/Murram Road.mat) is created once and reused, so tweaking its colours
    /// re-skins every murram road at once. Everything is Undo-able (Ctrl+Z) and works on a prefab open in the
    /// Prefab stage, so the look saves into the tile prefab and rides through to the build.
    /// </summary>
    public static class MurramRoadMenu
    {
        private const string ShaderName = "KenyaScooter/MurramRoad";
        private const string GeneratedFolder = "Assets/Impact Makers Around The world/Generated";
        private const string MaterialPath = GeneratedFolder + "/Murram Road.mat";

        // Only the road model pieces (Road_1A_+4 ...) start with "Road"; the ground plane sits on the tile root
        // ("dirt road tile" / "base Tile"), which does not — so this skips the ground automatically.
        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Make Road Sandy (Murram) on Selection", false, 40)]
        private static void ApplyToRoadPieces()
        {
            var renderers = new List<MeshRenderer>();
            foreach (GameObject go in Selection.gameObjects)
                foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.name.StartsWith("Road", System.StringComparison.OrdinalIgnoreCase))
                        renderers.Add(r);

            if (renderers.Count == 0)
            {
                EditorUtility.DisplayDialog("Murram Road",
                    "No road pieces found under the selection.\n\nSelect a road tile (or open its prefab and select " +
                    "the root). I look for child mesh objects whose name starts with \"Road\" — the Road_1A model " +
                    "pieces — and leave the ground plane alone.\n\nTo sand EVERY mesh in the selection instead, use " +
                    "\"Make ENTIRE Selection Sandy\".",
                    "OK");
                return;
            }

            Apply(renderers);
        }

        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Make ENTIRE Selection Sandy (Murram)", false, 41)]
        private static void ApplyToEverything()
        {
            var renderers = new List<MeshRenderer>();
            foreach (GameObject go in Selection.gameObjects)
                renderers.AddRange(go.GetComponentsInChildren<MeshRenderer>(true));

            if (renderers.Count == 0)
            {
                EditorUtility.DisplayDialog("Murram Road", "Select at least one object with a mesh renderer.", "OK");
                return;
            }

            Apply(renderers);
        }

        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Make Road Sandy (Murram) on Selection", true)]
        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Make ENTIRE Selection Sandy (Murram)", true)]
        private static bool ValidateHasSelection() => Selection.gameObjects.Length > 0;

        private static void Apply(List<MeshRenderer> renderers)
        {
            Material mat = LoadOrCreateMaterial();
            if (mat == null)
                return;

            Undo.RecordObjects(renderers.ToArray(), "Make Road Sandy");
            foreach (MeshRenderer r in renderers)
            {
                // One material across all submeshes: the whole piece becomes murram (the atlas asphalt / line
                // is intentionally dropped — a dirt road has no painted markings).
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mat;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }

            Debug.Log($"[Murram Road] Applied the sandy murram look to {renderers.Count} renderer(s). " +
                      $"Tune colours / grain on {MaterialPath} (it re-skins every murram road). Undo with Ctrl+Z.");
            Selection.activeObject = mat; // drop the material in the Inspector so tuning is one click away
        }

        private static Material LoadOrCreateMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Murram Road] Shader '{ShaderName}' not found. Make sure " +
                               "Shaders/Resources/KenyaMurramRoad.shader imported without errors.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/Impact Makers Around The world", "Generated");

            var mat = new Material(shader) { name = "Murram Road" }; // defaults come from the shader properties
            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
