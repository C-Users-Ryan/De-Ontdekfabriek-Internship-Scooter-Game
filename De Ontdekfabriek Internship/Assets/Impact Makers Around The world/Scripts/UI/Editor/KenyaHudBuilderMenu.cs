using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using KenyaScooter.UI;

namespace KenyaScooter.UIEditor
{
    /// <summary>
    /// One-click HUD builder. Tools → Kenya Scooter → UI Builders → Build HUD Cluster creates (or finds)
    /// a screen-space Canvas + a DiegeticHud and generates the whole instrument cluster,
    /// so you never have to hand-place the panel, reels, gauge, battery, limit and LEDs.
    /// Run it again any time to rebuild after tweaking the DiegeticHud's fields.
    /// </summary>
    public static class KenyaHudBuilderMenu
    {
        [MenuItem("Tools/Kenya Scooter/UI Builders/Build HUD Cluster", false, 220)]
        public static void BuildHud()
        {
            DiegeticHud hud = Object.FindObjectOfType<DiegeticHud>();

            if (hud == null)
            {
                var canvasGO = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGO, "Build HUD Cluster");

                var canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(2048f, 1536f); // iPad 4:3
                scaler.matchWidthOrHeight = 0.5f;

                var clusterGO = new GameObject("Diegetic HUD", typeof(RectTransform));
                clusterGO.transform.SetParent(canvasGO.transform, false);
                hud = clusterGO.AddComponent<DiegeticHud>();
            }

            hud.Build();

            Selection.activeObject = hud.gameObject;
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorGUIUtility.PingObject(hud.gameObject);

            Debug.Log("[Kenya HUD] Cluster built. Press Play to see it drive from the live game events " +
                      "(score, speed, timer, limit, warnings). Tweak the DiegeticHud fields and run this again to rebuild.");
        }

        [MenuItem("Tools/Kenya Scooter/UI Builders/Rebuild HUD Cluster", true)]
        private static bool ValidateRebuild() => Object.FindObjectOfType<DiegeticHud>() != null;

        [MenuItem("Tools/Kenya Scooter/UI Builders/Rebuild HUD Cluster", false, 221)]
        public static void Rebuild()
        {
            var hud = Object.FindObjectOfType<DiegeticHud>();
            if (hud != null) { hud.Build(); EditorSceneManager.MarkSceneDirty(hud.gameObject.scene); }
        }

        [MenuItem("Tools/Kenya Scooter/UI Builders/Build Menu Screens", false, 222)]
        public static void BuildMenuScreens()
        {
            KenyaMenuScreens screens = Object.FindObjectOfType<KenyaMenuScreens>();
            if (screens == null)
            {
                var canvasGO = new GameObject("Menu Screens Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGO, "Build Menu Screens");

                var canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10; // above the HUD cluster canvas

                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(2048f, 1536f); // iPad 4:3
                scaler.matchWidthOrHeight = 0.5f;

                var go = new GameObject("Menu Screens", typeof(RectTransform));
                go.transform.SetParent(canvasGO.transform, false);
                screens = go.AddComponent<KenyaMenuScreens>();
            }

            screens.Build();
            Selection.activeObject = screens.gameObject;
            EditorSceneManager.MarkSceneDirty(screens.gameObject.scene);
            EditorGUIUtility.PingObject(screens.gameObject);

            Debug.Log("[Kenya HUD] Menu screens built (Title / Finish / Game Over / Checkpoint). " +
                      "Press Play — they show automatically from the game state, and the buttons call the real GameManager. " +
                      "Needs an EventSystem in the scene for the buttons (your scene already has one).");
        }
    }
}
