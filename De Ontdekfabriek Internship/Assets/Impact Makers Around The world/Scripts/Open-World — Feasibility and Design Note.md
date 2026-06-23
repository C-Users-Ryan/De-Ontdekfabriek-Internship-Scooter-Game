# Open-World — Feasibility and Design Note
**Document type:** Design spike / decision note
**Author:** Ryan Putman · De Ontdekfabriek · 2026
**Scope:** Whether the on-rails Kenya Scooter Game can or should become an "open world", grounded in the live `KenyaScooter.*` codebase
**Companion documents:** [[README — Architecture and System Map]] · [[MDA — Mechanics Dynamics Aesthetics]] · [[Design/Requirements — Kenya Scooter Game v2]] · [[D17 — MDA-Driven Clean Code Generation]]

> [!abstract] What this is
> A feasibility and design note answering a recurring question: "could we make this open world?" It defines what that would actually mean against the real architecture, lists every architecture rule it breaks and the scripts each change touches, weighs two delivery options, reconstructs why the open-world idea was dropped after Sprint 4, and gives a recommendation. **No gameplay code is changed by this note** — it is a read-only spike.

> [!warning] Headline
> The shipping game is an **on-rails endless runner whose entire spine is built on the player never translating through space**. "Open world" is not a feature toggle here — it inverts the three load-bearing systems (`PlayerController`, `WorldSpeed`, `RoadSequencer`) and re-introduces exactly the moving-frame instability the 2026-06-17 rebuild was done to remove. **Recommendation: do not replace the runner. If the idea must be tested, build it as an isolated free-roam *mode* with explicit success criteria, and prototype a cheap "branching rails" middle path first.**

---

## 0. How the current game actually moves (the thing open world would undo)

Three facts from the code define the whole architecture. Everything below depends on them.

1. **The player never moves along the travel axis.** `PlayerController.ComposePosition()` rebuilds the scooter's transform every physics step from a *single sideways number* plus a fixed height — there is no longitudinal term at all:
   ```csharp
   // Player/PlayerController.cs:119
   private Vector3 ComposePosition()
       => RoadDirection.SteerAxis * LateralOffset + Vector3.up * baseHeight;
   ```
   `LateralOffset` is clamped to `RoadSideConfig.Active.playerLateralLimit` (±4.2 m, [RoadSideConfig.cs:24](Config/RoadSideConfig.cs)). The Rigidbody is `isKinematic = true` ([PlayerController.cs:46](Player/PlayerController.cs)). The scooter physically sits near the world origin and only slides left/right.

2. **The world scrolls past a stationary player; speed is a world property, not the player's velocity.** `WorldSpeed` owns one number, `Current` (m/s), and its own summary says it plainly: *"The scooter never actually drives forward — the whole world moves past it at this speed instead"* ([WorldSpeed.cs:6-12](Core/WorldSpeed.cs)). `DistanceTravelled` is metres of road scrolled *past* the player, not distance the player covered.

3. **The travel frame is a constant +Z / +X; turns bend the road around the player.** `RoadDirection.Current` is hard-coded `Vector3.forward` and `SteerAxis` is `Vector3.right` ([RoadDirection.cs:17-20](Core/RoadDirection.cs)). `Longitudinal()` / `Lateral()` are literally `worldPosition.z` / `worldPosition.x` ([RoadDirection.cs:37-40](Core/RoadDirection.cs)). A turn is not a rotation of the player or the compass — `RoadSequencer.RenderChain()` re-places *every active tile every frame* so the player's current point on the road sits at the origin facing +Z, "swinging the whole world around the stationary player — which is the turn" ([RoadSequencer.cs:170-198](Roads/RoadSequencer.cs)).

The README states this as an enforced architecture rule: *"Player never moves along the travel axis — Structural: `PlayerController` recomposes position from a lateral offset only — there is no code path that translates the player longitudinally"* ([README §Architecture rules](README — Architecture and System Map.md)). The constant frame was a deliberate 2026-06-17 rebuild specifically so traffic, hazards, the overtake/near-miss/wrong-lane detectors and the rewind "stay stable across turns" and have "no moving compass to desync" ([RoadDirection.cs:5-12](Core/RoadDirection.cs)). `TurnTrigger.cs` is now an **obsolete stub** kept only to avoid missing-script errors — its header records that the previous approach (a collider that *snapped a global compass*) was explicitly abandoned ([TurnTrigger.cs:5-11](Roads/TurnTrigger.cs)). That is the most direct in-code evidence that a moving-frame world was already tried and rejected.

