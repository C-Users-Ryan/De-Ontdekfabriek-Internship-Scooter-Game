<!-- Handover copy delivered with the repository. During the internship the master version lives in the developer's project vault and is mirrored here; after handover this folder is the authoritative copy. -->

# Handover Risk Analysis: Running the Game Without Unity

**Context:** De Ontdekfabriek staff will RUN the game with children but have no Unity experience and will not develop it. Whoever might one day change it is a future intern or school group, also not De Ontdekfabriek. So the handover has two audiences with different failure modes, and most of the risk is in operating and keeping the game alive, not in the game itself.

> **Why this exists**
> The facilitator settings menu solved one slice (tuning the game without Unity). This is the wider sweep of what else can go wrong once the game is handed over, ranked by how badly each bites, with what is already done and what is still open.

---

## 1. The build cliff (the single biggest risk) — CHECKLIST DONE, Ryan actions open
Unity is the only way to produce or update the APK. The moment Ryan graduates, if no one can open the project, the app is frozen forever, and one undiscovered bug becomes permanent.
- Hand over the **final signed APK**, the **signing keystore + passwords** (without the original keystore, no one can ship an update that installs over the existing app), a **"how to rebuild and export the APK" doc** (exact Unity version, URP, package list, Android module, signing), and confirm **repo ownership** transfers and **asset licences** permit continued public use.
- DONE: the written recipe and package list, [Build, Keystore and APK Rebuild Checklist](01%20Build%20Keystore%20and%20APK%20Rebuild%20Checklist.md), built from the project's real settings (Unity 6000.3.10f1, URP 17.3.0, IL2CPP/ARM64). It also flags three things to fix before the FINAL build, found in `ProjectSettings`: the Android app id is still the URP-template default, there is no signing keystore yet, and Default Orientation is still Auto Rotation.
- OPEN (Ryan only, cannot be done from outside Unity): create the keystore, build the signed APK, transfer the repo, confirm asset licences, assemble the handover package.

## 2. Kiosk lockdown — DONE (code in, needs device test)
On a public tablet the Android home/back/recents buttons let a child drop out into Settings or a browser, and the back button may quit the game.
- DONE: landscape is locked (`OrientationLock`).
- DONE: the back button is handled (`KioskLock` in `Scripts/Core/`, sets `Input.backButtonLeavesApp = false` at startup; needs Ryan to compile and confirm on device), and Android screen-pinning is documented as the real OS-level lock in [Tablet Setup and Golden-Image Checklist](03%20Tablet%20Setup%20and%20Golden%20Image%20Checklist.md) (enable + daily-use steps).
- OPTIONAL, not done: auto-launch on boot (a launcher/config choice, not needed if staff pin the app each morning).

## 3. Daily run-of-show for facilitators — DONE (draft)
Staff need to run group after group with no training.
- DONE: the [run-of-show card](07%20Facilitator%20Quick%20Reference.md).

## 4. Troubleshooting without a developer on site — DONE (draft) + code
"It froze / won't start / steering is off / no sound / too dark."
- DONE: the symptom-to-fix card, plus the in-app **recalibrate steering** and **reset-to-defaults** actions in the settings menu.

## 5. Keeping the tablet stable over months — DONE (checklist)
OS auto-updates, app-update prompts, notifications interrupting play, battery, a cracked or stolen tablet.
- DONE: orientation lock; the facilitator can reset the leaderboard and settings.
- DONE: [Tablet Setup and Golden-Image Checklist](03%20Tablet%20Setup%20and%20Golden%20Image%20Checklist.md) covers disabling OS/app auto-update, Do-Not-Disturb, never-sleep, fixed brightness, the charging plan, and a recommended pre-configured spare tablet (golden image) so a replacement is swap-in.

## 6. Content they may want to change without Unity — DONE
- DONE: the facilitator settings menu now exposes ~38 tunables (difficulty, traffic, pedestrians, drive side, day/night, scoring, audio, session length) plus actions (recalibrate, clear leaderboard).
- DONE: the boundary doc, [What You Can Change vs What Needs a Developer](04%20What%20You%20Can%20Change%20vs%20What%20Needs%20a%20Developer.md), so no one breaks the build trying to edit tiles or hazards.

## 7. Physical / AV setup — NEEDS A DECISION
Museum noise can drown the audio safety cues (which is why the HUD has visual LEDs and banners). Plus tablet mounting and security.
- OPEN DECISION: is the deployed version a plain handheld tablet, or is there a steering-wheel / Arduino rig? If any physical controller ships, that is a whole extra setup-and-maintenance doc and a failure point. (The stakeholder feedback also confirms the game is played BOTH handheld and in a holder, which is why on-screen gas/brake indicators are on the plan.)

## 8. Privacy and accessibility — DONE (note)
Minors in a public space. If any names are stored for the leaderboard (even locally), that touches AVG/GDPR (likely fine, names are local-only and optional, but worth a one-paragraph note). And a quick colour-blind check on the red/green safety cues (the design already pairs colour with shape/position).
- DONE: [Privacy (AVG/GDPR) and Accessibility Note](05%20Privacy%20AVG%20GDPR%20and%20Accessibility%20Note.md). Verified from the code that the game collects no personal data and makes no network call (preset team names, local-only aggregate analytics, Unity Connect services off), so the AVG/GDPR position is clean; and a per-cue check confirms every red/green safety cue is also carried by text, a number, position or haptics (colour-blind pass).

---

## Priority order
1. **The build/keystore/APK handover** (Track C top item): de-risks everything and is the one thing only Ryan can secure.
2. **Kiosk lockdown** (back-button + screen-pinning): makes it deployable on an unattended tablet.
3. The facilitator **run-of-show + troubleshooting** (drafted) and a **15-minute test of the settings menu with one real facilitator**. The test now has a concrete script: [Facilitator Acceptance Test (15 minutes)](09%20Facilitator%20Acceptance%20Test.md). Running it is a Ryan action.

## The deployment docs (this analysis, worked into checklists)
- [Start Here (README)](README.md) (the role-ordered index of the whole set)
- [Build, Keystore and APK Rebuild Checklist](01%20Build%20Keystore%20and%20APK%20Rebuild%20Checklist.md) (item 1: the build cliff)
- [Package Manifest and Asset-Licence Check](02%20Package%20Manifest%20and%20Asset%20Licence%20Check.md) (item 1: the fill-in manifest + licences)
- [Tablet Setup and Golden-Image Checklist](03%20Tablet%20Setup%20and%20Golden%20Image%20Checklist.md) (items 2 and 5: kiosk + tablet stability)
- [What You Can Change vs What Needs a Developer](04%20What%20You%20Can%20Change%20vs%20What%20Needs%20a%20Developer.md) (item 6: the change boundary)
- [Privacy (AVG/GDPR) and Accessibility Note](05%20Privacy%20AVG%20GDPR%20and%20Accessibility%20Note.md) (item 8: privacy + accessibility)
- [Facilitator Acceptance Test (15 minutes)](09%20Facilitator%20Acceptance%20Test.md) (items 3 and 4: the evidence test)

The whole set also ships as plain markdown inside the game repository (the `HANDOVER/` folder at the repo root), so the documents transfer with repo ownership and do not depend on this vault surviving.

