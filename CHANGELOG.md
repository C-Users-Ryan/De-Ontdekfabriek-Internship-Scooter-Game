# Changelog

All notable changes to the Kenya Scooter Game (`KenyaScooter`) codebase.
Format based on [Keep a Changelog](https://keepachangelog.com/). This entry covers the final feature and fix push.

## [Unreleased] - 2026-07-05

### Added — day/night blue hour + a real scooter headlight
- A sixth day phase, **MAGHARIBI (blue hour)**, plus a **USIKU** night bridge the dusk→night jump (a ~30 s sweep, not a 15 s collapse); phase times re-paced across the session and `DayCycleConfig.scaleToSessionLength` fits the arc to any facilitator-set timer.
- **`FX/ScooterHeadlight`** — the only real light in the scene besides the sun/moon: a shadowless spot, off by day, easing in below dusk off `DayCycleManager.NightFactor01`. Traffic never gets a real `Light` — cars glow **emissive** head/tail lights (`TrafficVehicleLights` / `TrafficVehicleAutoLights`), so the scene holds at two real lights regardless of traffic count.

### Added — dirt (murram) roads
- **`RoadTile.surface`** (Paved/Dirt) + **`RoadTile.hazardDensity`** (0–4, more potholes/rocks on a rough tile). `Roads/RoadSurfaceFeel` publishes the eased dirt blend. The surface is communicated by **dust** (`FX/ScooterDirtDust`, a rear-wheel plume that blooms on dirt, clean on tarmac), not by shaking (`Player/DirtRumble` cut to a whisper). Facilitator step "Onverharde wegen" (UIT/SUBTIEL/VOL). Feel/FX only — speed, steering and scoring are identical on every surface.

### Added — performance modes
- **`Core/PerformanceMode`** — a facilitator "Prestaties" tier (AUTOMATISCH/VOLLEDIG/GEBALANCEERD/LICHT) so old exhibition tablets stay smooth. Reversible tier engine (captures the authored baseline, restores before each apply, re-applies on session reset). **Trades atmosphere, never gameplay:** traffic, hazards, speeds and scoring are identical in every tier so leaderboard scores stay comparable across devices.
- Scoring amounts (overtake multiplier, per-hazard values, streak on/off) exposed to facilitators; a zone-aware speed read crossfades white speed lines (city) and dust (wild).

### Changed — road turns rebuilt clean-slate
- Turn authoring is now **two draggable points per turn** on `RoadTile` (green = begin, red = end); a tile defines itself (length, difficulty, allowed hazards, begin/exit points, turns). `RoadTile.EvaluateRun` is the single evaluator the sequencer, mesh, lean and spawn gating all read; **authored turns are never softened**. See `Assets/Impact Makers Around The world/ROAD SYSTEM — Step by Step Guide.md`.

### Removed — the old turn/junction stack
- Deleted 19 scripts: `TileTurn` (+`TileTurnArt`/`TileTurnConform`), `TurnTrigger`, `TurnScheduler`, `TurnSafety`, `RoadPath`, `RoadJunction`, `JunctionSideRoads`, `curveAngle`, the exit/stop anchors, and every turn/junction editor tool + its tests. **Branching junctions were removed as a feature.** Old turn/junction prefabs are dead — re-author tiles with the Turns list.

### Changed — UI rebuilt 1:1 to the mock (UI v2)
- The framing screens, the diegetic HUD and the settings menu were rebuilt against the 11-screen improved-UI mock across ~9 play-test rounds: measured sizing (not eyeballed), title-first flow (tap-to-start-anywhere retired), one-tap play, and drawn sprites where the font has no tick/cross/back glyph.

### Removed — UI elements that were not in the design
- The **A109 route-progress strip** on the relay + eindstand screens and the **"JOUW RIT" coaching panel** were removed (not in the mock). *Supersedes the "route strip on the relay + eindstand" and "coaching report on the relay hand-off" additions in the 2026-06-30 entry below.*

### Changed — sense of speed de-gimmicked (final state)
- The speed read is now a **gentle capped FOV** (~+8°) plus Kenyan dust (`SlipstreamDust` / `DustRush`) plus `SpeedLines` particle streaks. **Removed:** the camera speed-dive, the `SpeedGrade` chromatic-aberration/fisheye volume, and the velocity rattle — all distorted the fixed-camera view and pushed the first-person handlebars out of frame. `RealRiderMode` horizon roll capped at 15°. *Supersedes the "Velocity" speed-read description in the 2026-06-30 entry below.*

### Maintenance
- The "Levend Kenia" wind/ambience FX batch was copied to the live project (2026-07-03).
- `README — Architecture and System Map.md` and the Road System guide were updated for all of the above; the vault mirror and live project were re-synced.

## [Unreleased] - 2026-06-30

### Added — settings hub covers the newest systems (presets now shape the whole game)
- Four new facilitator settings in `SettingsCatalog.Definitions.cs`, with matching global hooks: **"Auto's halen elkaar in"** (gates `TrafficVehicle.OvertakingEnabled` — calm road for young groups), **"Pech langs de weg"** (`BreakdownBoost` 0–200%, multiplies every prefab's breakdown chance), **"Demostand (zelfspelend)"** (`AttractMode.IdleSecondsOverride` — attract delay, 0 = off), **"Reiskaart-tempo"** (`JourneyProgress.MapScale` ×2–×30). All persist via the existing GameSettings override store and are preset-stackable like every other catalog entry. Remaining un-hooked systems (SkyLife birds, dust devils, heat shimmer, backdrop toggles) documented as the next batch — same one-entry-per-setting pattern.

### Added — the Journey Map (score becomes geography)
- **`Session/JourneyProgress.cs`** (new, self-contained): every committed turn's driven distance accumulates into one shared class journey down the real **Nairobi–Mombasa A109**. Mirrors `GameManager.CommitTurn`'s semantics via the same events (checkpoint/finish commit once; `GroupReset` starts a fresh journey; persisted like the group score) — zero core edits. Real metres are scaled ×10 (`MapScale`) so a typical class genuinely travels the map (one turn ≈ 18 map-km; a class reaches Mtito Andei/Voi; a great one reaches Mombasa).
- **Route strip on the relay + eindstand screens** (`KenyaMenuScreens` both halves): a stylised A109 with town dots (Nairobi · Athi River · Emali · Mtito Andei · Voi · Mombasa), a progress fill and the class marker. Hand-off reads "ONDERWEG NAAR {volgende stad} · X KM GEREDEN"; the eindstand "JULLIE KWAMEN TOT {stad} · X KM VAN DE A109". The relay finally has a destination.

### Added — arcade attract mode (the kiosk demos itself)
- **`UI/AttractMode.cs`** (new, self-bootstrapping): after 60 s untouched on the Ready screen, the game runs a real session on a scripted autopilot — the bike cruises at base speed via `PlayerController`'s existing scripted-pose API while a simple road-space brain pulls out to pass slower cars only when the oncoming lane is clear — under a pulsing **"TIK OM TE STARTEN"** banner. Any touch/key ends it instantly. Safe by construction: exits only through `ForceReset()` (never commits a score) and self-ends at 70 s, well before the 120 s timer, so the checkpoint/commit path can never fire. A failed pass just demos the grace/rewind safety net. Tune/disable via `idleSeconds`.

### Fixed — kiosk & polish (expert pass over all per-frame code)
- **Tablet could dim/sleep mid-workshop**: nothing set Android's screen timeout, so the attract/title screen would blank whenever nobody touched the tablet for a few minutes. `GameManager.Awake` now sets `Screen.sleepTimeout = SleepTimeout.NeverSleep` (kiosk requirement, Req §16).
- **Audio kept droning while the game was paused**: the facilitator settings menu freezes the world (`Time.timeScale = 0`) but audio ignores timescale, so engine loops/ambience played over a frozen game. The menu now sets `AudioListener.pause` on open (both the keypad and direct paths) and releases it on close.
- **Per-frame render churn in the vehicle lights**: `TrafficVehicleLights` rewrote `MaterialPropertyBlock`s on 4+ renderers per vehicle every frame even when nothing changed (~thousands of redundant calls/sec across the fleet; `SetPropertyBlock` has no equality skip). Now caches the last-applied colours and writes only on change — steady-state cost is zero.
- **Wheel transforms written while parked**: `TrafficVehicleAnimator.SpinWheels` now skips the transform writes when the spin delta is zero (verge-stopped / broken-down / pooled cars).
- Swept every remaining `Update`/`LateUpdate`/`Tick` body for per-frame allocations and unguarded UI writes: the rest of the codebase is clean (rate-limited readouts, change-guarded text, zero-alloc detector loops) — verified, no further fixes needed.

### Maintenance — cross-session drift fully reconciled (both trees byte-identical)
- The vault mirror and the live project had drifted on 10 files after ~5 parallel work sessions. Verified the drift was strictly one-sided per file (the trees were hash-identical on 27 Jun 00:30; every change since happened on exactly one side), then reconciled deliberately:
  - **live → vault**: `Audio/AudioManager`, `Config/AudioConfig` (the audio-wiring session), `Player/NearMissDetector`, `Traffic/OvertakeDetector`, `Traffic/TrafficSpawner` (the road-space traffic rework).
  - **vault → live, as a complete set**: `Cameras/CameraRigController` + `Cameras/CameraShake` + **new** `FX/SpeedFeel.cs` + **new** `FX/SpeedGrade.cs` (the "Velocity" speed-feel rebuild — the cameras reference the two FX classes, so they ship together), plus **new** `UI/ObjectiveCue.cs` (the per-turn "HAAL VEILIG IN!" objective banner).
- Cross-API check: the co-resident reworks touch each other only via `GameEvents` and stable singletons — no signature collisions. Full-tree parity now **0 differences**. Unity will generate `.meta` for the 3 new files on next open — commit them.

### Fixed/Added — roadside life now MOVES (people mill, animals wander)
- **The world felt empty because the roadside figures were frozen** — people only *waved* and animals stood still, and the roadside config may never have been generated. Added `Environment/RoadsideWalker.cs`: a self-contained behaviour that gently wanders/paces a figure's *body child* (walking bob + stop-and-look), so people mill about the verge and animals graze/amble instead of standing dead. Composes with the spawner (which owns the root's road pose) and with `RoadsideWaver`; rides the curve and the rewind. No core edits.
- **Added animals**: the roadside starter factory now also builds **goats** (graze in small herds) and **zebu cattle** (amble further back), and **wires `RoadsideWalker` onto the people and the animals**, so one click populates a *moving, animal-filled* roadside (on top of the existing stalls / dukas / windmills / solar / acacias / elephant).
- **To fill the world**: run *Tools ▸ Kenya Scooter ▸ Build Kenya Roadside Starter* in Unity — it generates the prop prefabs, the `RoadsidePropConfig`, and a scene spawner — then save the scene. Busy-ness is the facilitator "Drukte langs de weg" dial; town-vs-country contrast comes from the per-row context tags. (Pedestrians *crossing* the road remain a separate system: `PedestrianCrossingSpawner`.)

### Added — coaching report on the relay hand-off (road-safety teaching)
- The between-players hand-off screen now shows a short **coaching report** built from the stats already tracked in `SessionStats`: three colour-graded lines — **lane discipline** (wrong-lane time), **speed** (speeding ticks) and a **safety summary** (collisions + hazard hits) — in plain Dutch, so each turn ends on a teachable beat instead of just a number. Green/amber/red reinforces the wording (it isn't the only cue). A pure read of data already collected — no new tracking, no scene wiring. (`KenyaMenuScreens` relay screen + `LaneVerdict`/`SpeedVerdict`/`SafetyVerdict` helpers; thresholds easy to tune.)

### Added — traffic game-feel (procedural vehicle animation)
- **`Traffic/TrafficVehicleAnimator.cs`** makes cars read as vehicles with weight instead of sliding blocks — no art or keyframes, all driven by the motion they already have: the body **rolls** into swerves/lane-changes/overtakes, **dives** under braking and **squats** under acceleration, **bobs** with the engine at speed, **trembles** when stopped (verge-stop / breakdown idle), and optional **wheel** transforms spin at the right rate for the speed. Self-contained companion to the engine/lights components: reads only the sibling's public motion and animates a body CHILD (never the road-driven root), so no traffic-core change and null-safe per part. Mirrors how the player scooter splits movement from `ScooterLean`/`ScooterWobble`. Setup: put the vehicle mesh on a child, assign it as `body` (or let it auto-find), optionally assign wheel transforms, tune to taste.
- **Per-spawn variety ("not clones")**: each vehicle now spawns with a randomised animation **phase** (so a queue of the same prefab never bobs/idles in lockstep), a small random **body size** (`scaleVariation`), and slight **idle-bob** variation (`bobVariation`). Combined with the existing per-spawn engine-pitch jitter and lane-drift phase, two of the same prefab no longer read as identical copies. All procedural, in `TrafficVehicleAnimator` — no art, no core edits.
- **Exhaust puffs** (`Traffic/TrafficExhaust.cs`): a puff out the back on hard acceleration plus a faint cruising trickle, world-simulated so it trails behind the car. Builds its own URP-safe particle system (reuses `FXMaterials`, no magenta), reads only the sibling's speed. Self-contained, no core edits.
- **Road-roughness jitter**: the body wobbles over the rough/dirt road (Perlin, scaled by speed) on top of the engine bob — cars no longer glide perfectly flat.
- **Per-spawn colour variety**: each car gets a small random brightness/hue shift via `MaterialPropertyBlock` (no material instancing), so same-prefab cars aren't the same colour. `TrafficVehicle.SetHighlight` now reads the block first, so the tint and the near-miss highlight coexist on one renderer.
- **Placeholder car factory** (`Traffic/Editor/KenyaTrafficVehicleFactory.cs`): *Tools ▸ Kenya Scooter ▸ Build Traffic Vehicle* assembles a primitive car (root + Body child + 4 wheels + emissive light strips) **pre-wired** with TrafficVehicle/Horn/Engine/Lights/Animator/Exhaust — so all the procedural feel works out of the box. Drop it into the spawner's prefab lists; swap the primitive meshes for real art later (the wiring holds as long as the Body/wheel/light children remain). This also solves the "mesh must be on a child" setup the animator needs.

### Added — traffic AI (overtaking & breakdowns)
- **Car-to-car overtaking** (`TrafficVehicle.UpdateOvertake`): a vehicle held up behind a clearly-slower car in its lane commits to a pass — pulls into the oncoming lane, accelerates past, and tucks back when there's room. It only commits when the oncoming lane is clear for ~55 m and **aborts (tucks back, pauses) the instant an oncoming vehicle enters that window**, so it never manufactures an AI head-on. Willingness scales with the driver (aggressive pass readily; cautious/distracted rarely). Per-prefab `canOvertake` (default on) + `overtakeUrgency`. One zero-allocation scan of the active list, like the other detectors.
- **Dynamic breakdowns** (`TrafficVehicle.breakdownChance`, default 0): a moving car can randomly stall to the verge ahead of the player and stop, raising `HazardFlashers` so `TrafficVehicleLights` blinks both indicators — a sudden, telegraphed obstacle. Reuses the existing verge-stop path.
- Both additive and backward-compatible (overtaking defaults on but is tunable / per-prefab off; breakdowns default off). No scene changes required.

### Changed — traffic AI (data-driven archetypes + the tuning now actually applies)
- **Driver archetypes are now data-driven.** Each entry in a `TrafficBehaviourProfile`'s Personalities list carries its own `label` + spawn `weight`, and the new `TrafficBehaviourProfile.PickSettings()` does a weighted draw over the list. **Adding a new archetype is now one list element** (name, weight, trait values) in the Inspector — no enum value, no extra weight field, no code (`DriverPersonality` is demoted to a legacy label). `TrafficVehicle.Activate` uses `PickSettings()`.
- **Fixed: the personality tuning wasn't reaching the game.** `TrafficProfile.asset` overrode the code defaults with an older list that predated the new trait fields — so `accelerationMult`/`followGapMult`/`reactionMult`/`laneBias`/`hornEagerness` fell back to neutral and *every* driver had `yieldShift = 0`. The asset's four archetypes are now authored with the full characterful values (+ `label` + `weight`), so Aggressive / Cautious / Distracted / Normal are genuinely distinct **at runtime**, not just in the code defaults.

### Changed — traffic AI (more distinct driver personalities)
- **Lane positioning by driver** (`PersonalitySettings.laneBias`): aggressive drivers ride toward the centre line, cautious hug the verge, distracted wander — the lane now reads as occupied by people, not cars stamped dead-centre.
- **Honking by driver** (`PersonalitySettings.hornEagerness`, read by `TrafficHorn`): aggressive lean on the horn (2.2×), cautious rarely use it (0.3×).
- **Pushed the four archetypes further apart** so each is recognisable at a glance — Aggressive: faster, tailgating, centre-hugging, honky, hard acceleration; Cautious: slower, big gaps, verge-hugging, quiet, gentle; Distracted: wanders more (bigger drift + speed jitter, slow reactions); Normal: the calm middle. All backward-compatible (new fields default neutral; built-in `TrafficBehaviourProfile` ships the characterful values).

### Changed — traffic AI (per-driver & per-type dynamics)
- **Acceleration now varies by type and driver.** Added `TrafficVehicle.acceleration` (m/s², per prefab — set a lorry low ~1.5, a boda-boda high ~6) and a per-personality `accelerationMult` (Aggressive 1.35×, Cautious 0.8×). The old fixed 3 m/s² for every vehicle is gone, so heavy traffic lugs up to speed and nimble traffic darts.
- **Tailgating vs hanging back.** Per-personality `followGapMult` scales the car-following distance and minimum gap — Aggressive tailgate (0.55×), Cautious leave a big cushion (1.6×). Previously every driver kept the same gap.
- **Reaction sluggishness.** Per-personality `reactionMult` scales how fast a driver corrects their lane position — Distracted (0.5×) wander and are slow to settle after a dodge/yield; alert drivers snap back to their line.
- All new values default to neutral (multipliers 1×, `acceleration` = the previous 3 m/s²), so **existing behaviour is unchanged until tuned** — and the built-in `TrafficBehaviourProfile` already ships sensible per-archetype values. Backward-compatible; no scene/prefab changes required. (The deeper items — car-to-car overtaking, corner slowdown, breakdowns, new archetypes — are written up in `_Workspace/_TRAFFIC AI — Advanced Behaviours Backlog.md`.)

### Modularity (handover readability — no behaviour change)
- **Split the two largest Settings files into `partial class` files**, completing the readability pass started on the UI/FX files. Byte-verified (the reconstructed class body differs from the pre-split file only by the `partial` keyword + a NOTE comment — no logic moved):
  - `Settings/SettingsMenu.cs` (state/lifecycle: open/close, the access-code PIN flow, Update) + `Settings/SettingsMenu.Build.cs` (Build() and the header/sidebar/rows/overview/profiles construction).
  - `Settings/SettingsCatalog.cs` (query API: `InCategory`/`ById`, actions, presets, labels, value formatters) + `Settings/SettingsCatalog.Definitions.cs` (`Build()`: the curated list of every facilitator setting).
  - With the earlier DiegeticHud / KenyaMenuScreens / SkyLife splits, all five oversized files are now split. On next Unity open the two new files get auto-generated `.meta` — commit those too.

## [Unreleased] - 2026-06-27

### Added
- **Vehicle lights & signals** (`Traffic/TrafficVehicleLights.cs`): a self-contained component giving each car the road's readable signals — **brake lights** (glow when slowing/stopped: yielding, following, verge-stops), **tail + headlights** that come on at dusk/evening (driven by the day cycle), and **turn indicators** that blink toward a lane change, with a broken-down vehicle flashing **both** as hazards. Atmosphere *and* road-safety teaching (read the car ahead's brake lights). Reads only public vehicle state + the day-phase event, drives emissive via MaterialPropertyBlock (no instancing), and is null-safe per light renderer — so it needs **zero changes to the traffic core** and any prefab without light meshes simply skips it.
- **Per-type traffic engine sound** (`Traffic/TrafficEngine.cs`): a per-vehicle looping engine sound so each vehicle *type* is audibly distinct (truck rumble, boda-boda buzz, matatu hum) — the audible half of "different vehicle types". Self-contained companion to `TrafficHorn`: owns its own child 3D AudioSource (never fights the horn), starts on spawn / stops on recycle (pooled cars stay silent), and optionally revs the pitch with the vehicle's own speed (with a small per-spawn jitter). Null-safe — no clip assigned = silent, like the rest of the audio system. Purely additive: no changes to the traffic core (`TrafficVehicle`/`TrafficSpawner`).
  - _Already supported, no code needed:_ **different vehicle types per direction** — `TrafficSpawner` has separate weighted `sameDirectionPrefabs[]` and `oncomingPrefabs[]` pools (assign different prefabs per direction); and **per-type horns** — `TrafficHorn.hornClips[]` is per-prefab. To use the new engine sound: add `TrafficEngine` to a vehicle prefab (next to `TrafficHorn`) and assign that type's engine clip. (Clips themselves are still the known audio-import backlog.)

### Modularity (handover readability — no behaviour change)
- **Split the three largest UI/FX files into `partial class` files**, so each file has one clear job and is far easier to take over. The original file keeps its name and its `.meta` GUID (every prefab/scene script reference stays valid); a second partial holds the procedural-construction half. Verified byte-for-byte: the reconstructed class body differs from the pre-split file *only* by the `partial` keyword and a NOTE comment — no logic moved, lost or duplicated.
  - `UI/DiegeticHud.cs` (runtime: lifecycle, per-frame `Drive*`/reels/LEDs, event handlers) + `UI/DiegeticHud.Build.cs` (the instrument-cluster construction + procedural sprites).
  - `UI/KenyaMenuScreens.cs` (runtime: screen flow, data population, button actions) + `UI/KenyaMenuScreens.Build.cs` (the framing-screen construction).
  - `Sky/SkyLife.cs` (runtime: spawning, the bird tick/billboard, pooling) + `Sky/SkyLife.Assets.cs` (shared material, quad meshes, procedural silhouette textures).
  - _On next Unity open, the three new files get auto-generated `.meta` files — commit those too._

### Performance & cleanup (tablet pass)
- **`DiegeticHud` per-frame GC removed**: `DriveLimit()` called `int.ToString()` and reassigned the speed-limit `TMP_Text` *every frame*, and `DriveWarning()` reassigned the warning text every frame while a warning showed — each forcing a string allocation and a text-mesh rebuild ~60×/s on the tablet. Both now cache the last value and only touch the text when it changes (the pattern `HUDController` and `Speedometer` already use). The colour throb stays per-frame (it's a struct assignment, no GC).
- **`DiegeticHud` no-op fixed**: `DriveLimit()` had `limitRing.color = (...) ? danger : danger;` — both branches identical, plus an unused per-frame `WorldSpeed` read. Reduced to `limitRing.color = danger;`.

### Removed
- **Dead wildlife scaffolding** (`Roads/WildlifeCrossing.cs` — the `WildlifeCrossing` and `WildlifeAnimal` types): no animals ship (already documented in the architecture README), the script was referenced by no prefab or scene, and `WildlifeCrossing` ran an empty-purpose `Update` every frame. Removed the file plus the now-dead `WildlifeAnimal` branch (a per-collision `GetComponentInParent`) from `PlayerCollisionHandler`.

### Maintenance
- **Source mirror re-synced**: the Obsidian vault mirror (`Code/KenyaScooter (current)/`) and the live Unity project are byte-identical again. The vault was ahead on the facilitator settings/PIN/presets work (`Settings/CustomPresets`, `FacilitatorLock`, `SettingsPreset`, `SettingDefinition`, `SettingsCatalog`, `SettingsMenu`, `Settings/Editor/KenyaSettingsBuilderMenu`, `FacilitatorGate`, `UI/UIStringsConfig`) plus new Settings EditMode tests — all copied into the project (tests into the Unity-ignored `Tests~/`).

### Fixed
- **Compile error `CS0136` in `Roads/Editor/TileTurnTools.cs`** (blocked all play-mode entry): two locals named `art` in one method — a `Transform art` inside the per-tile loop and a `string art` (summary text) in the enclosing method scope. Renamed the summary string to `artNote`. (Latent bug that only surfaced when Unity recompiled the Editor assembly.)
- **Runtime log spam "Particle Velocity curves must all be in the same mode" (hundreds/sec)**: three procedurally-built particle systems set `velocityOverLifetime` x/y/z in mixed `MinMaxCurve` modes (a single-value axis is *Constant*, a `(min,max)` axis is *TwoConstants*; Unity requires the trio to match). Fixed in `FX/DustDevil.cs` (column) and `FX/DustAtmosphere.cs` (veil + gust) by writing the constant axes as `(v, v)` — identical behaviour, one mode. The always-on `DustAtmosphere` was the spam source. (`FX/SpeedLines.cs` was already all-Constant — left as-is.)

## [Unreleased] - 2026-06-21

### Added
- **Diegetic HUD cluster** (`UI/DiegeticHud.cs`): code-generated instrument cluster for iPad 4:3 (score odometer, top-semicircle speedometer, battery-as-timer, speed-limit roundel, warning LEDs, top route/day strip). Built via `Tools > Kenya Scooter > Build HUD Cluster`.
- **Score punch** (`UI/ScorePunch.cs`): punch and flash on a score gain.
- **Framing screens** (`UI/KenyaMenuScreens.cs`): Title, Relay hand-off, Journey Complete (with class scoreboard), Game Over. Deterministic top-down layout, screen fade-in. Built via `Tools > Kenya Scooter > Build Menu Screens`.
- **Team-name chips**: one-tap Swahili animal names (chips only, no free typing) shown once per group.
- **Continuous team score**: the HUD odometer shows the running group total so the score carries between players.
- **Shared multiplier**: the clean-overtake streak persists across players within a group; resets on a new group or any violation. Always-visible, tier-coloured multiplier badge.
- **`GroupReset`** event (`Core/GameEvents.cs`), raised by `GroupScoreManager.ResetGroup`.
- **`IllegalOvertake`** event + `TrafficVehicle.PassOnCorrectSide`: wrong-side passes earn no points and trigger a corrective warning.
- **Crash vignette**: red screen-edge flash on a hard crash.
- **Pothole variants**: four hand-built low-poly pothole textures (`Prefab/hazards/Pothole_LP_*.png`) plus `Hazards/PotholeVariant.cs` (random texture + mirror per spawn).
- **Editor tooling**: `Tools > Kenya Scooter > Wire Scoring Managers` (optional, since the managers self-create).

### Changed
- **Speedometer** rebuilt as a top semicircle with the needle and fill driven from one value (cannot drift apart); lowered so the dial reads as one unit.
- **Panel** sits flush to the bottom with rounded top corners; route/distance strip moved to the top of the screen; the orange accent line is now an inset rounded stripe.
- **Odometer** framed with a lighter bezel for contrast.
- **Battery** colours flipped: green at the top, warning colours at the bottom (matches the top-down drain).
- **Warnings** reworked into a two-layer model (named-rule banner + peripheral LEDs); named hazard messages (KUIL / OBSTAKEL / DREMPEL); warning direction (Blijf links / Blijf rechts) now read from `RoadSideConfig`.
- **Wrong-lane warning** fires only after excessive time on the wrong side, with an immediate trigger when hugging the far edge.
- **Relay "next player"** continues straight into the next turn instead of returning to the title/setup screen.
- **`GroupScoreManager`** and **`StreakSystem`** now **self-create at runtime** if absent from the scene, so the team score and multiplier work with zero scene wiring. `StreakSystem` falls back to a default multiplier ladder when no `ScoreConfig` is set.
- **`ScoreManager`** auto-finds or creates a `StreakSystem`.
- **`LeaderboardManager.CommitGroup`** stores each group under its chosen team name.

### Fixed
- **Rewind desync** where the object you hit appeared to follow you back and traffic clipped through itself: `RewindSystem` was silently dropping participants past a fixed cap, so they were never rewound. Buffers now grow to fit every participant (registration only, allocation-free recording preserved).
- **Multiplier stuck at 1x and score not carrying between players**: root cause was that `GroupScoreManager` and `StreakSystem` were never placed in any scene. Fixed by the self-create change above.

### Removed
- The unlabelled grace-shield indicator from the top route strip (read as clutter; the grace event already has its own feedback).

### Scene setup still required (not code)
- Disable the legacy UI so it does not double up with the new HUD/screens: `HUDController`, `CheckpointScreen`, `EndScreen`, `ThemedEndScreen`, `WarningSystem`.
- Assign a checkpoint tile on `RoadSequencer` (and wire `CheckpointController`) so the timer coasts to a charge station instead of cutting to the finish.
- Add `PotholeVariant` to the pothole prefab and assign the four `Pothole_LP_*` textures (the round one is already the default texture).

### Notes
- Original pothole photo backed up as `Prefab/402-4020061_pothole-pothole-png.png.original.bak`.
- None of the above has been user-tested; legibility and a play test on the iPad with 12 to 18 year olds remain the open validation step.

## [Unreleased] - 2026-07-01 — Traffic mood & character depth
- **`Traffic/TrafficMood.cs`** (new): the road remembers HOW the player drives. A 0..1 respect scalar falls on collisions/wrong-lane/speeding/near-misses and recovers with clean driving + clean overtakes; traffic reads it live — low respect = less yielding, defensively wide oncoming swerves, far more honking, tailgating followers; high respect = courteous roads. The behaviour lesson expressed in behaviour. Facilitator toggle "Verkeer reageert op jouw rijgedrag".
- **Per-spawn temperament + contrast dial**: every driver now samples ±15% temperament over its archetype, and the new "Karakterverschil chauffeurs" slider (×0.25–×2, `TrafficMood.Contrast`) stretches all archetype traits away from neutral — personalities can be made unmistakable or flattened per occasion. Wired through effective traits (`effYield/effSwerve/effAccelMult/effGapMult/effReactionMult`) computed at Activate; horn eagerness now updates live with the road's mood.
