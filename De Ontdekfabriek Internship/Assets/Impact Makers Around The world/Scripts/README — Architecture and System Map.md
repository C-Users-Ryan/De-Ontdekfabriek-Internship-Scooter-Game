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
> - **Day and night: slower sundown + a real scooter headlight (`Session/`, `FX/`, 2026-07-05)** — a sixth day phase, **MAGHARIBI (blue hour)**, bridges the JIONI dusk and a new **USIKU** night, so evening→night is a ~30 s sweep instead of a 15 s collapse; phase times are re-paced across the whole session and scaled to any facilitator-set turn length (`DayCycleConfig.scaleToSessionLength`). Night stays dark but readable via the ONE real light in the scene besides the sun/moon: `FX/ScooterHeadlight` (a shadowless spot, off by day, easing in below dusk off `DayCycleManager.NightFactor01`). Rule held: a traffic car never gets a real `Light` — cars glow **emissive** head/tail lights (`TrafficVehicleLights` / `TrafficVehicleAutoLights`), so the scene stays at two real lights regardless of traffic count.
> - **Dirt (murram) roads (`Roads/`, `FX/`, 2026-07-05)** — a per-tile `RoadTile.surface` (Paved/Dirt) plus `RoadTile.hazardDensity` (0–4, more potholes/rocks on a rough tile). Feel and FX only — speed, steering and scoring are identical on every surface. `Roads/RoadSurfaceFeel` publishes an eased dirt blend the effects read; the surface is **communicated by dust** (`FX/ScooterDirtDust`, a rear-wheel plume that blooms on dirt and is clean on tarmac), not by shaking (`Player/DirtRumble` is cut to a faint texture). One facilitator step scales it all: OMGEVING ▸ "Onverharde wegen" (UIT / SUBTIEL / VOL).
> - **Performance modes (`Core/PerformanceMode`, 2026-07-05)** — a facilitator "Prestaties" tier (AUTOMATISCH / VOLLEDIG / GEBALANCEERD / LICHT) so old exhibition tablets stay smooth. Reversible tier engine: captures the authored baseline, restores before each apply, re-applies on session reset (catching pooled per-vehicle FX). **Trades atmosphere, never gameplay** — traffic, hazards, speeds and scoring are identical in every tier so leaderboard scores stay comparable across devices; only post-FX, shadows, render scale, effect density and roadside-prop count change. Auto-detects weak hardware on mobile.
> - **Horizon rebuilt as real scenery (`Sky/`)** — the old flat billboard silhouettes (`HorizonBackdrop`) were removed (they read as confusing cardboard cut-outs) and replaced by `HorizonRange` — two continuous low-poly mountain RIDGES as actual mesh rings that encircle the far distance, world-aligned so they never spin to face the camera, with feet that dissolve into the live fog colour. A companion `SkyAtmosphere` adds soft drifting clouds and a warm glow on the real sun. Both self-bootstrap, tint with the day cycle, and have facilitator toggles ("Bergen aan de horizon", "Wolken en zonnegloed").
> - **Traffic rides the curve (arc-based)** — vehicles carry a `RoadArc`/`RoadLateral` road-space position and re-derive their world pose each frame via `RoadSequencer.TryGetRoadPose`, like hazards. The `CurveAhead()` spawn-pause gate is gone, so traffic no longer thins through bends. See "Known limitations" for the full note.
> - **Hazards are fully data-driven** — behaviour and scoring live on each `HazardSpawnConfig` (`response`, `deduct`, `deduction`, `speedScaled`, `popupKey`, `warnKey`); the `HazardKind` enum is gone. A new hazard family is an asset, no code (see [[Location Pack — Authoring Guide and Asset Contracts]] §5).
> - **`RoadSequence.shuffleTiles`** — optional flag to remix a zone's tile order each time it plays.
> - **`HazardSpawnConfig.spawnHeight`** (default 0.1 m) places hazards just above the road; the spawner also preserves each prefab's authored scale.
> - **Speed read (`FX/`)** is deliberately restrained so speeding up never *distorts* the view: a **gentle, capped** `CameraRigController` FOV widening (~+8° max) is the only camera cue, alongside the Levend Kenia `SlipstreamDust`/`WindField` dust and the `SpeedLines` particle streaks. `SpeedFeel.Drive` (over-cruise, motion-sensitivity damped) drives the dust. **Removed as gimmicks/distortion:** a camera speed-dive (pushed the scooter into the ground), a `SpeedGrade` chromatic-aberration + fisheye volume, and a velocity rattle — they warped the view and shoved the first-person handlebars out of frame at speed. `RealRiderMode` horizon roll is capped at 15°.
> - **Same-direction traffic speed is capped** to a randomised ceiling below base speed (`TrafficVehicle.Activate`) so cars stay varied, overtakeable, and can't stall spawning; yielding is gradual and per-vehicle varied (`TrafficVehicle.Tick`).
> - **`GameEvents.RaiseSessionReset`** isolates each subscriber, so one failing reset handler can't freeze the game.
> - **Player spawns on its own lane** at ride height from the start (`PlayerController.Start`).
> - **UI text is translatable without touching code** via an optional `UIStringsConfig` ScriptableObject (Create > Kenya Scooter > UI Strings Config; right-click the asset > "Fill with built-in defaults", then fill the English/Dutch/Swahili columns). `SwahiliUI.Get(key)` reads from it when present (found in a `Resources` folder, or assigned with `SwahiliUI.Load`) and falls back to the built-in English/Swahili so the UI never breaks. **Dutch** is available as a language for the Dutch/Belgian audience (the runtime toggle still flips English/Swahili; switch to Dutch with `SwahiliUI.SetLanguage`).
> - **Turns rebuilt clean-slate (`Roads/`, 2026-07-04)** — the tile IS the system now. `RoadTile` alone defines a tile: **length**, **difficulty**, **allowed hazards** (per-tile list, empty = all), a **begin point** and an **exit point** (the game attaches every next tile begin-to-exit, so tiles always line up), and a **Turns list** — each entry places a **green ball (turn begins)** and a **red ball (turn ends)** at metre marks and bends the player by its degrees; add as many per tile as you want. `RoadTile.EvaluateRun` is the ONE evaluator the sequencer, `CurvedRoadMesh`, the lean/bank and spawn gating all sample, and authored turns are **never** softened or skipped. **Deleted the same day** (there is no legacy path): `TileTurn`, `TileTurnArt`, `TileTurnConform`, `TurnTrigger`, `TurnScheduler`, `TurnSafety`, `RoadPath`, `RoadJunction`, `JunctionSideRoads`, `curveAngle`, the exit/stop anchors, and all turn/junction editor tools + their tests. Old turn/junction prefabs are dead — re-author tiles with the Turns list. Still needs an in-Unity verify.
> - **Facilitator settings suite (`Settings/`)** — the front-of-house configuration layer: a declarative `SettingsCatalog` rendered by `SettingsMenu`, one-tap **Profielen** presets shown first (`SettingsPreset`, additive/stackable) plus custom presets, and a numeric access code gate (`FacilitatorLock`: keypad before the menu; change, disable, recover). Field tuning by facilitators is the project's validation strategy — see [[Validation Strategy — Tuning in the Field, Not the Lab]].
> - **Kenya atmosphere and ambient life (`Environment/`, `Sky/`, `FX/`)** — the "it feels empty" fix from the 25 June feedback: warmed laterite day-cycle palette + toggleable dust and haze (`WeatherConfig`, `DustAtmosphere`, `VehicleDustTrail`; "Stof en haze", on by default); heat shimmer and dust devils; sky life (birds by day, `NightSky` after dark); and a pooled, day-cycle-aware roadside-life spawner (`RoadsidePropSpawner`) with reactive props (`WindmillRotor`, `RoadsideWaver`, walkers, chickens) — pure scenery, no colliders, GPU-instanced, capped by `maxActiveProps`. A one-click editor factory (`KenyaRoadsidePropFactory`, "Build Kenya Roadside Starter") builds 12 primitive-based Kenyan props and wires the config, so real art is optional. A wind layer (`WindField`, `GustFront` + grass/leaf/clothes-line/dust responses — the "Levend Kenia" FX pass) drives the whole atmosphere off one shared gust clock, so the world gusts and leans together; copied to the live project on 2026-07-03.
> - **Charge-station relay choreography (`Session/ChargeStationSequence`)** — the checkpoint is now a cinematic relay: the bike pulls into the charge bay (a scripted-pose slide + yaw, no forward motion, so the never-move-longitudinally rule holds), charges for a few seconds with a battery-refill visual (`ChargingStarted` event), shows the hand-off screen, and the next player pulls out as the world ramps back up. Raises `CheckpointReached` after the charge. Toggle "Laadstation-animatie" (default on).
> - **Handheld controls + onboarding (`Controls/`, `UI/`)** — on-screen gas/brake pedal hints (`HandheldControlsHud`) over the existing half-screen touch zones, holder-vs-handheld detection from the tablet's charging state (`HandheldDetector`, with facilitator override), a first-run how-to card (`HowToPlayOverlay`), and a front-of-house Kenya/Dutch drive-side toggle on the Title/Setup screens wired to `world.driveLeft`.
> - **Kiosk deployment (`Core/`)** — `KioskLock` swallows the Android back button and `OrientationLock` pins the screen orientation for unattended flight-case use; both still need an on-device test. The deployment docs (build/keystore/APK, tablet golden image) live in the vault.
> - **UI direction locked (`UI/`)** — screens rebuilt to the approved visual language (two registers: colourful story screens for players, dark instrument screens for facilitators; the HUD as a physical diegetic dashboard — `DiegeticHud`, now allocation-free per frame), plus `AttractMode`, the `UiKit`/`UITheme` foundation, and micro-feedback (`ScorePunch`, `UiPulse`).
> - **Haptics moved to `Feedback/`** (`HapticDriver` + `HapticFeedback`).
> - **Tools menu regrouped (2026-07-04)** — the flat ~30-item `Tools > Kenya Scooter` list became 7 task submenus (**Turns and Junctions**, **Road Tiles and Seams**, **Roadside Props**, **Weather and FX**, **UI Builders**, **Game Setup**, **Facilitator**) with the **Road Tools panel pinned first**; turn tools are ordered make → place → junction → fix-up with separators. Only `[MenuItem]` paths/priorities changed — no tool was renamed or removed, and stale pointers to menu items removed on 2026-06-25 (`Create Branch Carrier Tile`, `Create Turn Tile`, `Create Path Tile`) were corrected in `RoadSequencer`/`TurnScheduler`/`RoadPath`. **Superseded later the same day** by the clean-slate turn rebuild above: the *Turns and Junctions* submenu and every turn/junction tool were **deleted** — turns need no tool any more. What remains: *Road Tiles and Seams* (create tile / ground / generated road strip + the seam fix-ups) and the slimmed Road Tools panel.

