using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using KenyaScooter.Config;
using KenyaScooter.Environment;

namespace KenyaScooter.EnvironmentEditor
{
    /// <summary>
    /// Builds a whole palette of flat-shaded, low-poly KENYAN ROADSIDE PROPS from Unity primitives, with NO
    /// imported art, so the "it feels empty" fix has real content to show on day one. One button
    /// (Tools > Kenya Scooter > Roadside Props > Build Kenya Roadside Starter) generates the prefabs, a RoadsidePropConfig wired
    /// to all of them, and a scene spawner linked to it: press Play and the roadside is alive.
    ///
    /// Efficiency: every prop is assembled from the shared primitive meshes (cube/cylinder/sphere) and a small set
    /// of shared, GPU-instancing-enabled materials, so a field of dozens of props collapses into a handful of
    /// instanced draw calls. Colliders are stripped (props are pure scenery). The windmill's blades are a spinning
    /// child wired to WindmillRotor; a person's arm is wired to RoadsideWaver, so they move, not stand dead.
    ///
    /// Re-runnable (fool-proof): prefabs and the config use fixed asset paths (overwritten in place), and an
    /// existing scene spawner is reused, so pressing the button again refreshes the art without making duplicates.
    /// Swap any generated prefab for real modelled art later by dropping it into the config row; nothing else changes.
    /// </summary>
    public static class KenyaRoadsidePropFactory
    {
        private const string Root = "Assets/Impact Makers Around The world";
        private const string Gen = Root + "/Generated";
        private const string PropsFolder = Gen + "/Roadside Props";
        private const string MatFolder = Gen + "/Roadside Materials";
        private const string ConfigPath = Gen + "/RoadsidePropConfig.asset";

        // ---- Kenya palette (warm laterite, acacia, market cloth, township ads) -------------------------------
        private static readonly Color Laterite   = new Color(0.62f, 0.30f, 0.18f);
        private static readonly Color Sand       = new Color(0.85f, 0.74f, 0.55f);
        private static readonly Color AcaciaGreen= new Color(0.42f, 0.50f, 0.24f);
        private static readonly Color Trunk      = new Color(0.40f, 0.28f, 0.18f);
        private static readonly Color Wood       = new Color(0.50f, 0.35f, 0.22f);
        private static readonly Color ClothRed   = new Color(0.80f, 0.16f, 0.14f); // Coca-Cola red
        private static readonly Color ClothBlue  = new Color(0.16f, 0.34f, 0.62f);
        private static readonly Color ClothYellow= new Color(0.93f, 0.74f, 0.18f);
        private static readonly Color ClothGreen = new Color(0.10f, 0.55f, 0.30f); // Safaricom green
        private static readonly Color Skin        = new Color(0.55f, 0.38f, 0.26f);
        private static readonly Color Trousers   = new Color(0.22f, 0.22f, 0.26f);
        private static readonly Color Metal      = new Color(0.86f, 0.86f, 0.82f);
        private static readonly Color Cream      = new Color(0.93f, 0.90f, 0.80f);
        private static readonly Color Black      = new Color(0.09f, 0.09f, 0.10f);
        private static readonly Color SolarBlue  = new Color(0.12f, 0.18f, 0.38f);
        private static readonly Color Grey       = new Color(0.50f, 0.50f, 0.52f);
        private static readonly Color Corrugated = new Color(0.55f, 0.55f, 0.58f);
        private static readonly Color Ochre      = new Color(0.78f, 0.62f, 0.40f);
        private static readonly Color ProduceA   = new Color(0.92f, 0.52f, 0.12f);
        private static readonly Color ProduceB   = new Color(0.40f, 0.60f, 0.20f);

        private static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();

