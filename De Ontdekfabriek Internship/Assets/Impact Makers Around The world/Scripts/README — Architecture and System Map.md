# README — Architecture and System Map
**Document type:** Technical reference
**Author:** Ryan Putman · De Ontdekfabriek · 2026
**Scope:** The rebuilt codebase in this folder — generated as 73 scripts from [[MDA — Mechanics Dynamics Aesthetics]] and [[Design/Requirements — Kenya Scooter Game v2]], grown to ~170 scripts (plus EditMode tests) through Sprint 8
**Companion documents:** [[Unity Setup Guide — Scene Wiring]] · [[D17 — MDA-Driven Clean Code Generation]] · [[Build Changelog — Final Features and Fixes]]

> [!abstract] What this is
> The system map for the rebuilt Kenya Scooter Game codebase. Every MDA mechanic (M1–M27) traces to a script; every architecture rule from the Requirements document is listed with where it is enforced. Systems added after the generation are logged under Recent changes. Read this first when taking over the codebase.

> [!note] Status & location (updated July 2026)
> These scripts live in the Unity project at `Assets/Impact Makers Around The world/Scripts/` (namespace `KenyaScooter.*`). They **compile and run**: a playable PC scene is wired, and the build was played by the client and two Smart Mobile teachers at the 25 June 2026 oplevering — their improvement backlog drove the environment, controls and choreography work under Recent changes. Tuning and content (audio clips, more art) are ongoing. The Obsidian vault remains the documented source for the design docs.
>
> **Getting started:** for the fast path to a first playable PC scene, follow `SETUP — Quick Start.md` (next to the scripts in the Unity project). For the complete wiring — all vehicles, sequences, audio, tablet build — see `Unity Setup Guide — Scene Wiring`. The companion design docs (MDA, Requirements, D17) live in the Obsidian vault.