---

## Folder → namespace map

| Folder | Namespace | Responsibility |
|---|---|---|
| `Core/` | `KenyaScooter.Core` | Game state machine, event hub, world speed, road axes, pooling, performance tiers (`PerformanceMode`), kiosk + orientation locks |
| `Config/` | `KenyaScooter.Config` | All ScriptableObject configs — every tunable number in the game |
| `Controls/` | `KenyaScooter.Controls` | Input providers (gyro, touch zones, keyboard/gamepad/mouse) + router + handheld detection |
| `Player/` | `KenyaScooter.Player` | Scooter movement, lean, wobble, collision/lane/near-miss/speed detection |
| `Cameras/` | `KenyaScooter.Cameras` | The single camera writer + shake and Real Rider value providers |
| `Traffic/` | `KenyaScooter.Traffic` | Vehicles, personalities, profiles, spawning, overtake detection, horns, lights/exhaust/animation |
| `Roads/` | `KenyaScooter.Roads` | Tiles (length, hazards, begin/exit points, turns, surface + hazard density — all on `RoadTile`; `RoadSurfaceFeel`), sequences, the sequencer grammar (+ basic tile/seam tools in `Editor/`) |
| `Hazards/` | `KenyaScooter.Hazards` | Potholes, rocks, speed bumps + the cluster spawner |
| `SafetyNet/` | `KenyaScooter.SafetyNet` | Grace charges, the rewind ring buffer, rewind visuals |
| `Scoring/` | `KenyaScooter.Scoring` | Score (single writer), streak, stats, group/high score, leaderboard |
| `Session/` | `KenyaScooter.Session` | Timer, day cycle, speed zones, checkpoint + charge-station relay, analytics |
| `Settings/` | `KenyaScooter.Settings` | Facilitator settings: catalogue, menu, presets, access code (+ builder menu in `Editor/`) |
| `Audio/` | `KenyaScooter.Audio` | Engine layers, ambient crossfade, SFX pool, ducking |
| `UI/` | `KenyaScooter.UI` | HUD (diegetic dashboard), warnings, popups, menu/system screens, leaderboard, language, onboarding, debug panel |
| `FX/` | `KenyaScooter.FX` | Speed read (`SpeedFeel`, `SpeedLines`, slipstream + `DustRush`), the scooter headlight (`ScooterHeadlight`) and dirt-road dust (`ScooterDirtDust`), warm grade, dust/haze, heat shimmer, dust devils, wind |
| `Environment/` | `KenyaScooter.Environment` | Pooled roadside life: prop spawner + reactive props (+ prop factory in `Editor/`) |
| `Sky/` | `KenyaScooter.Sky` | Sky birds, the night sky, the distant mountain range (`HorizonRange`) and clouds + sun glow (`SkyAtmosphere`) |
| `Feedback/` | `KenyaScooter.Feedback` | Haptics |

