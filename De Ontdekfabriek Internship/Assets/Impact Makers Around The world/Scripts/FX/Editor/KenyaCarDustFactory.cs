using System.IO;
using UnityEngine;
using UnityEditor;

namespace KenyaScooter.FXEditor
{
    /// <summary>
    /// One-click "make the car dust visible": bakes an AUTHORED, always-on dust ParticleSystem child into every
    /// traffic car prefab, on a URP-safe soft-sprite material (never magenta, never invisible — the same shader
    /// family the working haze uses). Unlike the runtime <c>VehicleDustTrail</c> — which is speed-gated and only
    /// appears in Play mode — this system is saved INTO the prefab, so you SEE the plume the instant you open the
    /// prefab, and it plays for every car in the build with zero further wiring.
    ///
    /// Idempotent: a car that already carries a "CarDustFX" child is left alone, so it is safe to re-run. The
    /// runtime speed-gated trail still stacks its own dust on top; if that ends up being too much, delete the
    /// authored child or dial its Emission down on the prefab.
    /// </summary>
    public static class KenyaCarDustFactory
    {
        private const string Folder = "Assets/Impact Makers Around The world";
        private const string VehiclesFolder = Folder + "/Prefab/Vehicles";
        private const string DotPath = Folder + "/CarDustDot.asset";
        private const string MatPath = Folder + "/CarDust.mat";
        private const string ChildName = "CarDustFX";

        [MenuItem("Tools/Kenya Scooter/Weather and FX/Create Dust Particles on Traffic Cars", false, 146)]
        public static void CreateOnAllCars()
        {
            Material mat = EnsureMaterial();

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { VehiclesFolder });
            int stamped = 0, skipped = 0, nonCar = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileName(path).StartsWith("Car_")) { nonCar++; continue; }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (FindChild(root.transform, ChildName) != null)
                {
                    skipped++;
                    PrefabUtility.UnloadPrefabContents(root);
                    continue;
                }
                BuildDustChild(root.transform, mat);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                stamped++;
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Kenya — Car Dust",
                $"Stamped dust particles onto {stamped} car prefab(s).\n{skipped} already had one; {nonCar} non-car prefab(s) skipped.\n\n" +
                "Open any Car_* prefab (or press Play) to see the plume. Tune it on the 'CarDustFX' child.", "OK");
            Debug.Log($"[Kenya Weather] Car dust particles: stamped {stamped}, skipped {skipped}, non-car {nonCar}. " +
                      "Authored/always-on, URP-safe material. Tune on the 'CarDustFX' child of each Car_* prefab.");
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>The URP-safe dust material (soft round sprite, alpha-blended, unlit). Built once and cached as
        /// an asset so every stamped car shares it and the reference is stable in the prefabs.</summary>
        private static Material EnsureMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (existing != null) return existing;

            Texture2D dot = AssetDatabase.LoadAssetAtPath<Texture2D>(DotPath);
            if (dot == null)
            {
                dot = BuildSoftDot();
                AssetDatabase.CreateAsset(dot, DotPath); // native Texture2D asset — keeps its pixels, no import step
            }

            // Sprites/Default is alpha-blended, unlit, respects the particle vertex colour, ships with every
            // pipeline and never resolves to the magenta error shader (same choice as the runtime FXMaterials).
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");

            var mat = new Material(sh) { name = "CarDust" };
            mat.mainTexture = dot;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", dot);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(mat, MatPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Texture2D BuildSoftDot()
        {
            const int size = 64;
            const float half = size * 0.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "CarDustDot",
                wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);   // 0 centre, 1 edge
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);                 // smoothstep falloff
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Builds the authored dust plume: behind the car and lifted off the road so the body never hides
        /// it, world-simulated so each puff is left behind as a trail, plays on awake.</summary>
        private static void BuildDustChild(Transform parent, Material mat)
        {
            var go = new GameObject(ChildName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.35f, -2f);
            go.transform.localRotation = Quaternion.identity;

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // puffs hang as the car drives on (a trail)
            main.startLifetime = 0.9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startColor = new Color(0.82f, 0.66f, 0.48f, 0.8f);     // warm murram dust
            main.gravityModifier = -0.02f;                              // a touch of lift
            main.maxParticles = 200;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 26f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(110f, 0f, 0f);                 // aim back-and-up off the wheels

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(1f, 1f)));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = mat;
        }
    }
}
