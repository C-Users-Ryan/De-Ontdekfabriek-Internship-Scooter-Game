using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using KenyaScooter.Roads;

namespace KenyaScooter.RoadsEditor
{
    /// <summary>
    /// Makes tiles CONNECT cleanly and fixes the "tiles clip / spawn on top of each other" problem (added
    /// 2026-06-26). The cause is simple: the sequencer attaches the NEXT tile at <c>RoadTile.length</c> metres
    /// along the driven line. If a tile's ART is bigger than its length (e.g. a 200 m City tile on a tile whose
    /// length is 20), the next tile spawns INSIDE the art (overlap); if the art is smaller, there is a gap. So a
    /// tile must advertise where its road really starts and ends, and its length must match the art.
    ///
    /// These tools do exactly that, with NO change to the proven sequencer chaining (so the straight build and the
    /// live crossroads turn are untouched). The core <see cref="FitLengthToArt"/> is reused by the one-click
    /// "Add Turn to Selected Tile" so a turn tile is clip-free the moment you make it. Editor-only, fully undoable.
    /// </summary>
    public static class TileSeamTools
    {
        private const string EntryName = "Seam - Entry";
        private const string ExitName = "Seam - Exit";

        // ---- Fit Tile to Art (the clip fix, also reused by Add Turn) ---------------------------------------

        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Fit Tile to Art (fix clipping)", true)]
        private static bool ValidateFit() => Selection.gameObjects != null && Selection.gameObjects.Length > 0;

        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Fit Tile to Art (fix clipping)", false, 42)]
        public static void FitTileToArt()
        {
            var objs = Selection.gameObjects;
            if (objs == null || objs.Length == 0)
            {
                EditorUtility.DisplayDialog("Kenya Scooter",
                    "Select a tile (or open its prefab) first, then run this again.", "OK");
                return;
            }

            int done = 0;
            string report = "";
            foreach (var go in objs)
            {
                if (FitLengthToArt(go, out float oldLength, out float newLength, out float slid))
                {
                    report += $"- {go.name}: length {oldLength:0.##} -> {newLength:0.##} m; art slid {slid:0.##} m so " +
                              "its entrance is at the seam.\n";
                    done++;
                }
                else
                {
                    report += $"- {go.name}: no art renderers to measure (skipped).\n";
                }
            }

            EditorUtility.DisplayDialog("Kenya Scooter",
                $"Fitted {done} tile(s) so they connect at both seams (no more overlap or gap):\n\n" + report + "\n" +
                "The next tile now attaches exactly at the far edge of this tile's art, and the art's entrance is " +
                "slid to the tile origin so the previous tile meets it.\n\n" +
                "Note: this measures ALL renderers (buildings included). If a building overhangs the road, use " +
                "'Add Seam Markers' and drag them onto the road's true ends for a precise fit. Undo reverts.",
                "Got it");
        }

        /// <summary>
        /// Sets <paramref name="go"/>'s <see cref="RoadTile.length"/> to its art's forward (local Z) span and slides
        /// its children so the art's near edge sits at the tile origin, so the tile meets its neighbours at both
        /// seams. This is the whole clip fix, with no sequencer change. Returns false (and changes nothing) when the
        /// tile has no renderers to measure. Registers its own Undo, so it is safe to call standalone or from the
        /// one-click Add Turn tool. Reports the old / new length and how far the art was slid via the out params.
        /// </summary>
        public static bool FitLengthToArt(GameObject go, out float oldLength, out float newLength, out float slid)
        {
            oldLength = 0f;
            newLength = 0f;
            slid = 0f;
            if (go == null)
                return false;

            var renderers = CollectArtRenderers(go.transform);
            if (renderers.Count == 0)
                return false;

            Undo.RegisterFullObjectHierarchyUndo(go, "Fit Tile to Art");

            Bounds local = LocalArtBounds(go.transform, renderers);
            float nearZ = local.min.z;          // the edge closest to the player (the road entrance)
            newLength = Mathf.Max(1f, local.size.z);
            slid = -nearZ;

            // Slide every child so the art's near edge lands on the tile origin (z = 0), so the START seam connects.
            // (Only Z: leaving X / Y alone respects any intentional offset art.)
            if (Mathf.Abs(nearZ) > 0.0001f)
            {
                Transform t = go.transform;
                for (int i = 0; i < t.childCount; i++)
                {
                    Transform c = t.GetChild(i);
                    Undo.RecordObject(c, "Fit Tile to Art");
                    Vector3 p = c.localPosition;
                    p.z -= nearZ;
                    c.localPosition = p;
                }
            }

            RoadTile tile = go.GetComponent<RoadTile>();
            if (tile == null)
                tile = Undo.AddComponent<RoadTile>(go);
            Undo.RecordObject(tile, "Fit Tile to Art");
            oldLength = tile.length;
            tile.length = newLength;
            EditorUtility.SetDirty(tile);
            return true;
        }

