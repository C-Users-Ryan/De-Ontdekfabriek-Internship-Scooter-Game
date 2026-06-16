using System;

namespace KenyaScooter.Core
{
    /// <summary>
    /// Session flow states (Requirements §9.3). Only GameManager transitions between
    /// them; other systems request transitions and gate their updates on the state.
    /// </summary>
    public enum GameState
    {
        Ready,        // waiting for the next student — world frozen, start overlay visible
        Playing,      // normal gameplay
        Rewinding,    // rewind in progress, input and simulation paused (M17)
        AtCheckpoint, // timer expired, player driving to the charge station (Req §9.2)
        GameOver,     // hard crash with no rewind available and gameOverOnCollision enabled
        Finished      // session ended via timer without a checkpoint prefab assigned
    }

    /// <summary>Collision classes derived from relative impact speed (M15).</summary>
    public enum CollisionSeverity
    {
        Light,  // below lightHitMaxKmh — grace eligible, never fatal
        Medium, // between thresholds — grace eligible, escalates to Hard when grace is empty
        Hard    // at or above hardHitMinKmh — always routed to rewind / game over
    }

    /// <summary>
    /// Behaviour family for a hazard, used for stats/analytics bucketing only (M21, M22, Req §6).
    /// Hazards are data-driven now: a new hazard type is a new HazardSpawnConfig asset, not a new
    /// enum value plus switch cases. This category just buckets a hit for the session stats.
    /// </summary>
    public enum HazardResponse { SurfaceDefect, StaticObstacle, ForcedSlowdown }

    /// <summary>Which lane a traffic vehicle drives in (M9).</summary>
    public enum LaneDirection { SameDirection, Oncoming }

    /// <summary>Traffic archetypes (Req §5.3). Per-type tuning lives on the vehicle prefab.</summary>
    public enum VehicleType { Car, Matatu, Truck, BodaBoda, DonkeyCart, BrokenDown }

    /// <summary>Warning panel categories (Req §12.1).</summary>
    public enum WarningKind { Collision, WrongLane, Speeding, Pothole, Rock }

    /// <summary>Lateral bands a hazard may spawn in (Req §6.1–6.3).</summary>
    [Flags]
    public enum SpawnZones
    {
        None = 0,
        OwnLane = 1,
        OncomingLane = 2,
        Shoulders = 4
    }
}
