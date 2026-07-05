using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// The Kenyan dusty-air look, as data (Oplevering 25 Jun 2026: a warm dry HAZE plus dust kicked up
    /// behind vehicles, so the air itself reads as dry Kenyan road). One ScriptableObject drives the whole
    /// thing so it is tunable without code and can be turned off in one place.
    ///
    /// Two layers, both gated by <see cref="dustEnabled"/>:
    ///  1. ATMOSPHERIC HAZE — DustAtmosphere enables exponential fog and drives its DENSITY, plus a slow
    ///     drifting warm-dust particle veil anchored to the camera. It does NOT set the fog COLOUR; that
    ///     stays owned by DayCycleManager (warmed per phase), so the two never fight.
    ///  2. BEHIND-VEHICLE DUST — VehicleDustTrail kicks a dust plume off a moving vehicle, scaled by world
    ///     speed.
    ///
    /// Opt-in and reversible by design: nothing happens unless this asset exists (DustAtmosphere
    /// self-bootstraps only when it finds one), and with the toggle OFF the scene is restored to its shipped
    /// fog state exactly. So the liked straight-road build is unaffected until a facilitator turns dust on.
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Weather Config", fileName = "WeatherConfig")]
    public sealed class WeatherConfig : ScriptableObject
    {
        [Header("Master toggle (facilitator)")]
        [Tooltip("On: the warm Kenyan dust haze and behind-vehicle dust are active. Off: the scene's original " +
                 "fog state is restored and no dust is emitted (identical to the no-dust build).")]
        public bool dustEnabled = true;

        [Header("Atmospheric haze (fog)")]
        [Tooltip("Exponential fog density used while dust is on. Higher = thicker, hazier air and a nearer " +
                 "horizon. Keep small; the warm fog COLOUR comes from the day cycle. 0 = no fog thickening.")]
        public float hazeFogDensity = 0.012f;
        [Tooltip("Seconds the fog density takes to ease in/out when the dust toggle flips, so it never pops.")]
        public float hazeFadeSeconds = 2.5f;

        [Header("Drifting dust veil (camera particles)")]
        [Tooltip("Warm dust colour for the drifting veil. Alpha sets how visible each mote is (keep it low).")]
        public Color hazeParticleColour = new Color(0.82f, 0.66f, 0.47f, 0.10f);
        [Tooltip("Motes spawned per second for the drifting veil. A handful reads as dusty air; too many fog the view.")]
        public float hazeEmissionRate = 14f;
        [Tooltip("Mote size range (metres). Big and soft, so they read as haze, not snow.")]
        public Vector2 hazeParticleSize = new Vector2(1.4f, 3.0f);
        [Tooltip("How fast the veil drifts past the camera (metres/second). Slow.")]
        public float hazeDriftSpeed = 2.2f;

        [Header("Behind-vehicle dust")]
        [Tooltip("Dust plume colour kicked up behind a moving vehicle (alpha = opacity at birth).")]
        public Color vehicleDustColour = new Color(0.80f, 0.64f, 0.45f, 0.45f);
        [Tooltip("Peak dust particles/second behind a vehicle, reached at full world speed.")]
        public float vehicleDustEmission = 40f;
        [Tooltip("Seconds a kicked-up dust puff lives before fading out.")]
        public float vehicleDustLifetime = 0.9f;
        [Tooltip("Dust puff size range (metres) over its life start..ish; it also grows over lifetime.")]
        public Vector2 vehicleDustSize = new Vector2(0.5f, 1.4f);
        [Tooltip("Below this fraction of max world speed (0..1) a vehicle kicks up no dust, so parked/slow cars stay clean.")]
        [Range(0f, 1f)] public float vehicleDustMinSpeedRatio = 0.15f;

        [Header("Dirt roads (2026-07-05)")]
        [Tooltip("How much MORE dust a dirt (murram) tile kicks up than tarmac: multiplies the behind-vehicle " +
                 "plume, the player's slipstream wake and the lean scrape while the road under the player is " +
                 "Dirt (RoadTile.surface), easing in/out across the surface change. 1 = dirt dusts like tarmac.")]
        public float dirtDustMultiplier = 2.5f;

        [Header("Wind (the shared WindField gust clock)")]
        [Tooltip("Seconds per gust-front window. One front lands somewhere inside each window (hashed), so the " +
                 "actual gaps vary around this. Shorter = blusterier day, longer = calmer. Read once at bootstrap.")]
        public float windGustPeriod = 13f;

        [Header("Slipstream (the player's speed dust)")]
        [Tooltip("Puffs per throttle-open KICK (both corners together) — the loudness of the beat.")]
        public int kickBurstCount = 18;
        [Tooltip("Rising Drive per second that counts as 'the throttle opened'. Higher = only decisive throttle kicks.")]
        public float kickEdgePerSecond = 0.5f;
        [Tooltip("Peak WAKE puffs/second at full Drive, born at the road edges near the ground. Scaled by Drive " +
                 "squared, so cruising stays clean and top speed blooms.")]
        public float wakePeakRate = 26f;
        [Tooltip("Peak GRAIN streaks/second at full Drive (the warm dust texture rushing past in the outer annulus).")]
        public float grainPeakRate = 70f;
        [Tooltip("Apex radius (m) of the grain annulus — the protected sightline centre where the road information " +
                 "lives. Dust never spawns inside it.")]
        public float centreClearRadius = 0.8f;

        [Header("Ground dust gusts (wind across the road)")]
        [Tooltip("Dust blowing across the road near the ground, for a windy dry-season feel. Since the Levend Kenia " +
                 "pass this is an EVENT (a rolling gust FRONT on the WindField clock, with a lull before and after), " +
                 "not a constant faucet. Part of the dust layer, so the same toggle turns it off.")]
        public bool groundGustEnabled = true;
        [Tooltip("Warm colour of the blowing ground dust (alpha = opacity).")]
        public Color groundGustColour = new Color(0.80f, 0.66f, 0.47f, 0.16f);
        [Tooltip("LEGACY (pre gust-front). The old constant streaks/second faucet; the gust-front rebuild replaced " +
                 "it with one roller volley + skitter per front (tuned on the GustFront component). Kept so old " +
                 "assets still deserialize.")]
        public float groundGustEmission = 10f;
        [Tooltip("Gust streak size range (metres).")]
        public Vector2 groundGustSize = new Vector2(1.0f, 2.4f);
        [Tooltip("How fast the dust blows across the road (metres/second).")]
        public float groundGustSpeed = 7f;

        [Header("Dust clouds: where they appear")]
        [Tooltip("When on, the blowing dust CLOUDS (ground gusts and dust devils) only appear on COUNTRY roads, not " +
                 "in town/city zones. The haze, behind-vehicle dust and speed dust are unaffected.")]
        public bool dustCloudsCountryOnly = true;
        [Tooltip("A RoadSequence carrying ANY of these context tags counts as a TOWN/CITY zone, where the dust clouds " +
                 "are suppressed when the option above is on. Matched against RoadSequence.contextTags.")]
        public string[] townContextTags = new[] { "township", "town", "market", "city" };
    }
}
