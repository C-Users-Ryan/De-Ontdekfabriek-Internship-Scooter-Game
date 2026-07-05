using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KenyaScooter.Core;
using KenyaScooter.Config;
using KenyaScooter.FX;
using KenyaScooter.Scoring;
using KenyaScooter.Session;
using KenyaScooter.Traffic;

namespace KenyaScooter.UI
{
    /// <summary>
    /// The whole diegetic instrument cluster, built and driven by code so it does not
    /// have to be assembled by hand in the Inspector. Call <see cref="Build"/> (or use
    /// the Tools → Kenya Scooter → Build HUD Cluster menu) and it generates the panel,
    /// the rolling odometer, the speedometer, the battery-timer, the speed-limit roundel,
    /// the warning LEDs and the route/day strip — all wired to the live game events.
    ///
    /// It owns its own procedural sprites, so no art assets are required to see it work.
    /// At play it drives from: ScoreChanged (odometer), StreakChanged (streak badge),
    /// WorldSpeed (speedometer), SpeedZoneManager (limit + tint), TimerManager (battery +
    /// route progress), WrongLane/Speeding/Collision (LEDs), Grace + DayPhase events.
    /// Tweak the fields below and press the context-menu "Rebuild" to re-generate.
    /// </summary>
    // NOTE: this class is split across two files for readability (2026-06-27, no behaviour change):
    //   DiegeticHud.cs       — runtime: fields, lifecycle, the per-frame Drive*/reel/LED drivers, event handlers.
    //   DiegeticHud.Build.cs — construction: Build() and the procedural panel/sprite helpers.
    [DisallowMultipleComponent]
    public sealed partial class DiegeticHud : MonoBehaviour
    {
        [Header("Panel")]
        // UI v2 (2026-07-05, "Kenya Game UI — Improved"): the cluster lost ~22% height and gained ~11% width,
        // so more tarmac stays in view mid-overtake. Scenes that serialized the old 1500×270 keep it until the
        // HUD is rebuilt with a reset component (or the value is updated by hand).
        [SerializeField] private Vector2 panelSize = new Vector2(1660f, 210f);
        [SerializeField] private float bottomMargin = 0f;

        [Header("Odometer")]
        [SerializeField, Range(3, 7)] private int digits = 5;
        [SerializeField] private float maxSpeedKmhFallback = 108f; // used only if WorldSpeed is absent

        [Header("Colours (Ugani / kept cluster)")]
        [SerializeField] private Color panelColour   = Hex("#1A1310");
        [SerializeField] private Color accent         = Hex("#F19141");
        [SerializeField] private Color gold           = Hex("#D0B85B");
        [SerializeField] private Color success        = Hex("#67B44E");
        [SerializeField] private Color caution        = Hex("#F5B43C");
        [SerializeField] private Color danger         = Hex("#E5352B");
        [SerializeField] private Color ink            = Hex("#FFF6EC");
        [SerializeField] private Color muted          = Hex("#9B8A78");
        [SerializeField] private Color ledOff         = Hex("#3A322C");
        [SerializeField] private Color trackDim       = Hex("#3A322C");

        // ---- built references --------------------------------------------------------
        private bool built;
        private readonly List<RectTransform> reelStrips = new();
        private readonly List<TMP_Text[]> reelCells = new();
        private float[] reelCellPos;       // current vertical position, in "cells"
        private int[] reelTargetCell;      // target cell index
        private float[] reelSettle;        // settle-spring offset per reel, in cells (the overshoot bounce)
        private float[] reelSettleVel;     // settle-spring velocity per reel
        private float cellHeight;
        private float flashTimer;
        private float flashDuration = PosFlashSeconds;
        private Color reelColour;
        // Play-test: the green "doing well" wash faded too fast to feel like a state — gains now glow for
        // over a second (and chained gains keep re-arming it), while a loss stays a short red sting.
        private const float PosFlashSeconds = 1.15f;
        private const float NegFlashSeconds = 0.5f;

        // Streak tier-up celebration (Animation Playbook, 2026-07-05): the ×-badge thumps, throws a ring
        // and a small spark burst when the shared multiplier climbs a tier. Everything scales with the
        // motion-sensitivity dial; at the Prikkelarm preset (0.5) the ring/burst stay off entirely.
        private Image tierRing;
        private Image[] tierSparks;
        private Vector2[] tierSparkDirs;
        private float tierFxTimer;
        private float lastMultiplier = -1f; // -1 = no streak event seen yet, so a mid-run rebuild never celebrates
        private const float TierFxSeconds = 0.55f;
        private const float CalmCutoff = 0.55f;

