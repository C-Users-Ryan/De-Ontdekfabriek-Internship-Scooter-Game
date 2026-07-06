# Audio — Do This (download → trim → auto-assign)

The shortest path from "nothing imported" to "all wired". Three steps:

1. **Download** each clip and **save it with the exact filename** in the sheet below.
2. **Trim/loop** it using the recipe (only the loops and a few SFX need any editing).
3. Drop everything into **`Assets/Audio/`** and run **Tools → Kenya Scooter → Assign Audio Clips** — it fills the whole `AudioConfig` and the matatu horns for you. No Inspector dragging.

> The filenames are the magic: the tool matches each file to its `AudioConfig` field by name (case-insensitive, any of `.wav .mp3 .ogg .aiff`). Get the names right and assignment is one click. Full details/licences live in `Audio Sourcing — Ready to Download.md`; how-well-it-fits ratings in `Audio Sources — Style Match Quick List.md`.

---

## Step 1 — Download & rename sheet

Tick as you save each. **Save-as name** is what the file must be called.

### freesound.org  (all CC0 unless flagged)
| Save as | Clip | Page |
|---|---|---|
| `motorLoop` | Electric Scooter — IENBA | https://freesound.org/people/IENBA/sounds/697379/ |
| `windLoop` | Soft Wind — florianreichelt | https://freesound.org/people/florianreichelt/sounds/459977/ |
| `surfaceLoop` | Car rolling on asphalt — orlandorizo | https://freesound.org/people/orlandorizo/sounds/591079/ |
| `savanna` | African savanna 2 (Masai Mara) — AugustSandberg | https://freesound.org/people/AugustSandberg/sounds/202876/ |
| `township` | African market atmos — mikewest | https://freesound.org/people/mikewest/sounds/407641/ |
| `wildlife` ⚠️ CC BY | Ambience, Night Wildlife — InspectorJ | https://freesound.org/people/InspectorJ/sounds/352514/ |
| `highland` ⚠️ CC BY | Nature Ambiance — Benjamin152 | https://freesound.org/people/Benjamin152/sounds/445144/ |
| `overtakeSting` | Game Reward — IENBA | https://freesound.org/people/IENBA/sounds/656643/ |
| `nearMissScreech` | Tire Squeals — craigsmith | https://freesound.org/people/craigsmith/sounds/438638/ |
| `crashHard` | Collision — qubodup | https://freesound.org/people/qubodup/sounds/332058/ |
| `crashLight` | Fast Collision Reverb — qubodup | https://freesound.org/people/qubodup/sounds/332057/ |
| `potholeBump` | Car hitting pot hole — bsumusictech | https://freesound.org/s/62532/ |
| `wrongLaneBuzz` | Beep warning — SamsterBirdies | https://freesound.org/people/SamsterBirdies/sounds/467882/ |
| `graceSaved` | Heal - Rpg — colorsCrimsonTears | https://freesound.org/people/colorsCrimsonTears/sounds/562292/ |
| `rewindWhoosh` | Reverse vinyl scratch / warp — al_sub | https://freesound.org/people/al_sub/sounds/647161/ |
| `checkpointArrive` | Goal cleared — colorsCrimsonTears | https://freesound.org/people/colorsCrimsonTears/sounds/545697/ |
| `hornClip1` | Car horn beep beep — AmishRob | https://freesound.org/people/AmishRob/sounds/423990/ |
| `hornClip2` | Car Horn Honk — DeVern | https://freesound.org/people/DeVern/sounds/349922/ |
| `hornClip3` | car horn — keweldog (confirm licence) | https://freesound.org/people/keweldog/sounds/182474/ |

### Pixabay  (Pixabay licence — keep the `.mp3`, no rename of extension needed)
| Save as | Clip | Page |
|---|---|---|
| `musicLoop` | Afrobeat 2026 Instrumental — Delon_Boomkin (preview first) | https://pixabay.com/music/upbeat-afrobeat-2026-instrumental-music-background-327258/ |
| `hadadaIbis` | hadedas and thunder | https://pixabay.com/sound-effects/hadedas-and-thunder-75672/ |

### No download needed
| Save as | How |
|---|---|
| `motorStart` | the **spin-up off-cut** you trim from the front of `motorLoop` (see Step 2) |
| `motorStop` | the **spin-down off-cut** from the tail of `motorLoop` |

