<!-- Handover copy delivered with the repository. During the internship the master version lives in the developer's project vault and is mirrored here; after handover this folder is the authoritative copy. -->

# Handover: Tablet Setup and Golden-Image Checklist

**For:** whoever prepares the exhibition tablet at De Ontdekfabriek (a one-time setup, then leave it alone).
**What it is:** the Android settings to lock down so the tablet runs the game all day, unattended, with children, without dropping out of the app, sleeping, dimming, or interrupting play; plus how to make a spare tablet that is a drop-in replacement.
**Companion to:** the [run-of-show card](07%20Facilitator%20Quick%20Reference.md) (daily use) and the [build checklist](01%20Build%20Keystore%20and%20APK%20Rebuild%20Checklist.md) (the APK on these tablets).

> **Device**
> The known test device is a **2022 Samsung Galaxy Tab A8** (Android). Menu names below are the generic Android wording with the Samsung path noted where it differs. On a different tablet the names move around but the settings exist; search the device Settings for the keyword in **bold**.

> **Kort gezegd (voor De Ontdekfabriek)**
> Doe dit een keer per tablet: (1) zet **App vastzetten / Pin app** aan en zet het spel elke ochtend vast (zo kan een kind er niet uit), (2) zet het scherm op **nooit uit** tijdens het opladen, (3) zet **Niet storen** aan en **automatische helderheid** uit, (4) zet **automatische updates** uit, (5) houd de tablet aan de lader. En houd een **tweede, identiek ingestelde tablet** klaar als reserve. De stappen staan hieronder; de afdrukbare dagkaart is de [run-of-show kaart](07%20Facilitator%20Quick%20Reference.md).

---

## A. The kiosk lock: screen-pinning (the most important setting)

The game's `OrientationLock` keeps it landscape and `KioskLock` stops the BACK button leaving the app, but the real, OS-level lock that stops a child reaching Home, Settings or a browser is **Android screen-pinning**. Turn it on for every deployed tablet.

**Enable the feature (once):**
- Samsung: **Settings > Security and privacy > Other security settings > Pin app** (older: **Pin windows**). Turn it **On**.
- Stock Android: **Settings > Security > App pinning** (or **Settings > Security > Advanced > Screen pinning**). Turn it **On**.
- Turn **OFF** "Ask for PIN/pattern before unpinning" only if staff prefer a quick exit; turn it **ON** if you want children unable to unpin. For an exhibit, requiring the unlock to unpin is the safer choice.

**Pin the game (each time the tablet is powered on for the day):**
1. Open the Kenya Scooter game.
2. Open the **Recents** view (the recent-apps button or the swipe-up-and-hold gesture).
3. Tap the game's icon at the top of its card and choose **Pin** (a pin/​thumbtack icon).
4. The nav buttons are now hidden/neutralised. A child pressing Back, Home or Recents stays in the game.

**Unpin (staff only, end of day or to change settings):** hold **Back + Recents** together (or **Back + Home** on gesture nav), then enter the unlock PIN if required.

> Why this matters: without pinning, one tap on Home drops a child onto the Android desktop. Pinning is the single setting that makes the tablet truly child-safe, and it costs ten seconds each morning.

---

## B. Stop the tablet interrupting play

| Goal | Setting | Where (generic / Samsung) |
|---|---|---|
| **Never sleep** while in use | Screen timeout to the maximum, AND keep it awake while charging (see the Developer Options note below the table) | **Display > Screen timeout** = longest option, plus **Developer options > Stay awake** = On. |
| **No notifications** popping over the game | Turn on **Do Not Disturb**, and turn off notifications app-by-app | **Notifications > Do Not Disturb** = On (schedule "always"). |
| **No banner/sound interruptions** | Silence system and app sounds you do not need; keep media volume up for the game | **Sounds > Do Not Disturb** allows media; confirm game audio still plays. |
| **Fixed brightness** (no auto-dimming in a bright room) | Turn **Adaptive/Auto brightness OFF**, set a high fixed level | **Display > Adaptive brightness** = Off, slider high. |
| **Tilt steering works** (the game steers by tilt) | Confirm the **auto-rotate / motion sensor** is enabled at the OS level, so the gyroscope is live | **Settings > search "motion"** / Quick settings, ensure the sensor is not disabled. |
| **No accidental rotation fights** | The game locks its own orientation; the OS sensor staying on is what the tilt steering needs | leave the motion sensor on; the game handles the rotation itself. |
| **No one-handed/edge gestures** triggering | Turn off edge panels and gesture shortcuts that can interrupt | Samsung: **Display > Edge panels** = Off. |

