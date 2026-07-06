# Audio Sources — Style-Match Quick List

A fast, scannable companion to **`Audio Sourcing — Ready to Download.md`** (full URLs, attribution strings and processing notes live there). Here each pick is rated on **how well it fits the game's style**, not just whether it's legal.

**Style being matched:** Kenya, the A109 Nairobi → Mombasa, an **electric** scooter, **upbeat & respectful East-African** tone, a **kids' museum exhibition** (Swahili + Dutch).

**Match scale**
- ★★★★★ — bang-on (authentic Kenyan/East-African, or literally the right object)
- ★★★★ — strong fit, neutral or near-region
- ★★★ — works, but generic / slightly off-flavour
- ★★ — functional placeholder, noticeably off-style → consider replacing
- 🌍 = genuine African field recording · ⚡ = real electric source · ⚠️ = licence or cleanup caveat

---

## Engine & looping layers
| Field | Clip (author) | Find it | Licence | Match | Why |
|---|---|---|---|---|---|
| `motorLoop` | "Electric Scooter" (IENBA) | freesound 697379 | CC0 | ★★★★★ ⚡ | Actually an electric scooter free-wheel — exactly the right vehicle, not a petrol fake. |
| `motorStart` / `motorStop` | head & tail off-cuts of the same IENBA clip | freesound 697379 | CC0 | ★★★★★ ⚡ | Same real e-scooter recording — the wind-up/down match the loop perfectly. Now wired (plays on run start / end). |
| `windLoop` | "Soft Wind" (florianreichelt) | freesound 459977 | CC0 | ★★★★ | Clean, universal wind. Fits any open road; not location-specific (it doesn't need to be). |
| `surfaceLoop` (tarmac) | "Car rolling on asphalt" (orlandorizo) | freesound 591079 | CC0 | ★★★★ | Neutral tyre-on-tarmac. Right texture for the highway; nothing distinctly Kenyan needed here. |
| murram variant *(not wired)* | "Bike tire on dirt road" (SpliceSound) | freesound 218301 | CC0 | ★★★★ | Good dirt/murram texture for the rough stretches — just needs code to swap it in. |

## Ambience beds
| Field | Clip (author) | Find it | Licence | Match | Why |
|---|---|---|---|---|---|
| `savanna` | "African savanna 2" (AugustSandberg) | freesound 202876 | CC0 | ★★★★★ 🌍 | **Recorded in Masai Mara, Kenya.** Peak authenticity — the standout of the whole set. |
| `township` | "African market atmos" (mikewest) + "A village in Africa" (felix.blume) | freesound 407641 + 174445 | CC0 | ★★★★ 🌍 | Real African market + village (Zambia / Mali). Authentically African, just not Kenya-specific. |
| `highland` | "Nature Ambiance" (Benjamin152) | freesound 445144 | CC BY 3.0 ⚠️ | ★★★★ 🌍 | African dawn birds (South Africa) — right fresh-highland feel; neighbouring country. Needs a credit line. |
| `wildlife` | "Ambience, Night Wildlife" (InspectorJ) | freesound 352514 | CC BY 4.0 ⚠️ | ★★ | **Florida crickets — wrong continent.** Works as texture but is the one real style mismatch → see "swap ideas". |

## Music
| Field | Clip (author) | Find it | Licence | Match | Why |
|---|---|---|---|---|---|
| `musicLoop` | "Afrobeat 2026 Instrumental…" (Delon_Boomkin) / "The Afrobeat" / MusicInMedia loop | Pixabay | Pixabay ⚠️ preview | ★★★★ | Modern, upbeat, **respectful** instrumental Afrobeat — avoids the "tribal-percussion" cliché. (Afrobeat is pan-African pop, not Kenya-specific Benga/Genge, but reads right and is easy to source clean.) Confirm it ducks nicely. |

## Gameplay SFX
| Field | Clip (author) | Find it | Licence | Match | Why |
|---|---|---|---|---|---|
| `potholeBump` | "Car hitting pot hole" (bsumusictech) | freesound 62532 | CC0 | ★★★★★ | Literally a car hitting a pothole — and potholes are a genuine A109 theme. On the nose. |
| `overtakeSting` | "Game Reward" (IENBA) | freesound 656643 | CC0 | ★★★★ | Bright, positive pickup sting — right energy for a kids' reward. Universal. |
| `nearMissScreech` | "Tire Squeals" (craigsmith) | freesound 438638 | CC0 | ★★★★ | Sharp, readable near-miss. Trim to one screech. Universal. |
| `crashHard` | "Collision" (qubodup) | freesound 332058 | CC0 | ★★★★ | Solid vehicle impact. Universal. |
| `crashLight` | "Fast Collision Reverb" (qubodup) | freesound 332057 | CC0 | ★★★★ | Same family, lighter — keeps hard/light coherent. |
| `wrongLaneBuzz` | "Beep warning" (SamsterBirdies) | freesound 467882 | CC0 | ★★★★ | Clean car-style warning beep. Neutral, repeatable. |
| `rewindWhoosh` | "Reverse vinyl scratch / warp" (al_sub) | freesound 647161 | CC0 | ★★★★ | Convincing time-rewind feel for the safety-net rewind. |
| `hadadaIbis` | "hadedas and thunder" (Pixabay) | Pixabay 75672 | Pixabay ⚠️ | ★★★★ 🌍 | **Real hadeda ibis** — the signature dawn sound. Authentic species; needs the thunder trimmed out. (The pristine xeno-canto cut is ★★★★★ but ⚠️ non-commercial.) |
| `graceSaved` | "Heal - Rpg" (colorsCrimsonTears) | freesound 562292 | CC0 | ★★★ | Positive "saved" cue, but slightly RPG-magic flavoured. Fine for a kids' game; swap if you want something more grounded. |
| `checkpointArrive` | "Goal cleared" (colorsCrimsonTears) | freesound 545697 | CC0 | ★★★ | Cheerful victory jingle, but a touch 8-bit/arcade. Works; trim to a short hit. |

## Matatu horns — `TrafficHorn.hornClips[]`
| Field | Clip (author) | Find it | Licence | Match | Why |
|---|---|---|---|---|---|
| `hornClips[]` (×2–3) | AmishRob 423990 + DeVern 349922 + keweldog 182474 — or Mixkit Truck/Car/Double horn | freesound / Mixkit | CC0 / Mixkit | ★★★ | Functional, and lower-pitched ones read as van/bus = matatu. But real matatus have distinctive, almost musical horns — these are generic. Good enough for now; an upgrade target if you find authentic matatu honks. |

## UI sounds *(sourced; needs code wiring — see main doc)*
| Field | Clip (author) | Find it | Licence | Match | Why |
|---|---|---|---|---|---|
| `uiButtonPress` / `uiStart` / `uiBack` / `uiConfirm` / `uiError` | **Kenney "Interface Sounds" pack** (one download) | kenney.nl | CC0 | ★★★★ | Clean, warm, consistent UI set. UI doesn't need cultural specificity — it needs to be soft and unobtrusive, which this is. |
| `uiToggle` | "Light Switch (on / off)" (Breviceps) | freesound 457037 | CC0 | ★★★★ | Tactile, satisfying toggle for the settings/language switches. |
| `uiSelect` (team chip) | "Videogame Menu Select" (Fupicat) | freesound 471937 | CC0 | ★★★ | Does the job, but it's 8-bit — may clash with the warm look. Prefer a softer Kenney tap for a more organic feel. |

---

## At a glance

**The stars of the set (lean into these):**
- 🌍 `savanna` — real Masai Mara recording (★★★★★)
- ⚡ `motorLoop` — a real electric scooter (★★★★★)
- `potholeBump` — a literal pothole, and it's thematic (★★★★★)
- 🌍 `hadadaIbis` — the authentic signature dawn call (★★★★, ★★★★★ once cleaned)
- `musicLoop` — respectful modern Afrobeat, not a stereotype (★★★★)

**The one real style mismatch to decide on:**
- `wildlife` (★★) — Florida crickets. **Swap ideas:** reuse the CC0 Masai Mara `savanna` bed for the Tsavo/wildlife zone, or source a dedicated daytime East-African bush recording. Everything else is at least a strong fit.

**Minor "could be more Kenyan" upgrades (optional, not blocking):**
- Matatu horns (★★★) — generic car/truck horns stand in for the iconic matatu honk.
- `checkpointArrive` / `graceSaved` (★★★) — slightly arcade/RPG-flavoured; fine for a kids' game.
- `uiSelect` (★★★) — 8-bit tone; a softer tap fits the warm UI better.

**Licence health:** of everything above, only **two clips** need a credits line (`highland`, `wildlife` — both CC BY); the rest is CC0, Pixabay, or Mixkit (no attribution). The only ⚠️ commercial blocker is the *alternate* xeno-canto hadeda, which you're not using as the primary.
