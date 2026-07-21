# Guide — Dev — The Settings Menu: Add, Change and Remove Settings

For the developer who inherits this project. The facilitator settings menu is fully **data-driven**: you add, change or remove a setting by editing ONE list in ONE file, and the store and the UI follow automatically. This guide explains the architecture in five minutes, then walks through the three jobs you'll actually do — adding a setting, changing a default, and removing a setting (including the one gotcha that can bite you).

All paths are under `Assets/Impact Makers Around The world/Scripts/Settings/` unless noted.

---

## 1. The 30-second map

```
SettingsCatalog  (the DATA — what exists)
      │  SettingsCatalog.cs             → query API, presets, category labels, value formatters
      │  SettingsCatalog.Definitions.cs → Build(): THE list of every setting  ← you edit this one
      │  SettingDefinition.cs           → what one setting IS (key, label, range, get/set…)
      │  SettingsPreset.cs              → one-tap profile (bundle of setting values)
      ▼
GameSettings.cs  (the PERSISTENCE — what the facilitator chose)
      │  PlayerPrefs: "ksg.set.<key>" = the override, "ksg.def.<key>" = the captured default
      │  Applies saved overrides onto the live configs at boot, before the first scene Awake.
      ▼
SettingsMenu     (the UI — rendered procedurally, no prefabs)
         SettingsMenu.cs       → state, PIN lock, toasts
         SettingsMenu.Build.cs → sidebar, rows, widgets, profiles page, search
```

A setting is a **single float** with a getter and a setter. The setter usually writes a field on a shared ScriptableObject config (via `ConfigLocator`), so every system that reads that config picks the change up immediately. Toggles are floats too (0/1). There is no JSON, no schema, no reflection — one list, plain closures.

## 2. Anatomy of one setting

```csharp
list.Add(new SettingDefinition(
    "feel.maxSpeed",                          // stable KEY — used in PlayerPrefs and presets. Never rename casually.
    "Topsnelheid",                            // label the facilitator sees (plain Dutch, never a field name)
    "De hoogste snelheid die met gas haalbaar is.",  // one-line description under the label
    SettingCategory.SpeedFeel,                // which sidebar category it renders in
    SettingWidget.Slider,                     // Slider | Stepper | Toggle (how it renders)
    12f, 34f, 1f,                             // min, max, step — the safe range; the store COERCES into this
    get: () => ConfigLocator.Scooter != null ? ConfigLocator.Scooter.maxSpeed : 30f,
    set: v  => { var s = ConfigLocator.Scooter; if (s != null) s.maxSpeed = Mathf.Max(v, s.baseSpeed); },
    format: Kmh));                            // optional pretty-printer: shows "79 km/u", not "22"
```

Things worth knowing:

- **Key naming**: `<area>.<name>` (`feel.`, `diff.`, `world.`, `env.`, `safe.`, `ctrl.`, `audio.`, `score.`, `session.`, `mgmt.`). The key is the identity — it's what PlayerPrefs, presets and the custom save-slots store.
- **The getter must be null-safe** (return a sensible fallback) — the catalog is built before any scene loads.
- **The setter is where clamps/invariants live** (e.g. max speed never below cruise speed).
- **Formatters** turn floats into words (`OnOff`, `Kmh`, `Seconds`, `Percent`, `Band(...)` for NORMAAL/SNEL-style words). They live at the bottom of `SettingsCatalog.cs`. A facilitator should never see a bare number that needs interpreting.
- **Basic vs "Meer opties"**: each category page opens with its essentials; a setting listed in the `_advanced` HashSet (bottom of `SettingsCatalog.cs`) renders only after the **MEER OPTIES** button. New finer-tuning settings should go in that set.
- **Actions** (buttons like "Klassement wissen") are the same idea: one `SettingAction` entry in `BuildActions()`, with an optional confirm-on-second-tap flag.

## 3. How persistence works (read this before changing defaults)

