# Scene Setup — Quick Start

Goal: get from "scripts imported" to **pressing Play and driving on PC** with the fewest steps.
For the complete version (all vehicle types, sequences, audio, tablet build), see `Unity Setup Guide — Scene Wiring.md` in the Obsidian vault.

Everything is `KenyaScooter.*` namespace. All tunable numbers live on ScriptableObject assets — create them once, drag them onto the matching component.

---

## 0. Prerequisites (do these first or nothing compiles)

- Project uses **URP** (the day-cycle and rewind use URP `Volume`).
- Install three packages (Window → Package Manager):
  - **Input System** (set Project Settings → Player → *Active Input Handling* = **Both**, then restart Unity)
  - **TextMeshPro** (Window → TextMeshPro → Import TMP Essentials)
  - **Universal RP**
- Let the 73 scripts compile cleanly (empty Console) before building the scene.

---

## 1. Create the config assets

Right-click in the Project window → **Create → Kenya Scooter → …** Make one of each, leave defaults unless noted:

`RoadSideConfig` (driveOnLeft = true) · `ScooterConfig` · `InputConfig` · `ScoreConfig` · `SafetyNetConfig` · `TrafficConfig` · `SessionConfig` · `DayCycleConfig` · `AudioConfig` · `HazardSpawnConfig` (optional for first drive) · **Traffic Behaviour Profile** (one, call it `TrafficProfile_Default`).

You'll also make a **Road Sequence** in step 4.

---

## 2. Layers

Create layers: `Player`, `Traffic`, `Hazard`, `TileTrigger`.
Physics (Project Settings → Physics): enable only **Player↔Traffic**, **Player↔Hazard**, **Player↔TileTrigger**. Turn the rest off.

---

## 3. Three prefabs (the minimum to drive)

**Player** (layer Player)
- Empty GameObject → add `Rigidbody` (the code forces it kinematic), a `CapsuleCollider` (NOT trigger), and components: `PlayerController` (→ ScooterConfig), `WrongLaneDetector`, `PlayerCollisionHandler` (→ SafetyNetConfig), `NearMissDetector` (→ TrafficConfig), `SpeedMonitor` (→ SessionConfig), `ScooterWobble` (→ ScooterConfig).
- Add a child mesh (a capsule/cube is fine for now). Add `ScooterLean` on the root → drag the child mesh into its **visual** field, and the `ScooterWobble` into its **wobble** field.

**Road tile** (a straight piece)
- A flat, long cube/plane (e.g. 6.5 wide × 30 long). Add `RoadTile`, set **length = 30**. Leave exitAnchor empty for a straight tile. Make it a prefab.

**Traffic vehicle** (one matatu, layer Traffic)
- A box mesh. Add a `BoxCollider` (**Is Trigger = on**), and `TrafficVehicle`: set **type = Matatu**, **length ≈ 4.5**, **overtakeScore = 150**, drag the mesh's `Renderer` into **bodyRenderer**. Make it a prefab.

> First drive without traffic? You can skip the vehicle and leave the spawner's arrays empty — but overtaking is the whole game, so add at least this one.

---

## 4. One Road Sequence

Create → Kenya Scooter → **Road Sequence** (`Seq_Highland_Start`). Set **zoneName = HIGHLAND**, **contextTags = [highland]**, **unlockAtTime = 0**, and drag your road-tile prefab into the **tiles** list a few times (e.g. ×4).

---

## 5. The Systems object

Create an empty `Systems` GameObject and add these components, assigning the matching Config asset (and a couple of references) to each:

| Component | Assign |
|---|---|
| `WorldSpeed` | ScooterConfig |
| `ScooterInputRouter` | InputConfig |
| `GraceSystem` | SafetyNetConfig |
| `RewindSystem` | SafetyNetConfig (leave *visuals* empty for now) |
| `TimerManager` | SessionConfig |
| `SpeedZoneManager` | — |
| `GroupScoreManager` | — |
| `LeaderboardManager` | — |
| `AnalyticsManager` | — |
| `AudioManager` | AudioConfig (clips can stay empty — runs silent) |
| `StreakSystem` | ScoreConfig |
| `ScoreManager` | ScoreConfig, StreakSystem, the Player's WrongLaneDetector |
| `RoadSequencer` | openingSequence = Seq_Highland_Start, player = Player, (sequences[] can stay empty for now) |
| `TrafficSpawner` | TrafficConfig, defaultProfile = TrafficProfile_Default, sameDirectionPrefabs = [your matatu], player = Player |
| `OvertakeDetector` | player = Player |
| `DayCycleManager` | DayCycleConfig (phaseVolumes[] and sun optional) |
| `GameManager` | roadSideConfig, player = Player, timer, rewind, checkpoint = leave empty, **gameOverOnCollision = off** |

> `CheckpointController` is optional for a first drive — leave GameManager's *checkpoint* field empty and the session ends on the Finish path at 0:00.

---

## 6. Camera

- `Main Camera` → add `CameraShake` and `RealRiderMode`. Position it behind/above the player (e.g. local position `0, 3.2, -5.5`, pitch ~12°).
- Parent the camera under an empty `CameraRig`. Add `CameraRigController` to the rig → set **cameraTransform = Main Camera**, **shake = the CameraShake**, **realRider = the RealRiderMode**.

---

## 7. Press Play

- Tap any key to start (the Ready overlay logic listens for it even without a HUD).
- **W** = gas, **S** = brake, **A/D** = steer, **R** = toggle Real Rider, **Tab** = debug panel.
- You should: cruise forward at the base speed, steer between lanes, and pass the matatu (watch the Console — `ScoreManager` logs nothing, but a popup/score event fires if you add the HUD).

If it drives — the core loop works. 🎉

---

## 8. Then expand (full guide)

Add when the basics run: the **HUD** (`HUDController`, `Speedometer`, `WarningSystem`, `ScorePopupUI`), the **screens** (`EndScreen` ×2, `CheckpointScreen`, `LeaderboardUI`), `HazardSpawner` + a pothole prefab, more vehicle types and sequences, the **URP day-cycle Volumes** + the **rewind B&W Volume** (wire into `RewindVisuals`), `NairobiSkylineBuilder`, and `DebugPanel`/`SettingsPanel`. Each is covered step-by-step in `Unity Setup Guide — Scene Wiring.md`.

---

## Common first-compile issues

- **Errors about `GravitySensor` / `Keyboard` / `TMP_Text` / `Volume`** → a package from step 0 is missing.
- **Tilt steers the wrong way on a tablet** → tick `invertGyro` on InputConfig (PC keyboard is unaffected).
- **`CreateAssetMenu` doesn't show "Kenya Scooter"** → scripts haven't compiled yet; clear the Console errors first.