        [MenuItem("Tools/Kenya Scooter/Roadside Props/Build Kenya Roadside Starter", false, 120)]
        public static void BuildStarter()
        {
            MatCache.Clear();
            EnsureFolder(PropsFolder);
            EnsureFolder(MatFolder);

            // --- Build the prop palette (each returns the saved prefab's RoadsideProp) ---
            RoadsideProp stallRed    = BuildStall("Red", ClothRed);
            RoadsideProp stallBlue   = BuildStall("Blue", ClothBlue);
            RoadsideProp stallYellow = BuildStall("Yellow", ClothYellow);
            RoadsideProp duka        = BuildDuka();
            RoadsideProp personA     = BuildPerson("A", ClothBlue);
            RoadsideProp personB     = BuildPerson("B", ClothGreen);
            RoadsideProp personC     = BuildPerson("C", ClothYellow);
            RoadsideProp windmill    = BuildWindmill();
            RoadsideProp solar       = BuildSolar();
            RoadsideProp acacia      = BuildAcacia();
            RoadsideProp elephant    = BuildElephant();
            RoadsideProp goat        = BuildGoat();
            RoadsideProp cattle      = BuildCattle();
            RoadsideProp bollard     = BuildBollard();
            RoadsideProp grass       = BuildGrassTuft();
            RoadsideProp laundry     = BuildLaundry();
            RoadsideProp chickens    = BuildChickens();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // --- Build (or refresh) the config wired to every prop ---
            // Tags left EMPTY so everything spawns everywhere out of the box (a full, alive roadside on first Play,
            // whatever the road zones are tagged). To get the town-crowded / country-sparse contrast later, set each
            // row's Required Context Tags to match your RoadSequence contextTags (e.g. township vs savanna).
            var entries = new List<RoadsidePropConfig.PropEntry>();
            void Add(RoadsideProp p, float weight, Vector2 depth, Vector2 scale, float yaw, int pool)
            {
                if (p == null) return;
                entries.Add(new RoadsidePropConfig.PropEntry
                {
                    prefab = p, weight = weight, side = RoadsidePropConfig.RoadSide.Either,
                    lateralDepthRange = depth, scaleRange = scale, yawJitter = yaw,
                    requiredContextTags = new string[0], poolSize = pool
                });
            }

            Add(stallRed,    1.0f, new Vector2(0.5f, 3.0f), new Vector2(0.92f, 1.08f), 16f, 4);
            Add(stallBlue,   1.0f, new Vector2(0.5f, 3.0f), new Vector2(0.92f, 1.08f), 16f, 4);
            Add(stallYellow, 1.0f, new Vector2(0.5f, 3.0f), new Vector2(0.92f, 1.08f), 16f, 4);
            Add(duka,        0.7f, new Vector2(1.0f, 4.0f), new Vector2(0.90f, 1.15f), 12f, 3);
            Add(personA,     1.3f, new Vector2(0.0f, 2.5f), new Vector2(0.92f, 1.06f), 30f, 5);
            Add(personB,     1.3f, new Vector2(0.0f, 2.5f), new Vector2(0.92f, 1.06f), 30f, 5);
            Add(personC,     1.3f, new Vector2(0.0f, 2.5f), new Vector2(0.92f, 1.06f), 30f, 5);
            Add(windmill,    0.8f, new Vector2(2.0f, 8.0f), new Vector2(0.90f, 1.20f), 30f, 3);
            Add(solar,       0.7f, new Vector2(1.5f, 6.0f), new Vector2(0.90f, 1.15f), 22f, 3);
            Add(acacia,      1.4f, new Vector2(1.5f, 7.0f), new Vector2(0.85f, 1.30f), 45f, 4);
            Add(elephant,    0.5f, new Vector2(3.0f, 9.0f), new Vector2(0.90f, 1.20f), 45f, 2);
            Add(goat,        1.2f, new Vector2(0.5f, 5.0f), new Vector2(0.85f, 1.10f), 40f, 5); // herds of goats wander/graze the verge
            Add(cattle,      0.9f, new Vector2(1.5f, 7.0f), new Vector2(0.90f, 1.15f), 40f, 4); // zebu amble further back
            Add(bollard,     1.0f, new Vector2(0.0f, 0.4f), new Vector2(0.90f, 1.10f),  0f, 6);
            Add(grass,       2.6f, new Vector2(0.0f, 1.5f), new Vector2(0.75f, 1.35f), 20f, 12); // dense: the verge IS the wind read
            Add(laundry,     0.5f, new Vector2(1.5f, 5.0f), new Vector2(0.95f, 1.10f), 14f, 3);  // near homesteads: someone washed this morning
            Add(chickens,    0.7f, new Vector2(0.5f, 3.0f), new Vector2(0.90f, 1.10f), 30f, 3);  // the flock that notices you (scatters OUTWARD)

            RoadsidePropConfig config = AssetDatabase.LoadAssetAtPath<RoadsidePropConfig>(ConfigPath);
            bool newConfig = config == null;
            if (newConfig)
                config = ScriptableObject.CreateInstance<RoadsidePropConfig>();
            config.props = entries.ToArray();
            if (newConfig)
                AssetDatabase.CreateAsset(config, ConfigPath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // --- Place (or reuse) a scene spawner linked to the config ---
            var spawner = Object.FindObjectOfType<RoadsidePropSpawner>();
            if (spawner == null)
            {
                var go = new GameObject("RoadsidePropSpawner");
                Undo.RegisterCreatedObjectUndo(go, "Create Roadside Prop Spawner");
                spawner = go.AddComponent<RoadsidePropSpawner>();
            }
            var so = new SerializedObject(spawner);
            var prop = so.FindProperty("config");
            if (prop != null) { prop.objectReferenceValue = config; so.ApplyModifiedPropertiesWithoutUndo(); }

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            EditorUtility.DisplayDialog("Kenya Scooter",
                "Built the Kenya roadside starter: " + entries.Count + " props, a wired config and a scene spawner.\n\n" +
                "Press Play: stalls, dukas (with cooking smoke), people (who wave), windmills (turning), solar\n" +
                "panels, acacias (swaying), grass tufts (leaning with the wind), laundry lines (fluttering),\n" +
                "chicken flocks (that scatter as you pass), an elephant and curb bollards now line both\n" +
                "shoulders, riding the road, the wind and the day cycle.\n\n" +
                "Tune it: the facilitator menu's 'Drukte langs de weg' dial sets density; edit the config rows for\n" +
                "side / depth / weight. For a town-crowded vs country-sparse blend, set each row's Required Context\n" +
                "Tags to your RoadSequence tags. Swap any generated prefab for real modelled art any time.\n\n" +
                "Save the scene to keep the spawner.", "Got it");
        }

        // ---- Prop builders -------------------------------------------------------------------------------------

        private static RoadsideProp BuildStall(string variant, Color cloth)
        {
            GameObject root = NewRoot("Kraampje_" + variant);
            Transform t = root.transform;
            Material wood = Mat("Wood", Wood);
            Prim(t, PrimitiveType.Cube, "Counter", new Vector3(0f, 0.50f, 0f), new Vector3(2.2f, 0.90f, 1.0f), Vector3.zero, wood);
            Prim(t, PrimitiveType.Cube, "Post1", new Vector3(-1.0f, 0.95f, -0.4f), new Vector3(0.10f, 1.90f, 0.10f), Vector3.zero, wood);
            Prim(t, PrimitiveType.Cube, "Post2", new Vector3( 1.0f, 0.95f, -0.4f), new Vector3(0.10f, 1.90f, 0.10f), Vector3.zero, wood);
            Prim(t, PrimitiveType.Cube, "Post3", new Vector3(-1.0f, 0.95f,  0.4f), new Vector3(0.10f, 1.90f, 0.10f), Vector3.zero, wood);
            Prim(t, PrimitiveType.Cube, "Post4", new Vector3( 1.0f, 0.95f,  0.4f), new Vector3(0.10f, 1.90f, 0.10f), Vector3.zero, wood);
            Prim(t, PrimitiveType.Cube, "Canopy", new Vector3(0f, 1.95f, 0.05f), new Vector3(2.5f, 0.08f, 1.4f), new Vector3(-8f, 0f, 0f), Mat("Cloth_" + variant, cloth));
            Prim(t, PrimitiveType.Cube, "Goods1", new Vector3(-0.6f, 1.02f, 0f), new Vector3(0.30f, 0.30f, 0.30f), Vector3.zero, Mat("ProduceA", ProduceA));
            Prim(t, PrimitiveType.Cube, "Goods2", new Vector3( 0.0f, 1.02f, 0f), new Vector3(0.30f, 0.30f, 0.30f), Vector3.zero, Mat("ProduceB", ProduceB));
            Prim(t, PrimitiveType.Cube, "Goods3", new Vector3( 0.6f, 1.02f, 0f), new Vector3(0.30f, 0.30f, 0.30f), Vector3.zero, Mat("Cloth_" + variant, cloth));
            return SavePrefab(root, "Kraampje_" + variant);
        }

        private static RoadsideProp BuildDuka()
        {
            GameObject root = NewRoot("Duka");
            Transform t = root.transform;
            Prim(t, PrimitiveType.Cube, "Wall", new Vector3(0f, 0.90f, 0f), new Vector3(2.2f, 1.8f, 2.0f), Vector3.zero, Mat("Wall", Ochre));
            Prim(t, PrimitiveType.Cube, "Roof", new Vector3(0f, 1.86f, 0f), new Vector3(2.45f, 0.12f, 2.2f), new Vector3(-4f, 0f, 0f), Mat("Corrugated", Corrugated));
            Prim(t, PrimitiveType.Cube, "Door", new Vector3(0f, 0.70f, 1.01f), new Vector3(0.65f, 1.3f, 0.06f), Vector3.zero, Mat("Black", Black));
            Prim(t, PrimitiveType.Cube, "AdBand", new Vector3(0f, 1.45f, 1.01f), new Vector3(2.2f, 0.30f, 0.04f), Vector3.zero, Mat("Cloth_Red", ClothRed));

            // The cooking-fire smoke column (Levend Kenia §4.3): a SmokePoint behind the roof line marks the
            // hearth; the component builds its own wind-bent column there and follows the day phase on its own.
            var smokePoint = new GameObject("SmokePoint");
            smokePoint.transform.SetParent(t, false);
            smokePoint.transform.localPosition = new Vector3(0.6f, 1.95f, -0.55f);
            root.AddComponent<KenyaScooter.FX.SmokeColumn>();

            return SavePrefab(root, "Duka");
        }

        private static RoadsideProp BuildPerson(string variant, Color shirt)
        {
            GameObject root = NewRoot("Person_" + variant);
            Transform body = new GameObject("Body").transform; // the walker moves this so the person mills about the verge
            body.SetParent(root.transform, false);
            Transform t = body; // build all the parts under Body, so RoadsideWalker can move the whole figure
            Material sh = Mat("Shirt_" + variant, shirt);
            Material tr = Mat("Trousers", Trousers);
            Material sk = Mat("Skin", Skin);
            Prim(t, PrimitiveType.Cube, "LegL", new Vector3(-0.12f, 0.40f, 0f), new Vector3(0.18f, 0.80f, 0.18f), Vector3.zero, tr);
            Prim(t, PrimitiveType.Cube, "LegR", new Vector3( 0.12f, 0.40f, 0f), new Vector3(0.18f, 0.80f, 0.18f), Vector3.zero, tr);
            Prim(t, PrimitiveType.Cube, "Torso", new Vector3(0f, 1.15f, 0f), new Vector3(0.46f, 0.72f, 0.26f), Vector3.zero, sh);
            Prim(t, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.66f, 0f), new Vector3(0.32f, 0.34f, 0.32f), Vector3.zero, sk);
            Prim(t, PrimitiveType.Cube, "ArmL", new Vector3(-0.30f, 1.15f, 0f), new Vector3(0.12f, 0.60f, 0.12f), Vector3.zero, sh);

            // Right arm = the waving arm: an empty pivot at the shoulder, with the arm hanging from it, so RoadsideWaver
            // can raise and rock it about the shoulder.
            var arm = new GameObject("Arm");
            arm.transform.SetParent(t, false);
            arm.transform.localPosition = new Vector3(0.30f, 1.48f, 0f);
            Prim(arm.transform, PrimitiveType.Cube, "Upper", new Vector3(0f, -0.27f, 0f), new Vector3(0.12f, 0.55f, 0.12f), Vector3.zero, sh);
            Prim(arm.transform, PrimitiveType.Sphere, "Hand", new Vector3(0f, -0.55f, 0f), new Vector3(0.15f, 0.15f, 0.15f), Vector3.zero, sk);

            var waver = root.AddComponent<RoadsideWaver>();
            var so = new SerializedObject(waver);
            var armProp = so.FindProperty("waveArm");
            if (armProp != null) { armProp.objectReferenceValue = arm.transform; so.ApplyModifiedPropertiesWithoutUndo(); }
            WireWalker(root, body, 0.7f, 1.4f, 0.4f); // the person now mills about the verge (waves AND moves)
            return SavePrefab(root, "Person_" + variant);
        }

