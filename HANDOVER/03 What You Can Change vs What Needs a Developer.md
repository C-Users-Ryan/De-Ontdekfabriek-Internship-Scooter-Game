<!-- Handover copy delivered with the repository. During the internship the master version lives in the developer's project vault and is mirrored here; after handover this folder is the authoritative copy. -->

# Handover: What You Can Change Yourselves vs What Needs a Developer

**For:** De Ontdekfabriek staff (the "yours" column) and any future maintainer (the "developer" column).
**What it is:** the boundary line. It says exactly which parts of the game staff can safely change from inside the build, and which parts can only be touched by someone with Unity, so no one breaks the working build trying to edit the wrong thing.
**Why it exists:** the game was deliberately split so the everyday adjustments live behind a safe, child-proof menu, and the risky structural changes stay in code. Knowing the line means staff can confidently tune the game and never accidentally break it.

> **Kort gezegd (voor begeleiders)**
> Alles in het **instellingenmenu** (linkerbovenhoek 2 seconden vasthouden) is veilig en kun je zelf aanpassen, het kan het spel niet stukmaken. Alles wat NIET in dat menu staat (nieuwe wegen, nieuwe hindernissen, teksten, plaatjes, geluiden, de regels van het spel) heeft een ontwikkelaar met Unity nodig. Twijfel je? Als je het in het menu kunt vinden, mag je het veranderen. Kun je het niet vinden, vraag dan een ontwikkelaar.

---

## Column 1: what you can change yourselves (no Unity, no risk)

Everything here is reachable from the **facilitator settings menu**: hold the top-left corner of the screen for 2 seconds (F8 on a computer). Your choices are saved and survive a restart, and there is always a reset, so nothing you do here can break the game. Full per-setting detail is in [FACILITATOR: Changing the Game (Dutch settings guide)](../De%20Ontdekfabriek%20Internship/FACILITATOR%20%E2%80%94%20Changing%20the%20Game.md).

- **How hard the game is**: number of hazards, amount of traffic, how strict the wrong-lane and speeding rules are.
- **How the scooter feels**: cruise speed, top speed, acceleration, braking, how far it leans.
- **The safety net**: how many times a turn can rewind after a crash (0 to 3), collision-absorb charges.
- **Controls**: tilt sensitivity, dead zone, invert steering, motion sensitivity (for motion-sensitive children).
- **Sound**: master, music and ambient volume, vibration on/off.
- **Scoring rules**: which deductions and bonuses are on, the collision penalty value, overtake rewards.
- **What is in the world**: oncoming traffic amount, pedestrian-crossing frequency (down to off), drive on the left (Kenya) or right (Dutch).
- **Look of the world**: day/night cycle on/off and a fixed time of day.
- **Session pacing**: turn length, and the wait time on the hand-off and end screens.
- **Facilitator actions**: recalibrate the steering (re-zero a drifted tilt), and clear the leaderboard for a new class.

If you can find it in the menu, it is safe to change. If you cannot find it in the menu, it belongs in Column 2.

---

## Column 2: what needs a developer (Unity required)

None of this is reachable from inside the build. Changing any of it means opening the project in **Unity 6000.3.10f1**, editing, and **rebuilding and re-signing the APK** (see the [build checklist](01%20Build%20Keystore%20and%20APK%20Rebuild%20Checklist.md)). It is not dangerous, it just cannot be done on the tablet.

- **The road itself**: adding turns, junctions, S-curves or new road tiles; changing the order of zones (town vs countryside). Authoring tools exist for a developer (`Tools > Kenya Scooter > Create Crossroads Turn Tile / Create Ready Junction`), but they run in Unity.
- **New hazards or new content**: a new kind of obstacle, a new vehicle, new roadside props (stalls, animals, windmills), a new location.
- **Art and models**: any 3D model, texture, the road materials, the red-dirt look beyond the day/night dials.
- **Text and language**: the on-screen words (Dutch/Swahili strings), screen labels, the team-name pool.
- **Sound files**: replacing the actual audio clips (the volume sliders are yours; the clips themselves are a developer's job).
- **The game's rules and structure**: how scoring is calculated, the relay flow, the HUD layout, what a setting's min/max range is.
- **App-level settings**: the app name and icon, the Android version it targets, the signing key, anything in Player Settings.

> **The one thing never to do without a developer**
> Do not try to "update" or reinstall the app from anywhere except the official signed APK in the handover package. A different build (or a differently-signed one) will not install over the existing app and will wipe the leaderboard. New versions only ever come from a developer who rebuilds with the original keystore.

---

## The grey area: settings vs content

The menu changes **how much** and **how strict** (numbers and on/off switches). It cannot change **what things are** (which models, which words, which roads). That is the line:

- "More traffic" or "no pedestrians" or "drive on the right": **yours** (a number or a switch).
- "A different car model" or "a market stall here" or "this sign in English": **developer** (new content).

A future developer can move items from Column 2 into Column 1 by adding them to the settings catalog (one entry per setting; the hook and the method are documented in Facilitator Settings Menu and the Override Architecture). Until then, the line above is the safe boundary.

---


---