> [!note] Recent changes (June–July 2026)
> - **Traffic rides the curve (arc-based)** — vehicles carry a `RoadArc`/`RoadLateral` road-space position and re-derive their world pose each frame via `RoadSequencer.TryGetRoadPose`, like hazards. The `CurveAhead()` spawn-pause gate is gone, so traffic no longer thins through bends. See "Known limitations" for the full note.
> - **Hazards are fully data-driven** — behaviour and scoring live on each `HazardSpawnConfig` (`response`, `deduct`, `deduction`, `speedScaled`, `popupKey`, `warnKey`); the `HazardKind` enum is gone. A new hazard family is an asset, no code (see [[Location Pack — Authoring Guide and Asset Contracts]] §5).
> - **`RoadSequence.shuffleTiles`** — optional flag to remix a zone's tile order each time it plays.
> - **`HazardSpawnConfig.spawnHeight`** (default 0.1 m) places hazards just above the road; the spawner also preserves each prefab's authored scale.
> - **Speed read ("Velocity", `FX/`)** sells speed by making the WORLD rush, not by painting lines: `CameraRigController` adds a **speed dive** (lens drops toward the tarmac + pushes forward + pitches down) on top of its FOV widening; `SpeedLines` draws in-world **ground-rush** streaks that smear backward on the road, keeps the warm speed-dust, and adds only a whisper of a speed vignette on an overlay *below* the HUD; `SpeedGrade` adds subtle chromatic aberration via a runtime URP Volume; `CameraShake` adds a continuous velocity rattle. All read one signal (`SpeedFeel.Drive` = over-cruise, gated by the "Snelheidsbeleving" toggle and damped by motion-sensitivity). Two earlier versions (3D white-particle streaks, then screen-space "anime" line streaks) were both cut for reading as a cheap filter over the game.
> - **Same-direction traffic speed is capped** to a randomised ceiling below base speed (`TrafficVehicle.Activate`) so cars stay varied, overtakeable, and can't stall spawning; yielding is gradual and per-vehicle varied (`TrafficVehicle.Tick`).
> - **`GameEvents.RaiseSessionReset`** isolates each subscriber, so one failing reset handler can't freeze the game.
> - **Player spawns on its own lane** at ride height from the start (`PlayerController.Start`).
> - **UI text is translatable without touching code** via an optional `UIStringsConfig` ScriptableObject (Create > Kenya Scooter > UI Strings Config; right-click the asset > "Fill with built-in defaults", then fill the English/Dutch/Swahili columns). `SwahiliUI.Get(key)` reads from it when present (found in a `Resources` folder, or assigned with `SwahiliUI.Load`) and falls back to the built-in English/Swahili so the UI never breaks. **Dutch** is available as a language for the Dutch/Belgian audience (the runtime toggle still flips English/Swahili; switch to Dutch with `SwahiliUI.SetLanguage`).
> - **Real turns and junctions (`Roads/`)** — the 90° crossroads turn is built on owned art (`TileTurn`/`TileTurnArt`/`TileTurnConform`, `RoadJunction` + `JunctionSideRoads`), scheduled by `TurnScheduler`, with `CurvedRoadMesh`/`RoadEdgeSand`/`RoadSandDrift` dressing the road. Authoring lives in `Roads/Editor` (RoadToolsWindow, CrossroadsTurnTools, seam/junction/turn tools), with EditMode tests and a committed crossroads prefab + demo sequence. Still needs an in-Unity verify.
> - **Facilitator settings suite (`Settings/`)** — the front-of-house configuration layer: a declarative `SettingsCatalog` rendered by `SettingsMenu`, one-tap **Profielen** presets shown first (`SettingsPreset`, additive/stackable) plus custom presets, and a numeric access code gate (`FacilitatorLock`: keypad before the menu; change, disable, recover). Field tuning by facilitators is the project's validation strategy — see [[Validation Strategy — Tuning in the Field, Not the Lab]].
> - **Kenya atmosphere and ambient life (`Environment/`, `Sky/`, `Backdrop/`, `FX/`)** — the "it feels empty" fix from the 25 June feedback: warmed laterite day-cycle palette + toggleable dust and haze (`WeatherConfig`, `DustAtmosphere`, `VehicleDustTrail`; "Stof en haze", on by default); heat shimmer and dust devils; a horizon backdrop (acacias, escarpment, volcano) and sky life (birds by day, `NightSky` after dark); and a pooled, day-cycle-aware roadside-life spawner (`RoadsidePropSpawner`) with reactive props (`WindmillRotor`, `RoadsideWaver`, walkers, chickens) — pure scenery, no colliders, GPU-instanced, capped by `maxActiveProps`. A one-click editor factory (`KenyaRoadsidePropFactory`, "Build Kenya Roadside Starter") builds 12 primitive-based Kenyan props and wires the config, so real art is optional. A wind layer (`WindField`, `GustFront` + grass/leaf/clothes-line/dust responses) exists in the vault mirror and still needs mirroring to the live project (see Known limitations).
> - **Charge-station relay choreography (`Session/ChargeStationSequence`)** — the checkpoint is now a cinematic relay: the bike pulls into the charge bay (a scripted-pose slide + yaw, no forward motion, so the never-move-longitudinally rule holds), charges for a few seconds with a battery-refill visual (`ChargingStarted` event), shows the hand-off screen, and the next player pulls out as the world ramps back up. Raises `CheckpointReached` after the charge. Toggle "Laadstation-animatie" (default on).
> - **Handheld controls + onboarding (`Controls/`, `UI/`)** — on-screen gas/brake pedal hints (`HandheldControlsHud`) over the existing half-screen touch zones, holder-vs-handheld detection from the tablet's charging state (`HandheldDetector`, with facilitator override), a first-run how-to card (`HowToPlayOverlay`), and a front-of-house Kenya/Dutch drive-side toggle on the Title/Setup screens wired to `world.driveLeft`.
> - **Kiosk deployment (`Core/`)** — `KioskLock` swallows the Android back button and `OrientationLock` pins the screen orientation for unattended flight-case use; both still need an on-device test. The deployment docs (build/keystore/APK, tablet golden image) live in the vault.
> - **UI direction locked (`UI/`)** — screens rebuilt to the approved visual language (two registers: colourful story screens for players, dark instrument screens for facilitators; the HUD as a physical diegetic dashboard — `DiegeticHud`, now allocation-free per frame), plus `AttractMode`, the `UiKit`/`UITheme` foundation, and micro-feedback (`ScorePunch`, `UiPulse`).
> - **Haptics moved to `Feedback/`** (`HapticDriver` + `HapticFeedback`).

