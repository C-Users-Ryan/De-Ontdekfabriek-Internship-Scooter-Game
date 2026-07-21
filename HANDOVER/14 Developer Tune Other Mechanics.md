# Guide (Dev): Tune the Other Mechanics — Which Config Governs What

**For:** a future developer with Unity, tuning the game in code rather than through the facilitator menu.
**What it is:** a one-page lookup table that maps each core mechanic to the single ScriptableObject that governs it, its most-tuned fields, and where in the system map to read more. So you can change a number without reading the whole codebase.
**What it is not:** a system explainer. Each row links into [[README — Architecture and System Map]] and the relevant deep doc; this card only tells you *which asset holds the dial*.

> [!note] Where these live, and the rule about defaults
> Every tunable number is a `ScriptableObject` in `Config/` (folder → namespace `KenyaScooter.Config`; architecture rule: *no gameplay number lives in a MonoBehaviour*). The C# field values below are the **defaults that seed a brand-new asset only** — the shipped `.asset` files serialise their own values. To change the live game you edit the **asset in the Project window** (or the Inspector), not the `.cs` default. Several assets say this explicitly in their tooltips (e.g. `SessionConfig.endlessLives`, `DayCycleConfig.phases`). Source-only edits in the vault mirror need a **Unity recompile in the live project** before they take effect, and a build to reach the tablet.

> [!tip] The safe-menu line
> Many of these fields are *also* exposed to non-developers through the facilitator settings menu (via `SettingsCatalog`). If a facilitator can already change it there, prefer that — see [[Handover — What You Can Change vs What Needs a Developer]] and [[Facilitator Settings Menu and the Override Architecture]]. This card is for the numbers that *aren't* surfaced, or for changing a min/max range or a default.

---

![Which config asset governs which mechanic](Diagram%20-%20config%20to%20mechanic%20map.svg)

*Which config asset holds each dial. Tune the `.asset` in the Inspector — the `.cs` value below is only the default that seeds a brand-new asset.*

## The map: mechanic → config asset → key fields → read more

