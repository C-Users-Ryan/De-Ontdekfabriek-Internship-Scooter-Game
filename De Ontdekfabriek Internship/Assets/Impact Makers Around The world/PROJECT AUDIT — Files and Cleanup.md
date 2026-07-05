# Project File Audit & Cleanup Guide

**Generated:** 2026-07-05 · **Scope:** `Assets/Impact Makers Around The world/` (the game), plus the rest of `Assets/` because that is where the real bloat is.

## How this was produced (and its limits)
Every "used / unused" call below comes from a **GUID reference trace**: I extracted the GUIDs referenced by the shipping build (the one scene in Build Settings — `Assets/Scenes/Impact Makers Around The World Kenya.unity`) plus every prefab and ScriptableObject inside the game folder, then checked what each asset/pack does or doesn't appear in.

**Read before deleting anything:**
- **Make a branch / commit first.** Cleanup is destructive; the `Assets/Impact Makers Around The world/` scripts are *untracked* in git (no history to restore from).
- **Always delete the `.meta` file with its asset** (and never edit `.meta` GUIDs). In Unity, delete from the **Project window**, not the file explorer, so it handles `.meta` and re-imports.
- **`Resources/` folders are a blind spot for this method.** Code loads them by *name* (`Resources.Load<UITheme>("UITheme")`, pedal-icon sprites), not by GUID — so a GUID trace wrongly reports them as "0 used". **Do not delete anything under a `Resources/` folder based on this audit.**
- The trace uses the **build** scene. Assets used *only* by the WIP `…Kenya turning.unity` dev scene may show as unused — noted where relevant.
- For a definitive check on any single asset, right-click it in Unity → **Find References In Scene**, or use a "find unused assets" editor tool before mass-deleting.

---

## TL;DR — biggest wins first
| Target | Size | Verdict |
|---|---|---|
| `Assets/Endless Runner/` | 135 MB | **Delete** — old prototype, 0 references |
| `Assets/RunnerV2/` | 96 MB | **Delete** — old prototype, 0 references |
| `Assets/_Recovery/` | 29 MB | **Delete** — 36 Unity crash-recovery scenes (`0 (1).unity`…) |
| `Assets/Low Poly Simple Urban City 3D Asset Pack/` | 5 MB | **Delete** — 0 references (city art in-game is Toon) |
| `Assets/Terrain/` | 1.5 MB | **Delete** — used only by the dead `TerrainScene.unity` |
| `Assets/Palmov Island/`, `Pack_FREE_Cars/`, `Open world/`, `pickup game/`, `TutorialInfo/` | ~2 MB | **Delete** — 0 references |
| `Assets/INab Studio/` | 185 MB | **Trim** — only **1 / 390** assets referenced; keep that one, drop the rest |
| `Assets/POLYBOX/` | 183 MB | **Trim** — only **9 / 481** used; the rest is demo scenes/vistas |
| `Assets/Toon Series/` | 96 MB | **Keep (trim carefully)** — 147 / 1515 used; this is the main car/city art |
| `Assets/Low Poly Arid Desert Environment/` | 3.9 MB | **Keep** — 40 / 140 used (the trees/rocks) |
| Old scenes in `Assets/Scenes/` | part of 8 MB | **Delete** all except the two Kenya scenes |

**Safe to delete right now: ~270 MB. With careful pack-trimming: ~600 MB+.**

---

## PART A — Inside `Impact Makers Around The world/` (55 MB)
This folder is the actual game. It is mostly clean; the cruft here is small but worth removing.

### Scripts/ — 174 `.cs` · **KEEP ALL** (2.3 MB)
The current, well-organised codebase (reviewed clean 2026-06-27; no dead scripts of note). 18 subsystem folders:
`UI (30) · FX (20) · Config (14) · Traffic (13) · Settings (13) · Roads (12) · Environment (12) · Core (9) · Session (8) · Player (8) · Scoring (7) · Controls (6) · Sky (5) · Hazards (5) · SafetyNet (4) · Cameras (3) · Audio (3) · Feedback (2)`
- Six `.md` docs are mixed into this code folder — **not needed by Unity/the build** (see *Docs* below).
- Script-level "unused" can't be judged by scene refs here because many systems **self-bootstrap** (`RuntimeInitializeOnLoadMethod`), so they're used without being placed in a scene. Treat all scripts as live unless you trace a specific class.