---

## Folder → namespace map

| Folder | Namespace | Responsibility |
|---|---|---|
| `Core/` | `KenyaScooter.Core` | Game state machine, event hub, world speed, road axes, pooling, kiosk + orientation locks |
| `Config/` | `KenyaScooter.Config` | All ScriptableObject configs — every tunable number in the game |
| `Controls/` | `KenyaScooter.Controls` | Input providers (gyro, touch zones, keyboard/gamepad/mouse) + router + handheld detection |
| `Player/` | `KenyaScooter.Player` | Scooter movement, lean, wobble, collision/lane/near-miss/speed detection |
| `Cameras/` | `KenyaScooter.Cameras` | The single camera writer + shake and Real Rider value providers |
| `Traffic/` | `KenyaScooter.Traffic` | Vehicles, personalities, profiles, spawning, overtake detection, horns, lights/exhaust/animation |
| `Roads/` | `KenyaScooter.Roads` | Tiles, sequences, the sequencer grammar, turns and junctions (+ authoring tools in `Editor/`) |
| `Hazards/` | `KenyaScooter.Hazards` | Potholes, rocks, speed bumps + the cluster spawner |
| `SafetyNet/` | `KenyaScooter.SafetyNet` | Grace charges, the rewind ring buffer, rewind visuals |
| `Scoring/` | `KenyaScooter.Scoring` | Score (single writer), streak, stats, group/high score, leaderboard |
| `Session/` | `KenyaScooter.Session` | Timer, day cycle, speed zones, checkpoint + charge-station relay, analytics |
| `Settings/` | `KenyaScooter.Settings` | Facilitator settings: catalogue, menu, presets, access code (+ builder menu in `Editor/`) |
| `Audio/` | `KenyaScooter.Audio` | Engine layers, ambient crossfade, SFX pool, ducking |
| `UI/` | `KenyaScooter.UI` | HUD (diegetic dashboard), warnings, popups, menu/system screens, leaderboard, language, onboarding, debug panel |
| `FX/` | `KenyaScooter.FX` | Speed read (Velocity: `SpeedFeel`, `SpeedLines`, `SpeedGrade` + camera dive), warm grade, dust/haze, heat shimmer, dust devils, wind |
| `Environment/` | `KenyaScooter.Environment` | Pooled roadside life: prop spawner + reactive props (+ prop factory in `Editor/`) |
| `Backdrop/` | `KenyaScooter.Backdrop` | Horizon silhouettes: acacias, escarpment, volcano |
| `Sky/` | `KenyaScooter.Sky` | Sky birds and the night sky |
| `Feedback/` | `KenyaScooter.Feedback` | Haptics |

---

## MDA mechanic → script traceability (M1–M27)

