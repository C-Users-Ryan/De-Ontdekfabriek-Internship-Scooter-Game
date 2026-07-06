# Road System: Step by Step Guide

Rebuilt 2026-07-05: **the road is authored with POINTS you place in the tile — no metres, no angles.**
Everything lives on the `RoadTile` component; every point is a draggable ball in the Scene view.

---

## How it works in 30 seconds

You (the scooter) never move. You sit at the centre and always face forward; the world scrolls past you, and in a turn the whole world rotates around you. The road is a chain of pooled **tiles**. The game attaches each new tile's **Begin Point** to the previous tile's **Exit Point**, so tiles always line up. Inside a tile the road simply connects the points you placed: **begin → each turn point → exit**, rounding every corner.

---

## The points (all draggable balls in the Scene view)

| Ball | What it is |
|---|---|
| **BLUE — Begin Point** | Where the road enters the tile. The game attaches this to the previous tile's exit. |
| **GREEN — Turn Start** | Each turn has its own green ball: the road runs dead straight until it reaches it — this is exactly where the turn begins. Place it anywhere in the tile. |
| **RED — Turn End** | The same turn's red ball: the bend finishes here, aimed at the next turn's green ball (or the exit). Green→red pairs give you precise control over every turn; add as many pairs as you want, in driving order (a dotted line ties each pair together). |
| **ORANGE — Exit Point** | Where the road leaves the tile. Always yours to place — the next tile attaches here. |
| **PURPLE — Stop Point** | Checkpoint tiles only: where the scooter comes to rest at the charge station. |

The **cyan line** is exactly what the scooter will drive — it redraws live while you drag. How much the road turns falls out of where the points sit (small green/red dots show where each bend starts and ends). **Length** is measured from the points automatically.

Other RoadTile settings: **Difficulty** (sequence authoring metadata) and **Allowed Hazards** — drag in the HazardSpawnConfig assets permitted on this tile (empty = all hazards allowed).

---

## Make a turning tile (60 seconds)

1. `Tools > Kenya Scooter > Road Tiles and Seams > Create Straight Road Tile` (or open any existing tile prefab).
2. On its **RoadTile**, open **Turns** and press **+**. A green + red ball pair appears on the tile, already bending the road so you can see it.
3. **Drag the green ball to exactly where the turn should begin** and **the red ball to exactly where it should end** — for a city corner, green just before the junction, red on the side street. Then **drag the orange exit ball to where the road should leave**. The road runs straight → bends between your two balls → straight to the exit.
4. Wider or tighter bend? Just move the balls further apart or closer together — the curve follows the points.
5. If the tile has a **CurvedRoadMesh**, the painted road regenerates along the bend automatically. Tiles with hand-modelled road art: place the points along the art's road — the cyan line shows the match.
6. Save the prefab and put it in a `RoadSequence` like any other tile. Wherever the generator places the tile, that exact road happens.

An S-curve is two pairs. A slalom is three. There is nothing else to learn.

## Checkpoint tile

Tick **Is Checkpoint** and drag the **purple stop ball** to the charge bay. Done.

---

## What NOT to look for (deleted in the 2026-07-04/05 rebuild)

- ~~TileTurn component~~ · ~~turn prefabs~~ · ~~TurnScheduler~~ · ~~RoadPath segments~~ · ~~junctions / branch carriers~~ · ~~Curve Angle~~ · ~~begin/end metres and degrees fields~~ · ~~exit anchors / stop markers~~ — all gone. If an old note mentions them, the note is stale; this file and the tooltips on `RoadTile` are current.
