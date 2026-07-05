using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;
using KenyaScooter.Config;
using KenyaScooter.Traffic;

namespace KenyaScooter.AudioEditor
{
    /// <summary>
    /// One-click audio wiring. Tools → Kenya Scooter → Assign Audio Clips finds the AudioConfig asset
    /// and fills every clip field from AudioClips named after that field (case-insensitive), wherever
    /// they live in the project — so you never drag a clip into the Inspector by hand.
    ///
    /// Naming convention (drop these into Assets/Audio/, any extension wav/mp3/ogg/aiff):
    ///   Engine   motorLoop, motorStart, motorStop, windLoop, surfaceLoop
    ///   Music    musicLoop
    ///   SFX      overtakeSting, nearMissScreech, crashHard, crashLight, potholeBump,
    ///            wrongLaneBuzz, graceSaved, rewindWhoosh, checkpointArrive, hadadaIbis
    ///   Ambience savanna, township, wildlife, highland   (→ AudioConfig.ambientVariants)
    ///   Horns    hornClip1, hornClip2, hornClip3 …       (→ every TrafficHorn prefab's hornClips[])
    ///
    /// Re-run any time you add or rename a clip. Anything missing is just skipped and listed in the
    /// summary — partial runs are fine, and the game stays null-safe either way.
    /// </summary>
    public static class AudioAutoAssign
    {
        // The context tags AudioConfig.ambientVariants expects, matched against RoadSequence.contextTags.
        private static readonly string[] AmbientTags = { "savanna", "township", "wildlife", "highland" };

        [MenuItem("Tools/Kenya Scooter/Game Setup/Assign Audio Clips", false, 223)]
        public static void AssignAll()
        {
            AudioConfig config = FindConfig();
            if (config == null)
            {
                EditorUtility.DisplayDialog("Assign Audio Clips",
                    "No AudioConfig asset found.\n\nCreate one via Assets → Create → Kenya Scooter → Audio Config, " +
                    "then run this again.", "OK");
                return;
            }

            var assigned = new List<string>();
            var missing = new List<string>();

            // ---- AudioClip fields by reflection (motor, wind, surface, music, every SFX) ----
            FieldInfo[] fields = typeof(AudioConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo f in fields)
            {
                if (f.FieldType != typeof(AudioClip))
                    continue;

                AudioClip clip = FindClip(f.Name);
                if (clip != null)
                {
                    f.SetValue(config, clip);
                    assigned.Add(f.Name + "  ←  " + clip.name);
                }
                else
                {
                    missing.Add(f.Name);
                }
            }

            // ---- Ambient variants (savanna / township / wildlife / highland) ----
            var variants = new List<AudioConfig.AmbientVariant>();
            foreach (string tag in AmbientTags)
            {
                AudioClip clip = FindClip(tag);
                if (clip != null)
                {
                    variants.Add(new AudioConfig.AmbientVariant { contextTag = tag, clip = clip });
                    assigned.Add("ambientVariants[" + tag + "]  ←  " + clip.name);
                }
                else
                {
                    missing.Add("ambientVariants[" + tag + "]");
                }
            }
            if (variants.Count > 0)
                config.ambientVariants = variants.ToArray();

            EditorUtility.SetDirty(config);

            // ---- Matatu horns on every TrafficHorn prefab ----
            int hornPrefabs = AssignHorns(out int hornClipCount, missing);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ---- Summary ----
            var sb = new StringBuilder();
            sb.AppendLine("AudioConfig: " + AssetDatabase.GetAssetPath(config));
            sb.AppendLine();
            sb.AppendLine("Assigned " + assigned.Count + " clip(s).");
            if (hornClipCount > 0)
                sb.AppendLine("Horns: " + hornClipCount + " clip(s) → " + hornPrefabs + " TrafficHorn prefab(s).");
            if (missing.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Not found (skipped) — drop a matching file in and re-run:");
                sb.AppendLine("  " + string.Join(", ", missing));
            }

            Debug.Log("[Assign Audio Clips]\n" + sb + "\nDetails:\n  " + string.Join("\n  ", assigned), config);
            EditorUtility.DisplayDialog("Assign Audio Clips", sb.ToString(), "OK");
        }

        /// <summary>Finds the first AudioConfig in the project (there should be exactly one).</summary>
        private static AudioConfig FindConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioConfig");
            if (guids.Length == 0)
                return null;
            if (guids.Length > 1)
                Debug.LogWarning("[Assign Audio Clips] Multiple AudioConfig assets found — using the first. " +
                                 "Delete the spares to avoid confusion.");
            return AssetDatabase.LoadAssetAtPath<AudioConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>Loads the AudioClip whose file name (without extension) equals <paramref name="baseName"/>.</summary>
        private static AudioClip FindClip(string baseName)
        {
            string[] guids = AssetDatabase.FindAssets(baseName + " t:AudioClip");
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), baseName, StringComparison.OrdinalIgnoreCase))
                {
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null)
                        return clip;
                }
            }
            return null;
        }

        /// <summary>
        /// Assigns every AudioClip named hornClip* to the private hornClips[] of every TrafficHorn on
        /// every prefab. Returns the number of prefabs touched; outputs how many horn clips were found.
        /// </summary>
        private static int AssignHorns(out int hornClipCount, List<string> missing)
        {
            List<AudioClip> horns = AssetDatabase.FindAssets("t:AudioClip")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith("hornClip", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(c => c != null)
                .ToList();

            hornClipCount = horns.Count;
            if (hornClipCount == 0)
            {
                missing.Add("hornClips (TrafficHorn) — no 'hornClip*' files");
                return 0;
            }

            int prefabsTouched = 0;
            foreach (string g in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                    continue;

                TrafficHorn[] components = root.GetComponentsInChildren<TrafficHorn>(true);
                if (components.Length == 0)
                    continue;

                foreach (TrafficHorn horn in components)
                {
                    var so = new SerializedObject(horn);
                    SerializedProperty prop = so.FindProperty("hornClips");
                    if (prop == null)
                        continue;
                    prop.arraySize = horns.Count;
                    for (int i = 0; i < horns.Count; i++)
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = horns[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SavePrefabAsset(root);
                prefabsTouched++;
            }
            return prefabsTouched;
        }
    }
}