        // Adds a RoadsideWalker that moves the given body child (people mill, animals graze). See RoadsideWalker.
        private static void WireWalker(GameObject root, Transform body, float speed, float radius, float pauseChance)
        {
            var walker = root.AddComponent<RoadsideWalker>();
            var so = new SerializedObject(walker);
            SetObj(so, "body", body);
            SetFloat(so, "speed", speed);
            SetFloat(so, "radius", radius);
            SetFloat(so, "pauseChance", pauseChance);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RoadsideProp BuildGoat()
        {
            GameObject root = NewRoot("Goat");
            Transform body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            Material hide = Mat("GoatHide", new Color(0.55f, 0.50f, 0.42f));
            Material dark = Mat("GoatDark", new Color(0.20f, 0.18f, 0.16f));
            Prim(body, PrimitiveType.Cube, "Torso", new Vector3(0f, 0.50f, 0f), new Vector3(0.35f, 0.35f, 0.75f), Vector3.zero, hide);
            Prim(body, PrimitiveType.Cube, "Head", new Vector3(0f, 0.62f, 0.50f), new Vector3(0.22f, 0.24f, 0.28f), Vector3.zero, hide);
            Prim(body, PrimitiveType.Cube, "LegFL", new Vector3(-0.12f, 0.20f, 0.28f), new Vector3(0.08f, 0.40f, 0.08f), Vector3.zero, dark);
            Prim(body, PrimitiveType.Cube, "LegFR", new Vector3( 0.12f, 0.20f, 0.28f), new Vector3(0.08f, 0.40f, 0.08f), Vector3.zero, dark);
            Prim(body, PrimitiveType.Cube, "LegBL", new Vector3(-0.12f, 0.20f, -0.28f), new Vector3(0.08f, 0.40f, 0.08f), Vector3.zero, dark);
            Prim(body, PrimitiveType.Cube, "LegBR", new Vector3( 0.12f, 0.20f, -0.28f), new Vector3(0.08f, 0.40f, 0.08f), Vector3.zero, dark);
            WireWalker(root, body, 0.4f, 2.5f, 0.55f); // graze: slow, wide, lots of pauses
            return SavePrefab(root, "Goat");
        }

        private static RoadsideProp BuildCattle()
        {
            GameObject root = NewRoot("Zebu");
            Transform body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            Material hide = Mat("ZebuHide", new Color(0.72f, 0.60f, 0.48f));
            Material dark = Mat("ZebuDark", new Color(0.28f, 0.22f, 0.18f));
            Prim(body, PrimitiveType.Cube, "Torso", new Vector3(0f, 0.85f, 0f), new Vector3(0.60f, 0.60f, 1.40f), Vector3.zero, hide);
            Prim(body, PrimitiveType.Cube, "Hump", new Vector3(0f, 1.25f, 0.35f), new Vector3(0.50f, 0.35f, 0.50f), Vector3.zero, hide);
            Prim(body, PrimitiveType.Cube, "Head", new Vector3(0f, 1.00f, 0.90f), new Vector3(0.36f, 0.40f, 0.50f), Vector3.zero, hide);
            Prim(body, PrimitiveType.Cube, "LegFL", new Vector3(-0.20f, 0.35f, 0.50f), new Vector3(0.12f, 0.70f, 0.12f), Vector3.zero, dark);
            Prim(body, PrimitiveType.Cube, "LegFR", new Vector3( 0.20f, 0.35f, 0.50f), new Vector3(0.12f, 0.70f, 0.12f), Vector3.zero, dark);
            Prim(body, PrimitiveType.Cube, "LegBL", new Vector3(-0.20f, 0.35f, -0.50f), new Vector3(0.12f, 0.70f, 0.12f), Vector3.zero, dark);
            Prim(body, PrimitiveType.Cube, "LegBR", new Vector3( 0.20f, 0.35f, -0.50f), new Vector3(0.12f, 0.70f, 0.12f), Vector3.zero, dark);
            WireWalker(root, body, 0.35f, 3.0f, 0.6f); // amble and graze slowly
            return SavePrefab(root, "Zebu");
        }

        private static RoadsideProp BuildWindmill()
        {
            GameObject root = NewRoot("Windmill");
            Transform t = root.transform;
            Material metal = Mat("Metal", Metal);
            Prim(t, PrimitiveType.Cylinder, "Tower", new Vector3(0f, 3.0f, 0f), new Vector3(0.18f, 3.0f, 0.18f), Vector3.zero, metal); // 6 m
            Prim(t, PrimitiveType.Cube, "Hub", new Vector3(0f, 6.0f, 0.18f), new Vector3(0.35f, 0.35f, 0.30f), Vector3.zero, metal);

            var blades = new GameObject("Blades");
            blades.transform.SetParent(t, false);
            blades.transform.localPosition = new Vector3(0f, 6.0f, 0.34f);
            Material blade = Mat("Blade", Cream);
            const int n = 6;
            for (int k = 0; k < n; k++)
            {
                float ang = k * 360f / n;
                float rad = ang * Mathf.Deg2Rad;
                var pos = new Vector3(-0.85f * Mathf.Sin(rad), 0.85f * Mathf.Cos(rad), 0f);
                Prim(blades.transform, PrimitiveType.Cube, "Blade" + k, pos, new Vector3(0.16f, 1.7f, 0.05f), new Vector3(0f, 0f, ang), blade);
            }

            var rotor = root.AddComponent<WindmillRotor>();
            var so = new SerializedObject(rotor);
            SetObj(so, "blades", blades.transform);
            SetVec(so, "spinAxis", new Vector3(0f, 0f, 1f));
            SetFloat(so, "degreesPerSecond", 50f);
            SetFloat(so, "speedJitter", 18f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(root, "Windmill");
        }

        private static RoadsideProp BuildSolar()
        {
            GameObject root = NewRoot("SolarPanel");
            Transform t = root.transform;
            Prim(t, PrimitiveType.Cylinder, "Post", new Vector3(0f, 0.50f, 0f), new Vector3(0.08f, 0.50f, 0.08f), Vector3.zero, Mat("Metal", Metal));
            Prim(t, PrimitiveType.Cube, "Panel", new Vector3(0f, 1.05f, 0f), new Vector3(1.7f, 0.06f, 1.1f), new Vector3(-32f, 0f, 0f), Mat("SolarBlue", SolarBlue, 0.45f));
            return SavePrefab(root, "SolarPanel");
        }

        private static RoadsideProp BuildAcacia()
        {
            GameObject root = NewRoot("Acacia");
            Transform t = root.transform;
            Prim(t, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 1.40f, 0f), new Vector3(0.18f, 1.40f, 0.18f), Vector3.zero, Mat("Trunk", Trunk));

            // The crowns hang from a Canopy pivot at the trunk top so GrassSway can lean them with the wind —
            // lagged half a second, because big things answer slowly (Levend Kenia §4.4).
            var canopy = new GameObject("Canopy");
            canopy.transform.SetParent(t, false);
            canopy.transform.localPosition = new Vector3(0f, 2.60f, 0f);
            Prim(canopy.transform, PrimitiveType.Cylinder, "Crown1", new Vector3(0f, 0.30f, 0f), new Vector3(2.6f, 0.22f, 2.6f), Vector3.zero, Mat("Acacia", AcaciaGreen));
            Prim(canopy.transform, PrimitiveType.Cylinder, "Crown2", new Vector3(0.2f, 0.60f, 0.1f), new Vector3(1.8f, 0.20f, 1.8f), Vector3.zero, Mat("Acacia", AcaciaGreen));

            var tuft = root.AddComponent<GrassTuft>();
            var so = new SerializedObject(tuft);
            SetObj(so, "body", canopy.transform);
            SetFloat(so, "leanScale", 0.35f);
            SetFloat(so, "lagSeconds", 0.5f);
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, "Acacia");
        }

