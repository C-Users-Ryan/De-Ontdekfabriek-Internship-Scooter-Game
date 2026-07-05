using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using KenyaScooter.Settings;

namespace KenyaScooter.SettingsEditor
{
    /// <summary>
    /// One-click builder for the facilitator settings menu, matching the HUD/menu builders
    /// (Tools > Kenya Scooter > ...). The menu also self-creates at runtime, so this is OPTIONAL —
    /// it just lets you place and preview the canvas in the scene ahead of time, and means a
    /// FacilitatorGate is in the scene without relying on the runtime auto-bootstrap.
    /// </summary>
    public static class KenyaSettingsBuilderMenu
    {
        [MenuItem("Tools/Kenya Scooter/UI Builders/Build Settings Menu", false, 223)]
        public static void BuildSettingsMenu()
        {
            SettingsMenu menu = Object.FindObjectOfType<SettingsMenu>();
            if (menu == null)
            {
                var canvasGO = new GameObject("Settings Menu Canvas",
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGO, "Build Settings Menu");

                var canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100; // above HUD and framing screens

                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(2048f, 1536f); // iPad 4:3
                scaler.matchWidthOrHeight = 0.5f;

                var go = new GameObject("Settings Menu", typeof(RectTransform));
                go.transform.SetParent(canvasGO.transform, false);
                menu = go.AddComponent<SettingsMenu>();
            }

            menu.Build();

            // Make sure a FacilitatorGate exists so staff can open it on a tablet (long-press top-left).
            if (Object.FindObjectOfType<FacilitatorGate>() == null)
            {
                var gateGO = new GameObject("FacilitatorGate");
                Undo.RegisterCreatedObjectUndo(gateGO, "Build Settings Menu");
                gateGO.AddComponent<FacilitatorGate>();
            }

            Selection.activeObject = menu.gameObject;
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            EditorGUIUtility.PingObject(menu.gameObject);

            Debug.Log("[Kenya Settings] Facilitator settings menu built, plus a FacilitatorGate. " +
                      "Open it in Play mode by HOLDING the top-left screen corner for 2 seconds (or press F8 on desktop), " +
                      "then enter the access code (default " + FacilitatorLock.DefaultPin + " — change it in Beheer for a real venue). " +
                      "Start on the PROFIELEN page for one-tap whole-game presets. " +
                      "Every change saves to PlayerPrefs and re-applies on the next launch — no rebuild needed.");
        }

        [MenuItem("Tools/Kenya Scooter/Facilitator/Reset Facilitator Settings to Default", false, 224)]
        public static void ResetSettings()
        {
            if (!EditorUtility.DisplayDialog("Reset facilitator settings",
                    "This clears every saved facilitator override on THIS machine (including the access code, back to " +
                    FacilitatorLock.DefaultPin + ") and returns the game to the shipped defaults. Continue?",
                    "Reset", "Cancel"))
                return;
            GameSettings.ResetAll();
            FacilitatorLock.ResetToDefault();
            FacilitatorLock.Enabled = true;
            Debug.Log("[Kenya Settings] All facilitator overrides cleared and access code reset to " +
                      FacilitatorLock.DefaultPin + "; back to shipped defaults.");
        }

        [MenuItem("Tools/Kenya Scooter/Facilitator/Reset Access Code", false, 225)]
        public static void ResetAccessCode()
        {
            if (!EditorUtility.DisplayDialog("Reset access code",
                    "Put the facilitator access code back to the default (" + FacilitatorLock.DefaultPin +
                    ") and switch the lock on. Use this if the code was changed and forgotten. Continue?",
                    "Reset code", "Cancel"))
                return;
            FacilitatorLock.ResetToDefault();
            FacilitatorLock.Enabled = true;
            Debug.Log("[Kenya Settings] Access code reset to " + FacilitatorLock.DefaultPin + " and the lock turned on.");
        }
    }
}
