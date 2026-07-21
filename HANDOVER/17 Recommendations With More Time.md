# RECOMMENDATIONS: What I Would Work On With More Time

**For:** the next developer (the technical column) and De Ontdekfabriek management (the "what this means for you" column).
**What it is:** an honest, opinionated hand-off note on the state of the Kenya Scooter Game — what is solid and should be **kept as-is**, what must **change or be finished before a real deployment**, and what to **keep in mind** as risks that are not bugs. It is deliberately short. Every technical claim is grounded in the code at `Code/KenyaScooter (current)/`.
**Why it exists:** so the person who takes over does not have to rediscover, by trial, which parts of this project are load-bearing and which are prototype-grade. Read [[HANDOVER — Start Here]] first for the map; this is the "if I had another month" note that sits beside it.

> [!info] The one honest sentence
> The game is **well-architected and playable on PC**, but it is **not a finished product**: it has never been verified on the target tablet, it ships **silent**, and its final packaging (app id, keystore, orientation lock) is not done. The architecture is the strong part; the "last mile" to a deployable exhibit is the work that remains.

![Keep / change-or-finish / keep-in-mind triage](Diagram%20-%20recommendations%20triage.svg)

*Keep / change-or-finish / keep-in-mind at a glance — the last mile from a PC-playable prototype to an exhibit De Ontdekfabriek can run unattended.*

---

## KEEP — what is solid, do not rewrite

These are the decisions I would defend. They are the reason the game is maintainable by someone who is not me, and they should survive any future work untouched.

- **The data-driven architecture.** A new hazard, road tile, or session mode is an **asset, not a code change**. Hazards live entirely on a `HazardSpawnConfig` (the `HazardKind` enum is gone); a road tile is one `RoadTile` with its own length, turns and allowed hazards; every tunable number is a ScriptableObject, never hardcoded in a MonoBehaviour. This is what lets a future intern or school group extend the game without touching the hot loops. See the README's "Folder → namespace map" and [[Road System — Step by Step Guide]].
- **The facilitator settings layer** — the declarative `SettingsCatalog` rendered by `SettingsMenu`, the safe override architecture, one-tap **Profielen** presets, the numeric PIN gate (`FacilitatorLock`), and the **PIN recovery** path (visible "Code vergeten?" reset → default `3683`). This is not a nicety; it *is* the project's validation strategy — facilitators tune the game in the field where a lab test cannot reach. See [[Facilitator Settings Menu and the Override Architecture]] and [[Validation Strategy — Tuning in the Field, Not the Lab]].
- **Point-based `RoadTile` turns.** The 2026-07-04 clean-slate rebuild deleted ~19 turn/junction scripts and made the tile *the* system: a turn is a begin-point / end-point pair with a degree value, and what you author is **always** ridden exactly — no silent overlap guard flattening authored turns. Do not reintroduce the old junction stack. See [[Turn System v4 — The Clean-Slate Rebuild]].
- **The single-fixed-camera rule.** One camera writer in `LateUpdate` (`CameraRigController`); `CameraShake` and `RealRiderMode` only expose values. This was hard-won: on this fixed-camera game, **anything that paints the screen, distorts the lens, or moves the camera reads as broken** (a speed-dive, chromatic aberration and a velocity rattle were all built and cut). Keep the camera boring. See [[SC1 — Real Rider Mode — Horizon Lock]].
- **The honesty guardrails** in the docs. "Done in the mirror" ≠ "working in a build"; EditMode tests under `Tests~/` are ignored by Unity so no "tests pass" claim is ever made; nothing is called device-tested until it is. Keep writing this way — it is what makes the rest trustworthy.

---

## CHANGE or FINISH — before this is deployed for real

None of these is a redesign. They are the last-mile items between "a playable prototype" and "an exhibit De Ontdekfabriek can run unattended." I would do them in roughly this order.

1. **Set the real Android app id, create & back up the keystore, lock LandscapeLeft — then build the final signed APK.** Today the build carries the URP-template default app id and there is no signing keystore. The **keystore is irreplaceable**: lose it and the app can never be updated in place again, only wiped and reinstalled — and a differently-signed APK **erases the per-tablet leaderboard** (see KEEP IN MIND). This is the single highest-stakes handover action. Full recipe: [[Handover — Build, Keystore and APK Rebuild Checklist]].
2. **On-device kiosk + gyro-sign test.** `KioskLock` (swallows the Android back button), `OrientationLock`, and the charging-state `HandheldDetector` have **never run on the target tablet** — they are structurally sound but hardware-unverified. The gyro roll math (`GyroTiltProvider.RollForOrientation`) still needs an on-device **sign check**; calibration absorbs constant error and `invertGyro` covers a flipped sign, but only a real device confirms it. Run [[Handover — Facilitator Acceptance Test (15 Minutes)]] on the actual tablet.
3. **Import the ~30 audio clips — the game ships silent.** `AudioManager` is fully wired and null-safe; the empty clip slots in `AudioConfig` are the only reason there is no sound. Import the clips **with a per-clip licence manifest** (source + licence recorded next to each clip) — the asset-licence review already flags this as a real risk. Sources and clip list: [[Audio Sourcing Pack — Royalty-Free Clip Sources]] and [[Kenya Soundscape — Audio Clip Spec]].
4. **Rename `Tests~/` to `Tests/` so the EditMode tests actually run.** The `RoadTileTests` and `Settings` suites are written but the trailing `~` makes Unity ignore the folder entirely. Until it is renamed, "tests" is a folder of dead code, not a safety net.
5. **Write and follow a documented one-way mirror → live sync checklist.** The vault mirror (`Code/KenyaScooter (current)/`) is source-only; features land there first and must be copied into the live Unity project (`Assets/Impact Makers Around The world/Scripts/`) and recompiled. Drift between the two is the **leading cause of "the feature exists but isn't in the build."** A short, always-`git diff --no-index`-first checklist would prevent it. (See the "Shipped in the July 2026 pass" list below — every item there is currently mirror-and-live but still needs a Unity recompile.)
6. **Native-Swahili review.** Strings marked `TODO: native review` in `SwahiliUI` deliberately fall back to English rather than risk invented Swahili — the right call, but the gap should be closed by a native speaker before Kenya-facing use. The `UIStringsConfig` asset already lets this happen outside code.