        private static RoadsideProp BuildElephant()
        {
            GameObject root = NewRoot("Elephant");
            Transform t = root.transform;
            Material g = Mat("Elephant", Grey);
            Prim(t, PrimitiveType.Cube, "Body", new Vector3(0f, 1.35f, 0f), new Vector3(1.0f, 1.3f, 2.0f), Vector3.zero, g);
            Prim(t, PrimitiveType.Cube, "Head", new Vector3(0f, 1.50f, 1.25f), new Vector3(0.85f, 0.85f, 0.70f), Vector3.zero, g);
            Prim(t, PrimitiveType.Cube, "Trunk1", new Vector3(0f, 1.15f, 1.65f), new Vector3(0.28f, 0.60f, 0.28f), new Vector3(28f, 0f, 0f), g);
            Prim(t, PrimitiveType.Cube, "Trunk2", new Vector3(0f, 0.72f, 1.80f), new Vector3(0.22f, 0.50f, 0.22f), new Vector3(52f, 0f, 0f), g);
            Prim(t, PrimitiveType.Cube, "LegFL", new Vector3(-0.35f, 0.50f,  0.70f), new Vector3(0.32f, 1.0f, 0.32f), Vector3.zero, g);
            Prim(t, PrimitiveType.Cube, "LegFR", new Vector3( 0.35f, 0.50f,  0.70f), new Vector3(0.32f, 1.0f, 0.32f), Vector3.zero, g);
            Prim(t, PrimitiveType.Cube, "LegBL", new Vector3(-0.35f, 0.50f, -0.70f), new Vector3(0.32f, 1.0f, 0.32f), Vector3.zero, g);
            Prim(t, PrimitiveType.Cube, "LegBR", new Vector3( 0.35f, 0.50f, -0.70f), new Vector3(0.32f, 1.0f, 0.32f), Vector3.zero, g);
            Prim(t, PrimitiveType.Cube, "EarL", new Vector3(-0.50f, 1.60f, 1.10f), new Vector3(0.10f, 0.60f, 0.50f), new Vector3(0f, 0f, 15f), g);
            Prim(t, PrimitiveType.Cube, "EarR", new Vector3( 0.50f, 1.60f, 1.10f), new Vector3(0.10f, 0.60f, 0.50f), new Vector3(0f, 0f, -15f), g);
            return SavePrefab(root, "Elephant");
        }