---

## 1. What "open world" would concretely mean here

"Open world" is not one decision. It is three *independent* axes, and the cost rises sharply with each one you add:

| Axis | The runner today | "Open world" version | Independent? |
|---|---|---|---|
| **A. Steering** | One lateral axis, ±4.2 m, clamped to lane width (`PlayerController` + `RoadSideConfig`) | Free heading — the player chooses a direction and drives along it | Can be added without B or C (e.g. a wider drivable plane, still scrolling) |
| **B. Translation** | World scrolls past a fixed player (`WorldSpeed` + `RoadSequencer.RenderChain`) | The player *actually moves through* a persistent space; the camera follows | The expensive one — inverts the spine |
| **C. Persistence / extent** | Endless, procedurally streamed, **no memory** — tiles return to the pool behind you (`DespawnBehind`, [RoadSequencer.cs:237](Roads/RoadSequencer.cs)) | A finite (or very large) **authored map you can revisit**; places stay where they are | Can be partial (large but bounded) |

- **Free steering only (A)** is the *cheapest* reading and is barely "open world" — it is a wider runner. It still breaks the lateral-offset-only structure but keeps the scroll.
- **True movement through space (B)** is what people usually mean. It inverts fact #2 above: speed stops being world-scroll and becomes the player's own velocity; the player's transform must carry a real position; the camera must follow.
- **A persistent finite map (C)** is the "world" in open world: you can drive somewhere, leave, and come back to find it unchanged. The current system is the opposite by design — it has *no persistence at all* (pooled tiles, 160 m ahead / 35 m behind, [RoadSequencer.cs:31-32](Roads/RoadSequencer.cs)).

**The full "open world" most people picture = A + B + C**: free heading, real translation, and a persistent explorable map. The minimum that still earns the name is **B + (A or C)**. This note's cost estimates assume the full version unless stated; a "free steering only" (A) variant is called out where it is cheaper.

---

## 2. Every architecture rule it breaks, and which scripts each change touches

The README lists six enforced architecture rules. Open world (A+B+C) breaks or fundamentally re-purposes **five of the six**, plus several systems that are not "rules" but are built on the same assumptions.

### 2.1 The six enforced rules

| Architecture rule (README §Architecture rules) | Broken by | Why it breaks | Scripts touched |
|---|---|---|---|
| **Player never moves along the travel axis** (structural) | A, B | `ComposePosition()` has no longitudinal term by design. Translation requires the player's position to become a real 2D point and the Rigidbody to carry velocity instead of being kinematic-slid. | `Player/PlayerController.cs` (core rewrite), `Config/ScooterConfig.cs` (forward-speed tuning moves onto the player), `Player/ScooterLean.cs`, `Player/ScooterWobble.cs` |
| **`RoadDirection` is the single axis source; the frame is a constant +Z/+X** | A, B | `Current`/`SteerAxis` are constants and `Longitudinal`/`Lateral` are raw `.z`/`.x`. With a free heading these projections are wrong — the frame must again *follow the player*, which is the moving compass the rebuild removed. | `Core/RoadDirection.cs` (revert to a dynamic frame), and **everything that projects onto it**: `Traffic/TrafficSpawner.cs`, `Traffic/OvertakeDetector.cs`, `Player/WrongLaneDetector.cs`, `Player/NearMissDetector.cs`, `Player/SpeedMonitor.cs`, `Hazards/HazardSpawner.cs`, `Player/PlayerController.cs` |
| **One camera writer, in LateUpdate** | B | The writer stays single, but its *job inverts*: today it rotates a rig that sits at the origin and never translates (`transform.rotation = ...` only, [CameraRigController.cs:75](Cameras/CameraRigController.cs)). It must become a third-person follow camera that tracks a moving player and handles occlusion. The "yaw follows constant +Z" logic ([CameraRigController.cs:103-107](Cameras/CameraRigController.cs)) becomes "follow the player's heading," and the speed-FOV cue reads from a different speed source. | `Cameras/CameraRigController.cs` (substantial rewrite), `Cameras/RealRiderMode.cs`, `Cameras/CameraShake.cs` |
| **Object pools only / bounded draw distance** | B, C | Pools of ~4 instances per tile over a 160 m horizon ([RoadSequencer.cs:31-33](Roads/RoadSequencer.cs)) cannot hold a persistent map. A real world needs spatial streaming (chunk load/unload, LOD) or a large static scene — a new subsystem. | `Roads/RoadSequencer.cs`, `Core/ObjectPool.cs` (no longer the world model), **new** streaming/scene-management system |
| **ScriptableObjects for all config** | (preserved) | This one survives — configs stay SOs. New tunables (follow-cam, movement) are new SO fields. | `Config/*` (additive only) |
| **New Input System only** | (preserved) | Survives, but the *mapping* changes: today input is one lateral axis + gas/brake ([ScooterInputRouter.cs:62-73](Controls/ScooterInputRouter.cs)). Free heading needs a 2D steer (a turn/heading axis), which is hard to express through `GyroTiltProvider`'s single `[-1,1]` tilt. | `Controls/ScooterInputRouter.cs`, `Controls/GyroTiltProvider.cs`, `Controls/TouchZoneProvider.cs`, `Controls/DesktopProvider.cs` |

