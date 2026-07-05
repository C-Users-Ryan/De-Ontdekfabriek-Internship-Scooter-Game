# Audio Sourcing — Ready to Download

**Game:** Kenya Scooter Game (De Ontdekfabriek internship) · Unity tablet game
**Authoritative clip list:** `Scripts/Config/AudioConfig.cs` (+ `Scripts/Traffic/TrafficHorn.cs` for horns)
**Wiring reference:** `Scripts/Audio/AudioManager.cs`
**Status:** Sources verified on the web June 2026. Clips **not yet downloaded/imported** (I cannot import into Unity — this is a sourcing list).
**Supersedes / extends:** the vault doc `Sound Assets — Sources and Licensing.md` (older "AudioManager v2" field names). Where they disagree, **this file matches the current code.**

> This list maps every clip field the **current** `AudioConfig` (and `TrafficHorn`) actually expects to a specific, licence-verified source. Every recommendation below was opened and its licence read on the source page (Pixabay pages block automated fetch — those are flagged "confirm on page"). Download → rename to the field name → drop into `Assets/Audio/` → assign in the `AudioConfig` asset (horns: on the matatu prefab's `TrafficHorn`).

---

## How the code uses each clip (so you pick the right thing)

- **Engine layers** (`motorLoop`, `windLoop`, `surfaceLoop`) are **looping** sources blended by speed. `motorLoop` is **pitch-shifted** 0.85→1.6 with speed, so pick a steady tone with no built-in pitch movement. `windLoop` and `surfaceLoop` only change volume. All three must be **seamless loops**.
- **Ambience** (`ambientVariants[]`) — looping beds that **crossfade** (2.5 s) when the road zone's `contextTag` changes. Must loop seamlessly.
- **Music** (`musicLoop`) — looping; **ducks to 0.35 on every collision** and recovers over 2 s. Pick something that reads fine quiet and isn't ruined by a sudden dip.
- **SFX** are **one-shots** through an 8-source pool (`PlayOneShot`). Keep them short and trimmed.
- **`hadadaIbis`** fires once at the **dawn** day-phase (index 0) — the signature Kenyan moment.
- **`hornClips[]`** live on `TrafficHorn` (per-vehicle, **3-D positioned**), not in `AudioConfig`. The code picks a random clip and does **not** pitch-shift, so variety must come from using **2–3 genuinely different** clips.

### Licence key
| Label | Attribution? | Commercial? |
|---|---|---|
| **CC0** | No | Yes |
| **CC BY** | Yes (credits line) | Yes |
| **Pixabay** | No (appreciated) | Yes |
| **Mixkit** | No | Yes (with redistribution limits) |
| **⚠️ CC BY-NC** | Yes | **No — replace before any paid release** |

---

## Engine layers (looping)

### `motorLoop` — electric-motor whine
| | |
|---|---|
| **Recommended** | "Electric Scooter" by **IENBA** |
| **URL** | https://freesound.org/people/IENBA/sounds/697379/ |
| **Licence** | **CC0** — *no attribution required* |
| **Processing** | ~19 s "free wheel spin of an electric scooter." Trim to the steady whine (drop the spin-up), set seamless loop points in Audacity. Leave pitch neutral — Unity pitches it with speed. |
| **Backup** | "Electric Motor Whir" by jasonLON — https://freesound.org/people/jasonLON/sounds/125401/ (confirm licence on the page before use) |

> **Correction vs old doc:** the page now shows **CC0**, not CC BY 4.0 — so no credit line is needed for the engine.

### `motorStart` / `motorStop` — engine spin-up & spin-down *(new — now wired)*
| | |
|---|---|
| **Recommended** | the **same IENBA clip** (697379) — no new download |
| **Find it** | freesound 697379 (CC0) |
| **Processing** | When you trim `motorLoop` to its steady middle, you cut off a **spin-up** at the head and a **spin-down** at the tail. **Save those two off-cuts** as separate clips: head → `motorStart`, tail → `motorStop`. Keep each short (~0.4–1.0 s) and matched to the loop's level so they blend. |
| **Plays when** | `motorStart` on run start; `motorStop` when a run ends (game over / finished). Both **optional** — leave empty and the engine just fades in/out as before. |
| **Backup** | any electric-motor start/stop one-shot, e.g. trim "Electric Motor Whir" by jasonLON (freesound 125401, confirm licence) |

> **Now wired in code** (`AudioConfig.motorStart` / `motorStop`, played from `AudioManager` on `SessionStarted` and on the transition into `GameOver`/`Finished`). This is the "spin-up/spin-down polish" — it gives the electric whine a wind-up at the start of a run and a wind-down at the end, layered over the loop's fade.

### `windLoop` — wind rush
| | |
|---|---|
| **Recommended** | "Soft Wind" by **florianreichelt** |
| **URL** | https://freesound.org/people/florianreichelt/sounds/459977/ |
| **Licence** | **CC0** |
| **Processing** | Loop seamlessly. Import at a normalised level — volume is fully curve-driven in Unity. |
| **Backup** | "Wind Ambience (Looping)" by jackyyang09 — https://freesound.org/people/jackyyang09/sounds/476849/ (confirm licence) |

### `surfaceLoop` — tyre-on-road texture (tarmac)
| | |
|---|---|
| **Recommended** | "Car rolling on asphalt .wav" by **orlandorizo** |
| **URL** | https://freesound.org/people/orlandorizo/sounds/591079/ |
| **Licence** | **CC0** |
| **Processing** | Loop seamlessly. Volume rises with speed via the curve. |
| **Murram/dirt variant** | "Bike tire on dirt road.wav" by **SpliceSound** — https://freesound.org/people/SpliceSound/sounds/218301/ — **CC0** |

> ⚠️ **Scope note:** `AudioConfig` has **one** `surfaceLoop` field and `AudioManager` drives it by **speed only** — there is currently **no roughness/murram swap** in code (the old doc's `OnRoadSurfaceChanged(roughness)` does not exist here). So: import the **tarmac** clip as `surfaceLoop` now, and keep the murram clip on standby for if/when a second surface layer is added.

---

## Ambience beds (looping crossfade)

Each goes in an `ambientVariants[]` element. The element's **`contextTag` string must exactly match** a tag in the active `RoadSequence.contextTags[]`. The `AudioConfig` tooltip's canonical four are **`savanna`, `township`, `wildlife`, `highland`** — note the code comment elsewhere also uses `tsavo_wildlife`; **pick one spelling and use it consistently** in both the sequence assets and the AudioConfig. (Current sequence assets still carry placeholder tags like `Low`, so these tags need authoring regardless.)

### `contextTag: savanna`
| | |
|---|---|
| **Recommended** | "African savanna 2.wav" by **AugustSandberg** |
| **URL** | https://freesound.org/people/AugustSandberg/sounds/202876/ |
| **Licence** | **CC0** |
| **Why** | Genuine Kenyan field recording — **Masai Mara National Park, Kenya** (Siana, Narok). The authentic open-landscape bed. |
| **Processing** | Loop. (Recorded at midnight — very sparse; ideal under an open-road zone.) |

> **Correction vs old doc:** page shows **CC0**, not "CC BY (verify)" — no credit line needed.

### `contextTag: township`
| | |
|---|---|
| **Recommended** | "African market atmos.wav" by **mikewest** (Livingstone, Zambia) |
| **URL** | https://freesound.org/people/mikewest/sounds/407641/ |
| **Licence** | **CC0** |
| **Layer under it (optional)** | "A village in Africa. General ambience." by **felix.blume** (Mali) — https://freesound.org/people/felix.blume/sounds/174445/ — **CC0** |
| **Processing** | Loop both; market as the primary layer, village lower for depth. |

### `contextTag: wildlife`
| | |
|---|---|
| **Recommended** | "Ambience, Night Wildlife, A.wav" by **InspectorJ** |
| **URL** | https://freesound.org/people/InspectorJ/sounds/352514/ |
| **Licence** | **CC BY 4.0** — *attribution required* (see credits block) |
| **Caveat** | Recorded in **Callahan, Florida** (crickets) — a generic insect/night bed, **not African**. It works as texture, but if you want authenticity here, reuse the CC0 savanna recording or source a daytime Tsavo bird bed instead. |
| **Backup** | "Open Field Night" by DeolandredeJager — https://freesound.org/people/DeolandredeJager/sounds/542956/ (confirm licence) |

### `contextTag: highland`
| | |
|---|---|
| **Recommended** | "Nature Ambiance" by **Benjamin152** |
| **URL** | https://freesound.org/people/Benjamin152/sounds/445144/ |
| **Licence** | **CC BY 3.0** — *attribution required* |
| **Why** | Birds + subtle wind/trees, recorded in Centurion, South Africa — fresh morning highland feel. |

> **Correction vs old doc:** it's **CC BY 3.0**, not 4.0.

---

## Music (looping, ducks on crash)

### `musicLoop`
| | |
|---|---|
| **Recommended #1** | "Afrobeat 2026 Instrumental Music Background" by **Delon_Boomkin** |
| **URL** | https://pixabay.com/music/upbeat-afrobeat-2026-instrumental-music-background-327258/ |
| **Recommended #2** | "The Afrobeat" — https://pixabay.com/music/afrobeat-the-afrobeat-153058/ |
| **Loop-friendly alt** | "Afrobeat Music Loop – Feelgood Groove" by **MusicInMedia** (and its "Loop No.2") — find via https://pixabay.com/music/search/afrobeat%20instrumental/ |
| **Licence** | **Pixabay Content License** — free for commercial use, no attribution required |
| **Why these** | Contemporary **instrumental Afrobeat** — upbeat and positive, and a respectful, modern representation rather than generic "tribal" percussion. The brief explicitly endorses an Afrobeats feel. |
| **Processing** | Set seamless loop points (the MusicInMedia "Loop" cuts are authored to loop). Pick a mix with a clear groove but no lead vocal, so the **duck-to-0.35 on crash** isn't jarring. |

> ⚠️ **Verify on page:** Pixabay blocks automated fetching, so I confirmed these tracks exist via Pixabay's indexed search results but could not open them. **Preview each in-browser** and confirm: instrumental (no dominant vocal), upbeat, and that it sits fine at low volume. The "Free for use under the Pixabay Content License" badge is shown on every track page.

---

## SFX — gameplay (one-shots)

### `overtakeSting` — overtake reward
| | |
|---|---|
| **Recommended** | "Game Reward" by **IENBA** — https://freesound.org/people/IENBA/sounds/656643/ |
| **Licence** | **CC0** |
| **Note** | Designed game pickup/bonus sting. Keep neutral pitch. (The current `AudioManager` plays it flat — it does **not** add a per-chain pitch rise, despite what the old doc said.) |
| **Backups** | "Victory sting 1" by Victor_Natas (741118); "Success Jingle" by JustInvoke (446111) — confirm licences |

### `nearMissScreech` — near-miss screech
| | |
|---|---|
| **Recommended** | "G39-21-Tire Squeals.wav" by **craigsmith** — https://freesound.org/people/craigsmith/sounds/438638/ |
| **Licence** | **CC0** |
| **Processing** | Trim to the single sharpest screech. |
| **Backup** | "screeching tyres" by johnnydekk (614627) — confirm licence |

### `crashHard` — heavy collision
| | |
|---|---|
| **Recommended** | "Collision" by **qubodup** — https://freesound.org/people/qubodup/sounds/332058/ |
| **Licence** | **CC0** |
| **Note** | "Vehicle crashing / mechanical object hit and smashed" — the full-weight impact. |

### `crashLight` — lighter collision *(new field — not in old doc)*
| | |
|---|---|
| **Recommended** | "Fast Collision Reverb" by **qubodup** — https://freesound.org/people/qubodup/sounds/332057/ |
| **Licence** | **CC0** |
| **Why** | Same author/family as `crashHard` but a **shorter, lighter** collision — keeps hard vs light coherent. `AudioManager` picks `crashHard` for `CollisionSeverity.Hard`, this otherwise. |
| **Backup** | "Wooden Thud (Mono)" by Breviceps (449955, CC0) |

### `potholeBump` — pothole / rock hit
| | |
|---|---|
| **Recommended** | "Car hitting pot hole.wav" by **bsumusictech** — https://freesound.org/s/62532/ |
| **Licence** | **CC0** |
| **Why** | Literally a car driving over a pothole — exactly on-brief and distinct from the metallic crash. |
| **Backup** | "Wooden Thud (Mono)" by **Breviceps** — https://freesound.org/people/Breviceps/sounds/449955/ — **CC0** (0.5 s) |

### `wrongLaneBuzz` — wrong-lane warning tone
| | |
|---|---|
| **Recommended** | "Beep warning" by **SamsterBirdies** — https://freesound.org/people/SamsterBirdies/sounds/467882/ |
| **Licence** | **CC0** |
| **Note** | ~1.7 s warning beep. `WrongLaneTick` can fire repeatedly, so trim it short and clean (car-warning beep, not a game "fail" jingle). |
| **Backup** | "Buzzer sounds (Wrong answer / Error)" by Breviceps (493163, CC0) — or any Mixkit alert tone |

### `graceSaved` — collision absorbed / "saved" *(new field — not in old doc)*
| | |
|---|---|
| **Recommended** | "Heal - Rpg" by **colorsCrimsonTears** — https://freesound.org/people/colorsCrimsonTears/sounds/562292/ |
| **Licence** | **CC0** |
| **Why** | Played when a crash is **absorbed** (grace save). A soft positive "heal/shield" cue reads as relief and is clearly distinct from the overtake sting and checkpoint chime. |
| **Backup** | "rpgPowerup.wav" by colorsCrimsonTears (577965, CC0) |

### `rewindWhoosh` — rewind start *(new field — not in old doc)*
| | |
|---|---|
| **Recommended** | "Reverse vinyl scratch / warp" by **al_sub** — https://freesound.org/people/al_sub/sounds/647161/ |
| **Licence** | **CC0** |
| **Why** | The author calls it a "time warp / rewind effect" — exactly the reversed-time feel for `RewindStarted`. |
| **Backup** | "Whoosh" by qubodup (60013, CC0) |

### `checkpointArrive` — checkpoint reached *(new field — not in old doc)*
| | |
|---|---|
| **Recommended** | "Goal cleared" by **colorsCrimsonTears** — https://freesound.org/people/colorsCrimsonTears/sounds/545697/ |
| **Licence** | **CC0** |
| **Processing** | Victory/goal jingle (~11 s) — trim to the first short hit so it works as a one-shot arrival. |
| **Backup** | "Complete Chime" by FoolBoyMedia (352661, **CC BY 4.0** — needs a credit) or "Success! Quest Complete!" by qubodup (166540) |

---

## SFX — animal / atmosphere

### `hadadaIbis` — Hadada ibis dawn call ⚠️ (the signature sound)
| | |
|---|---|
| **Recommended (commercial-safe)** | "hadedas and thunder" on **Pixabay** |
| **URL** | https://pixabay.com/sound-effects/hadedas-and-thunder-75672/ |
| **Licence** | **Pixabay Content License** — commercial OK, no attribution |
| **Processing** | Real hadeda calls, but recorded **with a thunderstorm**. Trim to a clean "haa-haa-de-dah" sequence and EQ/duck the thunder out (or keep a touch of it — a dawn storm is atmospheric). |
| **⚠️ Authentic alternative (NON-COMMERCIAL)** | xeno-canto **XC195524**, *Bostrychia hagedash* by **Rory Nefdt** — https://www.xeno-canto.org/195524 — **CC BY-NC-SA 4.0**. Scientifically clean, but **non-commercial only — must be replaced or licensed before any paid release.** |

> Note: the freesound hadeda option ("Hadedas flying in the distance" by Nagwense, 407657) is **also CC BY-NC 4.0**, so it is *not* commercial-safe. Pixabay is the only verified commercial-safe hadeda source found.

---

## Matatu horns — `TrafficHorn.hornClips[]` (NOT in AudioConfig)

These are assigned on the **matatu/traffic vehicle prefab's `TrafficHorn` component**, not in `AudioConfig`. Provide **2–3 distinct** clips (the code picks one at random and does **not** pitch-shift). Prefer lower-pitched "van/bus" honks for matatus.

**Verified CC0 set (recommended — clear licence):**
- "Car horn beep beep two beeps honk honk" by **AmishRob** — https://freesound.org/people/AmishRob/sounds/423990/ — **CC0**
- "Car Horn Honk.wav" by **DeVern** — https://freesound.org/people/DeVern/sounds/349922/ — **CC0**
- "car horn.wav" by keweldog — https://freesound.org/people/keweldog/sounds/182474/ (very widely used; confirm licence) — use as the third variant

**Alternative (Mixkit):** "Truck horn", "Car horn", "Car double horn" from https://mixkit.co/free-sound-effects/car-horn/ — **Mixkit Free License** (commercial OK, no attribution).

> **Mixkit licence:** the Mixkit Sound Effects Free License allows commercial use in videos/games/apps with **no attribution**, but you **may not** redistribute the sounds as standalone files or in a product whose main value is the sounds themselves. Full terms: https://mixkit.co/license/ (confirm at download).

---

## UI sounds (expanded menu/HUD) — proposed new fields ⚠️ not yet wired

The UI was expanded (`Scripts/UI/`): a procedural **title → team-setup → relay → standings → game-over** flow (`KenyaMenuScreens`), a **settings panel** (gyro / Real-Rider toggles), a **language toggle**, a **leaderboard** with a wipe-confirm, and the **checkpoint / end** screens. **None of it plays any sound today**, and `AudioConfig` has no UI fields — so the clips below need a small **code change** (see "Wiring" at the end of this section) before they do anything.

> **Touch tablet → no hover sounds.** Every interaction is a tap; there is no pointer hover state, so there is deliberately no "hover" clip in this list.

**Best single source:** the **Kenney "Interface Sounds"** pack — **CC0**, ~100 clicks / switches / confirmations / errors / back tones, consistent timbre. One download covers nearly every row below. https://kenney.nl/assets/interface-sounds
Per-row freesound CC0 alternatives are given so you can cherry-pick without the whole pack. **Everything here is CC0 — zero attribution.**

| Proposed field | Fires on | Recommended (CC0) | URL | Backup (CC0) |
|---|---|---|---|---|
| `uiButtonPress` | standard buttons: Next player, New group, end/checkpoint **Next**, settings buttons | "UI Button Click" by **el_boss** | https://freesound.org/people/el_boss/sounds/677861/ | "SFX UI Button Click" by suntemple — https://freesound.org/people/suntemple/sounds/253168/ ; or Kenney `click` |
| `uiStart` | the confident primary action: **ANZA!** (start), **NOG EEN KEER** (play again) | a punchier click — "UI Button Click Snap" by **el_boss** (677860) or Kenney `confirmation` | https://freesound.org/people/el_boss/sounds/677860/ | reuse `overtakeSting` feel; or `uiConfirm` below |
| `uiSelect` | tapping a **team-name chip** (`KenyaMenuScreens.PickTeam`) | "Videogame Menu Select" by **Fupicat** | https://freesound.org/people/Fupicat/sounds/471937/ | a soft `uiButtonPress`; or Kenney `select` (note: Fupicat is 8-bit — pick the softer Kenney tap if you want a more organic feel) |
| `uiToggle` | **gyro / Real-Rider / language / settings-panel** toggles | "Light Switch (on / off)" by **Breviceps** | https://freesound.org/people/Breviceps/sounds/457037/ | Kenney has separate `switch on` / `switch off` if you want distinct up/down ticks |
| `uiConfirm` | **leaderboard wipe / new-group commit** (a deliberate "yes, do it") | "Goal cleared" by **colorsCrimsonTears** (trim to a short affirmative) | https://freesound.org/people/colorsCrimsonTears/sounds/545697/ | Kenney `confirmation`; or "Complete Chime" by FoolBoyMedia (352661, **CC BY** → credit) |
| `uiBack` | **closing the settings / debug panel**, cancel | Kenney `back` / a soft low click | https://kenney.nl/assets/interface-sounds | a quieter / lower-pitched `uiButtonPress` |
| `uiError` | a **blocked / disabled** action (soft "can't do that") | "Buzzer sounds (Wrong answer / Error)" by **Breviceps** (use the softest) | https://freesound.org/people/Breviceps/sounds/493163/ | Kenney `error` (gentler) — prefer this for a kids' exhibition |
| `uiPanelOpen` | **settings / debug panel appears** (subtle) | "Whoosh" by **qubodup** | https://freesound.org/people/qubodup/sounds/60013/ | Kenney swoosh; optional — can be left silent |

**Tone guidance:** this is a kids' museum-exhibition game in Swahili + Dutch. Keep clicks **soft, warm, and quiet** — avoid harsh sci-fi blips or a jarring error buzzer. Trim every clip tight (a UI click should be < ~100 ms) and import at a modest level; UI sits *under* the music/ambience, not over it.

### Wiring (the code change these clips need)

Right now there is **no** UI→audio path, so sourcing alone won't make a sound. Minimal change for the dev who owns audio:

1. **Add fields** — a `[Header("UI")]` block on `AudioConfig` with the fields above (or a small dedicated `UiAudioConfig`).
2. **Expose a UI entry point** — `AudioManager.PlaySfx` is `private`; add e.g. `public void PlayUi(AudioClip clip) => PlaySfx(clip);` (the SFX pool already exists, so no new sources needed).
3. **Call it from the central button helpers** (the UI is procedural, so this is a handful of one-liners, not per-button work):
   - `KenyaMenuScreens.AddButton` (`Scripts/UI/KenyaMenuScreens.cs:423`) — add `AudioManager.Instance?.PlayUi(uiButtonPress)` to the created `btn.onClick`; use `uiStart` for the ANZA!/play-again rows.
   - team chip button (`KenyaMenuScreens.cs:239`) → `uiSelect`.
   - `ThemedEndScreen` / `EndScreen` / `CheckpointScreen` **Next** button (e.g. `ThemedEndScreen.cs:156`) → `uiButtonPress`.
   - `SettingsPanel.ToggleGyro / ToggleRealRider / TogglePanel` and `LangToggle.Toggle` → `uiToggle` (and `uiPanelOpen`/`uiBack` on panel show/hide).
   - `LeaderboardUI` confirm-wipe handler → `uiConfirm`.

That's a self-contained ticket; **say the word and I'll write the field block + `PlayUi` + the call-site hooks** (I still can't download clips or assign them in the Unity Inspector — that part stays with you).

---

## ⚠️ Fields in the OLD doc that are NOT wired in the current build

The current `AudioConfig`/`TrafficHorn` have **no** field for these, so there is nothing to assign them to today. Keep the sources for if/when the features land, but **don't spend download time on them now** unless the code is extended:

| Old-doc clip | Status in current code | Source kept on file (verify before use) |
|---|---|---|
| `sessionStartClip` / `sessionEndClip` | `SessionStarted` event exists but `AudioManager` plays **no** start/end cue | GameAudio "Game Audio - UI SFX" pack (freesound 13940) |
| `goatScatterClip` | No field; no event handler | "A herd of goat is eating" by felix.blume (131925, CC0) |
| `elephantClip` | No field; no event handler | "Elephants Trumpeting" by craigsmith (437973, CC0) |
| `dustDevilClip` | No field; no event handler | "Whoosh" by qubodup (60013, CC0) |

If you want these in the game, that's a **code change** (new `AudioConfig` fields + `AudioManager` handlers, or a new event) — flag it to the dev owning audio.

---

## (1) Download & import checklist

Tarmac engine + ambience + SFX, in priority order. Tick as you go.

**Engine / loops**
- [ ] `motorLoop` — IENBA "Electric Scooter" (CC0) → trim to steady middle, loop
- [ ] `motorStart` / `motorStop` — the head & tail off-cuts of that same IENBA clip (CC0) → save as two short one-shots *(optional, now wired)*
- [ ] `windLoop` — florianreichelt "Soft Wind" (CC0) → loop
- [ ] `surfaceLoop` — orlandorizo "Car rolling on asphalt" (CC0) → loop
- [ ] *(standby)* murram — SpliceSound "Bike tire on dirt road" (CC0)

**Ambience** (set each `contextTag` to match the RoadSequence tags)
- [ ] `savanna` — AugustSandberg "African savanna 2" (CC0) → loop
- [ ] `township` — mikewest "African market atmos" (CC0) [+ felix.blume "A village in Africa" (CC0)] → loop
- [ ] `wildlife` — InspectorJ "Ambience, Night Wildlife, A" (**CC BY 4.0 → credit**) → loop
- [ ] `highland` — Benjamin152 "Nature Ambiance" (**CC BY 3.0 → credit**) → loop

**Music**
- [ ] `musicLoop` — Pixabay: Delon_Boomkin "Afrobeat 2026 Instrumental…" (or "The Afrobeat" / MusicInMedia loop) → preview, confirm instrumental, loop

**SFX**
- [ ] `overtakeSting` — IENBA "Game Reward" (CC0)
- [ ] `nearMissScreech` — craigsmith "Tire Squeals" (CC0) → trim
- [ ] `crashHard` — qubodup "Collision" (CC0)
- [ ] `crashLight` — qubodup "Fast Collision Reverb" (CC0)
- [ ] `potholeBump` — bsumusictech "Car hitting pot hole" (CC0)
- [ ] `wrongLaneBuzz` — SamsterBirdies "Beep warning" (CC0) → trim short
- [ ] `graceSaved` — colorsCrimsonTears "Heal - Rpg" (CC0)
- [ ] `rewindWhoosh` — al_sub "Reverse vinyl scratch / warp" (CC0)
- [ ] `checkpointArrive` — colorsCrimsonTears "Goal cleared" (CC0) → trim head
- [ ] `hadadaIbis` — Pixabay "hadedas and thunder" (Pixabay) → trim, reduce thunder ⚠️ (or NC xeno-canto for internal builds only)

**Horns (on the matatu prefab's `TrafficHorn`, not AudioConfig)**
- [ ] `hornClips[0..2]` — AmishRob 423990 + DeVern 349922 + keweldog 182474 (CC0) — or Mixkit Truck/Car/Double horn

**UI sounds (needs code wiring first — all CC0, fastest via the Kenney pack)**
- [ ] Grab the Kenney "Interface Sounds" pack (CC0) — covers click / switch / confirm / error / back in one download
- [ ] `uiButtonPress` — el_boss "UI Button Click" (CC0) → trim < 100 ms
- [ ] `uiStart` — el_boss "UI Button Click Snap" / Kenney confirmation (CC0)
- [ ] `uiSelect` — Fupicat "Videogame Menu Select" (CC0)
- [ ] `uiToggle` — Breviceps "Light Switch (on / off)" (CC0)
- [ ] `uiConfirm` — colorsCrimsonTears "Goal cleared" (CC0) → trim
- [ ] `uiBack` — Kenney back / soft low click (CC0)
- [ ] `uiError` — Kenney error / Breviceps buzzer (CC0) → keep gentle
- [ ] `uiPanelOpen` *(optional)* — qubodup "Whoosh" (CC0)
- [ ] **Code:** add UI fields to `AudioConfig`, a public `AudioManager.PlayUi(...)`, and call it from the button helpers (see UI section)

---

## (2) Credits-screen attribution block

Paste into the in-game credits. With the recommended picks, **only two clips require attribution** (both CC BY) — plus the ⚠️ line *only if* the non-commercial Hadada is used in an internal build.

```
Audio
— "Ambience, Night Wildlife, A" by InspectorJ (freesound.org/people/InspectorJ/sounds/352514/), licensed under CC BY 4.0.
— "Nature Ambiance" by Benjamin152 (freesound.org/people/Benjamin152/sounds/445144/), licensed under CC BY 3.0.

Additional sound effects and music: freesound.org (CC0 / Public Domain), Pixabay (Pixabay Content License),
and Mixkit (Mixkit Free License). No attribution required.
```

**⚠️ Add this line ONLY in internal/educational builds that use the non-commercial Hadada call — and remove it (and the clip) before any paid release:**
```
— Hadada ibis call: Bostrychia hagedash, XC195524, by Rory Nefdt (xeno-canto.org/195524), CC BY-NC-SA 4.0. Non-commercial use only.
```

If you swap any backup in, add its credit too — only the **CC BY** ones need it: FoolBoyMedia "Complete Chime" (352661, CC BY 4.0) is the only backup above that would.

---

## (3) Still unfilled / needs a decision

- **Nothing is blocking** — every active `AudioConfig` field and the `TrafficHorn` horns have a verified, commercial-safe recommendation.
- **`hadadaIbis`** — commercial-safe pick (Pixabay "hadedas and thunder") needs **thunder cleanup** in Audacity; if you'd rather have a clean dry call you may need a paid library (e.g. Soundsnap/Epidemic) before release. The pristine xeno-canto recording stays ⚠️ non-commercial.
- **`wildlife` ambience** — the verified pick is non-African (Florida crickets, CC BY). Decide: accept it, reuse the CC0 Kenyan savanna bed, or source a dedicated Tsavo daytime recording.
- **`musicLoop`** — Pixabay track pages couldn't be auto-opened; **preview the 2–3 candidates** and pick the one that ducks cleanly. Low-risk, just needs your ear.
- **murram surface + session/goat/elephant/dust-devil** — these are **not wired in code** (see the ⚠️ table above). They need code work before any clip matters; sources are parked for that day.
- **UI sounds (button presses + more)** — clips are sourced and CC0, but the UI plays **no audio yet** and `AudioConfig` has no UI fields. Needs the small wiring change in the UI section (new fields + public `AudioManager.PlayUi` + ~6 call-site hooks). I can write that code on request; downloading/assigning the clips stays with you. **This is the only "and more" item that needs dev work, not just a download.**

---

### Verification note
Every freesound recommendation above, plus the Mixkit car-horn page and the Kenney "Interface Sounds" pack (confirmed CC0), was opened and its licence label read on the source page in June 2026. Pixabay pages (music + the hadeda SFX) block automated fetching, so those were confirmed via Pixabay's indexed search listings and are flagged "confirm on page" — the licence shown there is the standard Pixabay Content License badge. "Confirm licence" backups were surfaced by search but not individually opened.