---

## MDA mechanic → script traceability (M1–M27)

| # | Mechanic | Implemented in |
|---|----------|----------------|
| M1 | World scroll | `WorldSpeed` (speed owner) + central scroll loops in `RoadSequencer`, `HazardSpawner`, `TrafficVehicle.Tick` |
| M2 | Speed system | `WorldSpeed.ApplyThrottle` + `ScooterConfig` (10/30 m/s, 15/20/5 m/s²) |
| M3 | Road turn | A tile's **`RoadTile.turns` list** (each entry: green ball = begin metres, red ball = end metres, degrees; `RoadTile.EvaluateRun` is the single evaluator) bends the road in `RoadSequencer` (constant +Z/+X frame; the road curves around the stationary player); `CameraRigController` + `ScooterLean` bank into it from `RoadDirection.CurveRate` |
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
| M25 | Day/night cycle | `DayCycleManager` + `DayCycleConfig` (six phases: ASUBUHI → MCHANA → ALASIRI → JIONI → MAGHARIBI → USIKU) + URP volume crossfade; `FX/ScooterHeadlight` fades in at night off `NightFactor01` |
| M26 | Session timer | `TimerManager` (120 s, pauses during rewind) |
| M27 | Relay / group scoring | `GroupScoreManager` (add-only commits) + `LeaderboardManager` + `CheckpointScreen` + the `ChargeStationSequence` hand-off choreography |

