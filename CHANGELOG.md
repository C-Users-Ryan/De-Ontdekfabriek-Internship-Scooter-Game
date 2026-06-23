# Changelog

All notable changes to the Kenya Scooter Game (`KenyaScooter`) codebase.
Format based on [Keep a Changelog](https://keepachangelog.com/). This entry covers the final feature and fix push.

## [Unreleased] - 2026-06-21

### Added
- **Diegetic HUD cluster** (`UI/DiegeticHud.cs`): code-generated instrument cluster for iPad 4:3 (score odometer, top-semicircle speedometer, battery-as-timer, speed-limit roundel, warning LEDs, top route/day strip). Built via `Tools > Kenya Scooter > Build HUD Cluster`.
- **Score punch** (`UI/ScorePunch.cs`): punch and flash on a score gain.
- **Framing screens** (`UI/KenyaMenuScreens.cs`): Title, Relay hand-off, Journey Complete (with class scoreboard), Game Over. Deterministic top-down layout, screen fade-in. Built via `Tools > Kenya Scooter > Build Menu Screens`.
- **Team-name chips**: one-tap Swahili animal names (chips only, no free typing) shown once per group.
- **Continuous team score**: the HUD odometer shows the running group total so the score carries between players.
- **Shared multiplier**: the clean-overtake streak persists across players within a group; resets on a new group or any violation. Always-visible, tier-coloured multiplier badge.
- **`GroupReset`** event (`Core/GameEvents.cs`), raised by `GroupScoreManager.ResetGroup`.
- **`IllegalOvertake`** event + `TrafficVehicle.PassOnCorrectSide`: wrong-side passes earn no points and trigger a corrective warning.
- **Crash vignette**: red screen-edge flash on a hard crash.
- **Pothole variants**: four hand-built low-poly pothole textures (`Prefab/hazards/Pothole_LP_*.png`) plus `Hazards/PotholeVariant.cs` (random texture + mirror per spawn).
- **Editor tooling**: `Tools > Kenya Scooter > Wire Scoring Managers` (optional, since the managers self-create).

### Changed
- **Speedometer** rebuilt as a top semicircle with the needle and fill driven from one value (cannot drift apart); lowered so the dial reads as one unit.
- **Panel** sits flush to the bottom with rounded top corners; route/distance strip moved to the top of the screen; the orange accent line is now an inset rounded stripe.
- **Odometer** framed with a lighter bezel for contrast.
- **Battery** colours flipped: green at the top, warning colours at the bottom (matches the top-down drain).
- **Warnings** reworked into a two-layer model (named-rule banner + peripheral LEDs); named hazard messages (KUIL / OBSTAKEL / DREMPEL); warning direction (Blijf links / Blijf rechts) now read from `RoadSideConfig`.
- **Wrong-lane warning** fires only after excessive time on the wrong side, with an immediate trigger when hugging the far edge.
- **Relay "next player"** continues straight into the next turn instead of returning to the title/setup screen.
- **`GroupScoreManager`** and **`StreakSystem`** now **self-create at runtime** if absent from the scene, so the team score and multiplier work with zero scene wiring. `StreakSystem` falls back to a default multiplier ladder when no `ScoreConfig` is set.
- **`ScoreManager`** auto-finds or creates a `StreakSystem`.
- **`LeaderboardManager.CommitGroup`** stores each group under its chosen team name.

### Fixed
- **Rewind desync** where the object you hit appeared to follow you back and traffic clipped through itself: `RewindSystem` was silently dropping participants past a fixed cap, so they were never rewound. Buffers now grow to fit every participant (registration only, allocation-free recording preserved).
- **Multiplier stuck at 1x and score not carrying between players**: root cause was that `GroupScoreManager` and `StreakSystem` were never placed in any scene. Fixed by the self-create change above.

### Removed
- The unlabelled grace-shield indicator from the top route strip (read as clutter; the grace event already has its own feedback).

### Scene setup still required (not code)
- Disable the legacy UI so it does not double up with the new HUD/screens: `HUDController`, `CheckpointScreen`, `EndScreen`, `ThemedEndScreen`, `WarningSystem`.
- Assign a checkpoint tile on `RoadSequencer` (and wire `CheckpointController`) so the timer coasts to a charge station instead of cutting to the finish.
- Add `PotholeVariant` to the pothole prefab and assign the four `Pothole_LP_*` textures (the round one is already the default texture).

### Notes
- Original pothole photo backed up as `Prefab/402-4020061_pothole-pothole-png.png.original.bak`.
- None of the above has been user-tested; legibility and a play test on the iPad with 12 to 18 year olds remain the open validation step.