> **How to reach "Stay awake" (it lives in the hidden Developer Options)**
> "Stay awake while charging" is the most reliable never-sleep setting, but Developer Options is hidden by default. To show it: **Settings > About tablet > Software information**, then tap **Build number** seven times until it says "Developer mode on". Developer Options now appears (Samsung: **Settings > Developer options**); turn **Stay awake** on.
> **No-Developer-Options fallback:** if you would rather not enable Developer Options, set **Screen timeout** to its longest value (30 minutes on the Tab A8) and have staff tap the screen between groups so it never times out. Screen-pinning plus a long timeout covers most of the day; the charger keeps the battery up.

---

## C. Stop the tablet changing under you (the slow killers)

These are the settings that silently break a tablet weeks later, when no developer is around.

- **Disable OS automatic updates.** A forced Android version jump can change behaviour or reboot mid-session. **Settings > Software update > turn OFF "Auto download over Wi-Fi"** (Samsung) / **System > System update** (stock). Update only deliberately, with a developer present, after testing the game still runs.
- **Disable automatic app updates / hide the store.** The game is sideloaded, not from the Play Store, so the store will not touch it, but auto-updates of other apps can reboot or nag. **Play Store > Settings > Network preferences > Auto-update apps = Don't auto-update apps.**
- **Turn off Google/Samsung account sync prompts** and any "set up your device" nags, so a child never sees an account dialog over the game.
- **Lock screen / no PIN-on-wake during the day** is a choice: a simple swipe lock means staff can wake it fast; keep any unlock secret known only to staff if you require a PIN to unpin (Part A).
- **Battery: keep it plugged in.** Playing while charging is fine and is the recommended state (it also pairs with "stay awake while charging"). Use the original charger; a weak charger may not keep up with the screen at full brightness. Keep the cable strain-relieved at the mount so it is not yanked.

---

## D. After setup: verify the tablet is exhibition-ready

Run this once, end to end, before the tablet goes out:

1. Power on, open the game, **pin it** (Part A). Press Back, Home and Recents: you stay in the game.
2. Leave it untouched for longer than a normal turn: the screen does **not** sleep or dim.
3. Play a full relay (drive, score, hand-off, charge-station ending). Audio plays; no notification interrupts.
4. Open the facilitator settings (hold top-left corner 2 s), change one thing, close, restart the app: the change **persisted** and the landscape **held**.
5. Plug in and confirm it charges while the game runs.

---

## E. The golden image: a drop-in spare tablet

A single tablet is a single point of failure (cracked screen, theft, a battery that ages out). The cheap insurance is a **second, identical tablet set up exactly the same way**, kept charged in a drawer, so a swap takes two minutes and no Unity.

To make the spare a true drop-in:

1. Use the **same tablet model** if possible (same screen size and aspect, so the HUD looks identical; the game targets a 16:10 Samsung aspect).
2. Install the **same signed APK** (the exact file from the handover package, see the build checklist) so updates stay consistent.
3. Apply **every setting in Parts A to D** identically. Work from this checklist so nothing is missed.
4. **Verify it** with Part D.
5. Note that the leaderboard does **not** transfer between tablets (scores are stored locally on each device). The spare starts with an empty leaderboard, which is correct for a fresh class. No data needs copying.
6. Store the spare **charged and powered off** (or in a low-power state), and top it up monthly so it is ready.

> **Document which tablet is "live"**
> Label the two tablets (for example a sticker "A / live" and "B / spare") and note in the activity binder which one is in use, so staff always know which to grab and which to charge.

---


---