---

## KEEP IN MIND — risks that are not bugs

Things a future owner should understand before they are surprised by them. None is broken; each is a consequence of a deliberate scope choice.

- **The leaderboard is per-tablet, local `PlayerPrefs`** (`ksg.leaderboard`) — not cloud, not shared between tablets, and **wiped by a re-signed APK**. Clear-on-purpose lives in the settings menu; loss-on-resign is a build-hygiene hazard tied to the keystore point above.
- **The deployed control format is still an open decision** — plain Android tablet (handheld or in a holder, which is what the code and stakeholder feedback assume) versus a steering-wheel / Arduino rig. If a hardware rig ever becomes part of the exhibit it needs its **own** setup-and-maintenance doc and becomes an extra failure point. Confirm the format before final packaging.
- **Older-tablet performance is untested at scale.** `PerformanceMode` exists and trades atmosphere (post-FX, shadows, render scale, prop density) **never gameplay**, so scores stay comparable across devices — but it has not been profiled on a weak exhibition tablet with a full roadside-life scene. See [[Performance Modes — Trading Atmosphere, Never Gameplay]].
- **Several "done" items are mirror-only until synced and recompiled.** "Built" in this project means "written in the mirror and, at best, copied to live." Nothing is confirmed in a running build until Unity recompiles it — treat the list below as *pending a recompile*, not as live-verified.

---

## Shipped in the July 2026 pass

So the reader knows the current state: these were built this session, edited in the mirror **and** copied to the live project (SHA256-verified byte-identical where noted). **They still need a Unity recompile to take effect, and none is on-device tested.** Detail in [[Build Changelog — Final Features and Fixes]] and [[_FIX PLAN — July 2026 Issues]].

- **Kiosk attract demo is now collision-immune** — `AttractMode.DemoActive` gates an early-out in `PlayerCollisionHandler`, so the self-play demo can no longer hard-crash and reset to title.
- **Visible two-tap "Code vergeten?" reset** on the facilitator PIN keypad (`SettingsMenu`), resetting to the default `3683` — no more silent lock-out.
- **Road-tile per-frame budget** (`RoadSequencer.BuildAhead(maxThisCall)`) kills the lag spike when several short turn tiles spawn on one frame.
- **"Profiel gewijzigd" confirmation toast** on applying a settings profile / custom slot (`SettingsMenu.ShowToast`).
- **New Endless / "Vrij rijden" mode** — a solo run with **lives** (default 3, `SessionConfig.endlessLives`) instead of the timer, started by a "VRIJ RIJDEN · ENDLESS" button on the Title. No team select, no relay, **commits nothing to the class leaderboard**; lives + distance shown by a small `EndlessHud` overlay. (`GameMode` enum, static `GameManager.Mode` / `LivesRemaining`, `TimerManager` guard.)
- **Typed team name** — an "EIGEN NAAM" card on team-select opens an in-app keyboard (`KenyaMenuScreens.NameEntry`); the typed name flows everywhere a team name is used (relay / journey / game-over / leaderboard).
- **Settings search box** at the top of the facilitator settings sidebar (`BuildSearchField`).
- **Car tail lights** — two night-aware corner tail lights per car. **Needs a recompile and a dusk drive to confirm** the night-brightening and brake flare read correctly.

---

> [!abstract] Evidence (for the portfolio)
> **What it is:** the reflective hand-off — a developer being honest with the next developer and with management about what is solid, what is unfinished, and what is a lurking risk. It closes the gap between "it works on my PC" and "someone else can deploy and own this," which is the whole point of the handover set.

## Connections
- [[HANDOVER — Start Here]] — the role-ordered front door to the whole handover set
- [[_FIX PLAN — July 2026 Issues]] — the grounded investigation behind the July pass and the item numbers above
- [[Handover — Build, Keystore and APK Rebuild Checklist]] — the app-id / keystore / orientation / signing recipe
- [[Handover — Facilitator Acceptance Test (15 Minutes)]] — the on-device watch-do-not-help test
- [[Audio Sourcing Pack — Royalty-Free Clip Sources]] · [[Kenya Soundscape — Audio Clip Spec]] — filling the silence
- [[Improvement Ideas — Derived from the Oplevering Insights]] — the candidate content backlog (more Kenya, more life)
- [[Handover Risk Analysis — Running the Game Without Unity]] — the ranked risk analysis these recommendations draw on
