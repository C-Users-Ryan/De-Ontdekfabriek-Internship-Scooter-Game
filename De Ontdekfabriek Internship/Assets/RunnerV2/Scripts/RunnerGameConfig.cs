using UnityEngine;

/// <summary>
/// RunnerGameConfig — ScriptableObject containing every tunable value for the endless runner.
///
/// HOW TO CREATE ONE:
///   Right-click in the Project window > Create > Kenya Runner > Game Config
///
/// DESIGNED FOR STUDENT GROUPS:
///   Everything a new group needs to change their version of the game lives here.
///   No code changes needed — just edit this asset in the Inspector.
///
/// CATEGORIES:
///   [Session]      — how long a player plays, handoff duration
///   [Scoring]      — how points are earned and multiplied
///   [Traffic]      — vehicle types, spawn rates, difficulty curve
///   [Charging]     — when stops happen, how long they last
///   [Road]         — which road segment prefabs to use, road width
///   [Safety]       — lane penalty and bonus values
/// </summary>
[CreateAssetMenu(fileName = "RunnerGameConfig",
                 menuName  = "Kenya Runner/Game Config")]
public class RunnerGameConfig : ScriptableObject
{
    // ═══════════════════════════════════════════════════════════════
    // SESSION
    // ═══════════════════════════════════════════════════════════════
    [Header("── Session ──────────────────────────")]

    [Tooltip("How long each player's run lasts before handoff (seconds). Default: 120 (2 min)")]
    public float sessionDuration = 120f;

    [Tooltip("How long the handoff/break screen shows before auto-resetting.")]
    public float handoffDuration = 15f;

    // ═══════════════════════════════════════════════════════════════
    // SCORING
    // ═══════════════════════════════════════════════════════════════
    [Header("── Scoring ──────────────────────────")]

    [Tooltip("Points awarded per second of survival.")]
    public float pointsPerSecond = 10f;

    [Tooltip("Maximum score multiplier from the safe-driving streak.")]
    public float maxStreakMultiplier = 3f;

    [Tooltip("Seconds of correct-lane driving needed to reach max multiplier.")]
    public float secondsToMaxMultiplier = 30f;

    [Tooltip("Bonus points awarded for a clean charging stop (no collision in last 10s).")]
    public int cleanStopBonus = 500;

    // ═══════════════════════════════════════════════════════════════
    // TRAFFIC
    // ═══════════════════════════════════════════════════════════════
    [Header("── Traffic ──────────────────────────")]

    [Tooltip("Vehicle prefabs that can appear. Add or remove to change the traffic mix.\n" +
             "Each prefab must have a VehicleBehaviour component.")]
    public GameObject[] vehiclePrefabs;

    [Tooltip("Starting number of vehicles on the road at once.")]
    public int initialVehicleCount = 4;

    [Tooltip("Maximum vehicles on the road at once (reached after difficultyRampDuration).")]
    public int maxVehicleCount = 10;

    [Tooltip("Seconds over which vehicle count ramps from initial to max.")]
    public float difficultyRampDuration = 90f;

    [Tooltip("Starting scroll speed of the world (m/s).")]
    public float initialScrollSpeed = 6f;

    [Tooltip("Maximum scroll speed reached at end of difficulty ramp.")]
    public float maxScrollSpeed = 16f;

    [Tooltip("How far ahead of the player vehicles are spawned.")]
    public float vehicleSpawnDistance = 60f;

    [Tooltip("How far behind the player vehicles are despawned.")]
    public float vehicleDespawnDistance = 20f;

    // ═══════════════════════════════════════════════════════════════
    // CHARGING STOPS
    // ═══════════════════════════════════════════════════════════════
    [Header("── Charging stops ───────────────────")]

    [Tooltip("How often a charging stop occurs (seconds). Default: 120 (every 2 min).")]
    public float chargingInterval = 120f;

    [Tooltip("How long the charging stop lasts (seconds).")]
    public float chargingDuration = 8f;

    [Tooltip("Speed at which the scooter decelerates into the charging stop.")]
    public float chargingBrakeSpeed = 4f;

    [Tooltip("Charging station prefabs. One is picked randomly at each stop.\n" +
             "STUDENT TIP: Add solar, wind, hydro variants here.")]
    public GameObject[] chargingStationPrefabs;

    [Tooltip("Fact cards shown at each charging stop.\n" +
             "STUDENT TIP: Edit these strings to change the educational content.")]
    [TextArea(2, 4)]
    public string[] greenEnergyFacts =
    {
        "Kenya generates over 90% of its electricity from renewable sources.",
        "The Olkaria geothermal plant in the Rift Valley powers millions of Kenyan homes.",
        "Lake Turkana Wind Power is Africa's largest wind farm, located in northern Kenya.",
        "Kenya's electric vehicle sector is growing — Nairobi now has e-boda bodas on its streets.",
        "Solar energy is expanding rapidly in rural Kenya, bringing power to off-grid communities."
    };

    // ═══════════════════════════════════════════════════════════════
    // ROAD
    // ═══════════════════════════════════════════════════════════════
    [Header("── Road ─────────────────────────────")]

    [Tooltip("Road segment prefabs scrolled toward the player.\n" +
             "STUDENT TIP: Replace these to change the setting entirely.")]
    public GameObject[] roadSegmentPrefabs;

    [Tooltip("Length of each road segment (metres). Must match your prefab length.")]
    public float roadSegmentLength = 40f;

    [Tooltip("How many road segments to keep active ahead of the player.")]
    public int roadSegmentsAhead = 6;

    [Tooltip("Total width of the road in world units.")]
    public float roadWidth = 14f;

    [Tooltip("X position of the centre line dividing left and right lanes.")]
    public float centreLine = 0f;

    [Tooltip("Hazard prefabs (speed bumps, potholes) spawned on road segments.\n" +
             "STUDENT TIP: Add new obstacle prefabs here.")]
    public GameObject[] hazardPrefabs;

    [Tooltip("Chance (0-1) that a road segment contains a hazard.")]
    [Range(0f, 1f)]
    public float hazardSpawnChance = 0.3f;

    // ═══════════════════════════════════════════════════════════════
    // ROAD SAFETY
    // ═══════════════════════════════════════════════════════════════
    [Header("── Road safety ──────────────────────")]

    [Tooltip("Score multiplier applied while player is on the wrong side of the road.\n" +
             "0.5 = earn half points. 0 = earn nothing. Never go below 0.")]
    [Range(0f, 1f)]
    public float wrongSidePenaltyMultiplier = 0.25f;

    [Tooltip("How many seconds after returning to the correct side before the streak resets fully.")]
    public float streakRecoveryDelay = 2f;

    // ═══════════════════════════════════════════════════════════════
    // PLAYER
    // ═══════════════════════════════════════════════════════════════
    [Header("── Player ───────────────────────────")]

    [Tooltip("How fast the player moves left/right across the road.")]
    public float lateralSpeed = 8f;

    [Tooltip("Visual lean angle when moving sideways (degrees).")]
    public float lateralLeanAngle = 12f;

    [Tooltip("How quickly the lean animates.")]
    public float leanSmoothing = 8f;
}
