# GUIDE (Dev): Author a Hazard

**Sprint:** 8 · **Author:** Ryan Putman · De Ontdekfabriek
**Audience:** a developer adding or changing a road hazard (pothole, rock, speed bump, cone, branch…).
**Status:** source-only in the vault mirror; any code path here needs a Unity **recompile** in the live project. Nothing on this card is device-tested.

> [!info] The one idea
> A hazard is **data, not code.** A whole new hazard family — a new thing that goes wrong on the road — is **one `HazardSpawnConfig` asset** dropped into the spawner's list. No new script, no enum value, no `switch` arm. The [[Location Pack — Authoring Guide and Asset Contracts]] §5 is the full contract; this card is the fast path and the fair-placement rules to respect.

This is a **thin task-card**. For the complete Hazard Contract (archetypes, the Kenya→Netherlands worked example, the code-verification table) go to [[Location Pack — Authoring Guide and Asset Contracts]]. Come back here for the checklist and the placement rules baked into the spawner.

---

## Reskin vs. new mechanic — pick your lane first

| You want… | Is it code? | Do this |
|---|---|---|
| A new hazard that **behaves like** an existing one (surface defect / obstacle / slowdown) but a different look, label, cost or spawn pattern | **No code** | New `HazardSpawnConfig` asset + a reskinned `Hazard` prefab. This is the standard path — it covers essentially every real road. |
| A genuinely new **physics behaviour** (e.g. an oil slick that steers you) | **Code, once** | Add one value to `HazardResponse` (`Core/GameEnums.cs`) and one handler arm where hits are resolved, then author the config. This is the *only* remaining code case. |

The three built responses live in `Core/GameEnums.cs`: `HazardResponse { SurfaceDefect, StaticObstacle, ForcedSlowdown }`. The category buckets a hit for stats — it does **not** gate behaviour; the scoring/warning fields on the config do.

## The no-code reskin (do this)

1. **Prefab.** A `Hazard` component + a trigger collider, layer `Hazard`, pivot ground-centre. Swap the art. There is **no `kind` field** on the prefab anymore (June 2026 refactor) — one prefab can serve any archetype; the config decides what it does. Author it at the size you want; the spawner grounds it (see `spawnHeight`) so it won't be forced to scale 1, float, or sink.
2. **Config.** Create a `HazardSpawnConfig` (Assets → Create → Kenya Scooter → Hazard Spawn Config). It carries **both halves**, verified in `Config/HazardSpawnConfig.cs`:
   - *Definition / on-hit:* `response`, `deduct`, `deduction`, `speedScaled` (braking pays), `popupKey`, `warnKey`, plus feel (`speedPenalty`, `shakeStrength`, `wobbleStrength`).
   - *Spawn:* `prefab`, `poolSize`, density (`clustersPer100m`, `densityOverSession` curve, `minClusterGap`, `maxPerSession`), cluster shape (`clusterMin/Max`, `clusterSpacing`, `lateralStagger`, `scaleRange`), and placement (`zones`, `spawnAheadDistance`, `spawnHeight`, `requiredContextTags`, `lateralLimit`).
3. **Labels.** Set `warnKey` / `popupKey`, then add those keys to the location's `SwahiliUI` table with the local term. Empty key = no warning / no popup.
4. **Wire-up.** Add the config to `HazardSpawner.configs[]` in the scene ([[Unity Setup Guide — Scene Wiring]] §Hazards lists the three shipped ones).

That's the whole job. `HazardSpawner` stamps the config onto every instance it spawns (`hazard.definition = config`), and `ScoreManager` / `WarningSystem` / `AnalyticsManager` read it off the hit — so a new asset is a fully working hazard.

## Fair-placement rules already in the spawner (don't re-solve them)

The spawner enforces three fairness gates (`Hazards/HazardSpawner.cs`) so you don't have to script placement — author the config and trust these:

- **No hazards in turns.** A cluster whose footprint (plus a buffer each side) rides road bending more than `turnBendThreshold` (0.5°/m) waits until the road straightens — a pothole mid-bend, while the player is busy steering, is a cheap unfair hit. Deliberately re-introduced; keep it.
- **Lateral clamp.** Every hazard is clamped to ±`lateralLimit` (default 4 m) of the road centre — beyond the drivable road the surface banks up out of reach, so a hazard there floats or clips. The whole cluster, stagger included, stays on road the scooter can actually touch.
- **Cluster spacing.** `minClusterSeparation` (12 m) keeps any two clusters apart **across every hazard type**, so a rock cluster can't land on a pothole cluster from a different config. Each config also keeps its own (usually longer) `minClusterGap`.

Per-tile knobs override these locally: a tile can disallow a hazard (`RoadTile.allowedHazards`) or scale its density (`RoadTile.hazardDensity`, 0 = keep clear).

## Optional: a warning cone marker

If a hazard is hard to spot (the flat pothole quad is), assign `warningPrefab` on the config. The spawner drops one marker per cluster, `warningLead` metres up-road toward the player (`warningLateralOffset` nudges it sideways). It is **purely cosmetic** — `HazardMarker.cs` + the pool's `onCreated` switch **off** every collider, so it never scores, hits or blocks; it rides the curve and rewinds exactly like a hazard. Any plain prefab works with no setup. See [[Hazard Warning Marker]] for the design note.

## Preserve the shipped Kenya values (if you reauthor those configs)

| Hazard | response | speedScaled | deduction | warnKey |
|---|---|---|---|---|
| Pothole | SurfaceDefect | true | 50 | WARN_POTHOLE |
| Rock | StaticObstacle | false | 35 | WARN_ROCK |
| Speed bump (unmarked) | ForcedSlowdown | true | 100 | WARN_BUMP |

(Crossing entities — livestock, pedestrians — do **not** use this pool; they ride a tile through the collision pipeline. Animals were cut for scope; pedestrian crossings are their own spawner.)

> [!warning] Honesty / verification (before you ship a hazard)
> - Compile in the live Unity project — these scripts are source-only in the mirror until then.
> - The EditMode tests in `Tests~/Editor/` are **ignored** by Unity (trailing `~`); never claim "tests pass". Verify a hazard by driving the scene: it grounds flush, clusters leave a threadable path, none land in a turn, and the cone (if any) sits up-road.
> - The `HazardSpawnConfig` **assets** for the current location must actually exist and be wired into `HazardSpawner.configs[]`; a config that exists in code but has no asset instance spawns nothing.

---

## Connections
- [[Location Pack — Authoring Guide and Asset Contracts]] — §5 the full Hazard Contract (archetypes, code-verification table, worked example). **Read this for depth.**
- [[Unity Setup Guide — Scene Wiring]] — §Hazards: the prefab/collider/layer setup and the three shipped configs.
- [[Hazard Warning Marker]] — the cone-marker design note.
- [[Handover — What You Can Change vs What Needs a Developer]] — hazards are in the developer column.
- Code: `Config/HazardSpawnConfig.cs` · `Hazards/HazardSpawner.cs` · `Hazards/Hazard.cs` · `Hazards/HazardMarker.cs` · `Core/GameEnums.cs` (`HazardResponse`, `SpawnZones`).
