# Kenya Scooter Game (Drive Through Kenya)

An educational driving game built in **Unity 6 (URP)** for an **Android tablet**, developed by Ryan Putman during a graduation internship at [De Ontdekfabriek](https://www.deontdekfabriek.nl/) (2025-2026). Children of about 12 to 18 ride a boda boda electric scooter on Kenyan roads in a **team relay**: each child drives about two minutes, tilting the tablet to steer, then hands the tablet to the next teammate. Safe driving (correct lane, no speeding, yielding to pedestrians, clean overtakes) scores points for the shared team total.

> **New here? Start with the [`HANDOVER/`](HANDOVER/) folder.** It contains everything needed to run, maintain and rebuild the game without the original developer: the build and signing checklist, tablet setup, the facilitator guides, and the privacy note.

---

## Quick facts (verified against this repository)

| | |
|---|---|
| Unity version | **6000.3.10f1** (open with exactly this version if possible) |
| Render pipeline | URP 17.3.0 |
| Input | Unity Input System 1.18 (tilt steering + touch zones; WASD in the editor) |
| Target platform | **Android tablet** (tested on a Samsung Galaxy Tab A8), landscape only |
| Scripting backend | IL2CPP, ARM64, min API 25 |
| Unity project folder | `De Ontdekfabriek Internship/` |
| Game code | `Assets/Impact Makers Around The world/Scripts/` , namespace `KenyaScooter` |
| The playable scene | `Assets/Scenes/Impact Makers Around The World Kenya.unity` (the ONLY scene in Build Settings) |

> **Scene warning:** the project contains many old prototype scenes with similar names (`...Kenya turning`, `Overtake*`, `endlessRunner`, `EndlessV2`, `Tiles`, `turn test`, and a `_Recovery/` folder). None of them are the game. Open only the scene named above.

---

## Opening and building the project

1. Install Unity Hub, then Unity **6000.3.10f1** with the **Android Build Support** module (including *Android SDK & NDK Tools* and *OpenJDK*).
2. In Unity Hub, add the folder `De Ontdekfabriek Internship/` and open it. The first import takes a while.
3. Open the playable scene (path above) and press Play. Keyboard works in the editor; tilt steering needs a device build.
4. To ship to a tablet, follow **[`HANDOVER/01 Build Keystore and APK Rebuild Checklist.md`](HANDOVER/01%20Build%20Keystore%20and%20APK%20Rebuild%20Checklist.md)** exactly. Signing matters: an APK signed with a different key cannot update the installed app.

---

## How the game works (short version)

- **World-moves-player:** the scooter never moves forward; the world scrolls toward it (`WorldSpeed`). The travel frame (`RoadDirection`) is a constant; on turn tiles the road bends around the player rather than the player rotating through the world.
- **Everything is pooled:** road tiles, traffic, hazards and pedestrians are pre-allocated and recycled; no runtime Instantiate/Destroy.
- **Data-driven config:** every tunable lives on a ScriptableObject config asset (`Scripts/Config/`), read by a matching manager.
- **Facilitator settings menu:** staff hold the **top-left corner of the screen for 2 seconds** (F8 in the editor) to open an in-build settings menu (about 38 settings in Dutch, plus actions like steering recalibration and leaderboard reset). Choices are stored in `PlayerPrefs` and re-applied onto the live config instances at startup, before any manager reads them (`Scripts/Settings/`), so nothing needs a rebuild.
- **Relay flow:** a turn ends at the charge station; the hand-off screen carries the team total and class standings to the next player. Team names come from a fixed pool of one-tap chips; children never type.
- **Self-bootstrapping managers:** most systems create themselves at runtime if not placed in the scene, so the scene needs minimal wiring.
- **Kiosk hardening:** `OrientationLock` pins landscape; `KioskLock` stops the Android back button leaving the app. The real lock is Android **screen-pinning**, documented in the handover docs.
- **Local-only data:** the leaderboard and the usage counters live in `PlayerPrefs` on the device. No network calls, no personal data (see `HANDOVER/05`).

### Script folders

`Assets/Impact Makers Around The world/Scripts/` : `Core/` (game state, events, world speed, orientation and kiosk locks) · `Player/`, `Controls/`, `Cameras/` (riding, tilt input, camera rig) · `Roads/` (tile sequencing, turns, junctions) · `Traffic/`, `Hazards/` (vehicles, potholes, pedestrians) · `Scoring/`, `Session/`, `SafetyNet/` (points, relay, leaderboard, rewind) · `Settings/` (the facilitator menu and override store) · `UI/`, `Feedback/`, `FX/`, `Audio/`, `Config/`.

---

## Documentation

- **[`HANDOVER/`](HANDOVER/)** : the operational handover set (build + signing, tablet setup, what staff may change, privacy, facilitator guides, acceptance test).
- **`De Ontdekfabriek Internship/FACILITATOR — Changing the Game.md`** : the Dutch guide to every setting in the facilitator menu.
- In-project developer docs: `Scripts/README — Architecture and System Map.md` and the `HOW TO ADD A TURN` / `HOW TO ADD A JUNCTION` guides at the project root.

## Legacy content in this repository

The repository also contains early prototype folders that are **not part of the shipped game**: `openworld/` (a separate standalone prototype), and inside the Unity project `Endless Runner/`, `RunnerV2/`, `pickup game/`, `Open world/`, and several imported art packs that the final build may not use. See `HANDOVER/02` for the asset-licence status of the imported packs before redistributing anything.

---

*Developed by Ryan Putman, internship project at De Ontdekfabriek, 2025-2026.*