| # | Mechanic | Implemented in |
|---|----------|----------------|
| M1 | World scroll | `WorldSpeed` (speed owner) + central scroll loops in `RoadSequencer`, `HazardSpawner`, `TrafficVehicle.Tick` |
| M2 | Speed system | `WorldSpeed.ApplyThrottle` + `ScooterConfig` (10/30 m/s, 15/20/5 m/s²) |
| M3 | Road turn | A tile's `RoadTile.curveAngle` bends the road in `RoadSequencer` (constant +Z/+X frame; the road curves around the stationary player); `CameraRigController` + `ScooterLean` bank into it from `RoadDirection.CurveRate`. The 90° crossroads turn adds `RoadJunction`/`TileTurnArt`, scheduled by `TurnScheduler` |
| M4 | Gas/brake touch zones | `TouchZoneProvider` (right = gas, left = brake), combined in `ScooterInputRouter`; on-screen pedal hints via `HandheldControlsHud` |
| M5 | Lateral steering | `GyroTiltProvider` (tilt → [-1,1]) + `PlayerController` (MoveTowards, 30 m/s², no instant reversal) |
| M6 | Scooter lean | `ScooterLean` — reads **raw input**, so lean precedes position change |
| M7 | Real Rider Mode | `RealRiderMode` (counter-roll, horizon flat) applied by `CameraRigController` |
| M8 | Gyro calibration | `GyroTiltProvider.Calibrate`, auto-fired on session start by `ScooterInputRouter` |
| M9 | Two-lane architecture | `RoadSideConfig` (driveOnLeft, lane centres) + `TrafficSpawner` lane placement; front-of-house Kenya/Dutch toggle writes `world.driveLeft` |
| M10 | Traffic prewarm | `TrafficSpawner.HandleSessionReset` (prewarm band 12–90 m) |
| M11 | Spawn guarantee | `TrafficSpawner.SpawnSameDirection` (min gap + car length) / `SpawnOncoming` (120 m + headway) |
| M12 | Driver personality | `DriverPersonality`, `TrafficBehaviourProfile` (weighted draw), behaviour in `TrafficVehicle.Tick` |
| M13 | Overtake detection | `OvertakeDetector` — road-space arc/lateral comparison, axis-agnostic |
| M14 | Wrong-lane detection | `WrongLaneDetector` — SteerAxis projection vs `RoadSideConfig`, grace window then per-second ticks |
| M15 | Collision system | `PlayerCollisionHandler` — relative-speed classification (35 / 60 km/h in `SafetyNetConfig`) |
| M16 | Grace system | `GraceSystem.TryAbsorb`, 8 s clean-driving recharge |
| M17 | Rewind system | `RewindSystem` (3.5 s ring buffer @ 0.15 s, cap 2) + `RewindVisuals` (B&W volume) + `IRewindable` |
| M18 | Near-miss response | `NearMissDetector` + `TrafficVehicle.FlashHighlight` + `CameraShake` — **no score change** |
| M19 | Score events | `ScoreManager` (single writer) + `ScoreConfig` (values + toggles) |
| M20 | Streak multiplier | `StreakSystem` (tiers in `ScoreConfig`), award-then-increment order in `ScoreManager` |
| M21 | Potholes | `Hazard` + `HazardSpawner` + density curve in `HazardSpawnConfig` |
| M22 | Rocks | `Hazard` (rock config) |
| M23 | Road sequencing | `RoadSequencer` grammar (weights, cooldowns, forbidden/preferred prev tags) over `RoadSequence` assets |
| M24 | Journey Arc unlock gates | `RoadSequence.unlockAtTime` checked against `TimerManager.Elapsed` |
| M25 | Day/night cycle | `DayCycleManager` + `DayCycleConfig` (ASUBUHI → MCHANA → ALASIRI → JIONI) + URP volume crossfade |
| M26 | Session timer | `TimerManager` (120 s, pauses during rewind) |
| M27 | Relay / group scoring | `GroupScoreManager` (add-only commits) + `LeaderboardManager` + `CheckpointScreen` + the `ChargeStationSequence` hand-off choreography |

Requirements-only systems with no MDA number: speeding tiers (`SpeedMonitor` + `SpeedZoneManager`, Req §7.3), checkpoint choreography (`CheckpointController` + `ChargeStationSequence`, Req §9.2), audio (`AudioManager`, `TrafficHorn`, Req §11), UI suite (Req §12–13), analytics (`AnalyticsManager`, Req §14), accessibility/facilitator controls (the `Settings/` suite, Req §16), kiosk deployment (`KioskLock`, `OrientationLock`), and the ambient environment layer (`Environment/`, `Sky/`, `Backdrop/`).

---

## Architecture rules and where they are enforced

| Rule (Req §2) | Enforcement |
|---|---|
| Player never moves along the travel axis | Structural: `PlayerController` recomposes position from a lateral offset only — there is no code path that translates the player longitudinally. The charge-station pull-in is a scripted-pose slide + yaw, still with no longitudinal motion |
| One camera writer, in LateUpdate | `CameraRigController` is the only transform writer; `CameraShake` and `RealRiderMode` expose values only |
| New Input System only | All input via `Keyboard.current` / `Gamepad.current` / `Touchscreen.current` / `GravitySensor` — zero legacy `Input.` calls |
| Object pools only | `ObjectPool<T>` everywhere; the only runtime `Instantiate` calls are at `Awake`/`Start` (pool prewarm, UI pools, checkpoint instance) |
| ScriptableObjects for all config | Config SO classes for every gameplay constant; no gameplay number lives in a MonoBehaviour |
| `RoadDirection` is the single axis source | All spatial code projects via `RoadDirection.Longitudinal`/`Lateral`; no hardcoded `Vector3.back`/`right` in world code. The frame is a **constant** +Z/+X — turns bend the road around the player rather than rotating the frame, so nothing downstream desyncs |