        private Image speedFill, needle, ledLeft, ledRight, limitRing, routeFill;
        private TMP_Text kmhText, limitText, dayLabel, streakText;
        private Image[] batterySegments;
        [SerializeField] private float chargeVisualSeconds = 1.6f; // how long the battery takes to refill at the charge station
        private bool charging;
        private float chargeT;
        private RectTransform streakBadge;
        private int currentScore;
        private int speedingTier;
        private float ledLeftTimer, ledRightTimer, collisionFlashTimer;
        private bool wrongLaneOverstay;
        private GameObject warningRoot;
        private Image warningRing;      // the pulsing danger ring around the banner pill (v2 warnpulse)
        private Image warningIcon;      // the filled icon block leading the banner (shape + colour, never colour alone)
        private TMP_Text warningText;   // line 1: the rule that is being broken
        private TMP_Text warningSub;    // line 2: the fix ("← BLIJF LINKS")
        private Image routeMarker;
        private float routeBarWidth, routePenalty;
        private float alertTimer;       // momentary alert banner (hazard hit, illegal overtake)
        private string alertMsg;
        private float cautionTimer;     // calm caution banner, lower priority than alert (crossing-ahead telegraph)
        private string cautionMsg;
        private Image streakBg, vignette;
        private float vignetteTimer;
        // Cluster danger agreement (v2, screen 07): when a rule is actively broken the whole console shifts —
        // lip, hairline, readout, needle and the limit-sign glow all move to alert red together.
        private Image clusterLip, clusterLipGlow, clusterHair, limitGlow;
        private bool dangerState;

        // Per-frame text guards (same pattern as HUDController/Speedometer): only touch a TMP_Text when its
        // value actually changes, so the HUD does not allocate a string and rebuild a text mesh every frame.
        private int lastLimitShown = int.MinValue;
        private string lastWarningMsg, lastWarningSub;

        // ---- lifecycle ---------------------------------------------------------------
        private void Awake()
        {
            if (!built) Build();
            reelColour = accent;
        }

        private void OnEnable()
        {
            GameEvents.ScoreChanged += OnScore;
            GameEvents.StreakChanged += OnStreak;
            GameEvents.WrongLaneChanged += OnWrongLane;
            GameEvents.WrongLaneTick += OnWrongLaneTick;
            GameEvents.SpeedingTierChanged += OnSpeedingTier;
            GameEvents.CollisionOccurred += OnCollision;
            GameEvents.HazardHit += OnHazardHit;
            GameEvents.IllegalOvertake += OnIllegalOvertake;
            GameEvents.CrossingAhead += OnCrossingAhead;
            GameEvents.TurnAhead += OnTurnAhead;
            GameEvents.PedestrianYielded += OnPedestrianYielded;
            GameEvents.DayPhaseChanged += OnDayPhase;
            GameEvents.RewindStarted += OnRewindStarted;
            GameEvents.ChargingStarted += OnChargingStarted;
            GameEvents.SessionReset += OnSessionReset;
        }

        private void OnDisable()
        {
            GameEvents.ScoreChanged -= OnScore;
            GameEvents.StreakChanged -= OnStreak;
            GameEvents.WrongLaneChanged -= OnWrongLane;
            GameEvents.WrongLaneTick -= OnWrongLaneTick;
            GameEvents.SpeedingTierChanged -= OnSpeedingTier;
            GameEvents.CollisionOccurred -= OnCollision;
            GameEvents.HazardHit -= OnHazardHit;
            GameEvents.IllegalOvertake -= OnIllegalOvertake;
            GameEvents.CrossingAhead -= OnCrossingAhead;
            GameEvents.TurnAhead -= OnTurnAhead;
            GameEvents.PedestrianYielded -= OnPedestrianYielded;
            GameEvents.DayPhaseChanged -= OnDayPhase;
            GameEvents.RewindStarted -= OnRewindStarted;
            GameEvents.ChargingStarted -= OnChargingStarted;
            GameEvents.SessionReset -= OnSessionReset;
        }

        private void Update()
        {
            if (!built) return;
            DriveSpeedometer();
            DriveBattery();
            DriveRoute();
            DriveLimit();
            DriveWarning();
            DriveDangerState();
            DriveVignette();
            AnimateReels();
            DriveTierFx();
            TickLeds();
        }

