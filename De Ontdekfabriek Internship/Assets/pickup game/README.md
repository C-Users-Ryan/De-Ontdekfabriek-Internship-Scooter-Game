# Capsule City — quick prototype

A tiny game: you're a capsule, drive around a city, pick up other capsules, and
deliver them to a drop-off point. A floating arrow points you to the nearest
capsule, and to the drop-off once you're carrying one.

Copy the four scripts in `Scripts/` into your Unity project's `Assets/` folder,
then wire up a scene like this.

## Scene setup

### 1. Player
- Create a **Capsule** (`GameObject ▸ 3D Object ▸ Capsule`).
- Add a **Rigidbody** (the `CapsulePlayer` script adds one automatically and sets
  it up: gravity off, rotation frozen).
- Add the **`CapsulePlayer`** script.
- The capsule's own Capsule Collider stays **solid** (not a trigger) so it bumps
  into buildings.
- Drive with **WASD / arrow keys**. Tick *Camera Relative* if you use a rotating
  follow camera; leave it off for fixed/world-axis movement.

### 2. Capsules to collect
- Make a capsule prefab, add a collider, and add the **`CapsulePickup`** script
  (it flips the collider to *Is Trigger* for you).
- Scatter a few around the city. They register themselves automatically — no list
  to maintain.

### 3. Drop-off point
- Create any object (a glowing pad, a zone, etc.) with a collider.
- Add the **`DropOffPoint`** script (it sets *Is Trigger* for you).
- Make the trigger big enough to drive into.

### 4. Navigation arrow
- Create an arrow mesh (or an empty with an arrow mesh as a child) that points
  along its **local +Z (blue) axis**.
- Add the **`NavigationArrow`** script.
- Assign **Player** = your capsule. Optionally assign **Arrow Visual** if the mesh
  is a child you want rotated separately.
- It floats above the player, swings to the nearest capsule, then to the drop-off
  while carrying, and hides itself once everything's delivered.

## Tuning
- `CapsulePlayer.moveSpeed` / `turnSpeed` — how fast the capsule drives and turns.
- `CapsulePlayer.carryCapacity` — how many capsules you can hold before delivering
  (default 1; raise it to carry several at once).
- `NavigationArrow.hoverHeight` / `turnSpeed` — arrow height and how snappy it
  swings.

## Reading state from code
`CapsulePlayer` exposes `Carried`, `Delivered`, `IsCarrying`, and `IsFull` — hook a
HUD or win condition onto those (e.g. "win when `Delivered == total capsules`").
