<!-- Handover copy delivered with the repository. During the internship the master version lives in the developer's project vault and is mirrored here; after handover this folder is the authoritative copy. -->

# Handover: Privacy (AVG/GDPR) and Accessibility Note

**For:** De Ontdekfabriek (to keep on file) and the portfolio assessment.
**What it is:** a short, honest statement of the game's privacy position under the AVG/GDPR, and a quick colour-blindness check on the red/green safety cues. Both matter because the players are minors in a public space.

> **Korte verklaring (voor De Ontdekfabriek, om te bewaren)**
> Het spel verzamelt **geen persoonsgegevens** en heeft **geen internetverbinding**. Op het scoreboard staan **teamnamen uit een vaste lijst** (geen echte namen van kinderen), alleen lokaal op de tablet opgeslagen. Verder bewaart het spel alleen **anonieme, algemene speelstatistieken** (aantal ritten, afstand, botsingen), ook lokaal. Geen account, camera, microfoon of locatie. Een begeleider kan alles wissen (instellingen > **BEHEER > Klassement wissen**). Omdat er geen persoonsgegevens worden verwerkt, gelden de zwaardere AVG-verplichtingen niet. De rood/groen-signalen in het spel zijn ook leesbaar voor kleurenblinde kinderen, omdat kleur altijd samengaat met tekst, een getal, positie of trilling.

---

## Privacy (AVG / GDPR)

**The game collects no personal data and makes no network connection.** Everything below is verified from the live project on 2026-06-25, not assumed.

In plain terms: nothing about a child ever leaves the tablet, and nothing identifying a child is stored even on the tablet. The leaderboard shows **team names chosen from a fixed preset pool** (Swahili animal-style team names picked by tapping a chip, not the child's own name typed in), saved only in the device's local `PlayerPrefs`. The only other stored data is **aggregate, anonymous gameplay counters** (total runs, distance, crashes by cause, play-time per part of the day), also local-only, with no link to any individual. There is **no account, no login, no camera or microphone use, no location, and no internet call**: `AnalyticsManager` is documented and built as "no network, ever", and Unity Analytics, Ads and Purchasing are all disabled in the project. Because no personal data is processed, the core AVG/GDPR obligations (consent, data-subject requests, retention limits, cross-border transfer) do not bite. As good practice, a facilitator can wipe both the leaderboard (settings menu, **BEHEER > Klassement wissen**) and the usage counters at any time, so the tablet is cleared between groups or exhibition periods.

> **If a parent or teacher asks at the exhibit**
> Plain answer staff can give: *"The game doesn't record anything about your child. No camera, no microphone, no names, no internet. It only keeps the team's score on this tablet, and we can wipe that any time."* In Dutch: *"Het spel slaat niets van uw kind op. Geen camera, microfoon, namen of internet, alleen de teamscore op deze tablet, en die kunnen we altijd wissen."*

**Basis for the above (the verified facts):**
- Leaderboard names are **preset team names**, not children's names, and are **optional** (any name simply starts the game) and **local-only** (PlayerPrefs on the device).
- Usage analytics are **aggregate and anonymous**, stored locally, resettable, never transmitted (`Scripts/Session/AnalyticsManager.cs`, "no network, ever").
- **No network code path** in the game; Unity Connect Analytics/Ads/Purchasing services are off (`UnityConnectSettings.asset`).
- A facilitator can **clear all stored data** on demand from inside the build.

**One small recommendation:** if staff ever decide to let children type their **own real names** (rather than pick a team chip), that would introduce personal data of minors and should be avoided, or used only with the team-name pool as now. The current preset-name design is the privacy-safe choice and should be kept.

---

## Accessibility: colour-blindness check on the red/green safety cues

The game teaches safe driving, so its "good vs bad" feedback leans on green (good) and red (danger). Red/green is the most common colour-blindness axis (deuteranopia/protanopia, roughly 8% of boys), so the question is: **can a colour-blind child still read every safety cue?** The design rule throughout the HUD was to **never carry meaning by colour alone**, and pairing it with text, shape or position. Checking each red/green cue against that rule:

| Cue | Colour | What ALSO carries the meaning (so colour is not the only signal) | Reads without colour? |
|---|---|---|---|
| Hazard / rule warning | red banner + LEDs | the banner shows the **named rule in words** (KUIL, OBSTAKEL, DREMPEL, REM AF), and the LEDs are in a **fixed peripheral position** | Yes |
| Hard crash | red screen-edge vignette | it is paired with **camera shake and a haptic buzz**, and the score visibly drops | Yes |
| Clean yield / good action | green score flash + "GOED GEWACHT!" | the **score number goes up** and a **text popup** says what was good | Yes |
| Battery-as-timer running low | green to gold to red | the bar also **drains by position** (top-down), so "low" is a length/position, not just a hue | Yes |
| Speed limit / over-speed | roundel + warning | the roundel shows the **number**, and over-speed adds the **named rule text** | Yes |

**Result: pass.** Every red/green safety cue is backed by text, a number, position, or a non-visual channel (shake/haptics), so a red/green colour-blind player loses no information. This is by design, not luck.

**Two low-cost improvements a future developer could make (not required):**
- Add a small **icon** inside the warning banner (a triangle for hazard, a person for a crossing) so the meaning is glanceable even faster, reinforcing the text.
- Make the danger red and the good green clearly **differ in brightness** as well as hue (they largely do), which helps the rarer total colour-blindness too.

**Already-built accessibility wins worth noting:** the facilitator can lower **motion sensitivity** for motion-sensitive children, turn **haptics** on or off, raise or lower every **volume** independently (so the audio safety cues can be made louder in a noisy room, which is also why the visual LEDs exist), and adjust **difficulty** right down for younger or less confident players, all from the settings menu without Unity.

---


---

