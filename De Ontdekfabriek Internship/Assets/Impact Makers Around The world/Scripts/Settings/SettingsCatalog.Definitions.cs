// Setting definitions for SettingsCatalog — split out 2026-06-30 for readability (no behaviour change).
// The query API (InCategory/ById, actions, presets, labels, value formatters) lives in SettingsCatalog.cs.
// This file holds Build(): the curated list of every facilitator setting, grouped by category.
using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Controls;
using KenyaScooter.Scoring;
using KenyaScooter.Core;
using KenyaScooter.Cameras;
using KenyaScooter.Roads;

namespace KenyaScooter.Settings
{
    public static partial class SettingsCatalog
    {
        private static List<SettingDefinition> Build()
        {
            var list = new List<SettingDefinition>();

            // ---- MOEILIJKHEID ------------------------------------------------------------

            // Hazard density: one dial scaling EVERY hazard config's clustersPer100m, relative to the
            // value captured as the shipped default (so 1.0 = "as designed", 1.5 = half as many again).
            list.Add(new SettingDefinition(
                "diff.hazardDensity", "Aantal hindernissen",
                "Hoeveel kuilen, stenen en drempels er op de weg liggen.",
                SettingCategory.Difficulty, SettingWidget.Slider, 0f, 2f, 0.1f,
                get: () =>
                {
                    var h = ConfigLocator.Hazards;
                    if (h == null || h.Length == 0) return 1f;
                    // Show the ratio against the first hazard's stored base.
                    float baseVal = GameSettings.BaseHazardDensity(h[0]);
                    return baseVal > 0.0001f ? h[0].clustersPer100m / baseVal : 1f;
                },
                set: v =>
                {
                    var h = ConfigLocator.Hazards;
                    if (h == null) return;
                    for (int i = 0; i < h.Length; i++)
                        if (h[i] != null)
                            h[i].clustersPer100m = GameSettings.BaseHazardDensity(h[i]) * v;
                },
                format: Percent));

            // ---- VERKEER EN WEG (what is on the road, and which side) --------------------

            list.Add(new SettingDefinition(
                "diff.trafficAmount", "Hoeveelheid verkeer",
                "Maximaal aantal auto's dat tegelijk op jouw weghelft rijdt. 0 = geen verkeer op jouw weghelft.",
                SettingCategory.TrafficWorld, SettingWidget.Stepper, 0f, 12f, 1f,
                get: () => ConfigLocator.Traffic != null ? ConfigLocator.Traffic.maxSameDirection : 8f,
                set: v => { if (ConfigLocator.Traffic != null) ConfigLocator.Traffic.maxSameDirection = Mathf.RoundToInt(v); }));

            list.Add(new SettingDefinition(
                "world.oncoming", "Tegemoetkomend verkeer",
                "Maximaal aantal auto's dat op de andere weghelft naar je toe rijdt. 0 = geen tegenliggers.",
                SettingCategory.TrafficWorld, SettingWidget.Stepper, 0f, 12f, 1f,
                get: () => ConfigLocator.Traffic != null ? ConfigLocator.Traffic.maxOncoming : 8f,
                set: v => { if (ConfigLocator.Traffic != null) ConfigLocator.Traffic.maxOncoming = Mathf.RoundToInt(v); }));

            // ("Voetgangers" was REMOVED here 2026-07-14: the pedestrian system never made it into the shipped
            // game, so the setting adjusted nothing a player could see. The Pedestrian code itself remains for a
            // future team; re-exposing it = re-adding one SettingDefinition. See GameSettings.CleanRetiredKeys.)

            list.Add(new SettingDefinition(
                "world.driveLeft", "Rijden aan welke kant",
                "Aan welke kant van de weg gereden wordt. Links is zoals in Kenia, rechts is zoals in Nederland.",
                SettingCategory.TrafficWorld, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.RoadSide != null && ConfigLocator.RoadSide.driveOnLeft ? 1f : 0f,
                set: v => { if (ConfigLocator.RoadSide != null) ConfigLocator.RoadSide.driveOnLeft = v >= 0.5f; },
                format: LeftRight));

            list.Add(new SettingDefinition(
                "diff.wrongLaneGrace", "Strengheid verkeerde weghelft",
                "Hoe lang een kind op de verkeerde weghelft mag voor het punten kost. Korter = strenger.",
                SettingCategory.Difficulty, SettingWidget.Slider, 1f, 10f, 0.5f,
                get: () => ConfigLocator.RoadSide != null ? ConfigLocator.RoadSide.wrongLaneGraceSeconds : 5f,
                set: v => { if (ConfigLocator.RoadSide != null) ConfigLocator.RoadSide.wrongLaneGraceSeconds = v; },
                format: Seconds));

            list.Add(new SettingDefinition(
                "diff.speedingGrace", "Strengheid snelheid",
                "Hoe lang een kind te hard mag voor het punten kost. Korter = strenger.",
                SettingCategory.Difficulty, SettingWidget.Slider, 1f, 8f, 0.5f,
                get: () => ConfigLocator.Session != null ? ConfigLocator.Session.speedingGraceSeconds : 3f,
                set: v => { if (ConfigLocator.Session != null) ConfigLocator.Session.speedingGraceSeconds = v; },
                format: Seconds));

            // ---- OMGEVING (time of day) --------------------------------------------------

            list.Add(new SettingDefinition(
                "env.dayCycle", "Dag-nacht cyclus",
                "Aan: de dag loopt van ochtend naar avond tijdens de beurt. Uit: vast licht (kies hieronder welk moment).",
                SettingCategory.Environment, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.DayCycle != null && ConfigLocator.DayCycle.cycleEnabled ? 1f : 0f,
                set: v => { if (ConfigLocator.DayCycle != null) ConfigLocator.DayCycle.cycleEnabled = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "env.fixedPhase", "Vast moment van de dag",
                "Welk moment van de dag te zien is als de dag-nacht cyclus uit staat.",
                SettingCategory.Environment, SettingWidget.Stepper, 0f, 3f, 1f,
                get: () => ConfigLocator.DayCycle != null ? ConfigLocator.DayCycle.fixedPhaseIndex : 0f,
                set: v => { if (ConfigLocator.DayCycle != null) ConfigLocator.DayCycle.fixedPhaseIndex = Mathf.RoundToInt(v); },
                format: PhaseName));

            // Dust and haze: the warm dry-air look (fog haze + dust behind vehicles). Needs a WeatherConfig
            // asset to exist; if none is found the toggle reads OFF and does nothing (feature not configured).
            list.Add(new SettingDefinition(
                "env.dust", "Stof en haze",
                "Aan: warme, stoffige Keniaanse lucht (een droge nevel plus opwaaiend stof achter de voertuigen). Uit: heldere lucht, zoals de weg er zonder stof uitziet.",
                SettingCategory.Environment, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Weather != null && ConfigLocator.Weather.dustEnabled ? 1f : 0f,
                set: v => { if (ConfigLocator.Weather != null) ConfigLocator.Weather.dustEnabled = v >= 0.5f; },
                format: OnOff));

            // ("Leven langs de weg" + "Drukte langs de weg" were REMOVED here 2026-07-14: the roadside-prop
            // spawner never got its RoadsidePropConfig asset wired in the live scene, so both rows read defaults
            // and did nothing. The spawner code remains; re-exposing it = wiring the asset + re-adding the two
            // SettingDefinitions. See GameSettings.CleanRetiredKeys.)

            list.Add(new SettingDefinition(
                "env.skyShifts", "Lucht kleurt mee met de dag",
                "Aan: de lucht verkleurt van ochtend naar avond mee met de dag-nacht cyclus. Uit: een vaste lucht.",
                SettingCategory.Environment, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.DayCycle != null && ConfigLocator.DayCycle.driveSkybox ? 1f : 0f,
                set: v => { if (ConfigLocator.DayCycle != null) ConfigLocator.DayCycle.driveSkybox = v >= 0.5f; },
                format: OnOff));

            // Far horizon: distant low-poly mountain ridges (the Rift Valley skyline) rising into the haze. Real
            // geometry, not the old flat billboards; HorizonRange self-bootstraps and reads this PlayerPrefs toggle.
            list.Add(new SettingDefinition(
                "env.range", "Bergen aan de horizon",
                "Aan: verre bergketens aan de horizon (de Riftvallei), die oplossen in de nevel. Uit: een lege, kale horizon.",
                SettingCategory.Environment, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => KenyaScooter.Sky.HorizonRange.Enabled ? 1f : 0f,
                set: v => KenyaScooter.Sky.HorizonRange.Enabled = v >= 0.5f,
                format: OnOff));

            // Upper sky: soft drifting clouds + a warm glow on the sun. Pure atmosphere; SkyAtmosphere self-bootstraps.
            list.Add(new SettingDefinition(
                "env.skyLife", "Wolken en zonnegloed",
                "Aan: zachte wolken drijven langs de lucht en de zon gloeit warm. Uit: een lege lucht.",
                SettingCategory.Environment, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => KenyaScooter.Sky.SkyAtmosphere.Enabled ? 1f : 0f,
                set: v => KenyaScooter.Sky.SkyAtmosphere.Enabled = v >= 0.5f,
                format: OnOff));

            // Which environment ("setting") the road leans towards — the only setting that steers WHICH tiles spawn,
            // not just how they look. AUTOMATISCH (default) leaves the weighted Journey Arc untouched; picking a zone
            // BIASES the road sequencer towards it (RoadSequencer.zoneBiasMultiplier), so that scenery dominates while
            // the arc still surfaces others. The choice lives in the PlayerPrefs key RoadSequencer reads; the picker is
            // sized from the zone count the sequencer published on the previous run (fallback 6 on the first ever run),
            // and the labels are read live so they always match the shipped sequences.
            float zoneMax = Mathf.Max(1, PlayerPrefs.GetInt(RoadSequencer.ZoneCountPrefKey, 6));
            list.Add(new SettingDefinition(
                "env.zone", "Omgeving kiezen",
                "Welke omgeving je ziet onderweg. Automatisch laat de omgeving vanzelf wisselen tijdens de rit; " +
                "kies een omgeving om vooral díe te laten zien.",
                SettingCategory.Environment, SettingWidget.Stepper, 0f, zoneMax, 1f,
                get: () => PlayerPrefs.GetInt(RoadSequencer.ZoneBiasPrefKey, 0),
                set: v => { PlayerPrefs.SetInt(RoadSequencer.ZoneBiasPrefKey, Mathf.RoundToInt(v)); PlayerPrefs.Save(); },
                format: ZoneName));

            // ("Regio-reis" was REMOVED here 2026-07-14: with the shipped sequence set its ordered-route mode was
            // indistinguishable from the normal mix ("doesn't really seem to do anything" — Ryan). The
            // RoadSequencer.PickNextRegion code path remains; its raw PlayerPrefs key is wiped at boot by
            // GameSettings.CleanRetiredKeys so a lingering AAN can't keep steering the road with no UI left.)

            // How strongly a DIRT tile (RoadTile.surface) is FELT: the handlebar shake plus the extra dust on
            // murram stretches, both scaled by the shared RoadSurfaceFeel signal this stepper writes. The
            // red-brown LOOK of a dirt tile always stays — that is what the road is — so UIT means "looks
            // different, rides like asphalt". VOL is the shipped default.
            list.Add(new SettingDefinition(
                "env.dirtFeel", "Onverharde wegen",
                "Hoe sterk een zandweg voelt: het stuur trilt en er komt extra stof op onverharde stukken. " +
                "SUBTIEL halveert dat; UIT laat zandwegen er anders uitzien maar rijden als asfalt.",
                SettingCategory.Environment, SettingWidget.Stepper, 0f, 2f, 1f,
                get: () => RoadSurfaceFeel.IntensityStep,
                set: v => RoadSurfaceFeel.IntensityStep = Mathf.RoundToInt(v),
                format: DirtFeel));

            // ---- SNELHEID EN GEVOEL ------------------------------------------------------

            list.Add(new SettingDefinition(
                "feel.baseSpeed", "Cruise-snelheid",
                "De snelheid waarop de scooter vanzelf rijdt. Lager = rustiger voor jonge kinderen.",
                SettingCategory.SpeedFeel, SettingWidget.Slider, 6f, 16f, 0.5f,
                get: () => ConfigLocator.Scooter != null ? ConfigLocator.Scooter.baseSpeed : 10f,
                set: v =>
                {
                    var s = ConfigLocator.Scooter;
                    if (s == null) return;
                    s.baseSpeed = v;
                    if (s.maxSpeed < v) s.maxSpeed = v; // keep max at or above base
                },
                format: Kmh));

            list.Add(new SettingDefinition(
                "feel.maxSpeed", "Topsnelheid",
                "De hoogste snelheid die met gas haalbaar is.",
                SettingCategory.SpeedFeel, SettingWidget.Slider, 12f, 34f, 1f,
                get: () => ConfigLocator.Scooter != null ? ConfigLocator.Scooter.maxSpeed : 30f,
                set: v =>
                {
                    var s = ConfigLocator.Scooter;
                    if (s == null) return;
                    s.maxSpeed = Mathf.Max(v, s.baseSpeed); // never below cruise
                },
                format: Kmh));

            list.Add(new SettingDefinition(
                "feel.acceleration", "Optrekken",
                "Hoe snel de scooter op snelheid komt als je gas geeft.",
                SettingCategory.SpeedFeel, SettingWidget.Slider, 6f, 30f, 1f,
                get: () => ConfigLocator.Scooter != null ? ConfigLocator.Scooter.acceleration : 15f,
                set: v => { if (ConfigLocator.Scooter != null) ConfigLocator.Scooter.acceleration = v; },
                format: v => Band(v, 6f, 30f, "ZACHT", "RUSTIG", "NORMAAL", "VLOT", "SNEL")));

            list.Add(new SettingDefinition(
                "feel.lean", "Overhellen in de bocht",
                "Hoe ver de scooter zichtbaar leunt. Puur het gevoel, niet de besturing.",
                SettingCategory.SpeedFeel, SettingWidget.Slider, 6f, 30f, 1f,
                get: () => ConfigLocator.Scooter != null ? ConfigLocator.Scooter.maxLeanAngle : 18f,
                set: v => { if (ConfigLocator.Scooter != null) ConfigLocator.Scooter.maxLeanAngle = v; },
                format: v => Band(v, 6f, 30f, "RECHTOP", "LICHT", "NORMAAL", "SCHUIN", "DIEP")));

            list.Add(new SettingDefinition(
                "feel.brake", "Remkracht",
                "Hoe hard de scooter remt als een kind remt. Hoger = sneller stoppen.",
                SettingCategory.SpeedFeel, SettingWidget.Slider, 8f, 40f, 1f,
                get: () => ConfigLocator.Scooter != null ? ConfigLocator.Scooter.brakeDeceleration : 20f,
                set: v => { if (ConfigLocator.Scooter != null) ConfigLocator.Scooter.brakeDeceleration = v; },
                format: v => Band(v, 8f, 40f, "ZACHT", "LICHT", "NORMAAL", "STERK", "KRACHTIG")));

            list.Add(new SettingDefinition(
                "feel.agility", "Wendbaarheid (zijwaarts)",
                "Hoe snel de scooter van links naar rechts beweegt. Lager = rustiger en makkelijker te beheersen voor jonge kinderen.",
                SettingCategory.SpeedFeel, SettingWidget.Slider, 3f, 9f, 0.5f,
                get: () => ConfigLocator.Scooter != null ? ConfigLocator.Scooter.maxLateralSpeed : 6f,
                set: v => { if (ConfigLocator.Scooter != null) ConfigLocator.Scooter.maxLateralSpeed = v; },
                format: v => Band(v, 3f, 9f, "TRAAG", "RUSTIG", "NORMAAL", "WENDBAAR", "ZEER WENDBAAR")));

            // The master switch (SpeedFeel.EffectsEnabled) over every speed-driven dust/vaart layer — the setter
            // persists its own PlayerPrefs key, like traffic.mood. On = the effects grow with the speed; off = calm.
            list.Add(new SettingDefinition(
                "feel.speedFx", "Snelheidsbeleving (stofeffecten)",
                "Aan: stof- en vaarteffecten die meegroeien met de snelheid, zodat harder rijden ook harder aanvoelt. Uit: een rustig beeld zonder die extra effecten.",
                SettingCategory.SpeedFeel, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => KenyaScooter.FX.SpeedFeel.EffectsEnabled ? 1f : 0f,
                set: v => KenyaScooter.FX.SpeedFeel.EffectsEnabled = v >= 0.5f,
                format: OnOff));

            // ---- VANGNET -----------------------------------------------------------------

            list.Add(new SettingDefinition(
                "safe.rewind", "Aantal keer terugspoelen",
                "Hoe vaak een harde botsing kort terugspoelt in plaats van te stoppen. 0 = uit (een harde botsing telt direct). Meer = vergevingsgezinder voor jonge kinderen.",
                SettingCategory.SafetyNet, SettingWidget.Stepper, 0f, 3f, 1f,
                get: () => ConfigLocator.Safety != null ? ConfigLocator.Safety.rewindsPerTurn : 2f,
                set: v => { if (ConfigLocator.Safety != null) ConfigLocator.Safety.rewindsPerTurn = Mathf.RoundToInt(v); },
                format: Rewinds));

            list.Add(new SettingDefinition(
                "safe.graceCharges", "Botsbescherming",
                "Aantal lichte botsingen dat zonder gevolg wordt opgevangen voor het meetelt.",
                SettingCategory.SafetyNet, SettingWidget.Stepper, 0f, 3f, 1f,
                get: () => ConfigLocator.Safety != null ? ConfigLocator.Safety.graceCharges : 1f,
                set: v => { if (ConfigLocator.Safety != null) ConfigLocator.Safety.graceCharges = Mathf.RoundToInt(v); }));

            // Forgiving (default) vs strict. OFF = a hard crash without a rewind left recovers in place so every
            // child finishes the journey; ON = a hard crash ends the turn. Backed by a PlayerPrefs key that
            // GameManager reads on Awake, so the choice applies at startup with no scene wiring.
            list.Add(new SettingDefinition(
                "safe.gameOver", "Stoppen bij zware botsing",
                "Uit (aanrader): na een zware botsing rijdt het kind gewoon door, zodat iedereen de rit afmaakt. Aan: een zware botsing beëindigt de beurt.",
                SettingCategory.SafetyNet, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => GameManager.Instance != null
                    ? (GameManager.Instance.gameOverOnCollision ? 1f : 0f)
                    : PlayerPrefs.GetInt(GameManager.GameOverPrefKey, 0),
                set: v =>
                {
                    bool on = v >= 0.5f;
                    PlayerPrefs.SetInt(GameManager.GameOverPrefKey, on ? 1 : 0); PlayerPrefs.Save();
                    if (GameManager.Instance != null) GameManager.Instance.gameOverOnCollision = on;
                },
                format: OnOff));

            list.Add(new SettingDefinition(
                "safe.graceRecharge", "Bescherming herstelt na",
                "Hoe lang netjes rijden duurt voordat een opgebruikte botsbescherming weer terugkomt. Korter = sneller weer beschermd.",
                SettingCategory.SafetyNet, SettingWidget.Slider, 3f, 20f, 1f,
                get: () => ConfigLocator.Safety != null ? ConfigLocator.Safety.graceRechargeSeconds : 8f,
                set: v => { if (ConfigLocator.Safety != null) ConfigLocator.Safety.graceRechargeSeconds = v; },
                format: Seconds));

            // ---- BESTURING ---------------------------------------------------------------

            list.Add(new SettingDefinition(
                "ctrl.tiltSensitivity", "Stuurgevoeligheid (kantelen)",
                "Hoe ver de tablet gekanteld moet worden voor volledig sturen. Lager getal = gevoeliger.",
                SettingCategory.Controls, SettingWidget.Slider, 12f, 45f, 1f,
                get: () => ConfigLocator.Input != null ? ConfigLocator.Input.gyroMaxTiltDegrees : 28f,
                set: v => { if (ConfigLocator.Input != null) ConfigLocator.Input.gyroMaxTiltDegrees = v; },
                format: v => Mathf.RoundToInt(v) + "°"));

            list.Add(new SettingDefinition(
                "ctrl.deadzone", "Dode zone",
                "Kleine kanteling die als 'recht' telt, zodat trillende handen niet sturen.",
                SettingCategory.Controls, SettingWidget.Slider, 0f, 8f, 0.5f,
                get: () => ConfigLocator.Input != null ? ConfigLocator.Input.gyroDeadZoneDegrees : 2f,
                set: v => { if (ConfigLocator.Input != null) ConfigLocator.Input.gyroDeadZoneDegrees = v; },
                format: v => v.ToString("0.0") + "°"));

            list.Add(new SettingDefinition(
                "ctrl.smoothing", "Stuurdemping (vloeiend sturen)",
                "Hoe rustig het sturen aanvoelt als de tablet stil wordt gehouden. Lager = vloeiender en kalmer " +
                "(goed tegen trillende handen), hoger = directer. Bij een echte, snelle kanteling reageert het " +
                "stuur sowieso meteen — deze demping geldt vooral voor kleine trillingen.",
                SettingCategory.Controls, SettingWidget.Slider, 4f, 20f, 1f,
                get: () => ConfigLocator.Input != null ? ConfigLocator.Input.gyroSmoothing : 12f,
                set: v => { if (ConfigLocator.Input != null) ConfigLocator.Input.gyroSmoothing = v; },
                format: v => Band(v, 4f, 20f, "ZEER VLOEIEND", "VLOEIEND", "NORMAAL", "DIRECT", "ZEER DIRECT")));

            list.Add(new SettingDefinition(
                "ctrl.steerCurve", "Stuurfijngevoeligheid",
                "Hoe fijn kleine kantelingen sturen. Hoger = rustiger rond het midden (makkelijker recht " +
                "houden), volledig sturen blijft mogelijk. Lager = feller meteen vanaf het midden.",
                SettingCategory.Controls, SettingWidget.Slider, 0.8f, 2f, 0.05f,
                get: () => ConfigLocator.Input != null ? ConfigLocator.Input.gyroResponseCurve : 1.25f,
                set: v => { if (ConfigLocator.Input != null) ConfigLocator.Input.gyroResponseCurve = v; },
                format: v => Band(v, 0.8f, 2f, "ZEER FEL", "FEL", "NORMAAL", "FIJN", "ZEER FIJN")));

            list.Add(new SettingDefinition(
                "ctrl.invert", "Sturen omkeren",
                "Zet aan als links en rechts verkeerd om aanvoelen op deze tablet.",
                SettingCategory.Controls, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Input != null && ConfigLocator.Input.invertGyro ? 1f : 0f,
                set: v => { if (ConfigLocator.Input != null) ConfigLocator.Input.invertGyro = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "ctrl.motion", "Bewegingsgevoeligheid",
                "Verlaag dit voor kinderen die snel misselijk worden; het tempert zowel het sturen als het meekantelen van de camera.",
                SettingCategory.Controls, SettingWidget.Slider, 0.3f, 1f, 0.05f,
                get: () => ConfigLocator.Input != null ? ConfigLocator.Input.motionSensitivity : 1f,
                set: v => { if (ConfigLocator.Input != null) ConfigLocator.Input.motionSensitivity = v; },
                format: Percent));

            // Real Rider Mode (SC1): the camera leans with the tablet so the horizon stays level. Immersive, but a
            // facilitator can turn it off for a child who finds the leaning camera disorienting. PlayerPrefs-backed
            // and read by RealRiderMode on Awake, so the choice applies at startup.
            list.Add(new SettingDefinition(
                "ctrl.realRider", "Meekantelen (Real Rider)",
                "Aan: de camera kantelt mee met de tablet, zodat de horizon recht blijft (meer het gevoel van echt rijden). Uit voor kinderen die daar duizelig van worden.",
                SettingCategory.Controls, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () =>
                {
                    var rr = Object.FindObjectOfType<RealRiderMode>();
                    if (rr != null) return rr.RealRiderEnabled ? 1f : 0f;
                    return PlayerPrefs.GetInt(RealRiderMode.PrefKey, 1);
                },
                set: v =>
                {
                    bool on = v >= 0.5f;
                    PlayerPrefs.SetInt(RealRiderMode.PrefKey, on ? 1 : 0); PlayerPrefs.Save();
                    var rr = Object.FindObjectOfType<RealRiderMode>();
                    if (rr != null) rr.RealRiderEnabled = on;
                },
                format: OnOff));

            // When the on-screen gas/brake buttons show (the handheld controls the client asked for). The buttons
            // themselves are drawn by HandheldControlsHud; this just stores the facilitator's choice in the same
            // PlayerPrefs key HandheldDetector reads. Automatic = only when the tablet is out of the holder.
            list.Add(new SettingDefinition(
                "ctrl.handheldControls", "Gas- en remknoppen",
                "Wanneer de gas- en remknoppen op het scherm staan voor spelen in de hand. Automatisch = alleen als de tablet niet in de houder / aan de lader zit.",
                SettingCategory.Controls, SettingWidget.Stepper, 0f, 2f, 1f,
                get: () => PlayerPrefs.GetInt(HandheldDetector.ModePrefKey, HandheldDetector.ModeAuto),
                set: v => { PlayerPrefs.SetInt(HandheldDetector.ModePrefKey, Mathf.RoundToInt(v)); PlayerPrefs.Save(); },
                format: HandheldMode));

            // Onboarding on/off: the first-run "ZO SPEEL JE" card AND the per-turn "kantel om te sturen" tip. OFF by
            // default (the relay is facilitated, so most groups are walked through it); a facilitator switches it ON
            // for a new group that should learn the controls in-game. The default lives once in HowToPlayOverlay.
            // TutorialDefault so this setting and the gate can never disagree; reads the same PlayerPrefs key the
            // overlays check, so no per-system wiring is needed.
            list.Add(new SettingDefinition(
                "ctrl.tutorial", "Uitleg tonen",
                "Toont voor nieuwe spelers de korte uitleg (kantelen om te sturen, rechts = gas, links = rem) en de stuurtip. Standaard uit; zet aan voor een nieuwe groep die de besturing nog moet leren.",
                SettingCategory.Controls, SettingWidget.Toggle, 0f, 1f, 1f, // step is 1f like every toggle; the default lives in the getter's PlayerPrefs fallback (HowToPlayOverlay.TutorialDefault)
                get: () => PlayerPrefs.GetInt(KenyaScooter.UI.HowToPlayOverlay.TutorialPrefKey, KenyaScooter.UI.HowToPlayOverlay.TutorialDefault) == 1 ? 1f : 0f,
                set: v => { PlayerPrefs.SetInt(KenyaScooter.UI.HowToPlayOverlay.TutorialPrefKey, v >= 0.5f ? 1 : 0); PlayerPrefs.Save(); },
                format: OnOff));

            // ---- GELUID ------------------------------------------------------------------

            list.Add(new SettingDefinition(
                "audio.master", "Hoofdvolume",
                "Algeheel volume van het spel.",
                SettingCategory.Audio, SettingWidget.Slider, 0f, 1f, 0.05f,
                get: () => AudioListener.volume,
                set: v => AudioListener.volume = v,
                format: Percent));

            list.Add(new SettingDefinition(
                "audio.music", "Muziekvolume",
                "Volume van de achtergrondmuziek.",
                SettingCategory.Audio, SettingWidget.Slider, 0f, 1f, 0.05f,
                get: () => ConfigLocator.Audio != null ? ConfigLocator.Audio.musicVolume : 0.5f,
                set: v => { if (ConfigLocator.Audio != null) ConfigLocator.Audio.musicVolume = v; },
                format: Percent));

            list.Add(new SettingDefinition(
                "audio.ambient", "Omgevingsgeluid",
                "Volume van de omgeving (savanne, dorp, natuur).",
                SettingCategory.Audio, SettingWidget.Slider, 0f, 1f, 0.05f,
                get: () => ConfigLocator.Audio != null ? ConfigLocator.Audio.ambientVolume : 0.6f,
                set: v => { if (ConfigLocator.Audio != null) ConfigLocator.Audio.ambientVolume = v; },
                format: Percent));

            list.Add(new SettingDefinition(
                "audio.engine", "Motorvolume",
                "Volume van de scootermotor. Lager als het geluid van de motor de omgeving overstemt.",
                SettingCategory.Audio, SettingWidget.Slider, 0f, 1f, 0.05f,
                get: () => ConfigLocator.Audio != null ? ConfigLocator.Audio.engineVolume : 0.35f,
                set: v => { if (ConfigLocator.Audio != null) ConfigLocator.Audio.engineVolume = v; },
                format: Percent));

            list.Add(new SettingDefinition(
                "audio.sfx", "Effectenvolume",
                "Volume van losse geluidseffecten (inhalen, botsen, kuil, terugspoelen).",
                SettingCategory.Audio, SettingWidget.Slider, 0f, 1f, 0.05f,
                get: () => ConfigLocator.Audio != null ? ConfigLocator.Audio.sfxVolume : 0.5f,
                set: v => { if (ConfigLocator.Audio != null) ConfigLocator.Audio.sfxVolume = v; },
                format: Percent));

            list.Add(new SettingDefinition(
                "audio.ui", "Menugeluid",
                "Volume van de tik- en schermgeluiden in de menu's. Op 0 zetten voor een stille interface.",
                SettingCategory.Audio, SettingWidget.Slider, 0f, 1f, 0.05f,
                get: () => ConfigLocator.Audio != null ? ConfigLocator.Audio.uiVolume : 0.6f,
                set: v => { if (ConfigLocator.Audio != null) ConfigLocator.Audio.uiVolume = v; },
                format: Percent));

            list.Add(new SettingDefinition(
                "audio.haptics", "Trillen (haptics)",
                "Laat de tablet trillen bij een botsing. Werkt alleen op een echte tablet.",
                SettingCategory.Audio, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => PlayerPrefs.GetInt("ksg.haptics", 1) == 1 ? 1f : 0f,
                set: v => { PlayerPrefs.SetInt("ksg.haptics", v >= 0.5f ? 1 : 0); PlayerPrefs.Save(); },
                format: OnOff));

            // ---- PUNTEN ------------------------------------------------------------------

            list.Add(new SettingDefinition(
                "score.deductCollisions", "Punten af bij botsen",
                "Trekt punten af bij een botsing met verkeer.",
                SettingCategory.Scoring, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Score != null && ConfigLocator.Score.deductCollisions ? 1f : 0f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.deductCollisions = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "score.deductWrongLane", "Punten af bij verkeerde weghelft",
                "Trekt punten af bij te lang op de verkeerde weghelft rijden.",
                SettingCategory.Scoring, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Score != null && ConfigLocator.Score.deductWrongLane ? 1f : 0f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.deductWrongLane = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "score.deductSpeeding", "Punten af bij te hard rijden",
                "Trekt punten af bij te hard rijden.",
                SettingCategory.Scoring, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Score != null && ConfigLocator.Score.deductSpeeding ? 1f : 0f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.deductSpeeding = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "score.overtakes", "Punten voor inhalen",
                "Geef punten voor een nette inhaalactie aan de juiste kant.",
                SettingCategory.Scoring, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Score != null && ConfigLocator.Score.scoreOvertakes ? 1f : 0f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.scoreOvertakes = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "score.deductPotholes", "Punten af bij kuilen",
                "Trekt punten af bij het raken van een kuil.",
                SettingCategory.Scoring, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Score != null && ConfigLocator.Score.deductPotholes ? 1f : 0f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.deductPotholes = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "score.deductRocks", "Punten af bij stenen",
                "Trekt punten af bij het raken van een steen.",
                SettingCategory.Scoring, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Score != null && ConfigLocator.Score.deductRocks ? 1f : 0f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.deductRocks = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "score.deductBumps", "Punten af bij drempels",
                "Trekt punten af bij het raken van een drempel.",
                SettingCategory.Scoring, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Score != null && ConfigLocator.Score.deductSpeedBumps ? 1f : 0f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.deductSpeedBumps = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "score.trickle", "Beloning voor doorrijden",
                "Kleine bonus die oploopt zolang het kind blijft rijden.",
                SettingCategory.Scoring, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Score != null && ConfigLocator.Score.trickleEnabled ? 1f : 0f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.trickleEnabled = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "score.correctLaneBonus", "Bonus voor juiste weghelft",
                "Geeft af en toe een bonus voor netjes op de eigen weghelft blijven rijden.",
                SettingCategory.Scoring, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Score != null && ConfigLocator.Score.correctLaneBonusEnabled ? 1f : 0f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.correctLaneBonusEnabled = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "score.cleanZoneBonus", "Bonus voor schoon wegstuk",
                "Geeft een bonus als een wegstuk zonder te hard rijden wordt afgelegd.",
                SettingCategory.Scoring, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Score != null && ConfigLocator.Score.cleanZoneBonusEnabled ? 1f : 0f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.cleanZoneBonusEnabled = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "score.collisionDeduction", "Strafpunten botsing",
                "Hoeveel punten een botsing kost.",
                SettingCategory.Scoring, SettingWidget.Stepper, 0f, 200f, 10f,
                get: () => ConfigLocator.Score != null ? ConfigLocator.Score.collisionDeduction : 60f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.collisionDeduction = Mathf.RoundToInt(v); },
                format: Points));

            // HOW MANY (2026-07-05): the amounts behind every toggle above, so the facilitator decides both
            // WHAT gives points and HOW MUCH. All read/write the live ScoreConfig like the toggles do, so
            // "Alles terug naar standaard" restores the shipped values.

            list.Add(new SettingDefinition(
                "score.overtakeMultiplier", "Punten voor inhalen ×",
                "Hoeveel een nette inhaalactie waard is. ×1 = normaal; hoger maakt inhalen belangrijker, lager rustiger. (De basiswaarde verschilt per voertuigtype.)",
                SettingCategory.Scoring, SettingWidget.Slider, 0f, 3f, 0.25f,
                get: () => ConfigLocator.Score != null ? ConfigLocator.Score.overtakeMultiplier : 1f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.overtakeMultiplier = v; },
                format: v => "×" + v.ToString("0.##")));

            list.Add(new SettingDefinition(
                "score.streak", "Streak-multiplier",
                "Aan: opeenvolgende nette inhaalacties tellen steeds zwaarder (×1,5 … ×3). Uit: elke beloning telt enkel.",
                SettingCategory.Scoring, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Score != null && ConfigLocator.Score.streakEnabled ? 1f : 0f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.streakEnabled = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "score.potholeDeduction", "Strafpunten kuil",
                "Hoeveel punten het raken van een kuil kost (op hoge snelheid; langzaam rijden dempt de straf).",
                SettingCategory.Scoring, SettingWidget.Stepper, 0f, 200f, 5f,
                get: () => ConfigLocator.Score != null ? ConfigLocator.Score.potholeDeduction : 50f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.potholeDeduction = Mathf.RoundToInt(v); },
                format: Points));