### 2.2 Systems that are not "rules" but assume the on-rails spine

- **World scroll (`WorldSpeed`).** Inverts entirely under B: `Current` stops being world-scroll and becomes player velocity. Every consumer that scrolls by `-Current` or reads `SpeedRatio` must change: `RoadSequencer` (`playerArc += Current·dt`, [RoadSequencer.cs:107](Roads/RoadSequencer.cs)), `Hazards/HazardSpawner.cs`, `Traffic/TrafficVehicle.cs` (`.Tick`), `FX/SpeedLines.cs`, `Audio/AudioManager.cs` (engine layers), `Cameras/CameraRigController.cs` (FOV). The checkpoint braking override ([WorldSpeed.cs:90-98](Core/WorldSpeed.cs)) also assumes the world owns speed.
- **The whole road model (`RoadSequencer` + `RoadTile`).** The arc-length "road space re-rendered relative to the player" model ([RoadSequencer.cs:170-198](Roads/RoadSequencer.cs)) is the single biggest rewrite. Under B the tiles must be placed at *fixed world positions* and the player must move through them. `RoadTile`'s road-space identity (`RoadPosition`, `RoadRotation`, `StartArc`, [RoadTile.cs:33-39](Roads/RoadTile.cs)) becomes world identity. The weighted tag grammar (`RoadSequence`, sequences, cooldowns, unlock gates) is built for *linear* zone-after-zone authoring (M23/M24) and does not describe a 2D map.
- **Spawners assume a straight +Z spawn line.** `TrafficSpawner.SpawnSingle()` places vehicles at `RoadDirection.Current * longitudinal + RoadDirection.SteerAxis * lateral` ([TrafficSpawner.cs:163](Traffic/TrafficSpawner.cs)), and *pauses spawning through curves* precisely because that straight line leaves a curved road ([TrafficSpawner.cs:82-93](Traffic/TrafficSpawner.cs), README "Known limitations"). In an open world traffic must become free agents on a road network (pathing/AI), not a 1-D queue. Same for `HazardSpawner`.
- **Detectors are 1-D dot products.** `OvertakeDetector` is "pure dot-product comparison along `RoadDirection.Current`" ([OvertakeDetector.cs:7-14, 27-38](Traffic/OvertakeDetector.cs)); `WrongLaneDetector` projects onto `SteerAxis` vs the two-lane `RoadSideConfig`. "Overtake" and "wrong lane" only have meaning on a two-lane rail; in a free world they need redefinition or removal.
- **Rewind (`RewindSystem` + `IRewindable`).** Today the player's rewind state is *one number* — `LateralOffset`/`playerArc` ([PlayerController.cs:124-139](Player/PlayerController.cs), [RoadSequencer.cs:449-457](Roads/RoadSequencer.cs)) — because the constant frame means "there is no compass to restore — just this one number" ([RoadSequencer.cs:80-86](Roads/RoadSequencer.cs)). Free movement makes the rewind state a full 2D transform + heading + world streaming state. The `IsTurning` collision-grace ([RoadDirection.cs:29-34](Core/RoadDirection.cs)) also loses meaning.
- **Session loop (`TimerManager`, `CheckpointController`, `GroupScoreManager`).** The 120 s timed relay, the checkpoint that brakes the *world* to a stop on a special tile ([RoadSequencer.SpawnCheckpoint](Roads/RoadSequencer.cs), [CheckpointController](Session/CheckpointController.cs)), and add-only group scoring are all framed around a bounded, forward-only ride. Open-ended exploration removes the natural session boundary they depend on.

