<!-- Handover copy delivered with the repository. During the internship the master version lives in the developer's project vault and is mirrored here; after handover this folder is the authoritative copy. -->

# Handover: Build, Keystore and APK Rebuild Checklist

**For:** Ryan (to do before the internship ends) and whoever maintains the app afterwards (a future intern or school group, not De Ontdekfabriek staff).
**What it is:** the exact, ordered steps to (a) fix a few project settings that must be right in the FINAL build, (b) create the signing keystore, (c) rebuild and export the signed APK, and (d) hand the whole package over so the app can be updated after Ryan is gone.
**Why it is the top handover task:** Unity is the only way to produce or update the APK. Without the keystore and a written rebuild recipe, the moment Ryan graduates the app is frozen forever and one undiscovered bug becomes permanent. This is the one thing only Ryan can secure.

> **Read this first**
> Everything in Part A and Part B must be done **once, by Ryan, before the final signed build**, because two of them cannot be changed after the app is installed without forcing a clean re-install (losing the leaderboard and settings). I (Claude) cannot run Unity or build the APK, so every step here is a Ryan action; the facts (versions, package list, current settings) are read from the live project and are accurate as of 2026-06-25.

> **Quick path for a routine update (once the first signed build exists)**
> If the keystore and the Part A settings are already done and you are just shipping a code/content change: (1) open the project in Unity **6000.3.10f1**, (2) **bump the Bundle Version Code** (Player > higher than the version on the tablet), (3) **File > Build Settings > Build** with the **same Custom Keystore** selected, (4) `adb install -r yourbuild.apk` (keeps the leaderboard because the signature matches). That is the whole loop. The long Parts A, B, D and E below are the **one-time** setup you only do for the first handover build.

---

## The verified project facts (read from the live project, 2026-06-25)

These are the real settings the rebuild depends on. Do not guess them; they are recorded here so a future maintainer has them even if the project file is hard to open.

| Setting | Current value | Where |
|---|---|---|
| Unity version | **6000.3.10f1** (revision e35f0c77bd8e) | `ProjectSettings/ProjectVersion.txt` |
| Render pipeline | **URP 17.3.0** (`com.unity.render-pipelines.universal`) | `Packages/manifest.json` |
| Input | **Input System 1.18.0**, Active Input Handling = **Both** | manifest + `activeInputHandler: 2` |
| Scripting backend (Android) | **IL2CPP** (`scriptingBackend.Android: 1`) | needs the Android **NDK + SDK**, installed with the module |
| Target architecture (Android) | **ARM64** (`AndroidTargetArchitectures: 2`) | required for 64-bit; correct as is |
| Min Android version | **API 25** (Android 7.1) | `AndroidMinSdkVersion: 25` |
| Target Android version | **Automatic / highest installed** | `AndroidTargetSdkVersion: 0` |
| Company / product | **De OntdekFabriek** / **De Ontdekfabriek Internship** | `ProjectSettings.asset` |
| Version name / code | **0.1.5** / **1** | `bundleVersion` / `AndroidBundleVersionCode` |
| Custom keystore | **NONE yet** (`androidUseCustomKeystore: 0`, keystore name empty) | see Part B |

Full package list is at the end of this doc (Appendix).

---

## Part A: one-time settings to fix BEFORE the final build

Each of these is a real, currently-wrong-or-default value found in the project on 2026-06-25. Fix them once, in **Edit > Project Settings > Player** (Android tab) unless noted, then never again.

1. **Change the application identifier (CRITICAL, cannot be changed after install).**
   It is still the URP template default: `com.UnityTechnologies.com.unity.template.urpblank`. Set it to a real, owned identifier, for example **`nl.deontdekfabriek.kenyascooter`**.
   *Why it must be done before the first real install:* the package name IS the app's identity to Android. Once a tablet has the app installed under one identifier, an APK built with a different identifier installs as a SEPARATE app, it cannot update over the old one. Pick the final name now so every future update can install on top of the existing app and keep the leaderboard.
   Field: Player > Other Settings > Identification > Override Default Package Name (already on) > Package Name.

