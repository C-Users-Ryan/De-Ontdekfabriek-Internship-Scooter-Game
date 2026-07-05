using UnityEngine;
using UnityEditor;
using KenyaScooter.Roads;

namespace KenyaScooter.RoadsEditor
{
    /// <summary>
    /// One-click creation of the basic road pieces: a straight road tile with a generated road strip, a
    /// scrolling ground, and "Add Generated Road to Selected" to give any existing tile a strip that follows
    /// its shape. That is ALL the tooling the road needs since 2026-07-04: turns are authored on the
    /// <see cref="RoadTile"/> itself (its Turns list — green ball = begin, red ball = end) and the tile's
    /// Begin/Exit points line tiles up, so there is nothing turn-specific to click.
    /// </summary>
    public static class RoadAuthoringMenu
    {
        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Create Straight Road Tile", false, 22)]
        public static void CreateStraightTile()
        {
            var go = new GameObject("Road Tile");
            Undo.RegisterCreatedObjectUndo(go, "Create Road Tile");

            Undo.AddComponent<MeshFilter>(go);
            var mr = Undo.AddComponent<MeshRenderer>(go);
            // Visible placeholder materials so the tile is not magenta on creation. Swap these for your real
            // road and ground materials; element 0 = road, element 1 = ground.
            mr.sharedMaterials = new[]
            {
                Placeholder("Road (placeholder)", new Color(0.18f, 0.18f, 0.20f)),
                Placeholder("Ground (placeholder)", new Color(0.78f, 0.68f, 0.50f))
            };

            var tile = Undo.AddComponent<RoadTile>(go);
            tile.length = 30f;
            tile.exitPoint = new Vector3(0f, 0f, 30f);

            var curved = Undo.AddComponent<CurvedRoadMesh>(go);
            curved.BuildFromTile(tile); // generate the road + ground strip right away

            if (SceneView.lastActiveSceneView != null)
                go.transform.position = SceneView.lastActiveSceneView.pivot;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            EditorUtility.DisplayDialog("Kenya Scooter",
                "Created a road tile.\n\n" +
                "Next:\n" +
                "1. Mesh Renderer > Materials: element 0 = your road material, element 1 = your ground material.\n" +
                "2. CurvedRoadMesh: set Road Width and Ground Width to match your other tiles.\n" +
                "3. Want it to turn? Add an entry to RoadTile > Turns — the green ball is where the turn begins,\n" +
                "   the red ball is where it ends. Add as many as you like.\n" +
                "4. Drag it into a Prefabs folder, then add it to a road sequence.", "Got it");
        }

        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Create Scrolling Ground", false, 23)]
        public static void CreateScrollingGround()
        {
            var go = new GameObject("Scrolling Ground");
            Undo.RegisterCreatedObjectUndo(go, "Create Scrolling Ground");
            go.transform.position = Vector3.zero; // under the stationary player
            Undo.AddComponent<MeshFilter>(go);
            Undo.AddComponent<MeshRenderer>(go);
            Undo.AddComponent<ScrollingGround>(go);
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            EditorUtility.DisplayDialog("Kenya Scooter",
                "Created a Scrolling Ground at the origin (under the player).\n\n" +
                "Next:\n" +
                "1. Assign your savannah ground material on the ScrollingGround component (else it uses a placeholder).\n" +
                "2. Make sure Size is bigger than your road's draw distance (spawnHorizon).\n" +
                "3. Convert your road tiles to thin strips with Add Generated Road to Selected, so the road sits on this ground.\n\n" +
                "Now the road can turn freely over a flat ground that never bends.", "Got it");
        }

        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Add Generated Road to Selected", true)]
        private static bool ValidateAddCurved() => Selection.gameObjects != null && Selection.gameObjects.Length > 0;

        /// <summary>
        /// Gives the SELECTED existing tile(s)/prefab(s) a generated road strip without rebuilding them from
        /// scratch: adds RoadTile + CurvedRoadMesh if missing and reuses the tile's own materials, so it keeps
        /// your look. The strip follows whatever is authored on the RoadTile (its Turns list included). Open a
        /// prefab (or select a scene tile) first. Note: CurvedRoadMesh REPLACES the object's mesh with the
        /// generated strip, so apply it to the road piece you want generated (Undo reverts). Child scenery is
        /// left untouched.
        /// </summary>
        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Add Generated Road to Selected", false, 24)]
        public static void AddCurvedToSelected()
        {
            var objs = Selection.gameObjects;
            if (objs == null || objs.Length == 0)
            {
                EditorUtility.DisplayDialog("Kenya Scooter", "Select a tile (or open its prefab) first, then run this again.", "OK");
                return;
            }

            int count = 0;
            foreach (var go in objs)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Add Generated Road");

                if (go.GetComponent<MeshFilter>() == null) Undo.AddComponent<MeshFilter>(go);
                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null) mr = Undo.AddComponent<MeshRenderer>(go);
                var tile = go.GetComponent<RoadTile>();
                if (tile == null) tile = Undo.AddComponent<RoadTile>(go);

                // Reuse the tile's existing materials so nothing goes magenta: keep slot 0 for the road, and use
                // the tile's second material (or the same one) for the ground sub-mesh.
                Undo.RecordObject(mr, "Add Generated Road");
                Material[] mats = mr.sharedMaterials;
                Material road = (mats.Length > 0 && mats[0] != null) ? mats[0] : Placeholder("Road (placeholder)", new Color(0.18f, 0.18f, 0.20f));
                Material ground = (mats.Length > 1 && mats[1] != null) ? mats[1] : road;
                mr.sharedMaterials = new[] { road, ground };

                var curved = go.GetComponent<CurvedRoadMesh>();
                if (curved == null) curved = Undo.AddComponent<CurvedRoadMesh>(go);
                curved.BuildFromTile(tile);
                count++;
            }

            EditorUtility.DisplayDialog("Kenya Scooter",
                $"Gave {count} tile(s) a generated road strip, reusing their materials.\n\n" +
                "The strip follows the tile's own shape: add turns on RoadTile > Turns (green ball = begin,\n" +
                "red ball = end) and the road regenerates along the bend automatically.", "Got it");
        }

        /// <summary>Gets or creates a simple coloured material asset so a new tile is visible (not magenta) the
        /// moment it is created. Meant to be replaced by the project's real road/ground materials.</summary>
        private static Material Placeholder(string assetName, Color color)
        {
            const string parent = "Assets/Impact Makers Around The world";
            const string folder = parent + "/Generated";
            string path = folder + "/" + assetName + ".mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(parent, "Generated");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = assetName };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