**Net:** the change radius is the entire `Core/` + `Player/` + `Roads/` + `Cameras/` spine, every `Traffic/` and `Hazards/` spawner, every detector in `Player/` and `Traffic/`, the `SafetyNet/` rewind, and the `Session/` loop. That is the majority of the 73-script codebase, and it touches every system the README lists as "stays stable *because* the frame is constant."

---

## 3. Two delivery options

### Option A — A separate free-roam **MODE** alongside the runner

A second scene/mode with its own movement controller, its own follow camera, and its own (finite, authored) map, **reusing assets but not the runner's spine.**

- **What you reuse:** art (tile meshes, vehicle and scooter models), audio, the `Config/*` ScriptableObjects, the UI shell, and the input *providers* (gyro/touch/desktop) — though re-mapped to 2D steering.
- **What you build new:** a free-movement `PlayerController` (real velocity, heading), a third-person follow camera, a hand-authored or chunk-streamed map, free-agent traffic AI (the 1-D queue/detectors do not transfer), and any goal/objective layer to give the space a point.
- **What you must accept losing:** the constant-frame detectors (overtake/wrong-lane/near-miss) and the timed-relay session loop do not carry over — a free-roam mode needs its own scoring/objective design.

| | Estimate |
|---|---|
| **Effort** | **High** — it is effectively a second small game, but a *contained* one. Bulk is the movement controller, follow camera, traffic AI, and map authoring. |
| **Risk to the shipping runner** | **Low** — fully isolated; the runner's spine is untouched. |
| **Risk to schedule/scope** | **High** — a second product to design, build, content-author, and test; the map and objectives are ongoing content cost the runner gets "for free" from procedural streaming. |
| **Reversibility** | **High** — can be cut without touching the shipping game. |

### Option B — **REPLACE** the runner with an open world

Rewrite the spine in place: `RoadDirection` → dynamic frame, `PlayerController` → 2D translation, `WorldSpeed` → player velocity, `RoadSequencer` → streamed persistent world, `CameraRigController` → follow camera — then revisit every spawner and detector that projects onto the axes (§2).

| | Estimate |
|---|---|
| **Effort** | **Very high** — touches the whole `Core`/`Player`/`Roads`/`Cameras`/`Traffic` spine plus every detector and the session loop. This *is* the open-world build that was already explored and dropped (§4). |
| **Risk to the shipping runner** | **Maximal** — there is no runner left; you bet the project. |
| **Technical risk** | **Very high** — reverting to a moving frame re-introduces precisely the cross-turn desync (traffic, detectors, rewind) the 2026-06-17 rebuild eliminated ([RoadDirection.cs:5-12](Core/RoadDirection.cs), [TurnTrigger.cs:5-11](Roads/TurnTrigger.cs)). You also lose the timed-relay group loop the exhibition is built around. |
| **Reversibility** | **Low** — a one-way door once the spine is gone. |

---

## 4. Why it was likely dropped after Sprint 4 — and what would have to be true to bring it back

> [!note] Sourcing
> The drop is given as project context (open world "was explored and DROPPED after Sprint 4 user testing"). The *reasons below* are reconstructed from what the code and the exhibition brief make true — each is grounded, not invented.

### Why it was almost certainly dropped

1. **The exhibition format needs a bounded, repeatable, ~2-minute hand-off loop — open world removes the boundary.** The shipping loop is a 120 s timer (`TimerManager`, M26), a checkpoint hand-off, and add-only **group/relay** scoring (`GroupScoreManager`, M27, [CheckpointController](Session/CheckpointController.cs)). That is purpose-built for a kiosk where students take turns. An open world is open-ended by definition; it does not bound a session or create the relay hand-off that makes group play work at a stand.
2. **Walk-up legibility for ages 12-18.** The runner is instantly readable: tilt to steer, right side gas / left side brake ([ScooterInputRouter](Controls/ScooterInputRouter.cs), M4), dodge what's coming — near-zero onboarding. Free roam needs navigation, goals, and sustained attention, and risks players wandering with nothing to do — poor fit for a short, high-turnover exhibition slot.
3. **The control scheme doesn't transfer cleanly.** `GyroTiltProvider` yields a single `[-1,1]` tilt mapped to one lateral axis. Two-axis heading control via tablet tilt is much harder to calibrate and control (and calibration re-zeros every session, M8) — the accessibility win of "tilt = steer" weakens the moment you need free heading.
4. **It was a known instability source.** The codebase carries direct scar tissue: `TurnTrigger` is a dead stub documenting that the moving-compass turn was abandoned, and `RoadDirection`'s header explains the constant frame exists so traffic/detectors/rewind "stay stable across turns" with "no moving compass to desync." A Sprint-4 open-world experiment would have run straight into that class of bug.
5. **Content cost.** Procedural streaming gives endless varied road for free (the `RoadSequence` grammar, M23/M24). A persistent world has to be *authored and populated with goals* — a large, ongoing content bill the team did not have.