Requirements-only systems with no MDA number: speeding tiers (`SpeedMonitor` + `SpeedZoneManager`, Req §7.3), checkpoint choreography (`CheckpointController` + `ChargeStationSequence`, Req §9.2), audio (`AudioManager`, `TrafficHorn`, Req §11), UI suite (Req §12–13), analytics (`AnalyticsManager`, Req §14), accessibility/facilitator controls (the `Settings/` suite, Req §16), kiosk deployment (`KioskLock`, `OrientationLock`), performance tiers (`PerformanceMode`), and the ambient environment layer (`Environment/`, `Sky/`).

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
- **Two sharp turns close together**: the author's responsibility now — by design. The old overlap guard silently flattened authored turns (the main reason "turns randomly didn't work"), so it was removed with the 2026-07-04 clean-slate rebuild: what you author on a tile's Turns list is ALWAYS ridden exactly. If two sharp turns sit close together within one draw distance, the road can visually overlap itself — the cyan driven-line gizmo on each tile shows exactly what will be ridden, so space turns sensibly when authoring sequences.
- **Rewind vs tile queue**: fixed — the rewind now replays the identical road. `RoadSequencer` snapshots its on-screen tiles on `RewindStarted`; after the rewind lands, any snapshot tile the rewind switched off was one spawned during the rewound window (so its prefab had been dequeued), and `RequeueRewoundTiles` pushes those prefabs back to the front of `prefabQueue` in near-to-far order. The horizon therefore re-spawns the same tiles in the same order rather than jumping to the next prefabs. Any sequence the window happened to advance into is already physically carried in the surviving queue, so no sequence selection is re-run. Pooling stays intact via `ObjectPool.ReconcileAvailability`. Instance→prefab mapping is the new `ObjectPool<T>.Prefab` getter.
- **Swahili strings** marked `TODO: native review` in `SwahiliUI` reuse English rather than risk invented Swahili (SC3 — respectful representation beats fake localisation). Verified strings (ALAMA, MUDA, POLE POLE, REKODI MPYA, and the Requirements-specified INGEHAALD/KUUKUA/JULLIE STAAN OP PLEK) are in. UI text can now also be edited and translated outside code via the optional `UIStringsConfig` asset (see Recent changes above); `SwahiliUI` reads it when present and falls back to these built-in strings otherwise.
- **Audio clips** are not imported yet (known backlog) — `AudioManager` is fully wired and null-safe and the layered Kenya soundscape system exists, but the game runs silent until clips land in `AudioConfig` (see [[Kenya Soundscape — Audio Clip Spec]] for what to source).
- **Gameplay animals stay cut.** The designed goat/cattle/elephant/warthog crossings (Req §6.4) remain out of scope per the Requirements' own list; the unused `WildlifeCrossing`/`WildlifeAnimal` scaffolding was removed on 2026-06-27 and no `AnimalManager` exists. Ambient scenery animals *do* now appear through the roadside-life spawner (e.g. `RoadsideChickens`, animal props) — pure scenery, no colliders, no gameplay.
- **EditMode tests exist but do not run yet.** The `Roads` (RoadTileTests) and `Settings` test suites are written, but in the live Unity project they sit in `Tests~/Editor/` — the trailing `~` makes Unity ignore the folder entirely. Rename it to `Tests` to compile and run them; until then no "tests pass" claim is meaningful.
- **Vault mirror ↔ live project drift.** The vault mirror (`Code/KenyaScooter (current)/`) and the live project (`Assets/Impact Makers Around The world/Scripts/`) drift apart over time. The 2026-07-03 wind/ambience batch and the 2026-07-04/05 turn rebuild, day/night-headlight, dirt-road and performance-mode work have all been copied to the live project (byte- or hash-verified per file). Always `git diff --no-index` live vs vault before copying in either direction — a file can carry a fix that exists on one side only.
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