### Configs / ScriptableObjects — mostly **KEEP**
Core configs, all referenced by the build scene: `DayCycleConfig, HazardSpawnConfig, InputConfig, RoadSideConfig_Kenya, SafetyNetConfig, ScooterConfig, ScoreConfig, SessionConfig, TrafficConfig, TrafficProfile, Sound/AudioConfig, UI/UITheme_Ugani`, and the road sequences `Prefab/Tiles/kenya.asset` + `Prefab/Tiles/city.asset`.
- ❌ **`emptySeq.asset`** (root) — orphan RoadSequence, unreferenced. **Delete.**
- ❌ **`Prefab/Tiles/Seq_.asset`** — orphan RoadSequence, unreferenced. **Delete** (and its only tiles, below).

### Prefab/ — 65 prefabs · **mixed** (6.1 MB)
**Used (keep):**
- `Vehicles/Car_*` — **all ~26 traffic cars are in the build scene.** Keep.
- `Vehicles/... Planeta Sport` (`Prefab/source/Planeta Sport.prefab`) — **the player scooter**, in the build scene. Keep, plus its `Prefab/source/*` materials (M_Chrome, M_Leather, M_Rubber, etc.).
- Road tiles used by `kenya.asset`: `Tiles/City tile 7`, `Tiles/OneSideSavanna`, `Tiles/Vegetation tile`, `Tiles/dirt road tile`; used by `city.asset`: `Tiles/City tile`.
- Nested scenery (keep — inside used tiles): `bunch.prefab`, `savana.prefab` (in OneSideSavanna); `SM_desert_tree_02 (5)` & `(6)` (in Vegetation tile).
- Hazards: `hazards/Pothole 1.prefab` (Potholes spawner), `hazards/Traffic_Cone_1A (1).prefab` (Cone spawner).

