# README — Architecture and System Map
**Document type:** Technical reference
**Author:** Ryan Putman · De Ontdekfabriek · 2026
**Scope:** The clean codebase in this folder — 73 scripts, generated from [[MDA — Mechanics Dynamics Aesthetics]] and [[Design/Requirements — Kenya Scooter Game v2]]
**Companion documents:** [[Unity Setup Guide — Scene Wiring]] · [[D17 — MDA-Driven Clean Code Generation]]

> [!abstract] What this is
> The system map for the rebuilt Kenya Scooter Game codebase. Every MDA mechanic (M1–M27) traces to a script; every architecture rule from the Requirements document is listed with where it is enforced. Read this first when taking over the codebase.

> [!note] Status & location (updated June 2026)
> These scripts live in the Unity project at `Assets/Impact Makers Around The world/Scripts/` (namespace `KenyaScooter.*`). They now **compile and run**: a playable PC scene is wired and the core loop, traffic, hazards, day cycle and speed-lines work. Tuning and content (audio clips, more art) are ongoing. The Obsidian vault remains the documented source for the design docs.
>
> **Getting started:** for the fast path to a first playable PC scene, follow `SETUP — Quick Start.md` (next to the scripts in the Unity project). For the complete wiring — all vehicles, sequences, audio, tablet build — see `Unity Setup Guide — Scene Wiring`. The companion design docs (MDA, Requirements, D17) live in the Obsidian vault.

> [!note] Recent changes (June 2026)
> - **Hazards are fully data-driven** — behaviour and scoring live on each `HazardSpawnConfig` (`response`, `deduct`, `deduction`, `speedScaled`, `popupKey`, `warnKey`); the `HazardKind` enum is gone. A new hazard family is an asset, no code (see [[Location Pack — Authoring Guide and Asset Contracts]] §5).
> - **`RoadSequence.shuffleTiles`** — optional flag to remix a zone's tile order each time it plays.
> - **`HazardSpawnConfig.spawnHeight`** (default 0.1 m) places hazards just above the road; the spawner also preserves each prefab's authored scale.
> - **`SpeedLines`** self-configures a URP-safe white stretched-billboard streak effect at runtime (no magenta; works with the fixed camera).
> - **Same-direction traffic speed is capped** to a randomised ceiling below base speed (`TrafficVehicle.Activate`) so cars stay varied, overtakeable, and can't stall spawning; yielding is gradual and per-vehicle varied (`TrafficVehicle.Tick`).
> - **`GameEvents.RaiseSessionReset`** isolates each subscriber, so one failing reset handler can't freeze the game.
> - **Player spawns on its own lane** at ride height from the start (`PlayerController.Start`).

---

## Folder → namespace map

| Folder | Namespace | Responsibility |
|---|---|---|
| `Core/` | `KenyaScooter.Core` | Game state machine, event hub, world speed, road axes, pooling |
| `Config/` | `KenyaScooter.Config` | All ScriptableObject configs — every tunable number in the game |
| `Controls/` | `KenyaScooter.Controls` | Input providers (gyro, touch zones, keyboard/gamepad/mouse) + router |
| `Player/` | `KenyaScooter.Player` | Scooter movement, lean, wobble, collision/lane/near-miss/speed detection |
| `Cameras/` | `KenyaScooter.Cameras` | The single camera writer + shake and Real Rider value providers |
| `Traffic/` | `KenyaScooter.Traffic` | Vehicles, personalities, profiles, spawning, overtake detection, horns |
| `Roads/` | `KenyaScooter.Roads` | Tiles, sequences, the sequencer grammar, turns, wildlife crossing |
| `Hazards/` | `KenyaScooter.Hazards` | Potholes, rocks, speed bumps + the cluster spawner |
| `SafetyNet/` | `KenyaScooter.SafetyNet` | Grace charges, the rewind ring buffer, rewind visuals |
| `Scoring/` | `KenyaScooter.Scoring` | Score (single writer), streak, stats, group/high score, leaderboard |
| `Session/` | `KenyaScooter.Session` | Timer, day cycle, speed zones, checkpoint, analytics |
| `Audio/` | `KenyaScooter.Audio` | Engine layers, ambient crossfade, SFX pool, ducking |
| `UI/` | `KenyaScooter.UI` | HUD, warnings, popups, screens, leaderboard, language, debug panel |
| `FX/` | `KenyaScooter.FX` | Speed lines |

