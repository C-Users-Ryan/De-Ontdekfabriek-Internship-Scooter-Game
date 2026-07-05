using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.FX
{
    /// <summary>
    /// THE standard speed read (2026-07-05, design direction): warm DUST MOTES that rush toward and past
    /// the player as soon as the scooter goes faster than cruise — on every road except the city, where
    /// the white <see cref="SpeedLines"/> take over (<see cref="SpeedReadZone"/> crossfades the two at
    /// zone borders). Built exactly like the proven SpeedLines tunnel — a deep cone of spawn positions
    /// ahead of the camera, centre sightline kept clear — but with round, warm, chunkier motes instead of
    /// thin white streaks, so it reads as dust coming off the road, not neon lines.
    ///
    /// Self-bootstrapping: nothing to place or wire. Uses the WeatherConfig dust palette
    /// (vehicleDustColour) so all dust in the game shares one colour, and respects the facilitator
    /// "Stof en haze" toggle like every other dust system.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class DustRush : MonoBehaviour
    {
        [Tooltip("Peak motes per second at top speed.")]
        [SerializeField] private float peakRate = 560f;
        [Tooltip("How fast motes rush toward and past the camera at full effect.")]
        [SerializeField] private float rushSpeed = 30f;
        [Tooltip("Fraction of the full rush speed the motes drift at while cruising (0..1). Lower = the dust " +
                 "clearly SPEEDS UP toward the player as the scooter goes faster, instead of only getting denser.")]
        [SerializeField, Range(0.2f, 1f)] private float cruiseRushScale = 0.5f;
        [Tooltip("Depth of the mote tunnel ahead of the camera (metres). Deeper = motes at all distances.")]
        [SerializeField] private float tunnelDepth = 26f;
        [Tooltip("Spread of the tunnel in degrees — wider = motes fan further toward the screen edges.")]
        [SerializeField] private float tunnelAngle = 28f;
        [Tooltip("How much the warm dust is lightened toward white (0 = raw palette colour, 1 = white). Kicked-up " +
                 "dust is paler than the ground, so a little lift makes it read against the warm desert.")]
        [SerializeField, Range(0f, 1f)] private float paleLift = 0.3f;
        [Tooltip("Fallback tint when no WeatherConfig is present (with one, its vehicleDustColour is used).")]
        [SerializeField] private Color fallbackColour = new Color(0.80f, 0.64f, 0.45f, 0.45f);

        private ParticleSystem.EmissionModule emission;
        private ParticleSystem.VelocityOverLifetimeModule velocity;
        private AnimationCurve rushEase; // cached so the per-frame speed update never allocates
        private Transform anchor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindObjectOfType<DustRush>() != null)
                return;
            var go = new GameObject("Dust Rush (auto)", typeof(ParticleSystem));
            go.AddComponent<DustRush>();
        }

        private void Awake()
        {
            ParticleSystem ps = GetComponent<ParticleSystem>();
            emission = ps.emission;
            Configure(ps);
        }

        private void Update()
        {
            // Ride the camera as soon as one exists (some scenes build it late).
            if (anchor == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                    return;
                anchor = cam.transform;
                transform.SetParent(anchor, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            float overCruise = -1f;
            if ((GameManager.State == GameState.Playing || GameManager.State == GameState.AtCheckpoint)
                && WorldSpeed.Instance != null)
            {
                WorldSpeed ws = WorldSpeed.Instance;
                // 0 at or below cruising (base) speed, 1 at max — the dust builds the faster you go than cruise.
                overCruise = Mathf.InverseLerp(ws.BaseSpeed, ws.MaxSpeed, ws.Current);
            }

            WeatherConfig weather = DustAtmosphere.ResolveConfig();
            bool dustOn = weather == null || weather.dustEnabled;

            // THE speed read, everywhere (2026-07-05 play-test verdict: dust INSTEAD of speed lines — the
            // zone hand-over is gone, the lines are retired). PERMANENT baseline: while riding, some dust
            // is ALWAYS streaming toward and past the player — a clear drift at cruise that ramps to the
            // full rush the faster the scooter goes. The effect is never absent, only calmer or wilder.
            float rate = 0f;
            if (dustOn && overCruise >= 0f)
            {
                float build = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 1f, overCruise));
                rate = peakRate * Mathf.Lerp(0.55f, 1f, build);

                // Two cues ramp together the faster the scooter goes: DENSITY (rate above) AND the RUSH SPEED
                // of each mote. Without the second, driving faster only made the dust thicker, not visibly
                // quicker toward the player — the missing "flying at you when you go fast" read. Reuses the
                // cached ease curve, so this per-frame write is allocation-free.
                float rush = rushSpeed * Mathf.Lerp(cruiseRushScale, 1f, build);
                velocity.z = new ParticleSystem.MinMaxCurve(-rush * 2f, rushEase);
            }
            emission.rateOverTime = rate;
        }

        /// <summary>
        /// The dust tunnel: a deep hollow cone of round warm motes ahead of the camera, rushing back past
        /// it with an easing-in speed so the texture accelerates by — the same construction that made the
        /// SpeedLines read, retuned from "thin white streak" to "chunky warm dust". The cone's apex radius
        /// is the protected sightline: no mote crosses the screen centre where the road information lives.
        /// </summary>
        private void Configure(ParticleSystem ps)
        {
            const float near = 2.5f;
            WeatherConfig weather = DustAtmosphere.ResolveConfig();
            Color dust = weather != null ? weather.vehicleDustColour : fallbackColour;
            // Kicked-up dust catches the light and reads paler than the ground it came off; lifting it toward
            // white is what makes it stand out against the warm desert instead of blending straight into it.
            dust = Color.Lerp(dust, Color.white, paleLift);

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            if (r != null)
            {
                // ROUND soft dust clouds (billboards), not stretched comet streaks — the white SpeedLines carry
                // the speed read, so the dust just needs to read as little round puffs drifting toward the player.
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.alignment = ParticleSystemRenderSpace.View;
                r.material = BuildDustMaterial(dust);
            }

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f; // motion comes from velocity-over-lifetime, so direction is exact
            main.startLifetime = Mathf.Max(0.2f, (near + tunnelDepth + 8f) / Mathf.Max(1f, rushSpeed));
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.5f); // big enough to read clearly as dust
            Color tint = dust;
            tint.a = Mathf.Clamp(dust.a <= 0f ? 0.7f : dust.a * 1.6f, 0.6f, 0.95f);
            main.startColor = tint; // one clean warm tone — no dark/murky grain variation
            main.gravityModifier = 0f;
            main.maxParticles = 1400;
            main.playOnAwake = true;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.ConeVolume;
            shape.angle = tunnelAngle;
            shape.radius = Mathf.Max(0.4f, weather != null ? weather.centreClearRadius : 0.8f);
            shape.length = tunnelDepth;
            shape.position = new Vector3(0f, 0f, near);
            shape.rotation = Vector3.zero;

            // Each mote's speed eases in over its life, so the dust ACCELERATES past the player — the rush.
            // The z magnitude is re-driven every frame from the scooter's over-cruise speed (see Update), so
            // the whole tunnel rushes quicker the faster you drive; here we just wire up the module + curve.
            velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            rushEase = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 1f));
            var flat = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
            velocity.x = new ParticleSystem.MinMaxCurve(1f, flat);
            velocity.y = new ParticleSystem.MinMaxCurve(1f, flat);
            velocity.z = new ParticleSystem.MinMaxCurve(-rushSpeed * 2f, rushEase); // rush back toward (and past) the camera

            // Billow: each little cloud swells softly as it drifts toward the camera before the alpha fade takes
            // it — that gentle growth is what reads as a small dust cloud rather than a hard dot.
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.8f), new Keyframe(1f, 1.3f)));

            // Feather in and out so clouds swell from the distance and dissolve as they sweep past.
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            if (!ps.isPlaying)
                ps.Play();
        }

        /// <summary>An unlit, alpha-blended material with a soft round sprite, guaranteed to exist in the
        /// active pipeline — the same never-magenta recipe the SpeedLines use.</summary>
        private static Material BuildDustMaterial(Color tint)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");

            Material mat = new Material(shader) { name = "DustRush (runtime)" };
            Texture2D tex = SoftMoteTexture();
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", tex);
            Color solid = new Color(tint.r, tint.g, tint.b, 1f);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", solid);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", solid);
            return mat;
        }

        // A clean soft ROUND dust puff: a smooth radial falloff, no mottling — reads as a small round cloud.
        private static Texture2D moteTex;
        private static Texture2D SoftMoteTexture()
        {
            if (moteTex != null)
                return moteTex;

            const int size = 64;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DustMote (runtime)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.SmoothStep(1f, 0f, d); // soft round falloff, feathered edge
                a *= a;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            moteTex = t;
            return t;
        }
    }
}
