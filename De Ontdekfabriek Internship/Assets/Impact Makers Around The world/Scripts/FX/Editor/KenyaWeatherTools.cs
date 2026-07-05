using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using KenyaScooter.Config;
using KenyaScooter.FX;

namespace KenyaScooter.FXEditor
{
    /// <summary>
    /// One-click setup for the Kenyan dust and haze (Tools > Kenya Scooter > ...), matching the project's other
    /// builders. It removes the two manual steps the feature otherwise needs: creating the WeatherConfig asset
    /// and getting it referenced so it actually ships.
    ///
    /// WHY a scene DustAtmosphere is placed, not just an asset: nothing in the game holds a serialized reference
    /// to WeatherConfig (both dust systems resolve it at runtime via ConfigLocator). An unreferenced
    /// ScriptableObject is STRIPPED from a player build, so on the tablet ConfigLocator.Find would return null and
    /// the dust would silently never appear, even though it works in the Editor. Placing a DustAtmosphere in the
    /// scene with the asset assigned guarantees the asset is included and loaded (so ConfigLocator finds it for the
    /// vehicle trails too), and the runtime self-bootstrap simply sees it already present and does nothing.
    /// </summary>
    public static class KenyaWeatherTools
    {
        private const string AssetFolder = "Assets/Impact Makers Around The world";
        private const string AssetPath = AssetFolder + "/WeatherConfig.asset";

        [MenuItem("Tools/Kenya Scooter/Weather and FX/Create Weather Config (Dust and Haze)", false, 122)]
        public static void CreateWeatherConfig()
        {
            WeatherConfig config = AssetDatabase.LoadAssetAtPath<WeatherConfig>(AssetPath);
            bool created = false;
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<WeatherConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                AssetDatabase.SaveAssets();
                created = true;
            }

            // Ensure a scene DustAtmosphere references the asset (so it ships and loads in a build).
            DustAtmosphere atmosphere = Object.FindObjectOfType<DustAtmosphere>(true);
            if (atmosphere == null)
            {
                var go = new GameObject("DustAtmosphere");
                Undo.RegisterCreatedObjectUndo(go, "Create Weather Config");
                atmosphere = go.AddComponent<DustAtmosphere>();
            }

            // Assign the config field (private [SerializeField]) via SerializedObject, the way the editor tools do.
            var so = new SerializedObject(atmosphere);
            SerializedProperty prop = so.FindProperty("config");
            if (prop != null)
            {
                prop.objectReferenceValue = config;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(atmosphere.gameObject.scene);

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            Debug.Log((created ? "[Kenya Weather] Created WeatherConfig and " : "[Kenya Weather] WeatherConfig already existed; ") +
                      "placed/linked a DustAtmosphere in the open scene so it ships and loads in a build. " +
                      "Toggle it in-game via the facilitator menu (OMGEVING > Stof en haze). " +
                      "Next: add a Vehicle Dust Trail to the traffic prefab(s) and the player scooter " +
                      "(select them and use Tools > Kenya Scooter > Weather and FX > Add Dust Trail to Selection). " +
                      "Save the scene to keep the DustAtmosphere.");
        }

        [MenuItem("Tools/Kenya Scooter/Weather and FX/Add Dust Trail to Selection", true)]
        public static bool AddDustTrailToSelectionValidate() => Selection.gameObjects.Length > 0;

