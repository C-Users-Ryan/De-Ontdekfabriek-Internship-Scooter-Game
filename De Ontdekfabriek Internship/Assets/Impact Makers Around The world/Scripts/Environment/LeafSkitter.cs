using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Settings;

namespace KenyaScooter.Environment
{
    /// <summary>
    /// A pooled handful of dry-leaf flecks that tumble-hop ACROSS the road only while a gust front is live
    /// (FX Design Spec "Levend Kenia" §4.4, 3 Jul 2026). The road is the stage: a leaf crossing it is what
    /// makes a front legible even when the player is watching traffic, not the verge.
    ///
    /// Six quads, recycled forever, manager-ticked like the grass tufts (no per-leaf component): gravity
    /// plus a decaying bounce that re-launches with <see cref="KenyaScooter.FX.WindField.Gust01"/>, a slow
    /// spin, and the world's scroll carrying them past. Scenery only — no colliders, never enters gameplay
    /// — and gated by the same roadside-life facilitator toggle as the props ("Leven langs de weg").
    /// Self-bootstraps after scene load; safe under URP (Sprites/Default, the SkyLife pattern).
    /// </summary>
    public sealed class LeafSkitter : MonoBehaviour
    {
        [Tooltip("Leaves in the pool. A handful reads as a gust's debris; more reads as autumn, which Kenya isn't.")]
        [SerializeField] private int leafCount = 6;
        [Tooltip("Dry-leaf tint (warm brown, opaque-ish — a fleck, not a haze).")]
        [SerializeField] private Color leafColour = new Color(0.55f, 0.38f, 0.16f, 0.95f);
        [Tooltip("Leaf quad size, metres.")]
        [SerializeField] private float leafSize = 0.16f;

        // Self-bootstrap after scene load (mirrors SkyLife / DustAtmosphere): zero scene wiring.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<LeafSkitter>() != null)
                return;
            var go = new GameObject("LeafSkitter (auto)");
            go.AddComponent<LeafSkitter>();
        }

        private sealed class Leaf
        {
            public Transform tr;
            public bool active;
            public Vector3 pos;   // world space
            public float vy;      // vertical speed (the hop)
            public float spin;    // accumulated roll, degrees
        }

        private Leaf[] leaves;
        private float nextSpawnAt;

        private void Awake()
        {
            Material mat = BuildLeafMaterial();
            Mesh quad = BuildQuad();
            leaves = new Leaf[Mathf.Max(1, leafCount)];
            for (int i = 0; i < leaves.Length; i++)
            {
                var go = new GameObject("Leaf");
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(leafSize, leafSize * 0.7f, leafSize);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                go.SetActive(false);
                leaves[i] = new Leaf { tr = go.transform };
            }
        }

        private void Update()
        {
            bool playing = GameManager.State == GameState.Playing;
            RoadsidePropConfig props = ConfigLocator.RoadsideProps;
            bool lifeOn = props == null || props.enabled; // the "Leven langs de weg" switch covers the leaves too
            if (!playing || !lifeOn)
            {
                RecycleAll();
                return;
            }

            float gust = FX.WindField.Gust01;
            float dir = FX.WindField.DirectionX;
            float dt = Time.deltaTime;
            float worldScroll = WorldSpeed.Instance != null ? WorldSpeed.Instance.Current : 0f;
            float shoulder = ShoulderEdge();

            // During a front, release leaves one at a time from the upwind shoulder ahead of the player.
            if (FX.WindField.FrontLive && Time.time >= nextSpawnAt)
            {
                nextSpawnAt = Time.time + 0.25f;
                SpawnOne(dir, shoulder);
            }

            for (int i = 0; i < leaves.Length; i++)
            {
                Leaf leaf = leaves[i];
                if (!leaf.active)
                    continue;

                // The spec's tumble-hop: gravity, the wind's push, decaying bounces re-fed by the gust.
                leaf.vy -= 5.5f * dt;
                leaf.pos.y += leaf.vy * dt;
                leaf.pos.x += (6f + 5f * gust) * dt * dir;
                leaf.pos.z -= worldScroll * dt; // the world scrolls: the leaf rides the road past the player
                if (leaf.pos.y < 0.04f)
                {
                    leaf.pos.y = 0.04f;
                    leaf.vy = Mathf.Abs(leaf.vy) * 0.55f + 0.7f * gust; // each hop lower, unless the gust re-feeds it
                }
                leaf.spin += 7f * Mathf.Rad2Deg * dt * dir;
                leaf.tr.position = leaf.pos;
                leaf.tr.rotation = Quaternion.Euler(0f, 0f, leaf.spin);

                // Gone once it clears the far shoulder, falls behind, or the front has fully died down.
                bool crossed = Mathf.Abs(leaf.pos.x) > shoulder + 8f;
                bool behind = leaf.pos.z < -12f;
                bool settled = gust < 0.03f && leaf.vy < 0.2f && leaf.pos.y <= 0.05f;
                if (crossed || behind || settled)
                {
                    leaf.active = false;
                    leaf.tr.gameObject.SetActive(false);
                }
            }
        }

        private void SpawnOne(float dir, float shoulder)
        {
            for (int i = 0; i < leaves.Length; i++)
            {
                if (leaves[i].active)
                    continue;
                Leaf leaf = leaves[i];
                leaf.active = true;
                leaf.pos = new Vector3(-dir * (shoulder + Random.Range(0.5f, 2.5f)), Random.Range(0.15f, 0.5f),
                                       Random.Range(8f, 35f));
                leaf.vy = Random.Range(0.5f, 1.4f);
                leaf.spin = Random.value * 360f;
                leaf.tr.position = leaf.pos;
                leaf.tr.gameObject.SetActive(true);
                return;
            }
        }

        private void RecycleAll()
        {
            if (leaves == null)
                return;
            for (int i = 0; i < leaves.Length; i++)
            {
                if (!leaves[i].active)
                    continue;
                leaves[i].active = false;
                leaves[i].tr.gameObject.SetActive(false);
            }
        }

        /// <summary>Outer edge of the shoulder (m from centre), read live like the prop spawner does.</summary>
        private static float ShoulderEdge()
        {
            RoadSideConfig rs = RoadSideConfig.Active;
            return rs != null ? rs.laneWidth + rs.shoulderWidth : 4.75f;
        }

        /// <summary>One shared unlit leaf material on Sprites/Default (never magenta under URP), tinted the
        /// dry warm brown, with the shared soft-dot texture so the fleck has soft edges.</summary>
        private Material BuildLeafMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            var mat = new Material(shader) { name = "LeafSkitter (runtime)" };
            Texture2D tex = FX.FXMaterials.SoftDustMaterial().mainTexture as Texture2D;
            if (tex != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            }
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", leafColour);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", leafColour);
            return mat;
        }

        /// <summary>A minimal double-visible quad (normal toward the camera's usual view direction).</summary>
        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "Leaf (runtime)" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            // Both windings, so the fleck never vanishes edge-on while it spins.
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 1, 2, 0, 3, 2, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
