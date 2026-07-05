using UnityEngine;
using UnityEditor;
using KenyaScooter.Config;

namespace KenyaScooter.SessionEditor
{
    /// <summary>
    /// One-click authoring for the day cycle. The shipped DayCycleConfig.asset serialises its OWN phase values, so
    /// improving the C# defaults in <see cref="DayCycleConfig"/> does NOT change the live game on its own. This tool
    /// copies the current code-default palette (the "Kenya reference" grade — warm rose dawn, bleached midday, golden
    /// afternoon, laterite-red dusk, indigo night) into every DayCycleConfig asset in the project, so a non-technical
    /// successor never has to retype 45 colour fields by hand.
    ///
    /// It overwrites ONLY the phase array (the look). The facilitator switches on the asset — cycleEnabled,
    /// fixedPhaseIndex, driveSkybox — are left untouched. Opt-in: nothing runs unless you pick the menu item.
    /// </summary>
    public static class DayCycleTools
    {
        [MenuItem("Tools/Kenya Scooter/Weather and FX/Apply Kenya Reference Day Palette", false, 124)]
        public static void ApplyPalette()
        {
            string[] guids = AssetDatabase.FindAssets("t:DayCycleConfig");
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Kenya Scooter",
                    "No DayCycleConfig asset found in the project. The day cycle still runs on the code default; " +
                    "create an asset (Create > Kenya Scooter > Day Cycle Config) if you want a hand-tunable one.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Kenya Scooter",
                    $"Overwrite the phase colours of {guids.Length} DayCycleConfig asset(s) with the Kenya reference " +
                    "palette?\n\nThe day/night look (sun, fog, sky per phase) is replaced. The facilitator switches " +
                    "(cycle on/off, fixed phase, drive sky) are kept. Any hand-tuning of the phase colours is lost.",
                    "Apply palette", "Cancel"))
                return;

            // A fresh instance runs the C# field initialisers, giving us the reference phase array.
            var reference = ScriptableObject.CreateInstance<DayCycleConfig>();
            int applied = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var target = AssetDatabase.LoadAssetAtPath<DayCycleConfig>(path);
                if (target == null)
                    continue;
                // Phase is a struct, so cloning the array gives the asset its own independent copies.
                target.phases = (DayCycleConfig.Phase[])reference.phases.Clone();
                EditorUtility.SetDirty(target);
                applied++;
            }
            Object.DestroyImmediate(reference);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Kenya Scooter",
                $"Applied the Kenya reference palette to {applied} asset(s).\n\nPress Play to see the sun arc from a " +
                "warm dawn through a bleached midday and golden afternoon into a laterite-red dusk and a starry night.",
                "Done");
        }
    }
}
