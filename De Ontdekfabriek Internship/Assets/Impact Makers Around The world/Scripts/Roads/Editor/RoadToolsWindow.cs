using UnityEngine;
using UnityEditor;

namespace KenyaScooter.RoadsEditor
{
    /// <summary>
    /// One small panel for the road basics (rebuilt 2026-07-04, when turns moved onto the RoadTile itself).
    /// There are no turn tools any more, because turns need no tool: select a tile and add entries to the
    /// RoadTile's TURNS list in the inspector — each places a green ball (begin) and a red ball (end), and
    /// the player turns between them. The begin/exit points on the same component line the tiles up.
    ///
    /// What remains here is creation of the basic pieces and the seam fix-ups for hand-modelled art.
    /// Open it from Tools &gt; Kenya Scooter &gt; Road Tools (panel).
    /// </summary>
    public sealed class RoadToolsWindow : EditorWindow
    {
        [MenuItem("Tools/Kenya Scooter/Road Tools (panel)", false, 0)]
        public static void Open()
        {
            var w = GetWindow<RoadToolsWindow>(false, "Road Tools");
            w.minSize = new Vector2(330, 320);
            w.Show();
        }

        private Vector2 scroll;

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Turns are authored ON the tile: select a road tile and add entries to RoadTile > Turns in the " +
                "inspector (green ball = turn begins, red ball = turn ends). Begin Point / Exit Point on the same " +
                "component decide where the road enters and leaves, so tiles line up. No tools needed.",
                MessageType.Info);

            EditorGUILayout.LabelField("Selected",
                Selection.activeGameObject != null ? Selection.activeGameObject.name : "(nothing - select a tile)");

            scroll = EditorGUILayout.BeginScrollView(scroll);

            Section("Create");
            Tool("Create Straight Road Tile", "A plain road tile with a generated road strip. Add turns to it in its inspector.",
                RoadAuthoringMenu.CreateStraightTile);
            Tool("Create Scrolling Ground", "The one flat ground under the road (make it once).",
                RoadAuthoringMenu.CreateScrollingGround);
            Tool("Add Generated Road to Selected", "Give an existing tile a generated road strip that follows its turns.",
                RoadAuthoringMenu.AddCurvedToSelected);

            Section("Seams (fix clipping on hand-modelled art)");
            Tool("Fit Tile to Art",
                "Sets the tile's length to its art and slides the art to the seam, so the next tile attaches at " +
                "its edge. Run this if tiles overlap or leave a gap.",
                TileSeamTools.FitTileToArt);
            Tool("Add Seam Markers (Entry + Exit)",
                "Drop two markers to mark the road's true ends on composite art (buildings, etc.).",
                TileSeamTools.AddSeamMarkers);
            Tool("Fit Tile to Seam Markers",
                "Fit the length and the start seam precisely to the markers you placed.",
                TileSeamTools.FitTileToSeamMarkers);

            EditorGUILayout.EndScrollView();
        }

        private static void Section(string title)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private static void Tool(string label, string help, System.Action action)
        {
            if (GUILayout.Button(label))
                action();
            EditorGUILayout.LabelField(help, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(3);
        }
    }
}