The most likely user-testing read: free roam did **not** raise engagement enough to justify itself, sessions ran long and unfocused (hurting kiosk throughput), control felt worse on tablet, and the content/stability cost was disproportionate to the value.

### What would have to be true for it to earn its place back

Open world only re-earns the spine rewrite if **all** of these hold — ideally evidenced at the next user test, not asserted:

- **Demonstrated player value over the runner.** A measurable engagement or *learning* win — e.g. exploring real Kenyan locations and meeting "impact makers" teaches the educational content better than dodging traffic does. The "impact makers" theme is genuinely a point in open world's favour *if* exploration is what conveys it — but that has to be shown, not assumed.
- **Preserved exhibition fit.** It must still bound a kiosk session (a timer, a task list, or a guided route), support quick hand-off / group play, and keep onboarding near-zero for a 12-18 walk-up audience.
- **Throughput intact.** High turnover is non-negotiable at a stand; a "turn" cannot become a 10-minute wander while a queue waits.
- **A control scheme that works on tablet tilt** for 2D navigation — or a deliberately constrained movement model (e.g. snap-to-road) that keeps tilt-steering meaningful.
- **A content budget** for authoring and populating a persistent map with goals.

If those cannot be evidenced, the runner remains the better-fit product on every axis the exhibition cares about.

---

## 5. Recommendation

> [!success] Recommendation
> **1. Do not pursue Option B (replace the runner).** The cost is the whole spine, the risk is the entire project, and it deliberately undoes the 2026-06-17 stability rebuild. It is the path that was already explored and dropped; nothing in the code has changed to make it cheaper or safer since.
>
> **2. Before touching open world at all, prototype the cheap middle path: "branching rails."** Most of the *felt* value of open world for this audience is **agency** — the sense of choosing where to go — not a simulated persistent world. The existing architecture can deliver a taste of agency for a fraction of the cost: let the player pick a path at a fork/junction, implemented as an extension of the `RoadSequence` grammar (a junction tile that offers two next-sequence branches based on the player's lateral position at the fork). This keeps the constant frame, the scroll model, the detectors, the rewind, and the timed loop **completely intact** — it is additive content authoring plus a small `RoadSequencer` branch-selection hook, not a spine rewrite. (`TurnTrigger` is already a free prefab slot, and curve tiles already bend the road; a junction is the natural next authoring primitive.) Prototype this first and user-test whether "choosing the path" delivers the agency people are really asking for.
>
> **3. Only if branching rails is shown to be insufficient *and* the §4 "earn it back" criteria are evidenced, build Option A (a separate free-roam mode) as a scoped experiment** with explicit, pre-registered success metrics — engagement, learning transfer, and kiosk throughput — measured against the runner at the next user test. Keep it isolated so it can be cut without risk. Do not greenlight a spine replacement off the back of it without the runner continuing to ship in parallel.

**In one line:** the runner already ships, already fits the exhibition, and its constant-frame spine was hard-won — protect it; buy the *feeling* of openness cheaply with branching rails before paying for the real thing.

---

## Connections
- [[README — Architecture and System Map]] — the system map these claims are grounded in
- [[Design/Requirements — Kenya Scooter Game v2]] — Req §2 (architecture rules), §3 (input), §9 (checkpoint/relay), §17 (performance)
- [[MDA — Mechanics Dynamics Aesthetics]] — M1 (world scroll), M3 (road turn), M5 (lateral steering), M23/M24 (sequencing, Journey Arc), M26/M27 (timer, relay)
- [[D17 — MDA-Driven Clean Code Generation]] — the devlog recording the 2026-06-17 constant-frame turn rebuild and the lateral-offset-only player decision