---

## MDA mechanic → script traceability (M1–M27)

| # | Mechanic | Implemented in |
|---|----------|----------------|
| M1 | World scroll | `WorldSpeed` (speed owner) + central scroll loops in `RoadSequencer`, `HazardSpawner`, `TrafficVehicle.Tick` |
| M2 | Speed system | `WorldSpeed.ApplyThrottle` + `ScooterConfig` (10/30 m/s, 15/20/5 m/s²) |
| M3 | Road turn | A tile's `RoadTile.curveAngle` bends the road in `RoadSequencer` (constant +Z/+X frame; the road curves around the stationary player); `CameraRigController` + `ScooterLean` bank into it from `RoadDirection.CurveRate` |
| M4 | Gas/brake touch zones | `TouchZoneProvider` (right = gas, left = brake), combined in `ScooterInputRouter` |
| M5 | Lateral steering | `GyroTiltProvider` (tilt → [-1,1]) + `PlayerController` (MoveTowards, 30 m/s², no instant reversal) |
| M6 | Scooter lean | `ScooterLean` — reads **raw input**, so lean precedes position change |
| M7 | Real Rider Mode | `RealRiderMode` (counter-roll, horizon flat) applied by `CameraRigController` |
| M8 | Gyro calibration | `GyroTiltProvider.Calibrate`, auto-fired on session start by `ScooterInputRouter` |
| M9 | Two-lane architecture | `RoadSideConfig` (driveOnLeft, lane centres) + `TrafficSpawner` lane placement |
| M10 | Traffic prewarm | `TrafficSpawner.HandleSessionReset` (prewarm band 12–90 m) |
| M11 | Spawn guarantee | `TrafficSpawner.SpawnSameDirection` (min gap + car length) / `SpawnOncoming` (120 m + headway) |
| M12 | Driver personality | `DriverPersonality`, `TrafficBehaviourProfile` (weighted draw), behaviour in `TrafficVehicle.Tick` |
| M13 | Overtake detection | `OvertakeDetector` — dot products on `RoadDirection.Current`, axis-agnostic |
| M14 | Wrong-lane detection | `WrongLaneDetector` — SteerAxis projection vs `RoadSideConfig`, grace window then per-second ticks |
| M15 | Collision system | `PlayerCollisionHandler` — relative-speed classification (35 / 60 km/h in `SafetyNetConfig`) |
| M16 | Grace system | `GraceSystem.TryAbsorb`, 8 s clean-driving recharge |
| M17 | Rewind system | `RewindSystem` (3.5 s ring buffer @ 0.15 s, cap 2) + `RewindVisuals` (B&W volume) + `IRewindable` |
| M18 | Near-miss response | `NearMissDetector` + `TrafficVehicle.FlashHighlight` + `CameraShake` — **no score change** |
| M19 | Score events | `ScoreManager` (single writer) + `ScoreConfig` (values + toggles) |
| M20 | Streak multiplier | `StreakSystem` (tiers in `ScoreConfig`), award-then-increment order in `ScoreManager` |
| M21 | Potholes | `Hazard` + `HazardSpawner` + density curve in `HazardSpawnConfig` |
| M22 | Rocks / wildlife tiles | `Hazard` (rock config) + `WildlifeCrossing`/`WildlifeAnimal` on tsavo tiles |
| M23 | Road sequencing | `RoadSequencer` grammar (weights, cooldowns, forbidden/preferred prev tags) over `RoadSequence` assets |
| M24 | Journey Arc unlock gates | `RoadSequence.unlockAtTime` checked against `TimerManager.Elapsed` |
| M25 | Day/night cycle | `DayCycleManager` + `DayCycleConfig` (ASUBUHI → MCHANA → ALASIRI → JIONI) + URP volume crossfade |
| M26 | Session timer | `TimerManager` (120 s, pauses during rewind) |
| M27 | Relay / group scoring | `GroupScoreManager` (add-only commits) + `LeaderboardManager` + `CheckpointScreen` |

