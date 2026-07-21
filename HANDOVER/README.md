<!-- Handover copy delivered with the repository. During the internship the master version lives in the developer's project vault and is mirrored here; after handover this folder is the authoritative copy. -->

# HANDOVER: Start Here

**What this is:** the front door to the Kenya Scooter handover. It points you to the right document depending on whether you **run** the game with children, **maintain** the game (a future developer), or are **Ryan finishing the handover** before the internship ends. Open this first; follow the link for your role.

> **The one idea behind everything here**
> De Ontdekfabriek runs the game but does not develop it, and after Ryan leaves nobody on site uses Unity. So the game was deliberately split: everyday adjustments happen safely inside the build (no Unity), and only structural changes need a developer. These docs make both halves runnable without Ryan.

## What the game and the activity is (for anyone new)

**Kenya Scooter** (also called *Drive Through Kenya*) is an educational driving game for an Android **tablet**, made for De Ontdekfabriek. Children roughly 12 to 18 play it, usually as a **team relay**: each child drives for about two minutes, tilting the tablet like a steering wheel to steer (press the right side to go faster, the left side to brake), then hands it to the next teammate, and the team builds a shared score. The road scrolls past a Kenyan landscape; the game rewards safe driving (staying in the correct lane, not speeding, yielding to people crossing) and gently penalises mistakes. It runs as a fixed exhibit: the tablet is mounted or handheld, locked into the game, and a staff member or teacher runs group after group. There is no internet, no login, and nothing to install per child.

> Built by **Ryan Putman** as a graduation internship project, 2026, in Unity 6000.3.10f1. The game is De Ontdekfabriek's to keep and run.

---

## If you RUN the game with children (De Ontdekfabriek staff)

You never need Unity. Start with the two cards, keep them by the tablet:

1. [Facilitator Quick Reference](07%20Facilitator%20Quick%20Reference.md), the daily run-of-show and the "what to do when X" troubleshooting card.
2. [FACILITATOR: Changing the Game (Dutch settings guide)](../De%20Ontdekfabriek%20Internship/FACILITATOR%20%E2%80%94%20Changing%20the%20Game.md), what every setting in the in-build menu does (hold the top-left corner 2 seconds to open it).
3. [What You Can Change vs What Needs a Developer](04%20What%20You%20Can%20Change%20vs%20What%20Needs%20a%20Developer.md), the safe line: anything in the settings menu is yours to change; anything else needs a developer.
4. [Facilitator Guide](06%20Facilitator%20Guide.md), the fuller teaching guide (pedagogy, the debrief) for context.

Setting up a tablet for the first time, or preparing the spare? Use [Tablet Setup and Golden-Image Checklist](03%20Tablet%20Setup%20and%20Golden%20Image%20Checklist.md) (screen-pinning, never-sleep, no auto-update, charging).