2. **Set Default Orientation to Landscape Left.**
   It is currently **Auto Rotation** (`defaultScreenOrientation: 4`). The in-game `OrientationLock` forces Landscape Left at runtime, but the Unity splash and the very first frames obey this Player setting, so without this the boot screen can appear rotated on a mounted tablet.
   Field: Player > Resolution and Presentation > Default Orientation = **Landscape Left**.

3. **Set a child-facing product name AND a custom app icon (recommended).**
   The app's name on the tablet is currently "De Ontdekfabriek Internship". Rename it to something the activity is known by (for example **"Kenya Scooter"** or **"Drive Through Kenya"**) so staff and teachers can find it on the home screen, and set a **custom launcher icon** so it is not the blank default Unity icon (a teacher recognises the app faster by a clear icon than by its text name). Both are cosmetic and safe to change anytime.
   Fields: Player > Product Name, and Player > Icon (set the Adaptive and Legacy Android icons).

4. **Bump the version on every build.**
   For the final build set a clear version (for example **1.0.0**) and increment **Bundle Version Code** to **2** or higher. Any later update MUST have a higher Bundle Version Code than the one on the tablet or Android will refuse to install it over the old one.

---

## Part B: create the signing keystore (the irreplaceable item)

Right now the project has **no custom keystore**, so any build is currently signed with Unity's throwaway debug key. A debug-signed APK installs and runs, but you **cannot ship a consistent update** with it: Android only lets an update install over an existing app if both are signed with the **same** key. So the final build must use a real keystore, and **that keystore file plus its passwords are the single most important thing to hand over.** Lose it and no one can ever update the installed app; they can only uninstall and reinstall, wiping the leaderboard.

Steps (in Unity, Player > Publishing Settings):

1. Tick **Custom Keystore**.
2. **Keystore > Create New...**, choose a path OUTSIDE the project folder (so it is never committed to git or shared by accident), for example `C:\Users\ryans\Documents\KenyaScooter-Keys\kenyascooter.keystore`.
3. Set a **keystore password**. Write it down (see the handover package list).
4. Create a **new key alias** (for example `kenyascooter`), with its own **alias password** and a long **validity (years)**, set it to **50** so it does not expire during the exhibit's life.
5. Fill the certificate fields (name, organisation = De Ontdekfabriek, country = NL); they are informational.
6. Confirm the **keystore** and **alias** are both selected and the passwords entered, so the build is signed with this key.

> **Back up the keystore in two places immediately**
> Copy the `.keystore` file to a second location (a USB stick handed to De Ontdekfabriek, and a cloud drive they own). A keystore is a single small file with no recovery: if every copy is lost, the app can never be updated again.

---

## Part C: how to rebuild and export the signed APK