Requirements-only systems with no MDA number: speeding tiers (`SpeedMonitor` + `SpeedZoneManager`, Req §7.3), checkpoint choreography (`CheckpointController`, Req §9.2), audio (`AudioManager`, `TrafficHorn`, Req §11), UI suite (Req §12–13), analytics (`AnalyticsManager`, Req §14), accessibility/facilitator controls (Req §16).

---

## Architecture rules and where they are enforced

| Rule (Req §2) | Enforcement |
|---|---|
| Player never moves along the travel axis | Structural: `PlayerController` recomposes position from a lateral offset only — there is no code path that translates the player longitudinally |
| One camera writer, in LateUpdate | `CameraRigController` is the only transform writer; `CameraShake` and `RealRiderMode` expose values only |
| New Input System only | All input via `Keyboard.current` / `Gamepad.current` / `Touchscreen.current` / `GravitySensor` — zero legacy `Input.` calls |
| Object pools only | `ObjectPool<T>` everywhere; the only runtime `Instantiate` calls are at `Awake`/`Start` (pool prewarm, UI pools, checkpoint instance) |
| ScriptableObjects for all config | 10 config SO classes; no gameplay constant lives in a MonoBehaviour |
| `RoadDirection` is the single axis source | All spatial code projects via `RoadDirection.Longitudinal`/`Lateral`; no hardcoded `Vector3.back`/`right` in world code. The frame is a **constant** +Z/+X — turns bend the road around the player rather than rotating the frame, so nothing downstream desyncs |

---

## Event catalogue (GameEvents)

Single static hub; each event has exactly one raiser.

| Event | Raised by | Main consumers |
|---|---|---|
| `SessionReset` / `SessionStarted` | GameManager | every system (reset to initial state / begin) |
| `StateChanged` | GameManager | screens, HUD, analytics |
| `OvertakeCompleted` | OvertakeDetector (+ spawner on despawn-confirm) | ScoreManager, GameManager stats, AudioManager |
| `NearMiss` | NearMissDetector | CameraShake, AudioManager, stats |
| `CollisionOccurred` | PlayerCollisionHandler | ScoreManager, GraceSystem (timer), shake, audio, warnings, stats |
| `HazardHit` | Hazard | ScoreManager, ScooterWobble, shake, audio, warnings, stats |
| `WrongLaneChanged` / `WrongLaneTick` | WrongLaneDetector | ScoreManager, WarningSystem, GraceSystem |
| `SpeedingTierChanged` / `SpeedingTick` | SpeedMonitor | ScoreManager, WarningSystem, Speedometer |
| `GraceAbsorbed` / `GraceRecharged` | GraceSystem | HUD grace icon, audio |
| `RewindStarted` / `RewindCompleted` | RewindSystem | GameManager (state), ScoreManager (penalty), spawner reconciles, audio |
| `ScoreChanged` / `StreakChanged` | ScoreManager / StreakSystem | HUD |
| `PopupRequested` | ScoreManager | ScorePopupUI |
| `SequenceChanged` | RoadSequencer | TrafficSpawner (profile), SpeedZoneManager, AudioManager (ambient), ScoreManager |
| `DayPhaseChanged` | DayCycleManager | HUD label, AudioManager (ibis), analytics |
| `CheckpointReached` | CheckpointController | GameManager (commit), CheckpointScreen, audio |

`TimerManager.OnTimerExpired` is deliberately an instance event consumed only by GameManager (D16 precedent: the timer reports, it does not know about game flow).

---

## Performance practices in this codebase

- **Manager-tick pattern** — vehicles, tiles and hazards have no `Update`; their owners run one loop each. ~60 fewer MonoBehaviour Update callbacks per frame.
- **Zero-allocation hot paths** — rewind frames preallocated; detectors iterate `TrafficVehicle.Active` with `for` loops; reservoir sampling instead of temp lists; `MaterialPropertyBlock` for highlights (no material instancing).
- **Event-driven UI** — text rebuilt only on change; timer string at most once per second; settings readout at 10 Hz.
- **Pools reconcile after rewind** — `ObjectPool.ReconcileAvailability` rebuilds bookkeeping from actual GameObject state, because the rewind toggles actives directly.
- **`[RuntimeInitializeOnLoadMethod]` static resets** — `GameEvents`, `RoadDirection`, `SwahiliUI` survive editor enter-play with domain reload disabled.
- 60 fps target set in `GameManager.Awake` (Req §17).