| Mechanic | Config asset (`Config/…`) | Key fields to tune | Read more |
|---|---|---|---|
| **Scooter feel** (speed, steering, lean) | `ScooterConfig` | `baseSpeed` 10 / `maxSpeed` 30 m/s, `acceleration` 15, `brakeDeceleration` 20, `maxLateralSpeed` 6, `lateralAcceleration` 18, `maxLeanAngle` 18°, `wobbleAmplitude`/`wobbleDecay` | [[README — Architecture and System Map]] — M2/M5/M6 rows |
| **Traffic** (spawn, gaps, interaction) | `TrafficConfig` | `oncomingMinGap` 70 (the "every overtake is completable" promise), `minimumCarGap` 20, `maxSameDirection`/`maxOncoming` 8, `yieldDistance` 10, `swerveDistance` 14, `nearMissDistance` 1.7, boda-swarm size | [[README — Architecture and System Map]] — M10/M11/M12; [[Road System v2 — Paths, S-Curves and Branching Junctions]] |
| **Traffic *personality*** (per-archetype behaviour) | `TrafficBehaviourProfile` (+ `DriverPersonality`) | per-profile weighted draw + behaviour biases (vergeStop / breakdown / overtake), flash-to-pass, courtesy hazards — set **per profile asset**, not global | [[README — Architecture and System Map]] — M12 row |
| **Day / night** (phases, sun, sky, headlight fade) | `DayCycleConfig` | the six-entry `phases[]` array (`label`, `startTime`, `sunColour`, `fogColour`, `artificialLight` → headlight fade), `scaleToSessionLength` + `referenceSessionSeconds` 120, `cycleEnabled` / `fixedPhaseIndex` (hold one look) | [[Day and Night — A Slower Sundown and a Real Scooter Headlight]]; README — M25 |
| **Scoring** (deductions, rewards) | `ScoreConfig` | per-event toggles (`deductCollisions`…), `collisionDeduction` 60, `rewindPenalty` 80, `potholeDeduction` 50, `rockDeduction` 35, `wrongLanePerSecond` 25, `speedingPerSecond` 15, `overtakeMultiplier`, `hazardSpeedScale` curve | [[Code Audit — Point System and Relay Flow]]; README — M19 |
| **Streak multiplier** | `ScoreConfig.streakTiers[]` | tier table `(fromStreak, multiplier)` — default 0→1×, 2→1.5×, 4→2×, 6→2.5×, 8→3×; `streakEnabled` off = every reward single | [[Curating the Juice — Two of Seventeen Animation Concepts]] (tier-up juice); README — M20 |
| **Safety net** (collision grade, grace, rewind) | `SafetyNetConfig` | `lightHitMaxKmh` 35 / `hardHitMinKmh` 60 (the grade thresholds), `graceCharges` 1 + `graceRechargeSeconds` 8, `rewindWindowSeconds` 3.5, `rewindsPerTurn` 2, `postRecoveryInvulnerability` 1.5 | [[Safety Net and Time Rewind System]]; README — M15/M16/M17 |
| **Timed session** (relay length) | `SessionConfig` | `sessionSeconds` 120 (a turn's length), `checkpointAutoAdvanceSeconds` 25, `endScreenAutoAdvanceSeconds` 20, `speedingGraceSeconds` 3 | [[Checkpoint and the Charging-Station Decision]]; README — M26 |
| **Checkpoint / charge relay choreography** | `SessionConfig` (relay block) | `cinematicRelay` on/off (falls back to the proven instant relay), `checkpointLeadSeconds` 10, `pullInSeconds`/`chargeSeconds`/`pullOutSeconds`, `bayLateral`/`bayYaw` (0 = stop in lane; >0 = pull to a bay), `checkpointBrakeRate` 9 | [[Checkpoint and the Charging-Station Decision]]; README — M27 (`ChargeStationSequence`) |
| **Endless / "Vrij rijden" lives** *(new)* | `SessionConfig.endlessLives` | lives for the solo run (default 3). Only the **Endless** mode reads it; the timed group relay ignores it. Set it **explicitly on the live asset** — the default only seeds a fresh one. Commits nothing to the leaderboard; lives + distance shown by `EndlessHud` | [[Build Changelog — Final Features and Fixes]] (Endless mode entry) |

> [!note] Two dials that live *off* these assets (don't hunt for them here)
> - **Overtake base points** are authored **per vehicle prefab** (they vary by vehicle type, Req §7.1). `ScoreConfig.overtakeMultiplier` only *scales* them; to change what one vehicle is worth, edit that prefab.
> - **Per-tile roughness** (potholes/rocks density) and **surface** (Paved/Dirt) live on each `RoadTile`, not in a global config — see [[Dirt Roads — Communicating Surface Through Dust, Not Shaking]] and [[Road System — Step by Step Guide]]. Hazard *behaviour and scoring* are data-driven per `HazardSpawnConfig` asset.

---

## How to actually apply a change

1. Find the asset in the Project window (search the asset name, e.g. `SessionConfig`), or use its `Create > Kenya Scooter > …` menu to make a fresh one.
2. Edit the field in the Inspector. Play the PC scene to feel it — the loop runs in-editor (needs Input System, TextMeshPro, URP).
3. If you edited a `.cs` default instead, remember it changes **only newly-created assets**; the shipped asset keeps its serialised value. For the day palette specifically there's a helper: *Tools → Kenya Scooter → Weather and FX → Apply Kenya Reference Day Palette*.
4. To reach the tablet: rebuild and re-sign the APK — [[Handover — Build, Keystore and APK Rebuild Checklist]].

> [!warning] Honesty caveats (hold these)
> - Nothing here is **device-tested**. The gyro sign (`GyroTiltProvider.RollForOrientation`), the kiosk lock (`KioskLock`), and all **audio** (clips aren't imported — the game runs silent) are **not yet verified on-device**. Tuning them in an asset does not change that.
> - This session's shipped changes (Endless mode, typed team name, settings search, PIN reset, tile-budget lag fix, night-aware corner **tail lights**) are **source-only in the mirror and need a Unity recompile in the live project**; the tail lights also want a real **dusk drive** to confirm.
> - **EditMode tests do not run** (they sit in `Tests~/Editor/`, which Unity ignores) — never read a change as "tests pass".

---

> [!abstract] Evidence (for the portfolio)
> **What it is:** a single lookup table that collapses "which knob governs which mechanic" into one page, so a maintainer can tune traffic, day/night, scoring, the safety net, the session/relay, or the new Endless lives without first reading every `Config/` file — while the deeper *why* stays in the system map and the per-system devlogs it links to.

## Connections
- [[README — Architecture and System Map]] — the full system map every row points into (MDA M1–M27, folder→namespace)
- [[Handover — What You Can Change vs What Needs a Developer]] — the safe line; many of these fields are also facilitator-editable
- [[Facilitator Settings Menu and the Override Architecture]] — how to promote a field into the safe menu
- [[Safety Net and Time Rewind System]] · [[Checkpoint and the Charging-Station Decision]] · [[Day and Night — A Slower Sundown and a Real Scooter Headlight]] — the deep docs behind three of the rows
- [[Handover — Build, Keystore and APK Rebuild Checklist]] — how a config change reaches the tablet
- [[Build Changelog — Final Features and Fixes]] — the dated log, including the Endless-mode entry
