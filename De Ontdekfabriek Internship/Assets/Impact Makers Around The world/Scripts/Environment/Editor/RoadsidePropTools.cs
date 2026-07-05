using UnityEngine;
using UnityEditor;
using KenyaScooter.Config;
using KenyaScooter.Environment;

namespace KenyaScooter.EnvironmentEditor
{
    /// <summary>
    /// One-click authoring for the roadside-life feature (Oplevering insight 2). The point is transferability:
    /// a non-technical successor scatters life along the road by creating a config, dragging prop prefabs in,
    /// and (optionally) dropping a placeholder to test before the real art exists. No code, no internals.
    ///
    /// Menu items:
    ///  - Create Roadside Prop Config — the one authoring asset (sensible defaults; add prop rows in the Inspector).
    ///  - Create Roadside Prop Placeholder — a capsule prop you can drag into the config to see it working now.
    ///  - Create Roadside Prop Spawner — an explicit scene spawner (the spawner also self-bootstraps from the config,
    ///    so this is only needed if you want a guaranteed serialized reference, e.g. to be sure the asset ships).
    /// </summary>
    public static class RoadsidePropTools
    {
        private const string Parent = "Assets/Impact Makers Around The world";
        private const string Folder = Parent + "/Generated";

        [MenuItem("Tools/Kenya Scooter/Roadside Props/Create Roadside Prop Config", false, 140)]
        public static void CreateConfig()
        {
            EnsureFolder();
            // CreateInstance runs the C# field initialisers, so the master switch, density, day-cycle multipliers
            // and placement defaults are all populated — the asset is usable the moment it exists.
            var config = ScriptableObject.CreateInstance<RoadsidePropConfig>();
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/RoadsidePropConfig.asset");
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            EditorUtility.DisplayDialog("Kenya Scooter",
                "Created a Roadside Prop Config.\n\n" +
                "Next:\n" +
                "1. Press + on its 'Props' list for each kind of prop (stall, person, windmill, animal...).\n" +
                "2. Drag a prop prefab into each row's 'Prefab' slot. Each prefab needs a RoadsideProp component\n" +
                "   on its root (use Create Roadside Prop Placeholder for a quick test prop).\n" +
                "3. Tune side / depth / weight / zone tags per row, and the overall density at the top.\n\n" +
                "The spawner finds this config automatically — no scene wiring needed. The facilitator menu's\n" +
                "'Leven langs de weg' toggle and 'Drukte langs de weg' dial drive it live.", "Got it");
        }

        [MenuItem("Tools/Kenya Scooter/Roadside Props/Create Roadside Prop Placeholder", false, 141)]
        public static void CreatePlaceholder()
        {
            var root = new GameObject("RoadsideProp_Placeholder");
            Undo.RegisterCreatedObjectUndo(root, "Create Roadside Prop");
            root.AddComponent<RoadsideProp>();

            // A simple visible body so the prop is not an invisible empty. Pure scenery, so strip the collider
            // the primitive ships with (props sit beyond the player's reach and never need to be hit).
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body (placeholder — swap for real art)";
            var col = body.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            body.transform.SetParent(root.transform, false);
            var mr = body.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = Placeholder("Roadside Prop (placeholder)", new Color(0.80f, 0.55f, 0.35f));

            if (SceneView.lastActiveSceneView != null)
                root.transform.position = SceneView.lastActiveSceneView.pivot;
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            EditorUtility.DisplayDialog("Kenya Scooter",
                "Created a placeholder roadside prop (a capsule with a RoadsideProp component).\n\n" +
                "Next:\n" +
                "1. Drag it into a Prefabs folder to make it a prefab.\n" +
                "2. Drag that prefab into a Roadside Prop Config row's 'Prefab' slot.\n\n" +
                "Swap the capsule child for your real model when it is ready. For a windmill, add a WindmillRotor\n" +
                "and assign its blades child; for a person, add a RoadsideWaver and assign an arm/body child.", "Got it");
        }

        [MenuItem("Tools/Kenya Scooter/Roadside Props/Create Roadside Prop Spawner", false, 142)]
        public static void CreateSpawner()
        {
            var go = new GameObject("RoadsidePropSpawner");
            Undo.RegisterCreatedObjectUndo(go, "Create Roadside Prop Spawner");
            var spawner = go.AddComponent<RoadsidePropSpawner>();

            // Auto-link the config so the reference is SERIALIZED in the scene. This matters for builds: the
            // spawner can find the config at runtime via ConfigLocator, but a config (and the prop prefabs it
            // references) that NOTHING holds a serialized reference to may be stripped from the build and the
            // roadside life would silently never appear on device — even though it works in the Editor. The same
            // build-inclusion lesson the Weather/DustAtmosphere tool documents.
            RoadsidePropConfig cfg = FindFirstConfig();
            if (cfg != null)
            {
                var so = new SerializedObject(spawner);
                var prop = so.FindProperty("config");
                if (prop != null)
                {
                    prop.objectReferenceValue = cfg;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            EditorUtility.DisplayDialog("Kenya Scooter",
                cfg != null
                    ? "Created a Roadside Prop Spawner and linked it to '" + cfg.name + "'.\n\n" +
                      "Save the scene to keep it. The serialized reference guarantees the config and its prop\n" +
                      "prefabs are included in the build (a runtime-only lookup can be stripped)."
                    : "Created a Roadside Prop Spawner in the scene.\n\n" +
                      "No Roadside Prop Config was found yet — create one (Tools > Kenya Scooter > Roadside Props >\n" +
                      "Create Roadside Prop Config), then assign it to this spawner's 'Config' field so it ships in the build.",
                "Got it");
        }

        /// <summary>The first RoadsidePropConfig asset in the project, or null. Used to auto-link a new spawner.</summary>
        private static RoadsidePropConfig FindFirstConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:RoadsidePropConfig");
            if (guids == null || guids.Length == 0)
                return null;
            return AssetDatabase.LoadAssetAtPath<RoadsidePropConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(Parent, "Generated");
        }

        /// <summary>Gets or creates a simple coloured URP/Standard material so the placeholder is visible, not magenta.</summary>
        private static Material Placeholder(string assetName, Color color)
        {
            EnsureFolder();
            string path = Folder + "/" + assetName + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

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