---

## Known limitations and review list

- **Traffic through a curve**: traffic spawning still pauses while a bend sits within the spawn distance (cars are placed on the straight +Z line, which leaves a curved road), so traffic thins approaching a turn and resumes after — not yet fixed. *Hazards now follow the curved road path*: `HazardSpawner` parametrises each hazard by its point on the road (arc-length + lateral) and re-derives its world pose every frame through `RoadSequencer.TryGetRoadPose` — the same player-anchored mapping that places the tiles — so hazards keep populating the road through bends with no thinning. The same arc-based scheme could be applied to traffic.
- **Two sharp turns close together**: guarded in-engine. `RoadSequencer.ResolveCurveAngle` rides a sharp curve (`|curveAngle|` ≥ `sharpCurveAngleThreshold`) flat whenever the previous sharp curve is less than `minSharpCurveSpacing` of road behind it, so at most one sharp bend is ever live within the draw distance and the road cannot fold over itself. Keep `minSharpCurveSpacing` ≥ `spawnHorizon`. The softened bend is held on `RoadTile.EffectiveCurveAngle` (the authored `curveAngle` is left as design intent); all road geometry, the camera bank/lean and the spawn gate read the effective value, so a softened tile is genuinely straight road in every system.
- **Rewind vs tile queue**: fixed — the rewind now replays the identical road. `RoadSequencer` snapshots its on-screen tiles on `RewindStarted`; after the rewind lands, any snapshot tile the rewind switched off was one spawned during the rewound window (so its prefab had been dequeued), and `RequeueRewoundTiles` pushes those prefabs back to the front of `prefabQueue` in near-to-far order. The horizon therefore re-spawns the same tiles in the same order rather than jumping to the next prefabs. Any sequence the window happened to advance into is already physically carried in the surviving queue, so no sequence selection is re-run (and the softened-curve `EffectiveCurveAngle` is re-resolved on respawn from the surviving tiles, as the overlap guard already does). Pooling stays intact via `ObjectPool.ReconcileAvailability`. Instance→prefab mapping is the new `ObjectPool<T>.Prefab` getter.
- **Swahili strings** marked `TODO: native review` in `SwahiliUI` reuse English rather than risk invented Swahili (SC3 — respectful representation beats fake localisation). Verified strings (ALAMA, MUDA, POLE POLE, REKODI MPYA, and the Requirements-specified INGEHAALD/KUUKUA/JULLIE STAAN OP PLEK) are in.
- **Audio clips** are not imported yet (known backlog) — `AudioManager` is fully wired and null-safe, the game runs silent until clips land in `AudioConfig`.
- **AnimalManager** (goat/cattle herds, elephant stop, warthog — Req §6.4) stays deferred per the Requirements' own out-of-scope list; `WildlifeCrossing` covers the MDA's flagship crossing moment.
- **Compiled and running on PC; tablet/gyro not yet verified on-device.** The scene is wired and the core loop runs in the editor (requires **Input System**, **TextMeshPro** and **URP**). The gyro orientation math (`GyroTiltProvider.RollForOrientation`) still needs an on-device sign check — calibration absorbs constant error, `invertGyro` covers a sign error. Audio clips remain a backlog item (see above).

---

## Connections

- [[MDA — Mechanics Dynamics Aesthetics]] — primary design spec
- [[Design/Requirements — Kenya Scooter Game v2]] — functional requirements
- [[Unity Setup Guide — Scene Wiring]] — how to get from these scripts to a running scene
- [[D17 — MDA-Driven Clean Code Generation]] — the devlog for this rebuild, including every interpretation decision
- [[D16 — Full Script Rewrite]] — the previous rebuild this one supersedes