            list.Add(new SettingDefinition(
                "score.rockDeduction", "Strafpunten steen",
                "Hoeveel punten het raken van een steen kost.",
                SettingCategory.Scoring, SettingWidget.Stepper, 0f, 200f, 5f,
                get: () => ConfigLocator.Score != null ? ConfigLocator.Score.rockDeduction : 35f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.rockDeduction = Mathf.RoundToInt(v); },
                format: Points));

            list.Add(new SettingDefinition(
                "score.bumpPainted", "Strafpunten drempel (gemarkeerd)",
                "Hoeveel punten een gemarkeerde drempel kost bij te snel rijden.",
                SettingCategory.Scoring, SettingWidget.Stepper, 0f, 200f, 10f,
                get: () => ConfigLocator.Score != null ? ConfigLocator.Score.speedBumpPaintedDeduction : 60f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.speedBumpPaintedDeduction = Mathf.RoundToInt(v); },
                format: Points));

            list.Add(new SettingDefinition(
                "score.bumpUnmarked", "Strafpunten drempel (ongemarkeerd)",
                "Hoeveel punten de verraderlijke ongemarkeerde drempel kost.",
                SettingCategory.Scoring, SettingWidget.Stepper, 0f, 200f, 10f,
                get: () => ConfigLocator.Score != null ? ConfigLocator.Score.speedBumpUnmarkedDeduction : 100f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.speedBumpUnmarkedDeduction = Mathf.RoundToInt(v); },
                format: Points));

            list.Add(new SettingDefinition(
                "score.wrongLanePerSecond", "Strafpunten verkeerde weghelft",
                "Hoeveel punten per seconde het kost om op de verkeerde weghelft te blijven rijden.",
                SettingCategory.Scoring, SettingWidget.Stepper, 0f, 100f, 5f,
                get: () => ConfigLocator.Score != null ? ConfigLocator.Score.wrongLanePerSecond : 25f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.wrongLanePerSecond = Mathf.RoundToInt(v); },
                format: v => Mathf.RoundToInt(v) + " p/s"));

            list.Add(new SettingDefinition(
                "score.speedingPerSecond", "Strafpunten te hard rijden",
                "Hoeveel punten per seconde te hard rijden kost.",
                SettingCategory.Scoring, SettingWidget.Stepper, 0f, 100f, 5f,
                get: () => ConfigLocator.Score != null ? ConfigLocator.Score.speedingPerSecond : 15f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.speedingPerSecond = Mathf.RoundToInt(v); },
                format: v => Mathf.RoundToInt(v) + " p/s"));

            list.Add(new SettingDefinition(
                "score.rewindPenalty", "Strafpunten na crash-terugspoelen",
                "Hoeveel punten het terugspoelen na een zware crash kost.",
                SettingCategory.Scoring, SettingWidget.Stepper, 0f, 200f, 10f,
                get: () => ConfigLocator.Score != null ? ConfigLocator.Score.rewindPenalty : 80f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.rewindPenalty = Mathf.RoundToInt(v); },
                format: Points));

            list.Add(new SettingDefinition(
                "score.tricklePerSecond", "Beloning doorrijden (hoogte)",
                "Hoeveel punten per seconde het kind krijgt zolang het blijft rijden.",
                SettingCategory.Scoring, SettingWidget.Stepper, 0f, 10f, 1f,
                get: () => ConfigLocator.Score != null ? ConfigLocator.Score.tricklePerSecond : 2f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.tricklePerSecond = Mathf.RoundToInt(v); },
                format: v => Mathf.RoundToInt(v) + " p/s"));

            list.Add(new SettingDefinition(
                "score.correctLaneAmount", "Bonus juiste weghelft (hoogte)",
                "Hoeveel punten de periodieke bonus voor netjes rechts rijden waard is.",
                SettingCategory.Scoring, SettingWidget.Stepper, 0f, 50f, 5f,
                get: () => ConfigLocator.Score != null ? ConfigLocator.Score.correctLaneBonus : 10f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.correctLaneBonus = Mathf.RoundToInt(v); },
                format: Points));

            list.Add(new SettingDefinition(
                "score.cleanZoneAmount", "Bonus schoon wegstuk (hoogte)",
                "Hoeveel punten een wegstuk zonder overtredingen oplevert.",
                SettingCategory.Scoring, SettingWidget.Stepper, 0f, 100f, 5f,
                get: () => ConfigLocator.Score != null ? ConfigLocator.Score.cleanZoneBonus : 25f,
                set: v => { if (ConfigLocator.Score != null) ConfigLocator.Score.cleanZoneBonus = Mathf.RoundToInt(v); },
                format: Points));

            // ---- SPEELDUUR ---------------------------------------------------------------

            // Eindeloze modus (endless): the facilitator switch that changes the WHOLE game shape — a solo run with
            // lives instead of the timed class relay. Deliberately a FACILITATOR setting (behind the access code),
            // never a button on the title screen, so a child can't put the game into it. The menu reads
            // GameManager.EndlessSelected when a run starts; "Alles terug naar standaard" turns it back off (default UIT).
            list.Add(new SettingDefinition(
                "session.endless", "Eindeloze modus",
                "AAN: solomodus met levens in plaats van een klok — geen teamkeuze, geen relay, telt niet mee voor de klas; het kind rijdt door tot de levens op zijn. UIT (standaard): de gewone klassenrelay.",
                SettingCategory.Session, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => GameManager.EndlessSelected ? 1f : 0f,
                set: v => GameManager.EndlessSelected = v >= 0.5f,
                format: OnOff));

            list.Add(new SettingDefinition(
                "session.endlessLives", "Levens in de eindeloze modus",
                "Hoeveel levens een kind heeft in de eindeloze modus. Een zware botsing kost een leven; bij het laatste leven stopt de rit. Geldt alleen in de eindeloze modus.",
                SettingCategory.Session, SettingWidget.Stepper, 1f, 5f, 1f,
                get: () => ConfigLocator.Session != null ? ConfigLocator.Session.endlessLives : 3f,
                set: v => { if (ConfigLocator.Session != null) ConfigLocator.Session.endlessLives = Mathf.RoundToInt(v); },
                format: v => { int n = Mathf.RoundToInt(v); return n + (n == 1 ? " leven" : " levens"); }));

            // Whether children may type their OWN team name. OFF by default (they only get the fixed Swahili animal
            // names, no free-text keyboard); a facilitator turns it ON to add the "EIGEN NAAM" card on team-select.
            list.Add(new SettingDefinition(
                "team.customName", "Eigen teamnaam toestaan",
                "AAN: op het teamkeuze-scherm staat een extra kaart 'EIGEN NAAM' waarmee een groep zelf een naam typt op een schermtoetsenbord. UIT (standaard): alleen de vaste Swahili-dierennamen — geen toetsenbord voor de kinderen.",
                SettingCategory.Session, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => PlayerPrefs.GetInt("ksg.customName", 0) == 1 ? 1f : 0f,
                set: v => { PlayerPrefs.SetInt("ksg.customName", v >= 0.5f ? 1 : 0); PlayerPrefs.Save(); },
                format: OnOff));

            list.Add(new SettingDefinition(
                "session.length", "Lengte van een beurt",
                "Hoe lang een kind rijdt voordat de beurt wordt doorgegeven.",
                SettingCategory.Session, SettingWidget.Slider, 45f, 240f, 15f,
                get: () => ConfigLocator.Session != null ? ConfigLocator.Session.sessionSeconds : 120f,
                set: v => { if (ConfigLocator.Session != null) ConfigLocator.Session.sessionSeconds = v; },
                format: Seconds));

            list.Add(new SettingDefinition(
                "session.cinematicRelay", "Laadstation-animatie",
                "Aan: bij het laadstation rijdt de scooter de pomp in, laadt kort op en rijdt weer weg. Uit: meteen het scorescherm.",
                SettingCategory.Session, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => ConfigLocator.Session != null && ConfigLocator.Session.cinematicRelay ? 1f : 0f,
                set: v => { if (ConfigLocator.Session != null) ConfigLocator.Session.cinematicRelay = v >= 0.5f; },
                format: OnOff));

            list.Add(new SettingDefinition(
                "session.handoffWait", "Wachttijd wisselscherm",
                "Hoe lang het wisselscherm tussen twee spelers blijft staan voor het vanzelf doorgaat.",
                SettingCategory.Session, SettingWidget.Slider, 5f, 60f, 5f,
                get: () => ConfigLocator.Session != null ? ConfigLocator.Session.checkpointAutoAdvanceSeconds : 25f,
                set: v => { if (ConfigLocator.Session != null) ConfigLocator.Session.checkpointAutoAdvanceSeconds = v; },
                format: Seconds));

            list.Add(new SettingDefinition(
                "session.endWait", "Wachttijd eindscherm",
                "Hoe lang het eindscherm blijft staan voor het vanzelf naar de volgende groep gaat.",
                SettingCategory.Session, SettingWidget.Slider, 5f, 60f, 5f,
                get: () => ConfigLocator.Session != null ? ConfigLocator.Session.endScreenAutoAdvanceSeconds : 20f,
                set: v => { if (ConfigLocator.Session != null) ConfigLocator.Session.endScreenAutoAdvanceSeconds = v; },
                format: Seconds));

            // ---- BEHEER ------------------------------------------------------------------

            // Whether the access code is asked before this menu opens. Routed through the override store like any
            // other setting, so it persists and "Alles terug naar standaard" restores it to ON (the safe default).
            list.Add(new SettingDefinition(
                "lock.enabled", "Toegangscode vereist",
                "Aan: er wordt een code gevraagd voordat dit menu opengaat, zodat leerlingen er niet in kunnen. Uit alleen op een vertrouwde, afgesloten locatie.",
                SettingCategory.Management, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => FacilitatorLock.Enabled ? 1f : 0f,
                set: v => FacilitatorLock.Enabled = v >= 0.5f,
                format: OnOff));

            // PERFORMANCE MODES (2026-07-05): one knob that trades atmosphere for frame rate on old tablets —
            // gameplay (traffic, hazards, speeds, scoring) is identical in every tier, so scores stay fair
            // between devices. AUTOMATISCH detects weak hardware and picks LICHT there; the explicit tiers
            // override the detection. Applied LIVE via PerformanceMode.Reapply(); persists in PlayerPrefs.
            list.Add(new SettingDefinition(
                "system.performance", "Prestaties",
                "Hoe zwaar de graphics zijn. AUTOMATISCH kiest zelf per apparaat; VOLLEDIG = alles aan; " +
                "GEBALANCEERD = iets eenvoudiger beeld; LICHT = maximale snelheid op oude tablets " +
                "(minder sfeer-effecten, geen schaduwen). Het spel zelf blijft in elke stand gelijk.",
                SettingCategory.Management, SettingWidget.Stepper, 0f, 3f, 1f,
                get: () => PlayerPrefs.GetInt(KenyaScooter.Core.PerformanceMode.TierPrefKey, 0),
                set: v =>
                {
                    PlayerPrefs.SetInt(KenyaScooter.Core.PerformanceMode.TierPrefKey, Mathf.RoundToInt(v));
                    PlayerPrefs.Save();
                    KenyaScooter.Core.PerformanceMode.Reapply();
                },
                format: v =>
                {
                    int tier = Mathf.RoundToInt(v);
                    return tier == 1 ? "VOLLEDIG" : tier == 2 ? "GEBALANCEERD" : tier == 3 ? "LICHT" : "AUTOMATISCH";
                }));

            // ---- SPEL & KIOSK (newest systems, so presets can shape the WHOLE game) --------
            list.Add(new SettingDefinition(
                "traffic.overtaking", "Auto's halen elkaar in",
                "AAN: auto's halen langzamer verkeer in via de andere weghelft (levendig, drukker). UIT: iedereen blijft netjes in de rij — rustiger en voorspelbaarder voor jonge spelers.",
                SettingCategory.Difficulty, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => KenyaScooter.Traffic.TrafficVehicle.OvertakingEnabled ? 1f : 0f,
                set: v => KenyaScooter.Traffic.TrafficVehicle.OvertakingEnabled = v >= 0.5f,
                format: OnOff));
            list.Add(new SettingDefinition(
                "traffic.breakdowns", "Pech langs de weg",
                "Hoe vaak een auto met pech naar de berm rijdt en met alarmlichten stilvalt — een plotselinge maar aangekondigde hindernis. 0% = nooit.",
                SettingCategory.Difficulty, SettingWidget.Slider, 0f, 2f, 0.25f,
                get: () => KenyaScooter.Traffic.TrafficVehicle.BreakdownBoost,
                set: v => KenyaScooter.Traffic.TrafficVehicle.BreakdownBoost = v,
                format: Percent));
            list.Add(new SettingDefinition(
                "traffic.mood", "Verkeer reageert op jouw rijgedrag",
                "AAN: roekeloos rijden maakt de weg boos — meer getoeter, minder ruimte, bumperklevers; netjes rijden verdient hoffelijkheid terug. Dé les in gedrag, gevoeld in het spel.",
                SettingCategory.Difficulty, SettingWidget.Toggle, 0f, 1f, 1f,
                get: () => KenyaScooter.Traffic.TrafficMood.Enabled ? 1f : 0f,
                set: v => KenyaScooter.Traffic.TrafficMood.Enabled = v >= 0.5f,
                format: OnOff));
            list.Add(new SettingDefinition(
                "traffic.contrast", "Karakterverschil chauffeurs",
                "Hoe verschillend de chauffeurstypes rijden. ×1 = normaal; hoger = agressieve, voorzichtige en afgeleide chauffeurs zijn onmiskenbaar anders; lager = iedereen rijdt vlakker.",
                SettingCategory.Difficulty, SettingWidget.Slider, 0.25f, 2f, 0.25f,
                get: () => KenyaScooter.Traffic.TrafficMood.Contrast,
                set: v => KenyaScooter.Traffic.TrafficMood.Contrast = v,
                format: v => "×" + v.ToString("0.##")));
            list.Add(new SettingDefinition(
                "kiosk.attractDelay", "Demostand (zelfspelend)",
                "Na zoveel seconden zonder aanraking speelt het spel zichzelf onder 'TIK OM TE STARTEN'. 0 = demostand uit. Demoscores tellen nooit mee.",
                SettingCategory.Management, SettingWidget.Slider, 0f, 300f, 15f,
                get: () => KenyaScooter.UI.AttractMode.IdleSecondsOverride >= 0f ? KenyaScooter.UI.AttractMode.IdleSecondsOverride : 60f,
                set: v => KenyaScooter.UI.AttractMode.IdleSecondsOverride = v,
                format: Seconds));
            list.Add(new SettingDefinition(
                "journey.mapScale", "Reiskaart-tempo",
                "Hoe snel de klas over de kaart Nairobi→Mombasa reist. Hoger = een kleine groep haalt tóch de kust; lager = een grote klas houdt uitdaging.",
                SettingCategory.Management, SettingWidget.Slider, 2f, 30f, 1f,
                get: () => KenyaScooter.Session.JourneyProgress.MapScale,
                set: v => KenyaScooter.Session.JourneyProgress.MapScale = v,
                format: v => "×" + Mathf.RoundToInt(v)));

            return list;
        }
    }
}