**Short task-cards (added July 2026):** [Access Code and Recovery](10%20Facilitator%20Access%20Code%20and%20Recovery.md) — the PIN and the "Code vergeten?" reset; and [Modes and Profiles](11%20Facilitator%20Modes%20and%20Profiles.md) — the two main modes (**Standaard** and **Vrij rijden / Endless**), the **Rustig**/**Uitdagend** variants, and the settings search box.

**New to the project?** Read [About the Project — for De Ontdekfabriek](19%20About%20the%20Project%20for%20De%20Ontdekfabriek.md) first (Dutch, non-technical): a single plain-language overview of what the game is, what it teaches, how you run it, what's safe to change, the honest status, and what to keep safe.

## If you MAINTAIN the game (a future intern or school group, with Unity)

> **If De Ontdekfabriek has no developer right now**
> That is expected. The game **keeps running as-is** with no developer: staff change a lot from the in-build settings menu (difficulty, traffic, sound, drive side, and more) without anyone touching code. You only need a developer to change the **structure** (roads, hazards, art, text). When that day comes, hand a future intern or a school group this whole `HANDOVER` set; the build checklist below is written so a newcomer can install Unity and rebuild from scratch. Nothing here needs Ryan personally.

You will rebuild and re-sign the APK to ship any change. Start here:

1. [Build, Keystore and APK Rebuild Checklist](01%20Build%20Keystore%20and%20APK%20Rebuild%20Checklist.md), the exact rebuild recipe (Unity 6000.3.10f1, URP 17.3, IL2CPP/ARM64, the Android module, signing) and the keystore handover.
2. The project's own `README — Architecture and System Map` and `Unity Setup Guide — Scene Wiring` (in the code repo) for how the game is built.
3. Facilitator Settings Menu and the Override Architecture, how the safe-change layer works and how to expose a new setting to staff.
4. [What You Can Change vs What Needs a Developer](04%20What%20You%20Can%20Change%20vs%20What%20Needs%20a%20Developer.md), the developer column lists what only you can touch (tiles, hazards, content, code).

**New developer or a Fontys student?** Start with [Project Orientation — for Fontys Game-Design Students](20%20Project%20Orientation%20for%20Fontys%20Students.md): the one mental model, the architecture map, the common jobs, the gotchas, and an honest debt list — the fastest way to get productive.

**Short developer task-guides (added July 2026):** [Open the Project and Work in the Scenes](12%20Developer%20Open%20the%20Project%20and%20Work%20in%20the%20Scenes.md) · [Author a Road Tile](13%20Developer%20Author%20a%20Road%20Tile.md) · [Author a Hazard](14%20Developer%20Author%20a%20Hazard.md) · [Tune Other Mechanics](15%20Developer%20Tune%20Other%20Mechanics.md) · [Build and Deploy the APK](16%20Developer%20Build%20and%20Deploy%20the%20APK.md) · [The Settings Menu: Add and Remove Settings](18%20Developer%20Settings%20Menu%20Add%20and%20Remove%20Settings.md). And read [Recommendations: What I Would Work On With More Time](17%20Recommendations%20With%20More%20Time.md) for what is solid, what to finish before deployment, and what to keep in mind.

## If you are RYAN, finishing the handover

The actions only you can do, before the internship ends. Each is detailed in the build checklist; this is the short list:

- [ ] Fix the three pre-final-build settings: real **app id** (not the URP-template default), create the **signing keystore**, set **Default Orientation = Landscape Left**. ([build checklist](01%20Build%20Keystore%20and%20APK%20Rebuild%20Checklist.md) Parts A and B.)
- [ ] Build the **final signed APK** and install + verify it on the tablet (and the spare).
- [ ] Compile and device-test the kiosk back-button lock (`KioskLock`) and confirm screen-pinning holds.
- [ ] Complete the **asset-licence check** — confirm/delete the remaining third-party packs. (The one flagged redistribution-site pack is already resolved: the Arid Desert Environment was replaced with a legitimately purchased copy. [Package Manifest and Asset-Licence Check](02%20Package%20Manifest%20and%20Asset%20Licence%20Check.md).)
- [ ] Transfer **GitHub repo ownership** to an account De Ontdekfabriek controls.
- [ ] Fill in and hand over the [package manifest](02%20Package%20Manifest%20and%20Asset%20Licence%20Check.md) (APK + keystore + passwords + repo + docs).
- [ ] Run the [15-minute facilitator acceptance test](09%20Facilitator%20Acceptance%20Test.md) with one real facilitator (the open validation step from the risk analysis; the linked doc is the exact script).

---

## The whole handover document set

| Document | Audience | Purpose |
|---|---|---|
| [Handover Risk Analysis](08%20Handover%20Risk%20Analysis.md) | all | the analysis behind this handover (what could go wrong, ranked) |
| [Build, Keystore and APK Rebuild Checklist](01%20Build%20Keystore%20and%20APK%20Rebuild%20Checklist.md) | developer / Ryan | rebuild + sign + ship the APK; the build cliff |
| [Package Manifest and Asset-Licence Check](02%20Package%20Manifest%20and%20Asset%20Licence%20Check.md) | Ryan | fill-in package contents + credentials + asset licences |
| [Tablet Setup and Golden-Image Checklist](03%20Tablet%20Setup%20and%20Golden%20Image%20Checklist.md) | staff | lock down a tablet; prepare a spare |
| [What You Can Change vs What Needs a Developer](04%20What%20You%20Can%20Change%20vs%20What%20Needs%20a%20Developer.md) | staff / developer | the safe change boundary |
| [Privacy (AVG/GDPR) and Accessibility Note](05%20Privacy%20AVG%20GDPR%20and%20Accessibility%20Note.md) | De Ontdekfabriek | no personal data / no network; colour-blind-safe cues |
| [Facilitator Quick Reference](07%20Facilitator%20Quick%20Reference.md) | staff | daily operation + troubleshooting |
| [FACILITATOR: Changing the Game (Dutch settings guide)](../De%20Ontdekfabriek%20Internship/FACILITATOR%20%E2%80%94%20Changing%20the%20Game.md) | staff | what each setting does |
| [Facilitator Guide](06%20Facilitator%20Guide.md) | staff | the fuller guide: timing, introduction script, the debrief |
| [Facilitator Acceptance Test (15 minutes)](09%20Facilitator%20Acceptance%20Test.md) | Ryan | the scripted watch-do-not-help test that proves the materials work |
| [Facilitator: Access Code and Recovery (NL)](10%20Facilitator%20Access%20Code%20and%20Recovery.md) | staff | task-card: open the menu, enter/change/disable the PIN, and the "Code vergeten?" reset |
| [Facilitator: Modes and Profiles (NL)](11%20Facilitator%20Modes%20and%20Profiles.md) | staff | task-card: the two main modes (Standaard + Vrij rijden/Endless) and the Rustig/Uitdagend variants, individual switches, the settings search |
| [About the Project — for De Ontdekfabriek (NL)](19%20About%20the%20Project%20for%20De%20Ontdekfabriek.md) | De Ontdekfabriek / management | plain-Dutch master overview: what the game is, what it teaches, how to run it, safe-to-change vs needs-a-developer, honest status, what to keep safe |
| [Developer: Open the Project and Work in the Scenes](12%20Developer%20Open%20the%20Project%20and%20Work%20in%20the%20Scenes.md) | developer | install Unity, the correct playable scene, hierarchy, Tools menu, PC controls |
| [Developer: Author a Road Tile](13%20Developer%20Author%20a%20Road%20Tile.md) | developer | task-card → the Road System step-by-step guide |
| [Developer: Author a Hazard](14%20Developer%20Author%20a%20Hazard.md) | developer | task-card: hazards are data-driven (one `HazardSpawnConfig`) → Location Pack |
| [Developer: Tune Other Mechanics](15%20Developer%20Tune%20Other%20Mechanics.md) | developer | which config governs which mechanic (traffic, day/night, scoring, safety net, endless lives) |
| [Developer: Build and Deploy the APK](16%20Developer%20Build%20and%20Deploy%20the%20APK.md) | developer | task-card: the routine APK update loop → the full build checklist |
| [Recommendations: What I Would Work On With More Time](17%20Recommendations%20With%20More%20Time.md) | developer / management | keep / change-or-finish / keep-in-mind, and what shipped in the July 2026 pass |
| [Developer: The Settings Menu — Add and Remove Settings](18%20Developer%20Settings%20Menu%20Add%20and%20Remove%20Settings.md) | developer | how the data-driven settings system works; add / change-default / remove walkthroughs (incl. the retired-key gotcha) |
| [Project Orientation — for Fontys Game-Design Students](20%20Project%20Orientation%20for%20Fontys%20Students.md) | developer / student | get-productive-in-an-afternoon on-ramp: the mental model, architecture map, common jobs, gotchas, and the honest debt list |

> **About this folder**
> This `HANDOVER/` folder is the delivered copy of the handover set, shipped inside the repository so the documents transfer with repo ownership. During the internship the master versions live in the developer's project vault and are mirrored here; **after the handover, this folder is the authoritative copy** and future developers should update these files directly.

---

## Plain-language glossary (the words the other docs use)

- **Unity** : the program a developer uses to build the game. You do **not** need it to run the game; you need it only to change the game's structure and make a new app file.
- **APK** : the Android app file. Installing this APK on a tablet puts the game on it. "Sideloading" just means installing it directly (by copying the file or over a cable) instead of from the Play Store.
- **Build / rebuild** : making a fresh APK from the project in Unity after a change.
- **Keystore** : a small secret file (plus passwords) that "signs" the app. The same keystore must be used every time, or a new version cannot install on top of the old one. Losing it means the app can never be updated again, only reinstalled from scratch. This is the most important thing to keep safe.
- **Screen-pinning** : an Android feature that locks the tablet into one app so a child cannot leave it. This is the real child-safety lock; you turn it on per tablet.
- **The settings menu (facilitator menu)** : the hidden menu inside the game (hold the top-left corner 2 seconds) where staff safely change how the game plays. Child-proof, no Unity needed.
- **Leaderboard** : the team scores, saved on that one tablet only. Not shared online, and clearable from the settings menu.
- **Repo (repository)** : the online place (GitHub) where the game's source files live, so a future developer can download and rebuild it.

## One open decision (for De Ontdekfabriek + Ryan)

**The deployed control format.** These docs assume the game ships as a **plain Android tablet**, played handheld or in a holder (which matches the stakeholder feedback: it is played both ways, which is why on-screen gas/brake indicators are on the plan). If a **steering-wheel / Arduino rig** is ever part of the exhibit, that hardware needs its own setup-and-maintenance doc and becomes an extra failure point. Confirm the format; if it is just the tablet, this set is complete. (Risk analysis item 7.)

---


