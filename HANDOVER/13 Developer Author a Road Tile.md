# GUIDE — Dev: Author a Road Tile

**Sprint:** 8 · 2026-07-13
**Author:** Ryan Putman · De Ontdekfabriek
**Audience:** a developer with Unity open.
**Status:** thin task-card. The full authoring flow already lives in [[Road System — Step by Step Guide]] — this card is the one-page mental model plus a maintainer note; it does not restate the click-by-click steps.

> [!abstract] In one line
> A road is a chain of pooled **tiles**; you author one `RoadTile` (place its balls), drop it into a `RoadSequence`, and the `RoadSequencer` streams it. Read [[Road System — Step by Step Guide]] for the actual clicks; read this for *what a tile is* and the *one thing a maintainer must not undo*.

---

## What a tile IS

Everything a tile is lives on one component, `Roads/RoadTile.cs`. There are no metre or angle fields — you author with **points you place in the Scene view**, and every derived number (length, bend, radius) falls out of where the balls sit.

- **Points (draggable balls):** a **blue Begin Point** (where the chain attaches to the previous tile's exit), an **orange Exit Point** (where the next tile attaches), and any number of **turns**. Each turn is a **pair** — a **green ball** where the bend *starts* and a **red ball** where it *ends*. The driven line runs straight to green, curves green→red (a 16-piece Hermite arc, aimed at the next point), straight to the next turn or the exit. An S-curve is two pairs; a slalom is three.
- **Checkpoint:** tick `isCheckpoint` and drag the **purple Stop Point** — where the scooter rests at the charge station. `StopRunDistance` measures the rest point *along the road* (not straight-line), so it stays right through a curve.
- **Per-tile surface:** `surface` = `Paved` or `Dirt`. Dirt is **feel + FX only** — it does **not** change how the tile looks; it drives rumble and extra dust. See [[Dirt Roads — Communicating Surface Through Dust, Not Shaking]].
- **Per-tile roughness:** `hazardDensity` (0–4, 1 = normal) scales how many potholes/rocks land on this tile. This — not the surface flag — is how you make a dirt stretch actually ride rough. `allowedHazards` (empty = all) whitelists which `HazardSpawnConfig` assets may spawn here.

`RoadTile.EvaluateRun(u, …)` is the single source of truth for the tile's shape: the sequencer chains tiles with it, the generated mesh samples it, the lean/bank read its bend. What you see (the cyan gizmo line) is exactly what the scooter drives.

## How it reaches the road

1. Author the tile (see [[Road System — Step by Step Guide]]). If it carries a `CurvedRoadMesh`, the painted strip regenerates along your bend — see [[Curved Road Mesh — Corner Tiles]]. Hand-modelled corner art instead: place the points along the art (see [[Crossroads Turn — Wiring Pack Art as a Turn]]).
2. Add the tile prefab to a **`RoadSequence`** (its `tiles[]` array). A sequence is a zone/region; the sequencer picks sequences by a weighted tag grammar (or an ordered Regio-reis route), then lays their tiles head-to-tail — each tile's Begin Point stamped onto the previous tile's Exit Point in a stable "road space".
3. `RoadSequencer` re-places the whole chain every frame so the player's current point sits at the world origin facing +Z. The world bends around the stationary player — that is the turn.

## Maintainer note — the per-frame tile budget (do not remove)

`Roads/RoadSequencer.cs` streams **at most `TilesPerFrame` (currently 2) tiles per frame during play** (`BuildAhead(TilesPerFrame)` in `Update`). Entering a turn can need several short turn tiles at once, and each one that carries a `CurvedRoadMesh` rebuilds its mesh on spawn — landing all of that on a single frame is what caused the turn lag spike (fixed 2026-07-13). The budget spreads those spawns over a few frames; because `spawnHorizon` (160 m) sits far past the camera, refilling 1–2 tiles/frame is invisible.

Two things keep this safe, so don't "simplify" them away:
- The pre-play fill (`HandleSessionReset`) and the checkpoint cap (`SpawnCheckpoint`) deliberately pass a **large** budget (`MaxTilesPerFill`) so the road is fully present before it is needed — only the per-frame `Update` path is budgeted.
- `BuildAhead` still breaks when a spawn adds no tile (`activeTiles.Count == before`), the zero-length-tile backstop that stops the loop ever freezing.

> [!warning] Honesty
> This budget change is **source-only in the vault mirror** and needs a **Unity recompile in the live project** to take effect; it has not been device-tested. The EditMode road tests live in `Tests~/Editor/` (trailing `~`), so Unity ignores them — never read a green Test Runner into this, there isn't one.

## Connections
- [[Road System — Step by Step Guide]] — the full click-by-click authoring flow (this card's parent).
- [[Turn System v4 — The Clean-Slate Rebuild]] — why the model is points-only, and what was deleted.
- [[Curved Road Mesh — Corner Tiles]] — the generated strip that follows your bend.
- [[Crossroads Turn — Wiring Pack Art as a Turn]] — placing points along hand-modelled corner art.
- [[Dirt Roads — Communicating Surface Through Dust, Not Shaking]] — `surface` / `hazardDensity` feel.
- [[Build Changelog — Final Features and Fixes]] — the dated per-frame-budget entry.
- Code: `Roads/RoadTile.cs`, `Roads/RoadSequencer.cs`.