        /// <summary>A verge grass tuft: crossed dry blades under a Blades pivot the GrassSway manager leans
        /// with the wind. Spawned densely, this is the highest-coverage wind read in the game (Levend Kenia §4.4).</summary>
        private static RoadsideProp BuildGrassTuft()
        {
            GameObject root = NewRoot("GrassTuft");
            var blades = new GameObject("Blades");
            blades.transform.SetParent(root.transform, false);
            Material dry = Mat("DryGrass", new Color(0.72f, 0.62f, 0.32f));
            Prim(blades.transform, PrimitiveType.Cube, "Blade1", new Vector3(0f, 0.28f, 0f), new Vector3(0.05f, 0.56f, 0.05f), new Vector3(0f, 0f, 6f), dry);
            Prim(blades.transform, PrimitiveType.Cube, "Blade2", new Vector3(-0.08f, 0.24f, 0.04f), new Vector3(0.05f, 0.48f, 0.05f), new Vector3(4f, 30f, -10f), dry);
            Prim(blades.transform, PrimitiveType.Cube, "Blade3", new Vector3(0.08f, 0.22f, -0.05f), new Vector3(0.05f, 0.44f, 0.05f), new Vector3(-5f, -40f, 12f), dry);
            Prim(blades.transform, PrimitiveType.Cube, "Blade4", new Vector3(0.02f, 0.20f, 0.08f), new Vector3(0.05f, 0.40f, 0.05f), new Vector3(8f, 70f, -5f), dry);

            var tuft = root.AddComponent<GrassTuft>();
            var so = new SerializedObject(tuft);
            SetObj(so, "body", blades.transform);
            so.ApplyModifiedPropertiesWithoutUndo(); // grass keeps the defaults: full lean, no lag

            return SavePrefab(root, "GrassTuft");
        }

