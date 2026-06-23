using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using KenyaScooter.Scoring;

namespace KenyaScooter.ScoringEditor
{
    /// <summary>
    /// One-click fix for the two missing scoring managers. The relay/group total (GroupScoreManager) and the
    /// shared multiplier (StreakSystem) are components that have to live in the scene; if they are absent the
    /// score never carries between players and the multiplier stays 1x. This adds both to the GameObject that
    /// already holds ScoreManager, copies ScoreManager's ScoreConfig onto StreakSystem, and points
    /// ScoreManager's Streak field at it. Run it with the game scene open, then Save.
    /// </summary>
    public static class KenyaScoringWiringMenu
    {
        [MenuItem("Tools/Kenya Scooter/Wire Scoring Managers")]
        public static void Wire()
        {
            ScoreManager score = Object.FindObjectOfType<ScoreManager>();
            if (score == null)
            {
                EditorUtility.DisplayDialog("Kenya Scooter",
                    "No ScoreManager found in the open scene. Open your game scene first, then run this again.", "OK");
                return;
            }

            GameObject host = score.gameObject;
            string added = "";

            if (Object.FindObjectOfType<GroupScoreManager>() == null)
            {
                Undo.AddComponent<GroupScoreManager>(host);
                added += "\n• GroupScoreManager (team score now carries between players)";
            }

            StreakSystem streak = Object.FindObjectOfType<StreakSystem>();
            if (streak == null)
            {
                streak = Undo.AddComponent<StreakSystem>(host);
                added += "\n• StreakSystem (the shared multiplier)";
            }

            // Give StreakSystem the same ScoreConfig as ScoreManager (it reads the Streak Tiers from it).
            var scoreSO = new SerializedObject(score);
            var scoreConfig = scoreSO.FindProperty("config");
            var streakSO = new SerializedObject(streak);
            var streakConfig = streakSO.FindProperty("config");
            if (streakConfig != null && scoreConfig != null && streakConfig.objectReferenceValue == null)
            {
                streakConfig.objectReferenceValue = scoreConfig.objectReferenceValue;
                streakSO.ApplyModifiedProperties();
            }

            // Point ScoreManager.streak at the StreakSystem.
            var streakField = scoreSO.FindProperty("streak");
            if (streakField != null)
            {
                streakField.objectReferenceValue = streak;
                scoreSO.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(host.scene);
            EditorUtility.DisplayDialog("Kenya Scooter",
                "Scoring wired onto '" + host.name + "':" + (added == "" ? "\n(both were already present)" : added) +
                "\n\nNow confirm the ScoreConfig's Streak Tiers are filled in (e.g. streak 3 -> 1.5x, 6 -> 2x, 10 -> 3x), " +
                "then File > Save the scene.", "OK");
        }
    }
}
