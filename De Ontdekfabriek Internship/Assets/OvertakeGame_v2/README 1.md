# Overtake Game — Unity Setup & Reference Guide

A modular scooter overtaking game built for user testing traffic behaviour.
All features are individually toggleable per session from the Inspector.

---

## Table of Contents

1. [Project Structure](#1-project-structure)
2. [Script Reference](#2-script-reference)
3. [Scene Setup (Step by Step)](#3-scene-setup-step-by-step)
4. [Inspector Quick-Reference](#4-inspector-quick-reference)
5. [Per-Session Toggle Guide](#5-per-session-toggle-guide)
6. [Pothole Prefab Setup](#6-pothole-prefab-setup)
7. [UI Canvas Structure](#7-ui-canvas-structure)
8. [Common Issues & Fixes](#8-common-issues--fixes)
9. [Suggested Future Features](#9-suggested-future-features)

---

## 1. Project Structure

```
Assets/OvertakeGame/Scripts/
│
├── Config/
│   └── RoadSideConfig.cs          ScriptableObject — left or right hand traffic
│
├── Core/
│   ├── GameManager.cs             Central state machine + all session toggles
│   ├── ScoreManager.cs            Points, deductions, per-reason rates
│   ├── TimerManager.cs            Session countdown (limited or unlimited)
│   ├── WarningSystem.cs           HUD warning messages with cooldowns
│   ├── RewardSystem.cs            Points for good behaviour (survival, overtakes, lane, speed)
│   ├── RoadTileRecycler.cs        Endless road via tile recycling
│   ├── CheckpointManager.cs       End-of-round checkpoint flow
│   └── CheckpointTrigger.cs       Placed on checkpoint prefab — detects player entry
│
├── Detection/
│   ├── OvertakeCollisionHandler.cs  Detects player–traffic collisions
│   ├── OvertakeDetector.cs          Detects successful overtakes, triggers reward
│   ├── WrongLaneDetector.cs         Monitors which side of road player is on
│   └── SpeedMonitor.cs              Detects sustained speeding
│
├── Player/
│   ├── PlayerController.cs        Gas, brake, lateral movement, pothole response
│   └── ScooterLean.cs             Visual lean on steering + pothole wobble
│
├── Potholes/
│   ├── PotholeManager.cs          Spawns/recycles potholes, controls density ramp
│   ├── Pothole.cs                 Per-pothole trigger, notifies GameManager
│   └── CameraShake.cs             Shake effect on pothole hit
│
├── Traffic/
│   ├── TrafficManager.cs          Pool-based spawner for both lanes
│   └── TrafficVehicle.cs          Per-car movement behaviour
│
└── UI/
    ├── UIManager.cs               HUD, game over, finish, checkpoint screens
    └── Speedometer.cs             Rotating needle + numeric km/h readout
```

> **Namespace:** Every class is in the `OvertakeGame` namespace. If you have other
> scripts in your project with the same class names (e.g. `GameManager`) they will
> not conflict.

---

## 2. Script Reference

### Config

**`RoadSideConfig`** *(ScriptableObject)*
Create via: *Right-click in Project → Create → OvertakeGame → Road Side Config*

| Field | Default | Description |
|---|---|---|
| `driveOnRight` | `true` | TRUE = Netherlands/USA style. FALSE = UK style. Flips which lane is "correct" everywhere automatically. |

---

### Core

**`GameManager`**
The single source of truth for game state. All per-session toggles live here.
Attach to a persistent scene GameObject alongside `ScoreManager`, `TimerManager`, and `WarningSystem`.

| Field | Default | Description |
|---|---|---|
| `gameOverOnCollision` | `true` | TRUE = crash with traffic ends the game. FALSE = deducts points instead. |
| `useSessionTimer` | `true` | Enables the countdown timer. When false, game runs until game-over. |
| `sessionDuration` | `120` | Session length in seconds. |
| `deductOnCollision` | `true` | Deduct points on traffic collision (non-game-over mode). |
| `deductOnWrongLane` | `true` | Deduct points per second while in the wrong lane. |
| `deductOnSpeeding` | `true` | Deduct points per second while over the speed limit. |
| `deductOnPothole` | `true` | Deduct points when hitting a pothole. |
| `warnOnWrongLane` | `true` | Show HUD warning when in wrong lane. |
| `warnOnSpeeding` | `true` | Show HUD warning when speeding. |
| `warnOnCollision` | `true` | Show HUD warning on collision (non-game-over mode). |
| `warnOnPothole` | `true` | Show HUD warning on pothole hit. |

Manager reference slots: `ScoreManager`, `TimerManager`, `UIManager`, `TrafficManager`, `WarningSystem`, `CheckpointManager`, `PotholeManager`.

---

**`ScoreManager`**

| Field | Default | Description |
|---|---|---|
| `startingScore` | `1000` | Score at session start. |
| `collisionDeduction` | `50` | Points lost per traffic collision. |
| `potholeDeduction` | `20` | Points lost per pothole hit. |
| `wrongLaneDeductionPerSecond` | `10` | Points lost per second in wrong lane. |
| `speedingDeductionPerSecond` | `8` | Points lost per second while speeding. |
| `minimumScore` | `0` | Score floor — cannot go below this. Set to a large negative number to allow negative scores. |

---

**`TimerManager`**
No Inspector fields. Controlled entirely by `GameManager.sessionDuration` and `GameManager.useSessionTimer`.
Call `GetFormattedTime()` to get a `"M:SS"` string, or `"∞"` when unlimited.

---

**`WarningSystem`**

| Field | Default | Description |
|---|---|---|
| `collisionWarningText` | `"⚠ COLLISION!"` | Message shown on traffic hit. |
| `wrongLaneWarningText` | `"⚠ WRONG LANE!"` | Message shown when in wrong lane. |
| `speedingWarningText` | `"⚠ SPEEDING!"` | Message shown when speeding. |
| `potholeWarningText` | `"⚠ POTHOLE!"` | Message shown on pothole hit. |
| `warningDisplayDuration` | `2` | Seconds each warning stays visible. |
| `warningCooldownPerType` | `1.5` | Minimum seconds between showing the same warning type again. |

Wire: `warningPanel` (parent GameObject), `warningText` (TMP_Text).

---

**`RewardSystem`**
Attach to the player car alongside `PlayerController`.

| Field | Default | Description |
|---|---|---|
| `rewardSurvivalTime` | `true` | Award points per second for staying alive. |
| `rewardOvertaking` | `true` | Award flat points per completed overtake. |
| `rewardCorrectLane` | `true` | Award points per second while in the correct lane. |
| `rewardGoodSpeed` | `true` | Award points per second while within the good speed window. |
| `survivalPointsPerSecond` | `2` | Rate for survival reward. |
| `overtakePoints` | `25` | Flat points per overtake. |
| `correctLanePointsPerSecond` | `3` | Rate for correct lane reward. |
| `goodSpeedPointsPerSecond` | `2` | Rate for good speed reward. |
| `goodSpeedMin` | `40` | km/h — below this, no good-speed reward. |
| `goodSpeedMax` | `75` | km/h — above this, no good-speed reward (SpeedMonitor handles penalties). |

Wire: `ScoreManager`, `WrongLaneDetector`, `PlayerController`.

---

**`RoadTileRecycler`**
Attach to any persistent GameObject. Recycles 3+ road tile instances to create an endless road.

| Field | Default | Description |
|---|---|---|
| `tiles` | — | Assign all road tile instances (minimum 3 recommended). |
| `tileLength` | `100` | Length of each tile in world units (Z axis). Must match your prefab. |
| `recycleOffset` | `10` | How far behind the player a tile must be before it moves to the front. |

**Setup:** Place 3 tile instances in scene at Z = 0, 100, 200. Assign all 3 to the `tiles` list. The script auto-finds the player.

---

**`CheckpointManager`**
Controls the end-of-round flow: spawn checkpoint → player drives to it → pause → next round.

| Field | Default | Description |
|---|---|---|
| `checkpointPrefab` | — | Prefab with a trigger collider. Can be a gate, finish line, etc. |
| `checkpointSpawnDistance` | `80` | How far ahead the checkpoint spawns when the timer expires. |
| `checkpointY` | `0` | World Y of the checkpoint (should sit on road surface). |
| `pauseDuration` | `5` | Seconds the game pauses at the checkpoint before the next round. |
| `checkpointBrakeForce` | `25` | How quickly the scooter brakes to a stop at the checkpoint. |

The `CheckpointTrigger` component is auto-added to the prefab at runtime — you don't need to add it manually. Just make sure the prefab has a trigger collider and the player has the `Player` tag.

---

### Detection

**`OvertakeCollisionHandler`** *(was `CollisionHandler`)*
Attach to the player car.

| Field | Default | Description |
|---|---|---|
| `trafficTag` | `"Traffic"` | Tag on all traffic vehicle GameObjects. |
| `collisionCooldown` | `1.5` | Seconds between registering repeated collisions from the same car. |

Responds to both `OnCollisionEnter` and `OnTriggerEnter`. Check your collider setup — use one mode consistently (see [Common Issues](#8-common-issues--fixes)).

---

**`OvertakeDetector`**
Attach to the player car.

| Field | Default | Description |
|---|---|---|
| `overtakeThreshold` | `3` | World units the player must be ahead of a car's centre for it to count as an overtake. |

Wire: `RewardSystem`.

---

**`WrongLaneDetector`**
Attach to the player car.

| Field | Default | Description |
|---|---|---|
| `roadConfig` | — | Assign the `RoadSideConfig` ScriptableObject. |
| `centreLaneX` | `0` | World X of the road centre line. |
| `graceBuffer` | `0.2` | World units past centre before wrong-lane activates. |
| `gracePeriod` | `0.5` | Seconds in wrong lane before penalties start. |

---

**`SpeedMonitor`**
Attach to the player car (requires `PlayerController`).

| Field | Default | Description |
|---|---|---|
| `speedLimitKmh` | `80` | Speed above which speeding penalties apply. |
| `speedingGracePeriod` | `2` | Seconds over the limit before deductions start. |

---

### Player

**`PlayerController`**
Attach to the player root GameObject (the one with the Rigidbody).

| Field | Default | Description |
|---|---|---|
| `baseSpeed` | `10` | Forward speed when no input is held (m/s). |
| `maxSpeed` | `30` | Maximum speed with gas held (m/s). |
| `accelerationForce` | `15` | Rate of speed increase when gas is held. |
| `brakeForce` | `20` | Rate of speed decrease when brake is held. |
| `naturalDeceleration` | `5` | Rate of drift back to base speed with no input. |
| `lateralSpeed` | `6` | Side-to-side movement speed. |
| `roadHalfWidth` | `4` | Player is clamped within ±this value on the X axis. |
| `gasKey` | `W` | Accelerate. |
| `brakeKey` | `S` | Brake. |
| `leftKey` | `A` | Steer left. |
| `rightKey` | `D` | Steer right. |
| `potholeSpeedPenalty` | `4` | m/s lost instantly on pothole hit. |
| `potholeWobbleDuration` | `0.6` | Seconds of wobble after pothole hit. |
| `potholeWobbleAngle` | `12` | Max lean angle during wobble (degrees). |

---

**`ScooterLean`**
Attach to the **scooter mesh child**, not the Rigidbody root.

```
PlayerRoot          ← Rigidbody, PlayerController, all Detection scripts
  └── ScooterMesh   ← ScooterLean goes here
```

| Field | Default | Description |
|---|---|---|
| `maxLeanAngle` | `20` | Max lean in degrees when steering at full speed. |
| `leanSpeed` | `8` | How fast lean responds to input. |
| `returnSpeed` | `10` | How fast scooter returns upright. |
| `scaleWithSpeed` | `true` | Lean is reduced at low speed, full at `fullLeanSpeed`. |
| `fullLeanSpeed` | `15` | m/s at which full lean is reached. |
| `handlebarBone` | — | Optional: assign handlebar bone for counter-steer twist. |

---

### Potholes

**`PotholeManager`**
Attach to any manager GameObject. Wire into `GameManager.potholeManager`.

| Field | Default | Description |
|---|---|---|
| `spawnSide` | `PlayerLaneOnly` | Dropdown: PlayerLaneOnly / OncomingLaneOnly / Both. |
| `playerLaneX` | `1.5` | Must match `TrafficManager.playerLaneX`. |
| `oncomingLaneX` | `-1.5` | Must match `TrafficManager.oncomingLaneX`. |
| `laneHalfWidth` | `0.8` | Potholes randomise within ±this of the lane centre. |
| `spawnY` | `0.01` | World Y. Slightly above 0 so the mesh sits on the road. |
| `spawnDistanceAhead` | `70` | How far ahead of the player potholes appear. |
| `despawnDistanceBehind` | `20` | How far behind before a pothole is recycled. |
| `spawnIntervalMin` | `3` | Starting minimum seconds between spawns. |
| `spawnIntervalMax` | `6` | Starting maximum seconds between spawns. |
| `rampDensityOverTime` | `true` | Gradually increase pothole frequency over time. |
| `rampDuration` | `90` | Seconds until peak difficulty is reached. |
| `minIntervalAtPeak` | `1` | Spawn interval at peak difficulty. |
| `minScale` | `0.7` | Minimum pothole size multiplier. |
| `maxScale` | `1.4` | Maximum pothole size multiplier. |
| `poolSize` | `12` | Number of pooled pothole instances. |

---

**`Pothole`**
Placed on pothole prefab automatically. Has one optional field:

| Field | Description |
|---|---|
| `warningIndicator` | Optional child object (e.g. a coloured ring) shown ahead of the pothole and hidden on hit. |
| `playerTag` | Tag on the player. Default `"Player"`. |

---

**`CameraShake`**
Attach to the follow camera.

| Field | Default | Description |
|---|---|---|
| `shakeDuration` | `0.35` | Seconds the shake lasts. |
| `shakeMagnitude` | `0.15` | Max positional offset in world units. |
| `dampingSpeed` | `4` | How quickly the shake decays. |

---

### Traffic

**`TrafficManager`**
Attach to any manager GameObject.

| Field | Default | Description |
|---|---|---|
| `roadConfig` | — | Assign `RoadSideConfig` ScriptableObject. |
| `playerLaneX` | `1.5` | World X of player lane centre. |
| `oncomingLaneX` | `-1.5` | World X of oncoming lane centre. |
| `spawnY` | `0` | World Y for all traffic. **Set to your road surface Y, not the player's Y.** |
| `prewarmStartDistance` | `15` | How close the first pre-warmed car is at game start. |
| `prewarmEndDistance` | `200` | How far the pre-warmed queue reaches at start. |
| `sameDirectionSpawnMin/Max` | `3/6` | Interval between ongoing same-direction spawns. |
| `oncomingSpawnDistance` | `120` | How far ahead oncoming cars spawn (far enough to react to). |
| `oncomingSpawnMin/Max` | `1.5/3.5` | Interval between oncoming spawns. |
| `sameDirectionSpeedMin/Max` | `4/8` | Speed range for same-direction traffic (m/s). |
| `oncomingSpeedMin/Max` | `8/14` | Speed range for oncoming traffic (m/s). |
| `poolSizePerLane` | `16` | Keep this at 16+ so the prewarm can fill the pool. |
| `minimumCarGap` | `12` | Clear space between same-direction cars. Increase if gaps feel too tight. |
| `carLength` | `4` | Approximate Z length of traffic cars. Match your prefab. |

---

**`TrafficVehicle`**
Auto-added to prefabs by `TrafficManager` if missing. No configuration needed.
Prefab requirements: a **Box Collider** (non-trigger), tagged `"Traffic"`.

---

### UI

**`UIManager`**
Attach to the Canvas or a UI manager GameObject.

Requires TMP_Text and Button references for: Score, Timer, Game Over panel, Finish panel, Checkpoint panel. See [UI Canvas Structure](#7-ui-canvas-structure).

---

**`Speedometer`**
Attach to the speedometer sub-object in the Canvas.

| Field | Default | Description |
|---|---|---|
| `needleTransform` | — | The needle RectTransform. Set its pivot to the rotation centre. |
| `needleZeroAngle` | `-135` | Z rotation when speed = 0. |
| `needleMaxAngle` | `135` | Z rotation at max speed. |
| `maxSpeedKmh` | `200` | Speed at which needle reaches max angle. |
| `speedText` | — | Optional TMP text showing numeric speed. |
| `textFormat` | `"{0:0} km/h"` | Format string. `{0}` = speed in km/h. |

---

## 3. Scene Setup (Step by Step)

### Step 1 — Tags
In **Edit → Project Settings → Tags and Layers**, create these tags:
- `Traffic`
- `Pothole`
- `Player` (assign to player car)
- `Checkpoint` (optional, for the checkpoint prefab)

### Step 2 — RoadSideConfig
Right-click in Project → **Create → OvertakeGame → Road Side Config**.
Name it `RoadSideConfig`. Set `driveOnRight` for your scenario.

### Step 3 — Road (Endless)
1. Create a road tile prefab, exactly `100` units long in Z.
2. Place 3 instances in the scene at Z = `0`, `100`, `200`.
3. Add `RoadTileRecycler` to any GameObject. Assign all 3 instances to `Tiles`. Set `tileLength = 100`.

### Step 4 — Managers GameObject
Create an empty GameObject named `Managers`. Add all of these components:
- `GameManager`
- `ScoreManager`
- `TimerManager`
- `WarningSystem`
- `TrafficManager`
- `PotholeManager`
- `CheckpointManager`

Wire all cross-references in the Inspector. GameManager has a slot for every other manager.

### Step 5 — Player Car
Your player root needs a **Rigidbody** and a **Box Collider**.

Components on the root:
- `PlayerController`
- `OvertakeCollisionHandler`
- `WrongLaneDetector` (assign RoadSideConfig)
- `SpeedMonitor`
- `OvertakeDetector` (wire RewardSystem)
- `RewardSystem` (wire ScoreManager, WrongLaneDetector, PlayerController)

Components on the mesh child:
- `ScooterLean` (wire PlayerController)

Tag the player root as `Player`.

### Step 6 — Camera
Add `CameraShake` to your follow camera.

### Step 7 — Traffic Prefabs
Each traffic prefab needs:
- A Box Collider (non-trigger)
- Tag set to `Traffic`
- `TrafficVehicle` component (or it will be auto-added)

Assign prefab lists to `TrafficManager.sameDirectionPrefabs` and `oncomingPrefabs`.

### Step 8 — Pothole Prefab
See [Section 6](#6-pothole-prefab-setup).

### Step 9 — Checkpoint Prefab
Any GameObject with a **trigger collider**. Can be a gate, finish line, coloured quad — anything visible. `CheckpointTrigger` is auto-added at runtime. Assign to `CheckpointManager.checkpointPrefab`.

### Step 10 — UI Canvas
See [Section 7](#7-ui-canvas-structure).

---

## 4. Inspector Quick-Reference

| You want to change... | Script | Field |
|---|---|---|
| Left/right hand traffic | `RoadSideConfig` | `driveOnRight` |
| Session length | `GameManager` | `sessionDuration` |
| No time limit | `GameManager` | `useSessionTimer = false` |
| Crash ends game | `GameManager` | `gameOverOnCollision = true` |
| Points-only on crash | `GameManager` | `gameOverOnCollision = false` |
| Disable wrong-lane penalty | `GameManager` | `deductOnWrongLane = false` |
| Disable pothole penalty | `GameManager` | `deductOnPothole = false` |
| Points lost per crash | `ScoreManager` | `collisionDeduction` |
| Points lost per pothole | `ScoreManager` | `potholeDeduction` |
| Speed limit | `SpeedMonitor` | `speedLimitKmh` |
| Scooter max lean | `ScooterLean` | `maxLeanAngle` |
| Traffic gap size | `TrafficManager` | `minimumCarGap` |
| Pothole lane side | `PotholeManager` | `spawnSide` |
| Pothole density | `PotholeManager` | `spawnIntervalMin/Max` |
| Checkpoint pause length | `CheckpointManager` | `pauseDuration` |
| Camera shake intensity | `CameraShake` | `shakeMagnitude` |

---

## 5. Per-Session Toggle Guide

Use this table before each user test session to configure the experience.

| Scenario | Settings |
|---|---|
| **Observe safe driving** | `gameOverOnCollision = false`, `deductOnWrongLane = true`, `deductOnSpeeding = true`, `warnOnWrongLane = true` |
| **Punish aggression** | `gameOverOnCollision = true`, `deductOnSpeeding = true`, `speedLimitKmh = 60` |
| **No time pressure** | `useSessionTimer = false` |
| **Timed challenge** | `useSessionTimer = true`, `sessionDuration = 120` |
| **Potholes in player lane only** | `PotholeManager.spawnSide = PlayerLaneOnly` |
| **Potholes everywhere** | `PotholeManager.spawnSide = Both` |
| **No potholes** | Disable `PotholeManager` component |
| **Right-hand traffic (NL/USA)** | `RoadSideConfig.driveOnRight = true` |
| **Left-hand traffic (UK)** | `RoadSideConfig.driveOnRight = false` |
| **Maximum difficulty** | `rampDensityOverTime = true`, `minIntervalAtPeak = 0.8`, `minimumCarGap = 8` |
| **Beginner friendly** | `rampDensityOverTime = false`, `minimumCarGap = 18`, `potholeSpawnIntervalMin = 6` |

---

## 6. Pothole Prefab Setup

1. Create an empty GameObject named `Pothole`
2. Add a child GameObject with a **Cylinder** mesh:
   - Scale to roughly `(0.9, 0.05, 0.9)` — flat disc shape
   - Apply a dark cracked material
3. On the root, add a **Capsule Collider**:
   - Set `Is Trigger = true`
   - Resize to match the mesh footprint
4. Optionally add a second child — a coloured ring or arrow quad offset `+4` in Z:
   - This is the warning indicator visible before the pothole
   - Assign it to `Pothole.warningIndicator`
5. Make it a prefab and assign to `PotholeManager.potholePrefab`

The `Pothole` component is auto-added at runtime if missing, but you can add it manually to configure `warningIndicator` in the prefab editor.

---

## 7. UI Canvas Structure

```
Canvas
├── HUD
│   ├── ScoreText              (TMP_Text)   — live score
│   ├── TimerPanel             (GameObject) — hidden when timer is unlimited
│   │   └── TimerText          (TMP_Text)   — "M:SS" countdown
│   ├── Speedometer
│   │   ├── DialImage          (Image)      — background dial graphic
│   │   ├── NeedleImage        (RectTransform) — assign to Speedometer.needleTransform
│   │   └── SpeedText          (TMP_Text)   — numeric km/h
│   └── WarningPanel           (GameObject) — hidden by default
│       └── WarningText        (TMP_Text)
│
├── GameOverPanel              (GameObject) — hidden by default
│   ├── GameOverScoreText      (TMP_Text)
│   └── RestartButton          (Button)
│
├── FinishPanel                (GameObject) — hidden by default
│   ├── FinishScoreText        (TMP_Text)
│   └── RestartButton          (Button)
│
└── CheckpointPanel            (GameObject) — hidden by default
    ├── CheckpointRoundText    (TMP_Text)   — "Round X Complete!"
    ├── CheckpointScoreText    (TMP_Text)   — current score
    └── CheckpointCountdownText (TMP_Text)  — "Next round in Xs"
```

Wire all of these to `UIManager` in the Inspector.

---

## 8. Common Issues & Fixes

**Collision with traffic does nothing**

Most likely cause is a tag mismatch or missing component. Debug order:
1. Add `Debug.Log($"Hit: {collision.gameObject.name}, tag: {collision.gameObject.tag}");` to `OvertakeCollisionHandler.OnCollisionEnter`
2. If nothing prints → collider issue. Check that the player has a non-trigger Box Collider and the traffic prefabs also have non-trigger colliders. Both need Rigidbodies or one needs to be a trigger.
3. If it prints but nothing happens → check `GameManager.Instance` is not null, and `CurrentState == Playing`.
4. Check `collisionCooldown` — during testing set it to `0` so repeated hits register immediately.

**Traffic spawns in the air**

`TrafficManager.spawnY` is copying the player's Y position. The player sits higher than the road due to its Rigidbody/collider height. Set `spawnY` explicitly to your road surface Y (usually `0`).

**No traffic at game start / queue feels short**

Increase `TrafficManager.prewarmEndDistance` (try `300`) and increase `poolSizePerLane` to at least `20` so there are enough pooled instances to fill the longer queue.

**Wrong lane penalty triggers immediately**

Increase `WrongLaneDetector.gracePeriod` — the default `0.5s` may be too short if the player briefly crosses the line while overtaking. Try `1.0` or `1.5` seconds.

**Pothole warning indicator not hiding after hit**

Make sure `warningIndicator` is assigned in the prefab's `Pothole` component in the Inspector. If added at runtime (auto-added), the reference won't be set — add the `Pothole` component manually to the prefab and assign the child there.

**Camera shake not working**

`CameraShake` uses a static `Instance`. Make sure only one `CameraShake` component exists in the scene. It must be on the camera GameObject (or a camera rig parent), not on the Canvas.

**Namespace conflicts / duplicate class errors**

All classes are in the `OvertakeGame` namespace. If you see `CS0101` errors, you likely have old versions of these scripts without the namespace still in your project. Delete any scripts outside `Assets/OvertakeGame/` that share class names.

---

## 9. Suggested Future Features

These were discussed as candidates for future development:

- **Session Data Logger** — CSV export of player position, speed, lane, score events per frame. Most useful for extracting research data from user tests.
- **Difficulty Preset System** — ScriptableObject presets (Easy/Normal/Hard) so all spawn rates and penalties can be switched with one dropdown instead of 15 fields.
- **Near-Miss Reward** — bonus points + "CLOSE CALL!" flash when passing within range of oncoming traffic without collision.
- **Traffic Density Ramp** — same-direction gap narrows over time, matching how potholes already ramp.
- **NPC Braking** — occasionally a same-direction vehicle brakes suddenly, requiring the player to react.
- **Engine Sound** — pitch-shifted audio loop on the player, tied to `CurrentSpeedKmh`. Highest impact audio feature.
- **Tyre Marks** — decal trail renderer on the scooter, active only during hard braking above a threshold speed.
- **Combo Multiplier** — consecutive overtakes without hitting anything temporarily double the overtake point value.
- **Post-Session Review Screen** — stat breakdown: overtakes completed, potholes hit, time in wrong lane, top speed, collisions.
- **Minimap** — small HUD element showing upcoming traffic density so players can plan overtakes in advance.
- **Idle Wobble** — subtle sine-wave sway on `ScooterLean` at low speed that dampens at higher speed, making the vehicle feel alive when stationary.