At boot (`GameSettings.ApplyOnStartup`, before the first scene's Awake):

1. `CleanRetiredKeys()` — deletes PlayerPrefs residue of settings that were removed (see §6).
2. `CaptureBaselinesIfNeeded()` — the FIRST run ever stores each setting's current value as its default under `ksg.def.<key>`. **The first build to run defines the baseline** — a later build changing a script default does NOT update an already-deployed device's captured default. Known trade-off.
3. `ApplyAll()` — every setting with a saved override (`ksg.set.<key>`) gets it pushed through its setter, coerced into the min/max/step range.

While the menu is open, editing a row writes the override AND calls the setter, so changes are live immediately. The per-row ↺ resets one setting; "Alles terug naar standaard" (`GameSettings.ResetAll`) clears every override back to the captured defaults.

**The ScriptableObject gotcha (bites everyone once):** changing a default value in a config *script* (e.g. `ScooterConfig.cs → maxSpeed = 22f`) does NOT change the shipped `.asset` — field initialisers only seed newly created assets. To change a shipped default you must edit the **asset** in the Inspector too. And on devices that already ran the game, the OLD value is captured in `ksg.def.*` — either accept that (fine when the change is minor), reinstall the app, or set the new value once via the menu.

## 4. Adding a setting — checklist

1. Open `SettingsCatalog.Definitions.cs`, find the right category section, add one `SettingDefinition` (copy a neighbour).
2. Getter/setter → the right config field via `ConfigLocator` (or a static/PlayerPrefs if the feature has no config — see the gotcha in §6 if you ever remove it again).
3. Pick min/max/step so the range can't break the game (a facilitator can never type a value — they only get your range).
4. Pick or write a formatter so the value reads as words.
5. Finer-tuning setting? Add its key to `_advanced` in `SettingsCatalog.cs`.
6. That's it — the menu, search, overview page, per-row reset, custom save-slots and the override store all pick it up automatically. Recompile and check the category page.
7. Should any built-in profile set it? Add the key to that preset's dictionary in `BuildPresets()`.

## 5. The profiles (presets)

`BuildPresets()` in `SettingsCatalog.cs`. Since 2026-07-14 the lineup is deliberately small:

- **Two MAIN MODES** (`primary: true` → the page highlights them with an accent ring + HOOFDMODUS chip):
  - `preset.standard` — `resetToDefault: true` **plus** a values dictionary. Reset profiles clear every override FIRST, then write their listed values (see `SettingsPreset.Apply`) — that's how Standaard guarantees right-side driving + 79 km/u on any device, regardless of what its captured defaults say.
  - `preset.endless` — flips only `session.endless`.
- **Two VARIANTS** (`preset.rustig`, `preset.challenge`) — intensity-only bundles that never touch `session.endless`, so they stack on either main mode. They also set `session.endlessLives` (5 / 2), which is inert in the relay.

A profile is **additive**: it writes only the keys it lists. Keys not in the catalog are silently skipped — so a profile can never crash on a removed setting, it just stops affecting it.

The facilitator's own slots (`CustomPresets.cs`, "Mijn profiel 1/2/3", `ksg.slot.<n>.*`) snapshot every non-Management catalog setting and re-apply the lot. Removed settings degrade gracefully there too (their stored values are simply no longer iterated).

## 6. Removing a setting — checklist (with the one real gotcha)

1. Delete (or comment out, with a dated note — the codebase's convention) its entry in `SettingsCatalog.Definitions.cs`.
2. Remove its key from `_advanced` and from any preset dictionary that listed it. (Both are safe to miss — unknown keys are skipped — but keep the data honest.)
3. Delete its now-unused formatter if it had a dedicated one.
4. **THE GOTCHA — check what the setter wrote:**
   - Setter wrote a **config field** (`ConfigLocator.X.field = v`)? Nothing persists by itself — done. The config returns to its asset value next boot.
   - Setter wrote a **raw PlayerPrefs key** the game reads directly (e.g. Regio-reis wrote `ksg.regionJourney`, which `RoadSequencer` reads every zone pick)? Then a device where a facilitator once switched it ON would keep that behaviour FOREVER — with no UI left to switch it off. Add the raw key (plus the setting's `ksg.set.`/`ksg.def.` entries, for hygiene) to `GameSettings.CleanRetiredKeys()`, which wipes them at boot.
5. Leave the underlying feature code in place unless it's truly dead — a future team re-exposes it by re-adding one definition (and removing the key from the retired list).

Worked example: the 2026-07-14 removals (`world.pedestrians`, `env.roadsideLife`, `env.roadsideDensity`, `env.regionJourney`) — see the dated comments at each removal site and the retired list in `GameSettings.cs`.

## 7. How the menu itself works (only if you need to touch UI)

- Everything is built in code from `UiKit` tokens — no prefabs, no scene wiring. `SettingsMenu.Build.cs` builds the sheet: header (title + close), sidebar (search box + one button per non-empty category + orange changed-count badges), and the row host.
- `ShowCategory(cat)` clears the row host and rebuilds: blurb, essential rows, the **MEER OPTIES (n)** reveal (state: `showAdvanced`, always collapsed on entering a category), category reset, and for Profielen the preset cards + custom slots.
- Widgets are built per `SettingWidget` in `BuildRow`; each row shows label, description, the widget, the formatted value, a per-row ↺ and an orange changed dot.
- The **search box** filters across ALL categories and ignores the basic/advanced split (the user searched; show everything).
- `ShowToast(...)` is the confirmation pill (built on the menu root so page repaints can't destroy it).
- The menu sits behind the numeric access code (`FacilitatorLock`, `SettingsMenu` keypad states); settings themselves are menu-reachable only.

## 8. Verifying a change

Unity's EditMode tests for this system live in `Tests~/Editor/Settings/` — the trailing `~` means Unity IGNORES the folder (deliberate: no test framework in the kiosk build). To run them, temporarily rename `Tests~` → `Tests`, run in the Test Runner, rename back. `SettingsCatalogIntegrityTests` checks every definition has a label/description/valid range — extend it when you add settings.

Manual smoke test after any catalog change: open the menu → the category renders → change the value → close and reopen (persisted?) → per-row ↺ → "Alles terug naar standaard" → apply each profile card and check the toast + ACTIEF chip.

---

## Connecties

- [[FACILITATOR — Changing the Game]] — the facilitator-facing manual for the same menu.
- [[GIDS — Begeleider — Modi en Profielen]] — the profile lineup and the two main modes.
- [[Facilitator Settings Menu and the Override Architecture]] — the original design devlog (why data-driven, why PlayerPrefs).
- [[HANDOVER — Start Here]] — the handover hub.