        // ---- driving ----------------------------------------------------------------
        private void DriveSpeedometer()
        {
            float ratio, kmh, limit;
            if (WorldSpeed.Instance != null)
            {
                ratio = WorldSpeed.Instance.SpeedRatio;
                kmh = WorldSpeed.Instance.CurrentKmh;
            }
            else { ratio = 0f; kmh = 0f; }
            limit = SpeedZoneManager.Instance != null ? SpeedZoneManager.Instance.CurrentLimitKmh : 0f;

            // Fill covers the top semicircle (0..0.5); needle sweeps left(+90°)→up(0°)→right(-90°).
            if (speedFill != null) speedFill.fillAmount = ratio * 0.5f;
            if (needle != null)
                needle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(90f, -90f, ratio));
            if (kmhText != null) kmhText.SetText("{0}", Mathf.RoundToInt(kmh));

            Color tint = accent;
            if (limit > 0f && kmh > limit) tint = kmh > limit * 1.25f ? danger : caution;
            if (speedFill != null) speedFill.color = tint;
        }

        private void DriveBattery()
        {
            if (batterySegments == null) return;

            // Charge-station beat: the battery visibly refills (green, with a charging shimmer) so the stop reads
            // as a positive top-up. Runs from the ChargingStarted event for a fixed visual duration.
            if (charging)
            {
                chargeT += Time.unscaledDeltaTime;
                float fill = Mathf.Clamp01(chargeT / Mathf.Max(0.1f, chargeVisualSeconds));
                int litUp = Mathf.CeilToInt(fill * batterySegments.Length);
                float shimmer = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
                for (int i = 0; i < batterySegments.Length; i++)
                    batterySegments[i].color = i < litUp ? success * new Color(shimmer, shimmer, shimmer, 1f) : ledOff;
                return;
            }

            float remaining = TimerManager.Instance != null ? 1f - TimerManager.Instance.Normalized01 : 1f;
            int lit = Mathf.CeilToInt(remaining * batterySegments.Length);
            bool low = lit <= 3;
            float pulse = low ? (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 4f))) : 1f;
            for (int i = 0; i < batterySegments.Length; i++)
            {
                bool on = i < lit;
                Color c;
                if (!on) c = ledOff;
                else if (i == 0) c = danger;     // last bit of time — critical, at the bottom
                else if (i <= 2) c = gold;       // running-low zone near the bottom
                else c = success;                // plenty of time — green, toward the top
                if (on && low) c *= new Color(pulse, pulse, pulse, 1f);
                batterySegments[i].color = on ? c : ledOff;
            }
        }

        private void DriveRoute()
        {
            routePenalty = Mathf.MoveTowards(routePenalty, 0f, 0.06f * Time.deltaTime); // a crash setback recovers over ~2s
            if (routeFill != null)
            {
                float p = TimerManager.Instance != null ? TimerManager.Instance.Normalized01 : 0f;
                routeFill.fillAmount = Mathf.Clamp01(p - routePenalty);
            }
            if (routeMarker != null && routeFill != null)
                routeMarker.rectTransform.anchoredPosition = new Vector2(routeFill.fillAmount * routeBarWidth, 0f);
        }

        private void DriveLimit()
        {
            float limit = SpeedZoneManager.Instance != null ? SpeedZoneManager.Instance.CurrentLimitKmh : 0f;
            if (limitText != null)
            {
                int shown = limit > 0f ? Mathf.RoundToInt(limit) : int.MinValue; // sentinel == "no limit" ("--")
                if (shown != lastLimitShown)
                {
                    lastLimitShown = shown;
                    limitText.text = shown != int.MinValue ? shown.ToString() : "--";
                }
            }
            if (limitRing != null)
                limitRing.color = danger; // ring stays red (the old "tint on over" ternary returned danger on both sides)
        }

        // Which side is correct comes from the road config, so the messages match whatever side the game drives on.
        private bool DriveLeft => RoadSideConfig.Active == null || RoadSideConfig.Active.driveOnLeft;

        // The "you are breaking a rule" banner above the speedometer (the calm, named-rule layer).
        // v2 (screen 07): two lines — the rule on top, the fix underneath — behind a filled icon block,
        // with a pulsing danger ring instead of a full-pill colour wash.
        private void DriveWarning()
        {
            if (warningRoot == null) return;
            if (alertTimer > 0f) alertTimer -= Time.deltaTime;
            if (cautionTimer > 0f) cautionTimer -= Time.deltaTime;

            string msg = null, sub = null; Color col = caution;
            if (alertTimer > 0f)
            {
                SplitWarning(alertMsg, out msg, out sub); col = danger;
            }
            else if (cautionTimer > 0f)
            {
                // The calm telegraph layer: a crossing is coming up. Caution colour, not danger — this is an
                // anticipation cue, not a violation. It yields to any real red alert above.
                SplitWarning(cautionMsg, out msg, out sub); col = caution;
            }
            else if (speedingTier >= 1)
            {
                float limit = SpeedZoneManager.Instance != null ? SpeedZoneManager.Instance.CurrentLimitKmh : 0f;
                msg = "TE SNEL";
                sub = limit > 0f ? "MAX " + Mathf.RoundToInt(limit) : "REM AF";
                col = speedingTier >= 2 ? danger : caution;
            }
            else if (wrongLaneOverstay)
            {
                msg = "VERKEERDE WEGHELFT";
                sub = DriveLeft ? "← BLIJF LINKS" : "BLIJF RECHTS →";
                col = danger;
            }

            bool show = msg != null;
            if (show != warningRoot.activeSelf) warningRoot.SetActive(show);
            if (!show) { lastWarningMsg = null; lastWarningSub = null; return; }

            if (msg != lastWarningMsg || sub != lastWarningSub) // only rebuild the text mesh when the message actually changes
            {
                lastWarningMsg = msg; lastWarningSub = sub;
                warningText.text = msg;
                if (warningSub != null) warningSub.text = sub ?? "";
                // The pill hugs its content (play-test: a fixed width left "PAS OP · KUIL" swimming in dead
                // space). 96 = icon block zone, 26 = right padding; the ring/backing stretch along.
                float textW = warningText.GetPreferredValues(msg).x;
                if (warningSub != null && !string.IsNullOrEmpty(sub))
                    textW = Mathf.Max(textW, warningSub.GetPreferredValues(sub).x);
                var bannerRt = (RectTransform)warningRoot.transform;
                bannerRt.sizeDelta = new Vector2(Mathf.Clamp(96f + textW + 26f, 300f, 720f), bannerRt.sizeDelta.y);
            }
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 5f)); // colour throb stays per-frame (struct, no GC)
            if (warningRing != null) warningRing.color = new Color(col.r, col.g, col.b, 0.30f + 0.35f * pulse);
            if (warningIcon != null) warningIcon.color = col;
            if (warningSub != null) warningSub.color = Color.Lerp(ink, col, 0.45f);
        }

        // Split the existing "RULE  ·  FIX" message format into the banner's two lines (no separator → one line).
        private static void SplitWarning(string full, out string msg, out string sub)
        {
            msg = full; sub = null;
            if (string.IsNullOrEmpty(full)) return;
            int i = full.IndexOf("  ·  ", StringComparison.Ordinal);
            if (i < 0) return;
            msg = full.Substring(0, i);
            sub = full.Substring(i + 5);
        }

        // v2 (screen 07): the whole cluster agrees with the warning — lip, hairline, score readout, needle,
        // km/h numeral and the limit-sign glow shift to alert red while a rule is actively broken, and shift
        // back together when it clears. Colours are only touched on the state edge, not every frame.
        private void DriveDangerState()
        {
            bool dangerNow = alertTimer > 0f || wrongLaneOverstay || speedingTier >= 2;
            if (dangerNow == dangerState) return;
            dangerState = dangerNow;
            if (clusterLip != null) clusterLip.color = dangerNow ? danger : accent;
            if (clusterLipGlow != null) clusterLipGlow.color = UiKit.WithAlpha(dangerNow ? danger : accent, 0.35f);
            if (clusterHair != null) clusterHair.color = dangerNow ? UiKit.WithAlpha(danger, 0.45f) : UiKit.WithAlpha(UiKit.Rust, 0.55f);
            if (limitGlow != null) limitGlow.color = dangerNow ? UiKit.WithAlpha(danger, 0.65f) : new Color(0f, 0f, 0f, 0.4f);
            if (needle != null) needle.color = dangerNow ? danger : ink;
            if (kmhText != null) kmhText.color = dangerNow ? danger : ink;
        }

        // Red screen-edge flash on a crash — edge-only, so the centre of the screen stays clear.
        private void DriveVignette()
        {
            if (vignette == null) return;
            if (vignetteTimer > 0f) vignetteTimer -= Time.deltaTime;
            float a = Mathf.Clamp01(vignetteTimer / 0.6f) * 0.5f;
            vignette.color = new Color(danger.r, danger.g, danger.b, a);
        }

        // ---- events -----------------------------------------------------------------
        private void OnScore(int total, int delta)
        {
            // Show the running GROUP total (previous players + this turn) so the score never resets between players.
            int groupBase = GroupScoreManager.Instance != null ? GroupScoreManager.Instance.GroupTotal : 0;
            SetScore(groupBase + total, delta);
            reelColour = delta >= 0 ? success : danger;
            flashDuration = delta >= 0 ? PosFlashSeconds : NegFlashSeconds;
            flashTimer = flashDuration;
        }

        private void OnStreak(int streak, float multiplier)
        {
            // The team multiplier is always on screen (×1 minimum). Tiers (play-test 2026-07-05): a starting
            // streak wears brand GOLD, a HIGH streak (×3+) goes success GREEN — "the team is doing great"
            // should read green, not another shade of yellow.
            if (streakText != null) streakText.text = "×" + multiplier.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            if (streakBg != null)
                streakBg.color = multiplier >= 3f ? success : (multiplier > 1f ? gold : muted);

            // Celebrate only a CLIMB — never the session-reset refresh (same value) or a violation reset (drop),
            // and never the first event after a rebuild (lastMultiplier sentinel), so mid-run rejoins stay quiet.
            bool tierUp = lastMultiplier > 0f && multiplier > lastMultiplier + 0.01f;
            lastMultiplier = multiplier;
            if (tierUp) FireTierUp();
        }

        // Entering the oncoming lane is fine (it's how you overtake); only an OVERSTAY warns — so this just clears on return.
        private void OnWrongLane(bool inWrong) { if (!inWrong) { wrongLaneOverstay = false; ledLeftTimer = 0f; } }
        private void OnWrongLaneTick() { wrongLaneOverstay = true; ledLeftTimer = 9999f; } // fires only after the grace window closes
        private void OnSpeedingTier(int tier) { speedingTier = tier; ledRightTimer = tier > 0 ? 9999f : 0f; }
        // A real crash gets the banner too (play-test 2026-07-05: potholes and lane warnings spoke, but
        // driving into a car said nothing) — rule on top, the coaching fix underneath.
        private void OnCollision(CollisionSeverity sev, float kmh, Vector3 pos, bool absorbed)
        {
            collisionFlashTimer = 0.5f;
            if (absorbed) return;
            vignetteTimer = 0.6f;
            alertTimer = 1.6f;
            alertMsg = "BOTSING  ·  KIJK VERDER VOORUIT";
            cautionTimer = 0f; // a crash outranks any lingering "crossing ahead" caution
        }

        private void OnHazardHit(HazardSpawnConfig def, float kmh, Vector3 pos)
        {
            alertTimer = 1.3f;
            // A pedestrian hit comes through the hazard pipeline carrying the WARN_PEDESTRIAN key — show the
            // corrective lesson ("let pedestrians go first") rather than a generic obstacle warning.
            alertMsg = def != null && def.warnKey == "WARN_PEDESTRIAN" ? "VOETGANGER  ·  LAAT VOORGAAN"
                : def == null ? "PAS OP!" : def.response switch
                {
                    HazardResponse.SurfaceDefect  => "PAS OP  ·  KUIL",
                    HazardResponse.StaticObstacle => "PAS OP  ·  OBSTAKEL",
                    _                             => "PAS OP  ·  DREMPEL",
                };
            cautionTimer = 0f;          // a real hit cancels any lingering "crossing ahead" caution
            collisionFlashTimer = 0.5f; // blink the warning LEDs as well
        }

        // Telegraph: a pedestrian crossing is coming up. Calm caution banner so yielding is anticipated, not a gotcha.
        private void OnCrossingAhead(string warnKey, float metresAhead)
        {
            cautionMsg = "VOETGANGERS  ·  REM AF";
            cautionTimer = 2.2f;
        }

        // Telegraph: a bend is coming up (2026-07-05 play-test — a turn took a first-time player by surprise).
        // Same calm caution channel as the crossing, showing which way the road bends ("BOCHT  ·  ← LINKS").
        // A live crossing telegraph (safety-critical yield) outranks it, so a bend never stomps a crossing.
        private void OnTurnAhead(string warnKey, float metresAhead)
        {
            if (cautionTimer > 0f) return; // a crossing (or a just-shown bend) is still up — leave it be
            cautionMsg = SwahiliUI.Get(warnKey);
            cautionTimer = 2f;
        }

        // A clean yield — flash the score reel green (positive), no alert. The popup carries the points.
        private void OnPedestrianYielded(int basePoints, string popupKey, Vector3 pos)
        {
            reelColour = success;
            flashDuration = PosFlashSeconds;
            flashTimer = flashDuration;
        }

        private void OnIllegalOvertake(TrafficVehicle v)
        {
            alertTimer = 1.8f;
            alertMsg = "INHALEN DOET U " + (DriveLeft ? "RECHTS" : "LINKS"); // overtake on the side toward the oncoming lane
            collisionFlashTimer = 0.4f;
        }
        private void OnDayPhase(int index, string label) { if (dayLabel != null) dayLabel.text = string.IsNullOrEmpty(label) ? "ASUBUHI" : label; }
        private void OnRewindStarted() { routePenalty = Mathf.Min(routePenalty + 0.05f, 0.12f); } // route + rider dip back on a rewind
        private void OnChargingStarted() { charging = true; chargeT = 0f; } // charge-station beat: refill the battery
        // Score (group total) and the team multiplier carry across players, so this turn-reset leaves them alone.
        private void OnSessionReset() { wrongLaneOverstay = false; speedingTier = 0; routePenalty = 0f; alertTimer = 0f; cautionTimer = 0f; vignetteTimer = 0f; charging = false; chargeT = 0f; if (warningRoot != null) warningRoot.SetActive(false); }

        private void TickLeds()
        {
            if (collisionFlashTimer > 0f)
            {
                collisionFlashTimer -= Time.deltaTime;
                Color c = (Mathf.Repeat(Time.time * 12f, 1f) < 0.5f) ? danger : ledOff;
                if (ledLeft != null) ledLeft.color = c;
                if (ledRight != null) ledRight.color = c;
                return;
            }
            if (ledLeft != null) ledLeft.color = ledLeftTimer > 0f ? danger : ledOff;
            if (ledRight != null) ledRight.color = ledRightTimer > 0f ? (speedingTier >= 2 ? danger : caution) : ledOff;
        }

        // ---- reels ------------------------------------------------------------------
        private void SetScore(int newScore, int delta)
        {
            currentScore = Mathf.Clamp(newScore, 0, (int)Mathf.Pow(10, digits) - 1); // never overflow the reel count
            string s = currentScore.ToString().PadLeft(digits, '0');
            int dir = delta >= 0 ? 1 : -1;
            for (int i = 0; i < digits; i++)
            {
                int nd = s[i] - '0';
                int cur = Mathf.RoundToInt(reelCellPos[i]);
                int curDigit = ((cur % 10) + 10) % 10;
                int steps = dir > 0 ? (((nd - curDigit) % 10) + 10) % 10
                                    : -((((curDigit - nd) % 10) + 10) % 10);
                reelTargetCell[i] = cur + steps;
            }
        }

        // Score reel roll (Animation Playbook, 2026-07-05): the reels keep their linear spin, but on hitting
        // the detent each one overshoots a fraction of a cell and springs back, so points land with mechanical
        // weight. The colour wash now fades back to accent instead of snapping. Both temper with the
        // motion-sensitivity dial; at/below the Prikkelarm cutoff the roll is straight (no overshoot).
        private void AnimateReels()
        {
            if (flashTimer > 0f) flashTimer -= Time.deltaTime;
            float motion = SpeedFeel.MotionScale;
            float wash = Mathf.Clamp01(flashTimer / flashDuration) * Mathf.Lerp(0.45f, 1f, motion);
            Color c = Color.Lerp(dangerState ? danger : accent, reelColour, wash); // readout joins the cluster's danger shift

            float speed = 26f; // cells per second
            float dt = Time.deltaTime;
            for (int i = 0; i < reelStrips.Count; i++)
            {
                float before = reelCellPos[i];
                reelCellPos[i] = Mathf.MoveTowards(reelCellPos[i], reelTargetCell[i], speed * dt);
                if (Mathf.Approximately(reelCellPos[i], reelTargetCell[i]))
                {
                    // Arrival frame only (the reel actually moved): hand the roll's momentum to the settle
                    // spring so the digits click past the notch and bounce back once.
                    if (!Mathf.Approximately(before, reelTargetCell[i]) && motion > CalmCutoff)
                        reelSettleVel[i] += Mathf.Sign(reelTargetCell[i] - before) * speed * 0.18f * motion;
                    int p = reelTargetCell[i];
                    if (p < 10 || p > 19) { int np = 10 + (((p % 10) + 10) % 10); reelCellPos[i] = np; reelTargetCell[i] = np; }
                }
                // Under-damped settle spring, in cell units — one visible bounce (~0.25 cells), dead in ~0.5s.
                reelSettleVel[i] += (-160f * reelSettle[i] - 11f * reelSettleVel[i]) * dt;
                reelSettle[i] += reelSettleVel[i] * dt;
                reelStrips[i].anchoredPosition = new Vector2(0f, (reelCellPos[i] + reelSettle[i]) * cellHeight);
                foreach (var cell in reelCells[i]) if (cell != null) cell.color = c;
            }
        }

        // ---- streak tier-up celebration ------------------------------------------------
        private void FireTierUp()
        {
            tierFxTimer = TierFxSeconds;
            bool throwFx = SpeedFeel.MotionScale > CalmCutoff; // Prikkelarm keeps colour + a soft thump only
            if (tierRing != null)
            {
                tierRing.gameObject.SetActive(throwFx);
                tierRing.rectTransform.localScale = Vector3.one * 0.4f;
                tierRing.color = new Color(gold.r, gold.g, gold.b, 0.9f);
            }
            if (tierSparks == null) return;
            for (int i = 0; i < tierSparks.Length; i++)
            {
                Image sp = tierSparks[i];
                if (sp == null) continue;
                sp.gameObject.SetActive(throwFx);
                if (!throwFx) continue;
                float ang = (i + UnityEngine.Random.value * 0.8f) * (Mathf.PI * 2f / tierSparks.Length);
                tierSparkDirs[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (60f + UnityEngine.Random.value * 50f);
                sp.rectTransform.anchoredPosition = Vector2.zero;
                sp.rectTransform.localScale = Vector3.one;
                Color sc = sp.color; sc.a = 1f; sp.color = sc;
            }
        }

        private void DriveTierFx()
        {
            if (tierFxTimer <= 0f) return;
            tierFxTimer -= Time.deltaTime;
            if (tierFxTimer <= 0f)
            {
                if (streakBadge != null) streakBadge.localScale = Vector3.one;
                if (tierRing != null) tierRing.gameObject.SetActive(false);
                if (tierSparks != null)
                    for (int i = 0; i < tierSparks.Length; i++)
                        if (tierSparks[i] != null) tierSparks[i].gameObject.SetActive(false);
                return;
            }

            float motion = SpeedFeel.MotionScale;
            float t = 1f - tierFxTimer / TierFxSeconds;        // 0 → 1 over the celebration
            float easeOut = 1f - (1f - t) * (1f - t);

            // Badge thump: fast out, slow settle — ×1.5 at full motion, a soft ×1.2 on the calm dial.
            if (streakBadge != null)
            {
                float peak = Mathf.Lerp(0.2f, 0.5f, motion);
                float k = Mathf.Sin(Mathf.Pow(t, 0.6f) * Mathf.PI);
                streakBadge.localScale = Vector3.one * (1f + peak * k);
            }

            // Thrown ring: expands out of the badge while fading.
            if (tierRing != null && tierRing.gameObject.activeSelf)
            {
                tierRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.8f, easeOut);
                tierRing.color = new Color(gold.r, gold.g, gold.b, 0.9f * (1f - t));
            }

            // Spark burst: pooled dots fly outward, shrink and fade.
            if (tierSparks != null)
                for (int i = 0; i < tierSparks.Length; i++)
                {
                    Image sp = tierSparks[i];
                    if (sp == null || !sp.gameObject.activeSelf) continue;
                    sp.rectTransform.anchoredPosition = tierSparkDirs[i] * easeOut;
                    sp.rectTransform.localScale = Vector3.one * (1f - 0.7f * t);
                    Color sc = sp.color; sc.a = 1f - t; sp.color = sc;
                }
        }

    }
}
