# Project Orientation — for Fontys Game-Design Students

You've inherited (or are evaluating) *Impact Makers Around The World: Kenya* — internal name **Kenya Scooter**, activity name **Drive Through Kenya**. This document gets a semester-4 game-design student productive in an afternoon: what the project is, the one mental model that makes the codebase legible, where things live, how to do the common jobs, and — bluntly — what is and isn't finished. It **links into** the numbered developer guides rather than repeating them.

> **Read this once, then work from three sources of truth:** `Scripts/README — Architecture and System Map.md` (the script-level map, kept current), the numbered `HANDOVER/12–18` task-cards (how-to for each job), and the code itself. This orientation is the on-ramp, not the reference.

---

## 1. The project in 60 seconds

- **What:** a ~2-minute educational tablet game. You ride a scooter down a Kenyan highway, tilt to steer, right-screen = gas, left-screen = brake, overtake slow vehicles, avoid oncoming traffic and road hazards. Played as a **class relay** (each student's turn adds to a shared group total); there is also a solo **endless mode** ("Eindeloze modus") with lives.
- **Engine:** **Unity 6000.3.10f1** (Unity 6), **URP 17.3**. Android-first (Samsung tablet, min API 26, **IL2CPP + ARM64**), iOS secondary. Runs in the Editor with **A/D** to steer (gyro tilt only works on device).
- **Language/structure:** C#, one big `Assembly-CSharp` (no asmdefs outside the test folder), namespace root `KenyaScooter.*`, ~189 scripts.
- **Client:** De Ontdekfabriek — a maker-education institution with **no technical staff**. That constraint shapes everything: the whole facilitator-facing surface is designed to be operated and re-tuned with **zero Unity knowledge** (see §7).
- **Status:** a clean, well-architected, **playable prototype** — not a shipped product. See §8 for the honest debt list; don't skip it.

---

## 2. The one mental model: the world moves, the player stays

This is the single idea that makes the whole codebase click. **The player never translates forward.** The player Rigidbody is frozen on the travel axis; instead the **road, traffic and hazards scroll toward the player**, and on a turn the **whole world rotates around the stationary player** — that rotation *is* the turn.

Consequences you must respect:
- **Speed** is owned by one object: `Core/WorldSpeed` (`WorldSpeed.Instance`, `.Current` in m/s, `ApplyThrottle(gas, brake, dt)`, `.DistanceTravelled`). Everything reads scroll speed from here.
- **Direction/axis** is a single static frame: `Core/RoadDirection` (constant +Z forward / +X lateral). All spatial math goes through `RoadDirection.Longitudinal` / `.Lateral` — **never** raw `transform.position.z/.x`.
- Traffic and hazards ride the curve in **road-space coordinates** (arc + lateral), re-projected to world each frame via `RoadSequencer.TryGetRoadPose`, so they stay glued to the road through bends.

Once this clicks, the folder layout below reads itself.

---

## 3. Architecture at a glance

Scripts live under `…/Scripts/` (live project) and mirror to the vault (see §9). Folder → responsibility:

| Folder | What's in it |
| --- | --- |
| `Core` | `WorldSpeed`, `RoadDirection`, `GameManager` (state machine + session toggles + `GameMode`), `GameEvents` (the pub/sub hub), `ObjectPool<T>`, `GameEnums` |
| `Roads` | `RoadTile` (a tile fully defines itself), `RoadSequencer` (streams tiles), `CurvedRoadMesh` (builds the road surface along the driven line), `RoadSequence` assets |
| `Traffic` | `TrafficVehicle`, spawner/engine, `TrafficVehicleAutoLights`, behaviour profiles |
| `Player` | `PlayerController`, `PlayerCollisionHandler`, lean/wobble, `SpeedMonitor` |
| `Hazards` | `Hazard`, `HazardSpawner`, `HazardSpawnConfig` (data-driven — no per-hazard script) |
| `Scoring` | `ScoreManager`, `GroupScoreManager`, `LeaderboardManager`, `ScoreConfig` |
| `Session` | `TimerManager`, `SpeedZoneManager`, `DayCycleManager` |
| `SafetyNet` | `RewindSystem` + `IRewindable`, `GraceSystem`, `RewindVisuals` |
| `Settings` | the whole facilitator layer: `SettingsCatalog(.Definitions)`, `GameSettings`, `SettingsMenu(.Build)`, `SettingsPreset`, `CustomPresets`, `FacilitatorLock`, `FacilitatorGate`, `ConfigLocator` |
| `UI` | `DiegeticHud` (the in-world dashboard), `KenyaMenuScreens` (title/team/relay/gameover), `AttractMode`, `HUDController` |
| `FX` / `Sky` / `Cameras` / `Environment` / `Feedback` / `Audio` / `Controls` / `Config` | juice, sky/horizon, the single camera writer, ambient life, haptics, the (currently silent) audio layer, input providers, and the ScriptableObject config assets |

**Load-bearing systems:**
- **Data-driven config.** Every tunable number lives in a **ScriptableObject** under `Config/` (`ScooterConfig`, `ScoreConfig`, `SessionConfig`, `TrafficConfig`, `HazardSpawnConfig`, `DayCycleConfig`, …). `Settings/ConfigLocator` finds the single loaded instance of each at runtime (no scene wiring). **You change gameplay by editing assets, not code.**
- **Facilitator override store.** `SettingsCatalog` is a declarative list of what a facilitator may change; `GameSettings` persists overrides in `PlayerPrefs` (`ksg.*` keys) and pushes them onto the live configs at boot — so the menu re-tunes the game **without touching shipped assets**. Full walkthrough: `HANDOVER/18`.
- **Events.** `Core/GameEvents` is a single static hub (`SessionReset`, `StateChanged`, `CheckpointReached`, `LivesChanged`, `MenuScreenChanged`, …). Each event has one conceptual raiser; the README carries the raiser→consumer catalogue.
- **Pooling.** `Core/ObjectPool<T>` prewarms the whole pool up front; `Get()` returns an inactive instance. **Nothing is `Instantiate`d during play** — only at Awake/Start.
- **Manager-tick.** Vehicles/tiles/hazards/props have **no per-object `Update`**; their owning manager runs one loop over a static active-list. Hot paths are allocation-free.
- **Two modes.** `GameMode { GroupRelay, Endless }` on `GameManager`; only `GroupRelay` commits to the leaderboard; `Endless` is gated behind the facilitator PIN (`EndlessSelected`, PlayerPrefs `ksg.endlessMode`) so a child can't reach it.

**Invariants you must not break** (also in the README): world-moves-player; `CameraRigController` is the **only** camera writer and runs in `LateUpdate`; object pools only (no `Instantiate`/`Destroy` in play); axis math via `RoadDirection`; config in ScriptableObjects; New Input System.

---

## 4. Get productive in an afternoon

1. **Open the project.** Follow `HANDOVER/12 — Open the Project and Work in the Scenes`: install Unity **6000.3.10f1**, open the project, and load the **only** shippable scene — `Assets/Scenes/Impact Makers Around The World Kenya.unity`. Ignore the many prototype scenes lying around.
2. **Play in the Editor.** Steer with **A/D** (gyro is device-only). Press **F8** (or hold the top-left corner 2 s) to open the facilitator menu; default PIN **`3683`**.
3. **Read the map, not every script.** Skim `Scripts/README — Architecture and System Map.md` for the mechanic→script table (M1–M27) and the event catalogue. For the "why it's designed this way" narrative, the vault-root **`GAME GUIDE — Features, Design & Code.md`** covers each feature in four beats (experience / how it works / in the code / why).
4. **Make a trivial change** to prove your loop works: edit a number on `ScooterConfig` (e.g. `maxSpeed`) and re-enter play. Note that field defaults in a *script* don't change an existing *asset* — edit the asset in the Inspector.

---

## 5. The common jobs (follow the task-cards)

These are all documented — don't reinvent them:

| Job | Guide | One-liner |
| --- | --- | --- |
| Author a **road tile** / turn | `HANDOVER/13` | Everything is on `RoadTile` — length, allowed hazards, and a green-ball/red-ball **Turns** list (metre marks + degrees). `CurvedRoadMesh` builds the surface along the driven line. |
| Author a **hazard** | `HANDOVER/14` | Fully data-driven: a prefab + `Hazard` component + one `HazardSpawnConfig` asset. **No new script.** |
| **Tune** a mechanic | `HANDOVER/15` | Which config governs which mechanic (traffic, day/night, scoring, safety net, endless lives). |
| Add/remove/change a **facilitator setting** | `HANDOVER/18` | One `SettingDefinition` in `SettingsCatalog.Definitions.cs`; one `SettingsPreset` for a profile. **Mind the retired-key gotcha** when removing. |
| **Build & ship** the APK | `HANDOVER/16` (routine) + `HANDOVER/01` (full recipe + keystore) | The update loop and the one-time build-cliff work. |
| What to finish first | `HANDOVER/17` | Keep / change-or-finish / keep-in-mind triage. |

**The extension pattern is always the same:** new traffic behaviour = a `TrafficBehaviourProfile` asset; new hazard = a `HazardSpawnConfig` asset; new road = a `RoadSequence` asset (with `unlockAtTime` gates); new facilitator knob = a catalog entry. Structural content (new location/art) is bigger — the `Design/Location Pack — Authoring Guide and Asset Contracts.md` spells out the asset contracts for a "swap the location" effort (note: that swap is **designed but never demonstrated end-to-end** — see §8).

---

## 6. Gotchas cheat-sheet

- **Tests don't run in Unity.** The test suite lives in `…/Tests~/Editor/`. The trailing **`~` makes Unity ignore the whole folder** — so the RoadTile/Settings EditMode tests never compile in the editor. Rename `Tests~` → `Tests` to run them, then rename back. **Never claim "tests pass" without doing this.**
- **The vault is a mirror, and it has drifted.** The live Unity project is at `…/Assets/Impact Makers Around The world/Scripts/`. The vault `Code/KenyaScooter (current)/` is a **source-only mirror**. Several files are **newer in the mirror and were never copied to the live project** (`RealRiderMode`, `ScooterInputRouter`, `TurnTelegraph`, some Traffic scripts). Always `git diff --no-index` live-vs-mirror before trusting or copying either side. Edit the **live** project; the mirror is documentation.
- **`ScooterConfig` and other `.asset` defaults win over `.cs` field initialisers.** Changing a default in a config *script* does not change the shipped *asset*. Edit the asset in the Inspector.
- **New Input System caveat.** `Active Input Handling = Both`. `KioskLock`'s back-button suppression only compiles under `ENABLE_LEGACY_INPUT_MANAGER` — don't switch the project to New-Input-only without handling that line.
- **The old `OvertakeGame` code is dead.** Anything under the `OvertakeGame` namespace / `OvertakeGame_v2` folder is superseded; the current code was regenerated from the MDA/requirements specs after 11 June 2026. Don't work from it.
- **The 2026-07-04/05 road rebuild deleted ~19 turn/junction scripts** (`TileTurn`, `RoadJunction`, `TurnScheduler`, …). Turns are now point-based on `RoadTile`. Don't resurrect the old stack.
- **Two sharp turns close together can visually overlap** — the old auto-flatten guard was intentionally removed; it's now the tile author's responsibility.

---

## 7. Why it's built this "boring, safe" way (the client constraint)

De Ontdekfabriek runs the exhibit but has nobody who uses Unity. So the architecture optimises for **operability without a developer**, not for engine flexibility. That's why: config is data not code; the facilitator menu can re-tune everything at runtime and persist it without rebuilding; presets bundle whole scenarios into one tap; kiosk/attract/gate helpers self-bootstrap via `[RuntimeInitializeOnLoadMethod]` so there's nothing to wire in a scene. When you extend the project, **preserve that boundary**: if a change would be useful to a facilitator, expose it through `SettingsCatalog` rather than leaving it a code constant.

---

## 8. Honest status — the debt is operational, not structural

The code is genuinely clean (a June 2026 audit found no rewrite needed). What's unfinished is everything *around* the code:

- **Never compiled or device-tested from the authoring environment.** The README asserts it compiles, citing a 25 June client showing — but that predates most of the June/July work (turn rebuild, day/night, dirt roads, endless mode, the settings overhaul). Treat "compiles" as **unverified at the current head** until you build it.
- **Silent.** `AudioManager` is wired and null-safe but almost no clips are imported. Expect no sound.
- **On-device unverified.** Gyro sign (left/right), `KioskLock` back-button, `OrientationLock`, charging detection — all need a real Samsung tablet.
- **Build cliff.** No signing **keystore** exists, the **app id** is still the URP-template default, orientation isn't pinned to landscape. **No shippable signed APK exists yet.** These are one-time Unity actions (`HANDOVER/01`).
- **Testing is thin.** The only real on-device play tests are a 15 Jun N=2 session and a 23 Jun N=1 (age 10). The **relay itself was never tested**, and the career-impact claim rests on the debrief, which hasn't been validated with a real group. Difficulty reads **easy** for the 12–18 target. Don't cite testing beyond what's in the user-test docs.
- **"Modularity / second location" is a claim, not a demonstration.** No finished non-Kenya location has been built; the swap-with-no-code promise is unproven end-to-end.
- **Doc drift to ignore:** older prose (the Portfolio `Handover Document.md §6`, the front-door start pages) still describes **seven** profiles and removed settings (pedestrians, roadside-life density, region-journey). The **current** truth (in `HANDOVER/11` and the live catalog) is the Profielen page's two axes: a **MODE** (`preset.standard`, `preset.endless`) and a **DIFFICULTY** (`preset.rustig`, `preset.normal`, `preset.challenge`), tracked independently. Trust the code and `HANDOVER/11`.

None of this is a reason not to take the project on — it's a punch-list. `HANDOVER/17` and `HANDOVER/08` (risk analysis) rank it.

---

## 9. Where the source of truth lives

- **Script-level, always-current:** `…/Scripts/README — Architecture and System Map.md` (mechanic→script, events, rules) and `…/Scripts/Unity Setup Guide — Scene Wiring.md`.
- **Whole-game narrative deep-dive:** vault-root `GAME GUIDE — Features, Design & Code.md`.
- **Design intent:** `Portfolio/Portfolio Overview/Game Design Document.md` and `MDA — Mechanics Dynamics Aesthetics.md`.
- **Task-level how-to:** `HANDOVER/12–18`.
- **Orientation (long, but §6 stale on settings):** `Portfolio/Portfolio Overview/Handover Document.md`.
- **Everything, indexed:** `_ALL DOCS — quick access.md`.

When two docs disagree, prefer, in order: **the code** → `Scripts/README` → `HANDOVER/` numbered cards → older prose.

---

## Related

- [[HANDOVER — Start Here]] — the role-routed front door to the whole handover.
- [[OVER HET PROJECT — Voor De Ontdekfabriek]] — the non-technical companion to this document (Dutch), for the institution that runs it.
- [[GAME GUIDE — Features, Design & Code]] — the whole-game deep dive.
- [[Handover Document]] — the long technical orientation (architecture rules, location-change how-to, ADRs).
- [[RECOMMENDATIONS — What I would Work On With More Time]] · [[Handover Risk Analysis — Running the Game Without Unity]] — the honest punch-list and risk sweep.