---

## Event catalogue (GameEvents)

Single static hub; each event has exactly one conceptual raiser.

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
| `ChargingStarted` | ChargeStationSequence | charge visuals (battery refill, glow) |
| `CheckpointReached` | CheckpointController / ChargeStationSequence (after the charge) | GameManager (commit), CheckpointScreen, audio |

Newer events (e.g. `GroupReset`) are added in the same style — `GameEvents.cs` is the authoritative list. `TimerManager.OnTimerExpired` is deliberately an instance event consumed only by GameManager (D16 precedent: the timer reports, it does not know about game flow).

---

## Performance practices in this codebase

- **Manager-tick pattern** — vehicles, tiles, hazards and roadside props have no `Update`; their owners run one loop each. ~60 fewer MonoBehaviour Update callbacks per frame.
- **Zero-allocation hot paths** — rewind frames preallocated; detectors iterate `TrafficVehicle.Active` with `for` loops; reservoir sampling instead of temp lists; `MaterialPropertyBlock` for highlights (no material instancing); `DiegeticHud` rebuilt allocation-free after a per-frame GC audit finding (2026-06-27).
- **Event-driven UI** — text rebuilt only on change; timer string at most once per second; settings readout at 10 Hz.
- **Roadside scenery is pooled and GPU-instanced** — shared materials across the procedural props, capped by `maxActiveProps`.
- **Pools reconcile after rewind** — `ObjectPool.ReconcileAvailability` rebuilds bookkeeping from actual GameObject state, because the rewind toggles actives directly.
- **`[RuntimeInitializeOnLoadMethod]` static resets** — `GameEvents`, `RoadDirection`, `SwahiliUI` survive editor enter-play with domain reload disabled.
- 60 fps target set in `GameManager.Awake` (Req §17).

---

## Known limitations and review list