        /// <summary>A homestead laundry line (Levend Kenia §4.6): two poles, a line, and five bright garments
        /// hinged at the top edge for ClothesLine to swing with the wind. Domestic life without a character.</summary>
        private static RoadsideProp BuildLaundry()
        {
            GameObject root = NewRoot("LaundryLine");
            Transform t = root.transform;
            Material wood = Mat("Wood", Wood);
            Prim(t, PrimitiveType.Cylinder, "PoleL", new Vector3(-1.3f, 0.85f, 0f), new Vector3(0.07f, 0.85f, 0.07f), Vector3.zero, wood);
            Prim(t, PrimitiveType.Cylinder, "PoleR", new Vector3(1.3f, 0.85f, 0f), new Vector3(0.07f, 0.85f, 0.07f), Vector3.zero, wood);
            Prim(t, PrimitiveType.Cube, "Line", new Vector3(0f, 1.62f, 0f), new Vector3(2.6f, 0.02f, 0.02f), Vector3.zero, Mat("Black", Black));

            // Five garments in the huisstijl cloth colours, each a pivot AT the line with the cloth hanging
            // below it — ClothesLine auto-collects children named 'Garment*' and swings the pivots.
            Color[] cloth = { ClothRed, ClothBlue, ClothYellow, ClothGreen, Cream };
            string[] names = { "Red", "Blue", "Yellow", "Green", "White" };
            for (int i = 0; i < 5; i++)
            {
                var pivot = new GameObject("Garment" + i);
                pivot.transform.SetParent(t, false);
                pivot.transform.localPosition = new Vector3(-1.0f + i * 0.5f, 1.62f, 0f);
                float w = 0.34f + (i % 2) * 0.08f;
                float h = 0.42f + (i % 3) * 0.09f;
                Prim(pivot.transform, PrimitiveType.Cube, "Cloth", new Vector3(0f, -h * 0.5f, 0f),
                     new Vector3(w, h, 0.03f), Vector3.zero, Mat("Cloth_" + names[i], cloth[i]));
            }
            root.AddComponent<ClothesLine>();
            return SavePrefab(root, "LaundryLine");
        }

