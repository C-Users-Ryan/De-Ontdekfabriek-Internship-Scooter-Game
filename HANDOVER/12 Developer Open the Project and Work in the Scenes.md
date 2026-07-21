# GUIDE: Dev — Open the Project and Work in the Scenes

**For:** a developer sitting down at the Kenya Scooter project (a future intern or school group) for the first time.
**What it is:** the one-page orientation task — install the right Unity, open the project, open the *correct* scene (not one of the traps), and know where everything is before you touch code. It is deliberately thin: each topic that already has a deep doc gets a link, not a re-run.

> [!info] Read this alongside, not instead of
> This card gets you *in the door and oriented*. Two existing docs go deeper and you will need both: [[Unity Setup Guide — Scene Wiring]] (in the code repo) for full scene wiring and ScriptableObject creation, and `README — Architecture and System Map` (also in the repo) for the folder→namespace map and the MDA→script traceability. For building and signing the APK, use [[Handover — Build, Keystore and APK Rebuild Checklist]].

---

## 1. Install Unity + the Android module

Use the version the build checklist pins — **do not guess a patch**:

- **Unity `6000.3.10f1`** (revision e35f0c77bd8e). Install via **Unity Hub ▸ Installs ▸ Install Editor ▸ Archive** and match the version exactly. The pipeline is **URP 17.3.0** and input is the **Input System** (Active Input Handling = *Both*) — both come from the project, you do not add them.
- During install (or later via Hub ▸ the version's gear ▸ *Add Modules*) add **Android Build Support** with **both** sub-options ticked: **Android SDK & NDK Tools** and **OpenJDK**. IL2CPP needs the NDK — this is not optional for this project.

> [!warning] Version drift to be aware of
> The repo's own [[Unity Setup Guide — Scene Wiring]] §1 still says "Unity 2022.3 LTS (or Unity 6)" — that is the generic minimum from when the scripts were first generated. The **authoritative** version for *this* project is `6000.3.10f1`, read from `ProjectSettings/ProjectVersion.txt` and recorded in the [[Handover — Build, Keystore and APK Rebuild Checklist|build checklist]]. When they disagree, the build checklist wins.

Full version/package table (min SDK, IL2CPP, ARM64, keystore state): [[Handover — Build, Keystore and APK Rebuild Checklist]] "verified project facts".

## 2. Open the project

Point Unity Hub at the live project folder — **not** the vault mirror. The mirror is source-only `.cs` files (`Code/KenyaScooter (current)/`, namespace `KenyaScooter.*`); the openable Unity project lives at the GitHub repo path, with the scripts under `Assets/Impact Makers Around The world/Scripts/`. First open is slow (Unity reimports every asset and compiles) — wait for an empty Console before doing anything.

> [!note] Mirror vs live — the one gotcha that bites everyone
> Code you read in the vault mirror is **source-only**. Any change made in the mirror (including everything shipped this session) is **not compiled** until it is copied into the live project and Unity recompiles. Before copying in either direction, `git diff --no-index` live vs mirror per file — a fix can exist on only one side. (README "Known limitations", last two items.)

## 3. The ONE playable scene (and the traps)

Open exactly one scene and never ship any other:

> **`Assets/Scenes/Impact Makers Around The World Kenya.unity`**

It is the only scene enabled in **File ▸ Build Settings ▸ Scenes In Build**. Everything else in the project is an old prototype and will waste your afternoon:

- `Impact Makers Around The World Kenya turning.unity` — the nastiest trap, one word different from the real scene.
- `Overtake*.unity`, `endlessRunner.unity`, `EndlessV2.unity`, `turn test.unity`, `Tiles.unity`, and the `_Recovery/` crash-recovery folder.

If *Scenes In Build* is ever empty, open the real scene and click **Add Open Scenes**. Leave the prototype scenes where they are for now, but never build one. (Full note: [[Handover — Build, Keystore and APK Rebuild Checklist]] Part C step 5.)

## 4. Why the scene looks almost empty (self-bootstrapping systems)

Do not expect a hierarchy with one GameObject per feature. Roughly 50 systems **self-boot** via `[RuntimeInitializeOnLoadMethod]` — they create themselves at runtime, so the saved scene stays lean. The headlight, sky, dust/haze, wind, roadside props, attract mode, traffic auto-lights, UI sound, and the endless-mode HUD all appear only in Play mode. A handful of statics (`GameEvents`, `RoadDirection`, `SwahiliUI`) also reset through the same hook so entering Play with domain reload disabled stays clean.

What you *will* find hand-wired in the scene (and must wire if you rebuild it): the `— Systems —` root (`GameManager` and its serialized `TimerManager`/`RewindSystem`/`CheckpointController`/etc.), the `Player`, the `CameraRig`, the day-cycle Volumes, and the UI Canvas with its `EventSystem` on the **InputSystemUIInputModule**. `GameManager` is the single game-state writer and session orchestrator — read `Core/GameManager.cs` first; it also holds the new endless-mode branch (`Mode`, `LivesRemaining`, `SessionConfig.endlessLives`, default 3).

Full hierarchy, prefab contents and the ScriptableObjects to create: [[Unity Setup Guide — Scene Wiring]] §2–§5. Folder→namespace map and MDA-mechanic→script table: `README — Architecture and System Map`.

## 5. The `Tools ▸ Kenya Scooter` menu map

Editor helpers are grouped into task submenus (the flat ~30-item list was regrouped 2026-07-04), with the **Road Tools (panel)** pinned first:

| Submenu | For |
|---|---|
| **Road Tiles and Seams** | create a straight tile / scrolling ground / generated road strip; fix tile-to-art clipping and seam markers |
| **Roadside Props** | create prop config / placeholder / spawner; "Build Kenya Roadside Starter" (12 primitive props) |
| **Weather and FX** | apply the day palette, create the dust/haze + warm-grade volumes, add dust trails/wakes |
| **UI Builders** | build/rebuild the HUD cluster, menu screens, settings menu |
| **Game Setup** | wire scoring managers, build a placeholder traffic vehicle, assign audio clips |
| **Facilitator** | reset facilitator settings / reset the access code to default |

Note: the old **Turns and Junctions** submenu and every turn tool were **deleted** in the 2026-07-04 clean-slate rebuild — turns are now authored directly on a tile's *Turns* list in the Inspector, no tool needed ([[Turn System v4 — The Clean-Slate Rebuild]]). For road authoring end-to-end see [[Road System — Step by Step Guide]].

## 6. PC test controls

You can drive and test the whole loop on a laptop with no tablet:

| Input | Action |
|---|---|
| `A`/`D` or `←`/`→` | steer (ramps like tilt) |
| `W`/`↑` · `S`/`↓` | gas · brake |
| Mouse — hold right / left half | gas / brake (mirrors the tablet touch zones) |
| Gamepad left stick + triggers | steer + brake/gas |
| `R` | toggle Real Rider Mode (editor/standalone only) |
| `Tab` | facilitator debug panel |
| Any key / tap | start the session from the Ready overlay |

New this session (mirror + live, needs a Unity recompile): a **VRIJ RIJDEN · ENDLESS** button on the Title starts a solo run with lives instead of the timer (no team select, no relay, nothing committed to the leaderboard; lives + distance in a small `EndlessHud` overlay), and an **EIGEN NAAM** card on team-select opens an on-screen keyboard for a typed team name. First-run verification steps: [[Unity Setup Guide — Scene Wiring]] §8.

> [!warning] What is NOT verified on a device
> Nothing here is device-tested. The gyro sign (`GyroTiltProvider.RollForOrientation`), the kiosk back-button lock (`KioskLock`) and the charging-state handheld detector all still need a real tablet. Audio runs **silent** — `AudioManager` is wired but no clips are imported ([[Kenya Soundscape — Audio Clip Spec]]). The car tail-light change needs a recompile and a dusk drive to confirm. EditMode tests sit in `Tests~/Editor/` — the trailing `~` makes Unity ignore them, so never claim "tests pass".

---

> [!abstract] Evidence (for the portfolio)
> **What it is:** the newcomer's front door to the *code* side of the project — the missing first step between "here is the repo" and the deep wiring/build docs. It names the exact Unity version, the correct scene versus the near-identical trap, and why the lean scene is lean (self-bootstrapping systems), so a future developer is productive in an hour instead of a day.

## Connections
- [[Unity Setup Guide — Scene Wiring]] — full scene hierarchy, prefab contents, ScriptableObjects to create, PC test controls, first-run checklist
- [[Handover — Build, Keystore and APK Rebuild Checklist]] — the pinned Unity version, Android module, and the correct-scene note; then build + sign the APK
- [[HANDOVER — Start Here]] — the role-ordered front door to the whole handover set
- [[Turn System v4 — The Clean-Slate Rebuild]] — why the turn tools were removed and how turns are authored now
- [[Road System — Step by Step Guide]] — authoring a road end-to-end
- [[Build Changelog — Final Features and Fixes]] — the dated log, including this session's endless mode, name entry, and fixes
