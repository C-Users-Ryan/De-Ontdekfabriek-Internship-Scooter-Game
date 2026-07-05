using UnityEngine;
using UnityEditor;
using KenyaScooter.Roads;

namespace KenyaScooter.RoadsEditor
{
    /// <summary>
    /// Scene-view handles for <see cref="RoadTile"/> (2026-07-05): the road is authored by DRAGGING POINTS,
    /// so every point is a grabbable ball. BLUE = begin point, GREEN/RED = each turn's START and END (place
    /// both anywhere in the tile — the road runs straight to the green ball, bends until the red ball, then
    /// runs straight again), ORANGE = exit point, PURPLE = the checkpoint stop point. The cyan line re-draws
    /// live, so you always see exactly what the scooter will drive. Dragging runs the same validation as
    /// typing, so nothing can be authored broken.
    /// </summary>
    [CustomEditor(typeof(RoadTile))]
    public sealed class RoadTileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox(
                "Scene view: drag the balls. BLUE = begin point, GREEN = a turn starts here, RED = that turn " +
                "ends here (each turn has its own pair), ORANGE = exit point, PURPLE = checkpoint stop point. " +
                "The cyan line is exactly what the scooter will drive.", MessageType.Info);
        }

        private void OnSceneGUI()
        {
            var tile = (RoadTile)target;
            Transform t = tile.transform;

            // Begin point (blue): where the road enters the tile. Height is preserved so tiles stay flat.
            if (DragPoint(t, tile.beginPoint, new Color(0.25f, 0.55f, 1f), 0.2f, out Vector3 newBegin))
            {
                Undo.RecordObject(tile, "Move Begin Point");
                tile.beginPoint = newBegin;
                tile.EditorValidate();
            }

            // Exit point (orange): where the road leaves. Always draggable — the road ends where you put it.
            if (DragPoint(t, tile.exitPoint, new Color(1f, 0.6f, 0.1f), 0.2f, out Vector3 newExit))
            {
                Undo.RecordObject(tile, "Move Exit Point");
                newExit.y = tile.beginPoint.y;
                tile.exitPoint = newExit;
                tile.EditorValidate();
            }

            // Each turn's START (green) and END (red) ball — place them anywhere in the tile.
            if (tile.turns != null)
            {
                for (int i = 0; i < tile.turns.Length; i++)
                {
                    RoadTile.Turn turn = tile.turns[i];

                    if (DragPoint(t, turn.start, Color.green, 0.18f, out Vector3 newStart))
                    {
                        Undo.RecordObject(tile, "Move Turn Start");
                        newStart.y = tile.beginPoint.y;
                        turn.start = newStart;
                        tile.turns[i] = turn;
                        tile.EditorValidate();
                    }

                    turn = tile.turns[i];
                    if (DragPoint(t, turn.end, Color.red, 0.18f, out Vector3 newEnd))
                    {
                        Undo.RecordObject(tile, "Move Turn End");
                        newEnd.y = tile.beginPoint.y;
                        turn.end = newEnd;
                        tile.turns[i] = turn;
                        tile.EditorValidate();
                    }

                    // A faint tie between the pair, so it is obvious which green and red belong together.
                    Handles.color = new Color(1f, 1f, 1f, 0.25f);
                    Handles.DrawDottedLine(
                        t.TransformPoint(tile.turns[i].start), t.TransformPoint(tile.turns[i].end), 4f);
                }
            }

            // Checkpoint stop point (purple): where the scooter comes to rest at the charge station.
            if (tile.isCheckpoint &&
                DragPoint(t, tile.stopPoint, new Color(0.75f, 0.35f, 1f), 0.2f, out Vector3 newStop))
            {
                Undo.RecordObject(tile, "Move Stop Point");
                tile.stopPoint = newStop;
                tile.EditorValidate();
            }
        }

        /// <summary>One draggable ball for a local-space point. Returns true with the new LOCAL position when
        /// the user dragged it; keeps the point's own height so the road stays flat.</summary>
        private static bool DragPoint(Transform t, Vector3 localPoint, Color color, float size, out Vector3 dragged)
        {
            dragged = localPoint;
            Vector3 world = t.TransformPoint(localPoint);
            Handles.color = color;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(
                world, HandleUtility.GetHandleSize(world) * size, Vector3.zero, Handles.SphereHandleCap);
            if (!EditorGUI.EndChangeCheck())
                return false;
            Vector3 local = t.InverseTransformPoint(moved);
            local.y = localPoint.y; // dragging never tips the road out of its plane
            dragged = local;
            return true;
        }
    }
}
