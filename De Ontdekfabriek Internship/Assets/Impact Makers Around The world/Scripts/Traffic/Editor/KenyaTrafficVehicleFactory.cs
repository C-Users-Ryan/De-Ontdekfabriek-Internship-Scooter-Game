using UnityEngine;
using UnityEditor;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// One-click placeholder traffic car, assembled from primitives and pre-wired for every "feel" system, so the
    /// procedural animation/lights/sound all work out of the box with no manual setup (mirrors the roadside-prop
    /// factory). Builds: a root with the collider + all traffic components, a BODY child holding the hull/cabin (so
    /// TrafficVehicleAnimator can tilt it while the root follows the road), four wheels (each an empty the animator
    /// spins, with a disc child), and emissive light strips wired to TrafficVehicleLights. Drop the resulting prefab
    /// into TrafficSpawner's same-direction / oncoming lists. Replace the primitive meshes with real art later — the
    /// wiring stays valid as long as the Body / wheels / light children remain.
    /// </summary>
    public static class KenyaTrafficVehicleFactory
    {
        private const string PrefabFolder = "Assets/Impact Makers Around The world/Prefab/Traffic";
        private const string MatFolder = "Assets/Impact Makers Around The world/Prefab/Traffic/Materials";

        [MenuItem("Tools/Kenya Scooter/Game Setup/Build Traffic Vehicle (placeholder car)", false, 224)]
        public static void Build()
        {
            var root = new GameObject("PlaceholderCar");

            // Trigger collider so the player's kinematic Rigidbody registers the hit (PlayerCollisionHandler).
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.8f, 0f);
            box.size = new Vector3(1.9f, 1.4f, 4.5f);
            box.isTrigger = true;

            Material bodyMat = Mat("CarBody", new Color(0.85f, 0.78f, 0.55f));
            Material glassMat = Mat("CarGlass", new Color(0.15f, 0.18f, 0.20f));
            Material tyreMat = Mat("CarTyre", new Color(0.08f, 0.08f, 0.08f));
            Material lightMat = EmissiveMat("CarLight", new Color(0.10f, 0.02f, 0.02f));

            // Body — the visual the animator leans/dives/bobs (a CHILD, so the root stays free to follow the road).
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            body.localPosition = new Vector3(0f, 0.8f, 0f);
            GameObject hull = Prim(body, PrimitiveType.Cube, "Hull", Vector3.zero, new Vector3(1.9f, 1.1f, 4.5f), Vector3.zero, bodyMat);
            Prim(body, PrimitiveType.Cube, "Cabin", new Vector3(0f, 0.75f, -0.3f), new Vector3(1.75f, 0.7f, 3.2f), Vector3.zero, glassMat);

            // Wheels — each an empty the animator spins around X, with a disc child rotated to read as a wheel.
            var wheels = new Transform[4];
            Vector3[] pos = {
                new Vector3(-0.95f, 0.35f, 1.5f), new Vector3(0.95f, 0.35f, 1.5f),
                new Vector3(-0.95f, 0.35f, -1.5f), new Vector3(0.95f, 0.35f, -1.5f)
            };
            for (int i = 0; i < 4; i++)
            {
                var w = new GameObject("Wheel" + i).transform;
                w.SetParent(root.transform, false);
                w.localPosition = pos[i];
                Prim(w, PrimitiveType.Cylinder, "Tyre", Vector3.zero, new Vector3(0.7f, 0.1f, 0.7f), new Vector3(0f, 0f, 90f), tyreMat);
                wheels[i] = w;
            }

            // Emissive light strips — TrafficVehicleLights drives their _EmissionColor.
            Renderer rear = Prim(body, PrimitiveType.Cube, "RearLights", new Vector3(0f, -0.1f, -2.28f), new Vector3(1.7f, 0.18f, 0.06f), Vector3.zero, lightMat).GetComponent<Renderer>();
            Renderer head = Prim(body, PrimitiveType.Cube, "Headlights", new Vector3(0f, -0.1f, 2.28f), new Vector3(1.7f, 0.18f, 0.06f), Vector3.zero, lightMat).GetComponent<Renderer>();
            Renderer indL = Prim(body, PrimitiveType.Cube, "IndicatorL", new Vector3(-0.85f, -0.1f, -2.3f), new Vector3(0.18f, 0.18f, 0.06f), Vector3.zero, lightMat).GetComponent<Renderer>();
            Renderer indR = Prim(body, PrimitiveType.Cube, "IndicatorR", new Vector3(0.85f, -0.1f, -2.3f), new Vector3(0.18f, 0.18f, 0.06f), Vector3.zero, lightMat).GetComponent<Renderer>();

            // Components (Awake runs at play, not now — the audio/particle children are created at runtime).
            var vehicle = root.AddComponent<TrafficVehicle>();
            root.AddComponent<TrafficHorn>(); // auto-adds an AudioSource (RequireComponent)
            root.AddComponent<TrafficEngine>();
            var lights = root.AddComponent<TrafficVehicleLights>();
            var anim = root.AddComponent<TrafficVehicleAnimator>();
            root.AddComponent<TrafficExhaust>();

            // Wiring.
            var sv = new SerializedObject(vehicle);
            SetObj(sv, "bodyRenderer", hull.GetComponent<Renderer>());
            sv.ApplyModifiedPropertiesWithoutUndo();

            var sa = new SerializedObject(anim);
            SetObj(sa, "body", body);
            SetArray(sa, "wheels", wheels);
            sa.ApplyModifiedPropertiesWithoutUndo();

            var sl = new SerializedObject(lights);
            SetArray(sl, "rearLights", new Object[] { rear });
            SetArray(sl, "headlights", new Object[] { head });
            SetObj(sl, "leftIndicator", indL);
            SetObj(sl, "rightIndicator", indR);
            sl.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(PrefabFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(PrefabFolder + "/PlaceholderCar.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null) { Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab); }
            Debug.Log("[KenyaTrafficVehicleFactory] Built " + path + " — assign it to TrafficSpawner's sameDirectionPrefabs / oncomingPrefabs.");
        }

        // ---- helpers (mirror the roadside-prop factory) ---------------------------------------------------

        private static GameObject Prim(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            var col = g.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col); // the root's trigger box is the only collider
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            var mr = g.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
            return g;
        }

        private static Material Mat(string name, Color color)
        {
            EnsureFolder(MatFolder);
            string path = MatFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                m = new Material(sh) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material EmissiveMat(string name, Color baseColor)
        {
            Material m = Mat(name, baseColor);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black); // lights start off; the component drives them
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void SetObj(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject so, string field, Object[] values)
        {
            var p = so.FindProperty(field);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