**❌ Orphan — not referenced by the build scene, any config, or any used prefab (delete after a Unity "Find References" sanity check):**
```
Vehicles/Scooter_1A.prefab   Vehicles/Scooter_1B.prefab
Vehicles/Scooter_1C.prefab   Vehicles/Scooter_1D.prefab      ← pack scooters; player uses "Planeta Sport"
Tiles/Crossroads Art.prefab                                   ← whole branch unused
Tiles/base Tile.prefab   Tiles/base Tile (6).prefab   Tiles/base Tile (7).prefab
Tiles/Plane.prefab   Tiles/savannah tile.prefab               ← only used by the orphan Seq_.asset
Plane (4).prefab   Road_1A_+4 (12).prefab   bunch is kept; savana is kept
Rocks.prefab   Rocks/Rocks.prefab   Rocks/Rocks 1..4.prefab   ← 6 loose asset-pack drops
SM_desert_tree_02 (7)   SM_desert_tree_03 (2)   SM_desert_tree_03 (3)
SM_desert_tree_04 (1)   SM_desert_tree_05 (3)
SM_rock_01 (10)   SM_rock_01 (11)   SM_rock_05 (6)   SM_rock_09 (4)   SM_rockplat_03 (1)
```
These are experimental drag-ins (the `(N)` suffixes are Unity's duplicate-naming). They may only be referenced by old/unused scenes.

### Sound/ — **KEEP ALL, but it's 47 MB (85% of this folder)**
All 10 clips are wired through `AudioConfig.asset` (crash hard/soft, electric engine, horn, ambient nature, overtake sting, rewind, wind, wrong-indicator ding) plus two field recordings (`…lokichokio-basecamp….mp3`, `…nairobi-neighborhoods….wav`). Nothing unused — but if project size matters, this is the place to **compress** (the `.wav` and long ambiences especially).

### Small folders — **KEEP**
- `Shaders/` (`KenyaBaseDust.shader`, `Resources/KenyaRoadSand.shader`) — used.
- `Generated/` (`Ground (placeholder).mat`, `Road (placeholder).mat`) — runtime fallback materials; leave.
- `UI/UITheme_Ugani.asset` — the live UI palette; used.
- `Plugins/iOS/KenyaHaptics.mm` — referenced by `Scripts/Feedback/HapticDriver.cs`. iOS-only (compiles only on iOS builds); harmless on Android. Keep unless you formally drop iOS.
- `Tests~/` (58 KB) — the EditMode test suite. The trailing `~` makes Unity ignore it, so it costs nothing at build/compile time. Keep.

### ❌ Loose cruft in this folder — **delete**
- `images (4).jpg` — stray download, unreferenced.
- `Prefab/402-4020061_pothole-pothole-png.png.original.bak` — a `.bak` backup file.
- `Prefab/V10pro_21.png` — stray image (verify it's not a texture in use; appears orphan).
- Duplicate pothole textures (**verify which the used `Pothole 1` prefab points to, then drop the rest**): `Prefab/pothole.png`, `Prefab/402-4020061_pothole-pothole-png.png` (+ its `.mat`) vs `Prefab/hazards/Pothole_LP_*.png`.

### Docs (`.md`) — not needed by the build (keep for reference or move to the vault)
`ROAD SYSTEM — Step by Step Guide.md`, `Scripts/README — Architecture and System Map.md`, `Scripts/SETUP — Quick Start.md`, `Scripts/Open-World — Feasibility and Design Note.md`, and 3 `Audio …` sourcing notes. They don't ship; the README/SETUP are worth keeping as project documentation.

---

## PART B — The rest of `Assets/` (where the space actually is)

### ❌ Dead prototype folders — delete (0 references)
- **`Endless Runner/` (135 MB)** and **`RunnerV2/` (96 MB)** — earlier runner-game prototypes, unrelated to the Kenya game.
- **`Open world/` (35 KB)**, **`pickup game/` (38 KB)** — old experiments.
- **`_Recovery/` (29 MB)** — 36 Unity auto-recovery scenes (`0.unity`, `0 (1).unity` … `0 (35).unity`). Pure junk.
- **`TutorialInfo/` (63 KB)** — leftover from a Unity template.

### ❌ Unused asset packs — delete (0 references)
- **`Low Poly Simple Urban City 3D Asset Pack/` (5 MB)** — the in-game city art comes from Toon, not this.
- **`Palmov Island/` (994 KB)**, **`Pack_FREE_Cars/` (843 KB)** — unused (traffic cars are Toon).
- **`Terrain/` (1.5 MB)** — referenced only by the dead `Scenes/TerrainScene.unity`.

### ✂️ Load-bearing packs — KEEP, trim only inside Unity
Deleting whole files here **will break prefabs** (missing meshes/materials). Trim only with a Unity unused-asset tool, or leave them.
- **`Toon Series/` (96 MB)** — **147 / 1515** used. The cars + city buildings the game uses live here. Biggest "keep". Its `Scenes/Demo_Scene_*` and unused variants can go.
- **`Low Poly Arid Desert Environment/` (3.9 MB)** — **40 / 140** used (the desert trees/rocks in `Vegetation tile` / roadside).
- **`POLYBOX/` (183 MB)** — only **9 / 481** used (a few rocks). The bulk is `scenes/Fog_Scenes`, `Vista_scenes`, `Rocks_Scenes` demo content — huge and removable, but verify the 9 used rocks stay.
- **`INab Studio/` (185 MB)** — only **1 / 390** used. Almost entirely demo content (Toon Pro showcase scenes). Identify that single reference in Unity; if it's replaceable, this whole 185 MB can go.

### `Assets/Scenes/` (8 MB) — keep 2, delete the rest
- **KEEP:** `Impact Makers Around The World Kenya.unity` (**the build scene**) and `Impact Makers Around The World Kenya turning.unity` (active turn-rework WIP).
- ❌ **Delete:** `EndlessV2, endlessRunner, Overtake, Overtake turn, OvertakeGameScene2, overtakeGameScene, SampleScene, TerrainScene, Tiles, testturng, turn test, pick up system, pick-up game`.

### Required Unity/engine folders — **KEEP**
- `TextMesh Pro/` (4.6 MB) — required by all UI text.
- `Settings/` (87 KB) — URP render pipeline assets; the build depends on it.
- `Resources/` (46 KB) — **name-loaded** (pedal icons `UI/PedalIcons/*`, UI theme/strings). Keep — the GUID trace can't see these.

---

## Suggested order of operations
1. **Commit / branch** the current state.
2. Delete the **zero-reference** folders (Part B first two groups) — instant ~270 MB, no risk.
3. Delete the **old scenes** in `Scenes/` (keep the 2 Kenya scenes).
4. Delete the **IMW orphans** (the prefab list + `emptySeq`/`Seq_` + loose cruft in Part A).
5. Reimport / open the build scene, press Play, confirm nothing is pink/missing.
6. *Optionally, later:* trim **INab / POLYBOX / Toon** from inside Unity with a "find unused assets" tool — the largest remaining win, but do it deliberately and test after each.

## Caveats recap
GUID tracing proves what the **build scene + game prefabs/configs** reference. It does **not** see: `Resources.Load` (name-based), assets referenced only by the WIP turning scene, or things loaded by string paths. Everything flagged "delete" is high-confidence, but a 30-second Unity **Find References In Scene** on anything you're unsure about is cheap insurance before it's gone for good.