> **UI button sounds are not in this sheet** — they need the small code wiring first (no `AudioConfig` fields yet, so the tool has nothing to assign them to). Say the word and I'll add the fields + hooks, then they slot into this same flow. Sources are ready in the main doc (Kenney CC0 pack).

---

## Step 2 — Trim & loop recipe (Audacity)

Most SFX are fine as-is. The **loops** are the only ones that really need care. General export: `File → Export → Export as WAV` (16-bit PCM), named exactly as above. (Pixabay `.mp3` can stay mp3.)

### A. Seamless loops — `motorLoop`, `windLoop`, `surfaceLoop`, `savanna`, `township`, `wildlife`, `highland`, `musicLoop`
1. Import, listen, and **select a steady middle section** — no fade-in, no fade-out, no one-off events.
2. `Edit → Remove Special → Trim Audio` (Ctrl+Alt+T) to keep only that selection.
3. **Snap the ends to zero crossings:** select all (Ctrl+A), then press **Z** (`Select → At Zero Crossings`). This kills most loop clicks.
4. **Test it:** turn on loop playback (`Transport → Looping`, or hold Shift and press Space) and listen at the seam.
5. **Still clicking?** Add a tiny crossfade: `Effect → Crossfade Loop` (or fade the first/last ~15 ms with `Effect → Fade In`/`Fade Out`).
6. Export as `<name>.wav`. (In Unity you don't need to tick "Loop" — the code loops these.)

### B. Engine off-cuts — `motorStart`, `motorStop`  (from the IENBA `motorLoop` clip)
1. Open the original Electric Scooter clip (before trimming).
2. Select just the **spin-up** at the very start (~0.4–1.0 s) → `File → Export → Export Selected Audio` → `motorStart.wav`. Add a short `Fade Out` on its tail so it melts into the loop.
3. Select the **spin-down** at the very end → export as `motorStop.wav`. A short `Fade In` on its head helps it blend.

### C. One-shots — `overtakeSting`, `nearMissScreech`, `crashHard`, `crashLight`, `potholeBump`, `wrongLaneBuzz`, `graceSaved`, `rewindWhoosh`, `checkpointArrive`, `hornClip1–3`
1. **Trim leading/trailing silence** so the sound triggers instantly.
2. Special cases:
   - `nearMissScreech` — keep only the single sharpest screech.
   - `checkpointArrive` (Goal cleared is ~11 s) — keep just the first short hit (~1 s).
   - `wrongLaneBuzz` / `graceSaved` — keep them short and soft (they can fire repeatedly).
3. **Even out loudness:** `Effect → Normalize` to about −3 dB. Keep SFX a touch quieter than music.
4. Export each as `<name>.wav`.

### D. Special clean-ups
- **`hadadaIbis`** (hadedas and thunder): select the cleanest "haa-haa-de-dah" stretch with the least thunder; if thunder still bleeds, `Effect → Noise Reduction` (profile a thunder-only moment, then reduce) or roll off lows. Export as `hadadaIbis.wav`.
- **`township` layering (optional):** import the market clip + "A village in Africa" (felix.blume 174445, CC0), drop the village ~8 dB lower, `Tracks → Mix → Mix and Render`, then loop per recipe A and export as `township.wav`.

---

## Step 3 — Auto-assign

1. Put every exported file anywhere under **`Assets/Audio/`** (the tool searches the whole project by name, so subfolders are fine).
2. Let Unity import them.
3. Run **Tools → Kenya Scooter → Assign Audio Clips**.
4. A summary dialog tells you **how many clips it assigned** and **lists anything still missing** by field name. Add the missing files and re-run — partial runs are safe.

What it does for you:
- Fills every `AudioConfig` clip field (engine, wind, surface, spin-up/down, music, all SFX).
- Builds `ambientVariants` from `savanna` / `township` / `wildlife` / `highland`, with the right context tags.
- Writes `hornClip*` into the `hornClips[]` of every `TrafficHorn` prefab.

---

## The handful of things still on you

- **Pick the music track** — preview the Pixabay candidates and choose one that's instrumental and survives the crash duck (taste call, can't be automated).
- **Hadeda thunder cleanup** — manual editing (Step 2D).
- **Two credit lines** — `highland` and `wildlife` are CC BY; add them to the credits screen (block is in the main doc). Everything else needs no attribution.
- **UI button sounds** — need the code wiring before they can be assigned; ping me to add it.