        [MenuItem("Tools/Kenya Scooter/Weather and FX/Add Dust Trail to Selection", false, 144)]
        public static void AddDustTrailToSelection()
        {
            int added = 0, skipped = 0, prefabs = 0;
            foreach (GameObject go in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(go))
                {
                    // A prefab ASSET in the Project window: edit its contents and save back.
                    string path = AssetDatabase.GetAssetPath(go);
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    if (root.GetComponentInChildren<VehicleDustTrail>(true) == null)
                    {
                        root.AddComponent<VehicleDustTrail>();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabs++;
                    }
                    else skipped++;
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    // A GameObject in the open scene.
                    if (go.GetComponent<VehicleDustTrail>() == null)
                    {
                        Undo.AddComponent<VehicleDustTrail>(go);
                        added++;
                    }
                    else skipped++;
                }
            }
            if (added > 0 && Selection.gameObjects.Length > 0)
                EditorSceneManager.MarkSceneDirty(Selection.gameObjects[0].scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Kenya Weather] Vehicle Dust Trail: added to {added} scene object(s) and {prefabs} prefab(s); " +
                      $"{skipped} already had one. Tune the plume position via the component's 'Local Offset' (default is low and behind).");
        }

        // ---- Truck dust wake (the "its dust arrives" pass beat, Levend Kenia §4.1) -------------

        [MenuItem("Tools/Kenya Scooter/Weather and FX/Add Truck Dust Wake to Selection", true)]
        public static bool AddTruckWakeToSelectionValidate() => Selection.gameObjects.Length > 0;

        [MenuItem("Tools/Kenya Scooter/Weather and FX/Add Truck Dust Wake to Selection", false, 145)]
        public static void AddTruckWakeToSelection()
        {
            int added = 0, skipped = 0, prefabs = 0;
            foreach (GameObject go in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(go))
                {
                    // A prefab ASSET in the Project window: edit its contents and save back.
                    string path = AssetDatabase.GetAssetPath(go);
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    if (root.GetComponentInChildren<TruckDustWake>(true) == null)
                    {
                        root.AddComponent<TruckDustWake>();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabs++;
                    }
                    else skipped++;
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    if (go.GetComponent<TruckDustWake>() == null)
                    {
                        Undo.AddComponent<TruckDustWake>(go);
                        added++;
                    }
                    else skipped++;
                }
            }
            if (added > 0 && Selection.gameObjects.Length > 0)
                EditorSceneManager.MarkSceneDirty(Selection.gameObjects[0].scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Kenya Weather] Truck Dust Wake: added to {added} scene object(s) and {prefabs} prefab(s); " +
                      $"{skipped} already had one. Put it on the HEAVY vehicles only (truck/matatu prefabs) — the " +
                      "wall + wash is their beat; cars keep just the trail.");
        }

        // ---- Warm grade volume (the "breathe Kenya" colour grade) -----------------------------

        [MenuItem("Tools/Kenya Scooter/Weather and FX/Create Warm Grade Volume", false, 123)]
        public static void CreateWarmGrade()
        {
            const string profilePath = AssetFolder + "/KenyaWarmGrade.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);

                // Warm the whole image: white balance toward amber, a touch more contrast/saturation, a
                // soft warm colour filter, a gentle bloom so the hazy sky/sun glows, and a warm vignette.
                WhiteBalance wb = profile.Add<WhiteBalance>(true);
                wb.temperature.value = 12f;
                wb.tint.value = 4f;
                AddSub(wb, profile);

                ColorAdjustments ca = profile.Add<ColorAdjustments>(true);
                ca.contrast.value = 6f;
                ca.saturation.value = 6f;
                ca.colorFilter.value = new Color(1f, 0.96f, 0.9f);
                AddSub(ca, profile);

                Bloom bloom = profile.Add<Bloom>(true);
                bloom.threshold.value = 1.1f; // only the bright sky/sun blooms, not the whole scene
                bloom.intensity.value = 0.6f;
                bloom.tint.value = new Color(1f, 0.93f, 0.82f);
                AddSub(bloom, profile);

                Vignette vig = profile.Add<Vignette>(true);
                vig.intensity.value = 0.18f;
                vig.color.value = new Color(0.25f, 0.12f, 0.06f); // warm, not black
                AddSub(vig, profile);

                AssetDatabase.SaveAssets();
            }

            Volume volume = null;
            foreach (Volume v in Object.FindObjectsOfType<Volume>())
                if (v.gameObject.name == "Kenya Warm Grade") { volume = v; break; }
            if (volume == null)
            {
                var go = new GameObject("Kenya Warm Grade");
                Undo.RegisterCreatedObjectUndo(go, "Create Warm Grade Volume");
                volume = go.AddComponent<Volume>();
            }
            volume.isGlobal = true;
            volume.priority = -50f; // a BASE grade; the day-cycle phase Volumes sit above it
            volume.weight = 1f;
            volume.sharedProfile = profile;

            EnsurePostProcessingOn();
            EditorSceneManager.MarkSceneDirty(volume.gameObject.scene);
            Selection.activeObject = volume.gameObject;
            EditorGUIUtility.PingObject(volume.gameObject);
            Debug.Log("[Kenya Weather] Warm grade Volume created (white balance + gentle bloom + warm vignette) and " +
                      "post-processing enabled on the cameras. Tune it on the KenyaWarmGrade profile asset. Save the scene.");
        }

        private static void AddSub(VolumeComponent comp, VolumeProfile profile)
        {
            comp.hideFlags = HideFlags.HideInHierarchy; // sub-asset, like Unity's own volume profiles
            AssetDatabase.AddObjectToAsset(comp, profile);
        }

        private static void EnsurePostProcessingOn()
        {
            // A URP Volume only renders if the camera drawing it has post-processing on (mirrors RewindVisuals).
            foreach (Camera cam in Camera.allCameras)
            {
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                    data.renderPostProcessing = true;
            }
        }

        // ---- Base-dust material (dust settled at the foot of props) ----------------------------

        [MenuItem("Tools/Kenya Scooter/Weather and FX/Create Kenya Dust Material (height-tint)", false, 165)]
        public static void CreateDustMaterial()
        {
            Shader shader = Shader.Find("KenyaScooter/BaseDust");
            if (shader == null)
            {
                Debug.LogWarning("[Kenya Weather] Shader 'KenyaScooter/BaseDust' not found yet. Let Unity finish " +
                                 "importing KenyaBaseDust.shader (clear any Console errors first), then run this again.");
                return;
            }
            const string matPath = AssetFolder + "/KenyaBaseDust.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "KenyaBaseDust" };
                mat.SetColor("_DustColor", new Color(0.62f, 0.40f, 0.28f, 1f));
                mat.SetFloat("_DustTop", 0.5f);
                mat.SetFloat("_DustBottom", 0f);
                mat.SetFloat("_DustStrength", 0.85f);
                AssetDatabase.CreateAsset(mat, matPath);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = mat;
            EditorGUIUtility.PingObject(mat);
            Debug.Log("[Kenya Weather] KenyaBaseDust material created. Assign it to flat-shaded props/buildings (set " +
                      "its Base Map to the prop's own texture) so dust settles at their base. Tune Dust Top/Bottom " +
                      "(world Y) and Strength. For LIT props, add the same height-lerp in their Shader Graph instead.");
        }

        // ---- Dust coat (tint props into the red-dust palette, no material swap) ----------------

        [MenuItem("Tools/Kenya Scooter/Weather and FX/Apply Dust Coat to Selection", true)]
        public static bool ApplyDustCoatValidate() => Selection.gameObjects.Length > 0;

        [MenuItem("Tools/Kenya Scooter/Weather and FX/Apply Dust Coat to Selection", false, 166)]
        public static void ApplyDustCoatToSelection()
        {
            int added = 0, skipped = 0, prefabs = 0;
            foreach (GameObject go in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(go))
                {
                    string path = AssetDatabase.GetAssetPath(go);
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    if (root.GetComponent<DustCoat>() == null)
                    {
                        root.AddComponent<DustCoat>();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabs++;
                    }
                    else skipped++;
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    if (go.GetComponent<DustCoat>() == null)
                    {
                        Undo.AddComponent<DustCoat>(go);
                        added++;
                    }
                    else skipped++;
                }
            }
            if (added > 0 && Selection.gameObjects.Length > 0)
                EditorSceneManager.MarkSceneDirty(Selection.gameObjects[0].scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Kenya Weather] Dust Coat: added to {added} scene object(s) and {prefabs} prefab(s); " +
                      $"{skipped} already had one. Select them and drag 'Amount' to taste (multi-edit changes all at once).");
        }

        [MenuItem("Tools/Kenya Scooter/Weather and FX/Remove Dust Coat from Selection", true)]
        public static bool RemoveDustCoatValidate() => Selection.gameObjects.Length > 0;

        [MenuItem("Tools/Kenya Scooter/Weather and FX/Remove Dust Coat from Selection", false, 167)]
        public static void RemoveDustCoatFromSelection()
        {
            int removed = 0;
            foreach (GameObject go in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(go))
                {
                    string path = AssetDatabase.GetAssetPath(go);
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    DustCoat coat = root.GetComponent<DustCoat>();
                    if (coat != null)
                    {
                        Object.DestroyImmediate(coat, true);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        removed++;
                    }
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    DustCoat coat = go.GetComponent<DustCoat>();
                    if (coat != null)
                    {
                        Undo.DestroyObjectImmediate(coat);
                        removed++;
                    }
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Kenya Weather] Dust Coat removed from {removed} object(s); their original look is restored.");
        }
    }
}