For a fresh machine (the realistic future case: a new intern's laptop):

1. **Install Unity Hub**, then install Unity **exactly `6000.3.10f1`** (Hub > Installs > Install Editor > Archive, match the version). Using a different 6000.3.x patch usually opens fine but is not guaranteed; match it if possible.
2. During install (or afterwards via Hub > the version's gear > Add Modules), add the **Android Build Support** module **with both sub-options ticked**: **Android SDK & NDK Tools** and **OpenJDK**. IL2CPP needs the NDK, this is not optional for this project.
3. **Open the project** by pointing Unity Hub at the project folder
   `...\De-Ontdekfabriek-Internship-Scooter-Game\De Ontdekfabriek Internship`.
   First open is slow: Unity reimports all assets and compiles. Wait for an empty Console.
4. **File > Build Settings > Platform = Android**, **Switch Platform** if it is not already active.
5. Open the **right scene** (this trips up everyone new). The playable game is **`Assets/Scenes/Impact Makers Around The World Kenya.unity`**, and it is the only scene enabled in **File > Build Settings > Scenes In Build**. The project also contains many **old prototype scenes that are NOT the game**, including the very similarly named `Impact Makers Around The World Kenya turning.unity`, plus `Overtake*.unity`, `endlessRunner.unity`, `EndlessV2.unity`, `turn test.unity`, `Tiles.unity`, and a `_Recovery/` folder of crash-recovery scenes. Open only the one named above. If Scenes In Build is empty, open that scene and click **Add Open Scenes**. (A future tidy-up could delete the unused scenes; leave them for now if unsure, but never ship one.)
6. Do the **Part A** settings checks and the **Part B** keystore once (they persist in the project afterwards).
7. **Build**: File > Build Settings > **Build** (produces an `.apk`). For a single sideloaded kiosk tablet an APK is what you want, **not** an App Bundle (.aab is for the Play Store).
8. **Install on the tablet**: enable Developer Options + USB debugging on the tablet (or "install unknown apps"), then either copy the `.apk` to the tablet and tap it, or `adb install -r kenyascooter.apk` from the laptop. `-r` reinstalls over the existing app and keeps its data (works only if signed with the same keystore).
9. **Verify on the device**: landscape locks, the game plays a full relay, the facilitator settings menu opens (hold top-left 2 s), scores persist across an app restart, and screen-pinning holds (see the tablet-setup checklist).

> **Build mistakes that waste an afternoon**
> - Wrong/old Android module: build fails on IL2CPP/NDK. Re-add the module with SDK & NDK.
> - Built an `.aab` instead of `.apk`: cannot be sideloaded directly. Build > Build, with Build App Bundle unticked.
> - Built debug-signed: it installs but a later update refuses to install over it. Always confirm Custom Keystore is selected.
> - Higher version code rule: an update with an equal or lower Bundle Version Code will not install over the old app.

---

## Part D: repo ownership transfer

The project lives in a GitHub repo (`De-Ontdekfabriek-Internship-Scooter-Game`). After the internship:

1. **Transfer or grant ownership** to an account De Ontdekfabriek controls (or a long-lived shared account), so access does not die with Ryan's student account. GitHub: repo **Settings > Transfer ownership**, or add the org/account as an **Owner** collaborator.
2. If the repo is under a personal student account that will be deleted, **transfer is mandatory**, not optional.
3. Confirm the repo has **everything needed to rebuild**: all of `Assets/`, `Packages/manifest.json`, and `ProjectSettings/`. These three are enough to reopen the project; `Library/` is regenerated and need not be in the repo.
4. **Never commit the keystore or passwords to the repo.** They are handed over separately (see below).
5. Record the repo URL and the owning account in the handover package.

---

## Part E: asset-licence check (continued public/museum use)

The game is shown publicly in a maker-space, so every imported asset must permit **commercial / public display** use. Before handover, confirm the licence for each third-party asset and record it:

- The **Toon City / Low Poly Simple Urban City** kit (road tiles, crossroads, the gas-station model) and any other Asset Store packs: confirm the Unity Asset Store **Extension Asset** licence covers a single developer shipping it inside a built app for public display (it does for a standard single-seat licence; record the licence and the seat).
- Any **fonts** used in the HUD/menus: confirm the font licence allows embedding in an app (most open fonts do; record which font and its licence).
- Any **audio** (engine loops, ambience, the old-truck clip): confirm each clip is either self-made, CC0/royalty-free, or licensed, and record the source.
- The **brand assets** (the green world-map logo, the Kenya illustration, the Ugani/huisstijl palette) are De Ontdekfabriek's own, so they are fine; note that they are client-owned.

Write the result as a one-line-per-asset list in the handover package so a future maintainer (or the museum) can prove the right to keep showing it. Flag anything whose licence you cannot confirm rather than assuming it is fine.

---

## The handover package (what physically changes hands)

A single folder/USB given to De Ontdekfabriek, plus a cloud copy they own, containing:

- [ ] The **final signed APK** (filename includes the version, for example `kenyascooter-1.0.0.apk`).
- [ ] The **signing keystore** file (`.keystore`).
- [ ] A sealed note (or password manager entry) with the **keystore password, the key alias name, and the alias password**. Without all three the keystore is useless.
- [ ] The **GitHub repo URL** and the **owning account** it was transferred to.
- [ ] The **exact Unity version** to install: `6000.3.10f1` (also in this doc).
- [ ] This document and the [tablet-setup checklist](03%20Tablet%20Setup%20and%20Golden%20Image%20Checklist.md), the [change-boundary doc](04%20What%20You%20Can%20Change%20vs%20What%20Needs%20a%20Developer.md), and the [facilitator cards](07%20Facilitator%20Quick%20Reference.md).
- [ ] The **asset-licence list** from Part E.


---

