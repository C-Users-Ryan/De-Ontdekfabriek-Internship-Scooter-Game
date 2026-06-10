# Kenya Scooter Game

An educational endless-runner built in **Unity 6 (URP)** for **iPad**, developed during an internship at [De Ontdekfabriek](https://www.deontdekfabriek.nl/). The game puts players on a boda boda electric scooter on Kenyan roads, teaching responsible driving habits through immediate gameplay feedback.

> Designed for 1–2 minute group relay sessions in classroom and event settings.

---

## Gameplay

Players ride an electric scooter through an endless Kenyan road environment. The world moves toward the fixed player — you steer laterally, manage speed, and decide when to overtake.

**Core mechanics:**
- **Overtake** slower same-direction traffic to score points
- **Avoid** oncoming vehicles, potholes, and rocks
- **Stay in your lane** — crossing into oncoming traffic costs points
- **Don't speed** — the game monitors your velocity against the road limit
- **Relay mode** — a timer ends each round, the game pauses at a checkpoint, and the iPad passes to the next player

**Input:**
- Gyroscope tilt for lateral steering (iPad)
- Left touch zone = brake, right touch zone = gas
- WASD keyboard fallback for editor testing

---

## Technical Architecture

### World-Moves-Player
The player Rigidbody never moves forward — instead, all world objects translate toward the player at `WorldSpeed.Current`. This eliminates floating-point precision issues over long distances and simplifies object recycling.

### Axis-Aware Turn System (`RoadDirection`)
A static singleton holds the current travel direction (`RoadDirection.Current`) and perpendicular steer axis (`RoadDirection.SteerpAxis`). `TurnTrigger` updates both on player contact and swaps the Rigidbody position constraint (`FreezeZ ↔ FreezeX`), allowing the road to turn 90° without any coordinate remapping in downstream systems.

All world-moving components (tiles, traffic, potholes, rocks, player velocity) read from `RoadDirection` rather than hardcoding axis values.

### Object Pooling
No `Instantiate` or `Destroy` calls at runtime. All spawnable objects — road tiles, traffic vehicles, potholes, rocks — use pre-allocated pools. `RoadTileRecycler` moves the rearmost tile to the front; traffic and obstacle managers recycle objects when they pass behind the player.

### Data-Driven Road Content (`RoadSequence` ScriptableObjects)
Road content is defined as `RoadSequence` ScriptableObjects. `RoadSequencer` selects sequences using weighted random sampling with per-sequence cooldowns and an `unlockAtTime` journey arc gate — harder content only appears after the player has been riding for a set number of seconds.

### Real Rider Mode
Gyroscope steering (`GyroscopeSteering`) with optional horizon lock — the camera rig counter-rolls in `LateUpdate` to keep the horizon level as the player tilts the iPad. Configurable strength and smoothing. Includes desktop mouse simulation for editor testing.

### Day Cycle
`DayCycleManager` steps through Morning → Midday → Sunset URP Volume presets, blending between them over time to create a visual journey arc across a session.

### Responsible Driving Score System
`ScoreManager`, `WrongLaneDetector`, `SpeedMonitor`, and `OvertakeDetector` feed into a unified scoring loop. Deductions for wrong-lane driving and speeding are applied per-frame; overtake rewards fire on confirmed pass events. All toggles are exposed on `GameManager` for easy facilitator configuration.

### Relay / Checkpoint Flow
`TimerManager` fires `OnTimerExpired` at the end of each round. `CheckpointManager` brakes the world to a stop, shows a score screen, and restarts — passing control to the next player without reloading the scene.

### Analytics
`AnalyticsManager` writes run data (score, distance, crash events) to `PlayerPrefs`. No external service required — data persists on-device and is readable via the facilitator tools.

---

## Project Structure

```
Assets/
  OvertakeGame_v2/
    Scripts/
      Core/          # GameManager, WorldSpeed, RoadDirection, RoadTileRecycler, CheckpointManager
      Player/        # PlayerController, GyroscopeSteering, ScooterLean
      Traffic/       # TrafficManager, TrafficVehicle
      Potholes/      # PotholeManager, Pothole
      Obstacles/     # RockManager, RockObstacle
      Detection/     # OvertakeDetector, WrongLaneDetector, SpeedMonitor, CollisionHandler
      Managers/      # DayCycleManager, AudioManager, AnalyticsManager, UIManager
      Scoring/       # ScoreManager, RewardSystem
      Turn/          # TurnTrigger, RoadDirection
      Sequencing/    # RoadSequencer, RoadSequence (ScriptableObject)
```

All scripts live in the `OvertakeGame` namespace.

---

## Platform & Requirements

| | |
|---|---|
| Unity | 6.x (tested on Unity 6000.x) |
| Render Pipeline | Universal Render Pipeline (URP) |
| Target Platform | iOS (iPad) |
| Input | Unity Input System + gyroscope |
| Minimum iOS | 14+ (gyroscope required for Real Rider Mode) |

---

## Opening the Project

1. Clone the repository
2. Open **Unity Hub** and add the project folder (`De Ontdekfabriek Internship/`)
3. Open with Unity 6 — URP packages will resolve automatically
4. Open the main scene: `Assets/OvertakeGame_v2/Scenes/`
5. Press Play — keyboard (WASD) works out of the box; gyroscope requires a device build

For an iPad build, switch platform to iOS in Build Settings and deploy via Xcode.

---

## Key Design Decisions

- **No Instantiate at runtime** — all pooling, no garbage spikes on mobile
- **ScriptableObject-driven content** — road sequences can be authored and reordered without touching code
- **Single axis source** — all movement reads `RoadDirection`; adding a new turn angle only requires updating one singleton
- **Facilitator-first toggles** — game-over on collision, scoring deductions, and warning types are all Inspector booleans on `GameManager`

---

*Developed by Ryan Putman — internship project at De Ontdekfabriek, 2025–2026.*