        /// <summary>A pecking chicken flock (Levend Kenia §4.5): four Bird pivots RoadsideChickens pecks,
        /// startles outward and settles again. The reactive-world beat, ground edition.</summary>
        private static RoadsideProp BuildChickens()
        {
            GameObject root = NewRoot("ChickenFlock");
            Transform t = root.transform;
            Material white = Mat("HenWhite", new Color(0.92f, 0.90f, 0.85f));
            Material brown = Mat("HenBrown", new Color(0.62f, 0.40f, 0.22f));
            Material comb = Mat("HenComb", new Color(0.80f, 0.16f, 0.14f));
            Vector3[] spots =
            {
                new Vector3(0f, 0f, 0f), new Vector3(0.6f, 0f, 0.4f),
                new Vector3(-0.5f, 0f, 0.3f), new Vector3(0.2f, 0f, -0.5f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var bird = new GameObject("Bird" + i);
                bird.transform.SetParent(t, false);
                bird.transform.localPosition = spots[i];
                bird.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Material hen = i % 2 == 0 ? white : brown;
                Prim(bird.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0.14f, 0f), new Vector3(0.16f, 0.16f, 0.24f), Vector3.zero, hen);
                Prim(bird.transform, PrimitiveType.Cube, "Head", new Vector3(0f, 0.28f, 0.12f), new Vector3(0.08f, 0.10f, 0.08f), Vector3.zero, hen);
                Prim(bird.transform, PrimitiveType.Cube, "Comb", new Vector3(0f, 0.345f, 0.12f), new Vector3(0.03f, 0.04f, 0.06f), Vector3.zero, comb);
                Prim(bird.transform, PrimitiveType.Cube, "Tail", new Vector3(0f, 0.20f, -0.14f), new Vector3(0.06f, 0.12f, 0.08f), new Vector3(-30f, 0f, 0f), hen);
            }
            root.AddComponent<RoadsideChickens>();
            return SavePrefab(root, "ChickenFlock");
        }