- **Traffic through a curve**: *fixed (arc-based traffic)*. Traffic now uses the same road-space scheme as hazards. Each `TrafficVehicle` carries a `RoadArc` (distance along the centreline) and `RoadLateral`; `Tick` advances the arc by the vehicle's own driving (the world scroll is carried by the player's arc growing, so the player-relative gap still closes at *own speed − scroll*) and re-derives the world pose every frame through `RoadSequencer.TryGetRoadPose` — the same player-anchored mapping that places the tiles and hazards — so cars ride the bend and bank with it. The old `CurveAhead()` spawn-pause gate is gone, so the stream no longer thins approaching a turn. All the vehicle's spatial reasoning (overtake/near-miss detection, hazard dodging, car-following, lateral spacing) compares road-space arc/lateral instead of projecting onto the straight `RoadDirection` axes, so it stays correct through a bend. The rewind stores each vehicle's road point and `RoadSequencer` keeps its anchor synced during reverse playback (`CacheAnchorAtPlayer`), so cars track the rewinding road. Collisions remain physics-driven and so are automatically curve-accurate. *Hazards* follow the same path: `HazardSpawner` parametrises each hazard by its point on the road (arc-length + lateral) and re-derives its world pose every frame through `RoadSequencer.TryGetRoadPose`.
- **Two sharp turns close together**: guarded in-engine. `RoadSequencer.ResolveCurveAngle` rides a sharp curve (`|curveAngle|` ≥ `sharpCurveAngleThreshold`) flat whenever the previous sharp curve is less than `minSharpCurveSpacing` of road behind it, so at most one sharp bend is ever live within the draw distance and the road cannot fold over itself. Keep `minSharpCurveSpacing` ≥ `spawnHorizon`. The softened bend is held on `RoadTile.EffectiveCurveAngle` (the authored `curveAngle` is left as design intent); all road geometry, the camera bank/lean and the spawn gate read the effective value, so a softened tile is genuinely straight road in every system.
- **Rewind vs tile queue**: fixed — the rewind now replays the identical road. `RoadSequencer` snapshots its on-screen tiles on `RewindStarted`; after the rewind lands, any snapshot tile the rewind switched off was one spawned during the rewound window (so its prefab had been dequeued), and `RequeueRewoundTiles` pushes those prefabs back to the front of `prefabQueue` in near-to-far order. The horizon therefore re-spawns the same tiles in the same order rather than jumping to the next prefabs. Any sequence the window happened to advance into is already physically carried in the surviving queue, so no sequence selection is re-run (and the softened-curve `EffectiveCurveAngle` is re-resolved on respawn from the surviving tiles, as the overlap guard already does). Pooling stays intact via `ObjectPool.ReconcileAvailability`. Instance→prefab mapping is the new `ObjectPool<T>.Prefab` getter.
- **Swahili strings** marked `TODO: native review` in `SwahiliUI` reuse English rather than risk invented Swahili (SC3 — respectful representation beats fake localisation). Verified strings (ALAMA, MUDA, POLE POLE, REKODI MPYA, and the Requirements-specified INGEHAALD/KUUKUA/JULLIE STAAN OP PLEK) are in. UI text can now also be edited and translated outside code via the optional `UIStringsConfig` asset (see Recent changes above); `SwahiliUI` reads it when present and falls back to these built-in strings otherwise.
- **Audio clips** are not imported yet (known backlog) — `AudioManager` is fully wired and null-safe and the layered Kenya soundscape system exists, but the game runs silent until clips land in `AudioConfig` (see [[Kenya Soundscape — Audio Clip Spec]] for what to source).
- **Gameplay animals stay cut.** The designed goat/cattle/elephant/warthog crossings (Req §6.4) remain out of scope per the Requirements' own list; the unused `WildlifeCrossing`/`WildlifeAnimal` scaffolding was removed on 2026-06-27 and no `AnimalManager` exists. Ambient scenery animals *do* now appear through the roadside-life spawner (e.g. `RoadsideChickens`, animal props) — pure scenery, no colliders, no gameplay.
- **EditMode tests exist but do not run yet.** The `Roads` turn/junction and `Settings` test suites are written, but in the live Unity project they sit in `Tests~/Editor/` — the trailing `~` makes Unity ignore the folder entirely. Rename it to `Tests` to compile and run them; until then no "tests pass" claim is meaningful.
- **Vault mirror ↔ live project drift.** The vault mirror (`Code/KenyaScooter (current)/`) and the live project (`Assets/Impact Makers Around The world/Scripts/`) drift apart over time. As of 2026-07-03 the wind/ambience batch (`WindField`, `GustFront`, `ScooterContactDust`, `SlipstreamDust`, `SmokeColumn`, `TruckDustWake`, `GrassSway`, `LeafSkitter`, `ClothesLine`, `RoadsideChickens`) exists **only in the vault mirror**, and seven shared files differ between the two. Always `git diff --no-index` live vs vault before copying in either direction — a file can carry a fix that exists on one side only.
- **Compiled and running on PC; tablet not yet verified on-device.** The scene is wired and the core loop runs in the editor (requires **Input System**, **TextMeshPro** and **URP**). The gyro orientation math (`GyroTiltProvider.RollForOrientation`) still needs an on-device sign check — calibration absorbs constant error, `invertGyro` covers a sign error. `KioskLock` and the charging-state `HandheldDetector` also need a real device.

---

## Connections

- [[MDA — Mechanics Dynamics Aesthetics]] — primary design spec
- [[Design/Requirements — Kenya Scooter Game v2]] — functional requirements
- [[Unity Setup Guide — Scene Wiring]] — how to get from these scripts to a running scene
- [[Build Changelog — Final Features and Fixes]] — the dated log of every build change since the rebuild
- [[Validation Strategy — Tuning in the Field, Not the Lab]] — why the facilitator settings suite is the validation instrument
- [[D17 — MDA-Driven Clean Code Generation]] — the devlog for this rebuild, including every interpretation decision
- [[D16 — Full Script Rewrite]] — the previous rebuild this one supersedes