        // ---- Add Seam Markers (precise authoring for composite art) ---------------------------------------

        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Add Seam Markers (Entry + Exit)", true)]
        private static bool ValidateMarkers() => Selection.activeGameObject != null;

        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Add Seam Markers (Entry + Exit)", false, 43)]
        public static void AddSeamMarkers()
        {
            var go = Selection.activeGameObject;
            if (go == null)
                return;

            var tile = go.GetComponent<RoadTile>();
            float length = tile != null ? tile.length : 30f;

            FindOrCreateMarker(go.transform, EntryName, Vector3.zero);
            Transform exit = FindOrCreateMarker(go.transform, ExitName, new Vector3(0f, 0f, Mathf.Max(1f, length)));

            Selection.activeGameObject = exit.gameObject;
            EditorGUIUtility.PingObject(exit.gameObject);

            EditorUtility.DisplayDialog("Kenya Scooter",
                "Added two seam markers as children:\n\n" +
                $"- '{EntryName}' at the tile origin - drag it onto where the road ENTERS the art.\n" +
                $"- '{ExitName}' at z = {Mathf.Max(1f, length):0.##} - drag it onto where the road EXITS the art.\n\n" +
                "Then run 'Fit Tile to Seam Markers' to set the length and slide the art so the road runs origin -> " +
                "exit and the tile meets its neighbours exactly. Markers are plain empties; they cost nothing at runtime.",
                "Got it");
        }

        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Fit Tile to Seam Markers", true)]
        private static bool ValidateFitMarkers()
        {
            var go = Selection.activeGameObject;
            return go != null && go.transform.Find(EntryName) != null && go.transform.Find(ExitName) != null;
        }

        [MenuItem("Tools/Kenya Scooter/Road Tiles and Seams/Fit Tile to Seam Markers", false, 44)]
        public static void FitTileToSeamMarkers()
        {
            var go = Selection.activeGameObject;
            Transform entry = go != null ? go.transform.Find(EntryName) : null;
            Transform exit = go != null ? go.transform.Find(ExitName) : null;
            if (entry == null || exit == null)
            {
                EditorUtility.DisplayDialog("Kenya Scooter",
                    "This tile has no seam markers. Run 'Add Seam Markers (Entry + Exit)' first, place them on the " +
                    "road's ends, then run this.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(go, "Fit Tile to Seam Markers");

            // Slide every child so the Entry marker lands on the tile origin (z = 0), so the start seam connects.
            float shift = -entry.localPosition.z;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform c = t.GetChild(i);
                Undo.RecordObject(c, "Fit Tile to Seam Markers");
                Vector3 p = c.localPosition;
                p.z += shift;
                c.localPosition = p;
            }

            // Length is the distance between the markers along the tile's forward axis (Entry is now at z = 0).
            float length = Mathf.Max(1f, exit.localPosition.z);
            RoadTile tile = go.GetComponent<RoadTile>();
            if (tile == null)
                tile = Undo.AddComponent<RoadTile>(go);
            Undo.RecordObject(tile, "Fit Tile to Seam Markers");
            float oldLength = tile.length;
            tile.length = length;
            EditorUtility.SetDirty(tile);

            EditorUtility.DisplayDialog("Kenya Scooter",
                $"Fitted '{go.name}' to its seam markers:\n\n" +
                $"- Slid the art {shift:0.##} m so the Entry marker is at the tile origin.\n" +
                $"- Set length {oldLength:0.##} -> {length:0.##} m (origin to Exit), so the next tile attaches at the " +
                "Exit marker.\n\n" +
                "The tile now meets its neighbours at both seams regardless of how it turns inside. Undo reverts.",
                "Got it");
        }

        // ---- Helpers --------------------------------------------------------------------------------------

        /// <summary>All renderers under the tile (the markers are plain empties with none).</summary>
        private static List<Renderer> CollectArtRenderers(Transform tile)
        {
            var list = new List<Renderer>();
            tile.GetComponentsInChildren(true, list);
            return list;
        }

        /// <summary>Combined bounds of the renderers expressed in the tile's LOCAL space, so a rotated or scaled
        /// tile is measured along its own forward (Z) axis. Encapsulates each renderer's world-AABB corners.</summary>
        private static Bounds LocalArtBounds(Transform tile, List<Renderer> renderers)
        {
            Matrix4x4 w2l = tile.worldToLocalMatrix;
            bool started = false;
            Bounds b = new Bounds();
            for (int r = 0; r < renderers.Count; r++)
            {
                Bounds wb = renderers[r].bounds;
                Vector3 c = wb.center, e = wb.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + new Vector3(
                        (i & 1) == 0 ? -e.x : e.x,
                        (i & 2) == 0 ? -e.y : e.y,
                        (i & 4) == 0 ? -e.z : e.z);
                    Vector3 lp = w2l.MultiplyPoint3x4(corner);
                    if (!started) { b = new Bounds(lp, Vector3.zero); started = true; }
                    else b.Encapsulate(lp);
                }
            }
            return b;
        }

        private static Transform FindOrCreateMarker(Transform parent, string name, Vector3 localPos)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Add Seam Marker");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }
    }
}