        private static RoadsideProp BuildBollard()
        {
            GameObject root = NewRoot("Bollard");
            Transform t = root.transform;
            Prim(t, PrimitiveType.Cylinder, "Post", new Vector3(0f, 0.42f, 0f), new Vector3(0.16f, 0.42f, 0.16f), Vector3.zero, Mat("White", Cream));
            Prim(t, PrimitiveType.Cylinder, "Band", new Vector3(0f, 0.74f, 0f), new Vector3(0.17f, 0.10f, 0.17f), Vector3.zero, Mat("Black", Black));
            return SavePrefab(root, "Bollard");
        }

        // ---- Helpers ------------------------------------------------------------------------------------------

        private static GameObject NewRoot(string name)
        {
            var go = new GameObject(name);
            go.AddComponent<RoadsideProp>();
            return go;
        }

        private static GameObject Prim(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            var col = g.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col); // pure scenery: strip the primitive collider
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            var mr = g.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
            return g;
        }

        private static RoadsideProp SavePrefab(GameObject root, string fileName)
        {
            EnsureFolder(PropsFolder);
            string path = PropsFolder + "/" + fileName + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root); // we only wanted the asset, not the scene temp
            return prefab != null ? prefab.GetComponent<RoadsideProp>() : null;
        }

        private static Material Mat(string name, Color color, float smoothness = 0.08f)
        {
            if (MatCache.TryGetValue(name, out Material cached) && cached != null)
                return cached;
            EnsureFolder(MatFolder);
            string path = MatFolder + "/Kenya_" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                m = new Material(sh) { name = "Kenya_" + name };
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            m.enableInstancing = true; // many props share one material -> a handful of instanced draw calls
            EditorUtility.SetDirty(m);
            MatCache[name] = m;
            return m;
        }

        private static void SetObj(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetVec(SerializedObject so, string field, Vector3 value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.vector3Value = value;
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
