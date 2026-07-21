# Handover: Package Manifest and Asset-Licence Check

**For:** Ryan to fill in before the internship ends, then hand the completed copy to De Ontdekfabriek.
**What it is:** two fill-in lists. Part 1 is the contents-and-credentials manifest for the physical handover package (so nothing is forgotten and the irreplaceable keystore passwords are recorded in one controlled place). Part 2 is the asset-licence check from the [[Handover — Build, Keystore and APK Rebuild Checklist|build checklist]] Part E, pre-filled with every third-party asset pack actually imported in the project, for Ryan to confirm the right to keep showing the game in public.

> [!warning] This file will hold secrets once filled in
> Part 1 records keystore passwords. Do **not** commit the filled-in version to the public git repo. Keep it with the keystore file itself (USB + a cloud drive De Ontdekfabriek owns), or store the passwords in a password manager and write only "see password manager" here.

---

## Part 1: handover package manifest (fill in and tick)

The single folder/USB (plus an owned cloud copy) handed to De Ontdekfabriek:

**Files**
- [ ] Final signed APK: `__________________________.apk` (version: `________`)
- [ ] Signing keystore file: `__________________________.keystore`
- [ ] Source project: GitHub repo URL `__________________________________`
- [ ] This filled-in manifest + the full handover document set (also shipped in the repo's `HANDOVER/` folder, see [[HANDOVER — Start Here]]) + the printed facilitator cards

**Credentials (keystore, the irreplaceable part)**
- Keystore password: `________________`
- Key alias name: `________________`
- Key alias password: `________________`
- Keystore backed up in a 2nd location? `☐ USB  ☐ cloud  ☐ password manager`

**Ownership and access**
- [ ] GitHub repo transferred to / owned by: `__________________________` (an account De Ontdekfabriek controls, not a student account that may be deleted)
- [ ] Unity version recorded for a future rebuild: **6000.3.10f1**
- [ ] Asset-licence check (Part 2 below) completed and any flagged pack resolved

**Tablets**
- [ ] Live tablet set up per the [[Handover — Tablet Setup and Golden-Image Checklist|tablet checklist]] and screen-pinned
- [ ] Spare/golden-image tablet prepared identically
- [ ] Chargers and any mount included

---

## Part 2: asset-licence check (confirm each, before the final build)

Because the game is shown publicly in a maker-space, every imported third-party asset must permit **commercial / public-display** use. Below is every third-party pack currently imported under `Assets/` (read from the project on 2026-06-25). For each: confirm where it came from and that its licence allows public display, and **delete the ones the build does not actually use** so they carry no licence question and shrink the project.

> [!success] Asset-provenance flag — found and resolved (2026-07-06)
> **Found during this check:** `Assets/Low Poly Arid Desert Environment/README.txt` showed the pack had come from a redistribution site (`unityassetcollection.com`), not the original Asset Store author — a copy obtained that way is not licensed for public display.
> **Resolved:** the pack has been **replaced with a legitimately purchased copy** from the Unity Asset Store, downloaded under Ryan's own account and swapped in over the redistribution-site download in `Assets/`. Provenance now traces to a real purchase and the Asset Store standard EULA covers public display, so **nothing further needs to be acquired** for this pack — this flag is **closed**.
> **On file / to check:** keep the Asset Store **invoice / order confirmation** with the handover credentials as proof of licence; after the swap, confirm in Unity there are no missing-prefab / pink (missing-shader) material references (regenerated `.meta` GUIDs can break existing scene references).
> **Other packs:** still confirm each remaining row's provenance traces to a real purchase/download under your own Asset Store account.

| Pack (under `Assets/`) | Used in the build? | Where it came from (fill in) | Licence allows public display? | Action |
|---|---|---|---|---|
| `Low Poly Simple Urban City 3D Asset Pack` | **Yes** (roads, crossroads; referenced in code) | `____________` | `____________` | confirm licence |
| `Toon Series` | **Likely** (the Toon Gas Station / charge-station model) | `____________` | `____________` | confirm licence |
| `Pack_FREE_Cars` | **Likely** (traffic vehicles; "FREE" implies a free pack) | `____________` | `____________` | confirm the free licence covers public display |
| `Low Poly Arid Desert Environment` | **Yes** (environment / terrain) | **Unity Asset Store — purchased under own account, 2026-07-06** (replaced the redistribution-site copy) | **Yes** — Asset Store standard EULA covers public display | ✅ **resolved** — legit paid copy swapped in |
| `INab Studio` | `____` (bundles a Unity Companion License in its demo) | `____________` | `____________` | confirm + delete if unused |
| `Endless Runner` | likely **no** (custom code, not this template) | `____________` | n/a if removed | delete if unused |
| `RunnerV2` | likely **no** | `____________` | n/a if removed | delete if unused |
| `Open world` | `____` | `____________` | `____________` | confirm or delete |
| `POLYBOX` | `____` | `____________` | `____________` | confirm or delete |
| `Palmov Island` | `____` | `____________` | `____________` | confirm or delete |
| `pickup game` | likely **no** (sample/tutorial) | `____________` | n/a if removed | delete if unused |
| Fonts (HUD/menus) | **Yes** | `____________` | confirm embedding allowed | confirm licence |
| Audio (engine, ambience, truck clip) | **Yes** | `____________` | confirm each clip | self-made / CC0 / licensed |

De Ontdekfabriek's own **brand assets** (the green world-map + focus-circle logo, the Kenya illustration, the Ugani/huisstijl palette) are client-owned and fine; note them as such.

> [!tip] Cleanest path
> Decide which packs the shipped build actually depends on (most low-poly/runner template packs in the list are probably leftovers from early prototyping). Delete every unused pack, then you only have to clear the licence on the handful that remain, and the redistribution-site risk disappears with the packs you remove.

---

> [!abstract] Evidence (for the portfolio)
> **What it is:** the fillable companion to the build checklist: the package/credentials manifest and the asset-licence check, grounded in the project's actual imported packs.
> **What I gained:** the licence sweep surfaced a concrete compliance risk (a pack whose own README shows it came from an Asset Store redistribution site, not a legitimate purchase), which turns "check the licences" from a formality into a real before-public-deployment action. It also showed most imported packs are likely unused prototype leftovers, so deleting them is the cleanest way to de-risk the whole question.

---

## Connections
- [[Handover — Build, Keystore and APK Rebuild Checklist]] (Part E points here)
- [[HANDOVER — Start Here]] (the index this belongs to)
- [[Handover Risk Analysis — Running the Game Without Unity]]
