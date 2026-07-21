# Guide (Dev): Build and Deploy the APK

**For:** whoever ships a code or content change to the tablet (a future intern or school group, with Unity).
**What it is:** the short, everyday **update loop** only, so "build an APK" is findable as a one-page task. The one-time setup (keystore creation, app id, orientation, signing, repo transfer) lives in the full [[Handover — Build, Keystore and APK Rebuild Checklist|build checklist]] and is **not** repeated here.

> [!warning] Do this loop ONLY after the first signed build exists
> This card assumes the signing keystore and the Part A settings from the [[Handover — Build, Keystore and APK Rebuild Checklist|build checklist]] are already done. If there is **no keystore yet**, stop and do the checklist first — a build without it cannot be updated in place later.

---

## The loop (four steps)

1. **Open the project in the right Unity version.** Unity **6000.3.10f1** (from `ProjectSettings/ProjectVersion.txt`; also recorded in the build checklist). A different 6000.3.x patch usually opens but is not guaranteed. Confirm the open scene is the real game — `Assets/Scenes/Impact Makers Around The World Kenya.unity` — and **not** one of the similarly named prototype scenes (the checklist lists the traps).
2. **Bump the Bundle Version Code.** Player > Other Settings > **Android Bundle Version Code**, set it **higher than the version on the tablet**. Android refuses to install an update whose code is equal or lower, so this is not optional. (Bump the human-readable Version Name too if you like; only the code is enforced.)
3. **Build with the SAME keystore.** File > Build Settings > **Build** (an `.apk`, Build App Bundle **unticked** — `.aab` is for the Play Store, it cannot be sideloaded). Confirm **Custom Keystore** is ticked and the **same** keystore + alias + passwords from the handover package are selected before you build.
4. **Install over the old app.** `adb install -r yourbuild.apk` from the laptop (or copy the `.apk` to the tablet and tap it). `-r` reinstalls over the existing app and **keeps the leaderboard and settings** — but only because the signature matches.

That is the whole routine. Everything else is the one-time setup in the checklist.

> [!danger] The keystore is irreplaceable — a different signature wipes the tablet
> If you build with a **different** (or debug) key, Android treats it as a different app: `adb install -r` fails, and a clean install **erases the per-tablet leaderboard and facilitator settings** (they are stored locally on that tablet, not online). Always sign with the one handed-over keystore. There is no recovery if every copy of that keystore is lost — see the checklist's back-up-in-two-places warning.

---

## Shipping the changes from THIS session

The recent code changes (kiosk attract demo now collision-immune, the "Code vergeten?" PIN reset, the road-tile per-frame budget that kills the turn lag spike, the "Profiel gewijzigd" toast, the new **Vrij rijden / Endless** solo-with-lives mode — default **3** lives from `SessionConfig.endlessLives`, `Config/SessionConfig.cs` — the **EIGEN NAAM** typed-team-name keyboard, the settings **search box**, and the two night-aware corner car tail lights) are **source-only in the vault mirror**. They are not in a build yet: they need to be **copied into the live project and recompiled in Unity** before this loop produces an APK containing them. Nothing here has been device-verified.

> [!note] Still unverified on a real tablet (flag on the changelog when you do it)
> - The **car tail lights** need a **recompile and a dusk drive** to confirm the night-aware corners read correctly.
> - The **kiosk lock** (`KioskLock`), the **gyro / tilt-steer sign**, and all **audio** are **not yet device-verified** — check them on the tablet after installing, per the acceptance test.
> - The EditMode tests are ignored by Unity (their folder ends in `~`), so **do not** treat a build as "tested" because tests exist — verify on the device.

Log the build in [[Build Changelog — Final Features and Fixes]] (version code, what changed, what you verified on-device).

---

## Connections
- [[Handover — Build, Keystore and APK Rebuild Checklist]] — the full one-time setup (keystore, app id, orientation, repo transfer, asset licences)
- [[HANDOVER — Start Here]] — the role-ordered front door
- [[Handover — Tablet Setup and Golden-Image Checklist]] — locking down the tablet after install
- [[Handover — Facilitator Acceptance Test (15 Minutes)]] — the on-device verification pass
- [[Build Changelog — Final Features and Fixes]] — log every build here
