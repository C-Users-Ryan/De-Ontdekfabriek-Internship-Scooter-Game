using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KenyaScooter.Core;
using KenyaScooter.Config;
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
    [DisallowMultipleComponent]
    public sealed class DiegeticHud : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private Vector2 panelSize = new Vector2(1500f, 270f);
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
        private float cellHeight;
        private float flashTimer;
        private Color reelColour;

        private Image speedFill, needle, ledLeft, ledRight, limitRing, routeFill;
        private TMP_Text kmhText, limitText, dayLabel, streakText;
        private Image[] batterySegments;
        private RectTransform streakBadge;
        private int currentScore;
        private int speedingTier;
        private float ledLeftTimer, ledRightTimer, collisionFlashTimer;
        private bool wrongLaneOverstay;
        private GameObject warningRoot;
        private Image warningBg;
        private TMP_Text warningText;
        private Image routeMarker;
        private float routeBarWidth, routePenalty;
        private float alertTimer;       // momentary alert banner (hazard hit, illegal overtake)
        private string alertMsg;
        private Image streakBg, vignette;
        private float vignetteTimer;

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
            GameEvents.DayPhaseChanged += OnDayPhase;
            GameEvents.RewindStarted += OnRewindStarted;
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
            GameEvents.DayPhaseChanged -= OnDayPhase;
            GameEvents.RewindStarted -= OnRewindStarted;
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
            DriveVignette();
            AnimateReels();
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
            if (limitText != null) limitText.text = limit > 0f ? Mathf.RoundToInt(limit).ToString() : "--";
            if (limitRing != null)
            {
                float kmh = WorldSpeed.Instance != null ? WorldSpeed.Instance.CurrentKmh : 0f;
                limitRing.color = (limit > 0f && kmh > limit) ? danger : danger; // ring stays red; could tint on over
            }
        }

        // Which side is correct comes from the road config, so the messages match whatever side the game drives on.
        private bool DriveLeft => RoadSideConfig.Active == null || RoadSideConfig.Active.driveOnLeft;

        // The "you are breaking a rule" banner above the speedometer (the calm, named-rule layer).
        private void DriveWarning()
        {
            if (warningRoot == null) return;
            if (alertTimer > 0f) alertTimer -= Time.deltaTime;

            string msg = null; Color col = caution;
            if (alertTimer > 0f)
            {
                msg = alertMsg; col = danger;
            }
            else if (speedingTier >= 1)
            {
                float limit = SpeedZoneManager.Instance != null ? SpeedZoneManager.Instance.CurrentLimitKmh : 0f;
                msg = limit > 0f ? "TE SNEL  ·  MAX " + Mathf.RoundToInt(limit) : "TE SNEL  ·  REM AF";
                col = speedingTier >= 2 ? danger : caution;
            }
            else if (wrongLaneOverstay)
            {
                msg = "VERKEERDE WEGHELFT  ·  " + (DriveLeft ? "BLIJF LINKS" : "BLIJF RECHTS");
                col = danger;
            }

            bool show = msg != null;
            if (show != warningRoot.activeSelf) warningRoot.SetActive(show);
            if (!show) return;

            warningText.text = msg;
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
            warningBg.color = new Color(col.r, col.g, col.b, 0.14f + 0.16f * pulse);
            warningText.color = Color.Lerp(ink, col, 0.3f);
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
            flashTimer = 0.45f;
        }

        private void OnStreak(int streak, float multiplier)
        {
            // The team multiplier is always on screen (×1 minimum) and brightens as the streak climbs.
            if (streakText != null) streakText.text = "×" + multiplier.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            if (streakBg != null)
                streakBg.color = multiplier >= 3f ? success : (multiplier >= 2f ? accent : (multiplier > 1f ? gold : muted));
        }

        // Entering the oncoming lane is fine (it's how you overtake); only an OVERSTAY warns — so this just clears on return.
        private void OnWrongLane(bool inWrong) { if (!inWrong) { wrongLaneOverstay = false; ledLeftTimer = 0f; } }
        private void OnWrongLaneTick() { wrongLaneOverstay = true; ledLeftTimer = 9999f; } // fires only after the grace window closes
        private void OnSpeedingTier(int tier) { speedingTier = tier; ledRightTimer = tier > 0 ? 9999f : 0f; }
        private void OnCollision(CollisionSeverity sev, float kmh, Vector3 pos, bool absorbed) { collisionFlashTimer = 0.5f; if (!absorbed) vignetteTimer = 0.6f; }

        private void OnHazardHit(HazardSpawnConfig def, float kmh, Vector3 pos)
        {
            alertTimer = 1.3f;
            alertMsg = def == null ? "PAS OP!" : def.response switch
            {
                HazardResponse.SurfaceDefect  => "PAS OP  ·  KUIL",
                HazardResponse.StaticObstacle => "PAS OP  ·  OBSTAKEL",
                _                             => "PAS OP  ·  DREMPEL",
            };
            collisionFlashTimer = 0.5f; // blink the warning LEDs as well
        }

        private void OnIllegalOvertake(TrafficVehicle v)
        {
            alertTimer = 1.8f;
            alertMsg = "INHALEN DOET U " + (DriveLeft ? "RECHTS" : "LINKS"); // overtake on the side toward the oncoming lane
            collisionFlashTimer = 0.4f;
        }
        private void OnDayPhase(int index, string label) { if (dayLabel != null) dayLabel.text = string.IsNullOrEmpty(label) ? "ASUBUHI" : label; }
        private void OnRewindStarted() { routePenalty = Mathf.Min(routePenalty + 0.05f, 0.12f); } // route + rider dip back on a rewind
        // Score (group total) and the team multiplier carry across players, so this turn-reset leaves them alone.
        private void OnSessionReset() { wrongLaneOverstay = false; speedingTier = 0; routePenalty = 0f; alertTimer = 0f; vignetteTimer = 0f; if (warningRoot != null) warningRoot.SetActive(false); }

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

        private void AnimateReels()
        {
            if (flashTimer > 0f) flashTimer -= Time.deltaTime;
            Color c = flashTimer > 0f ? reelColour : accent;
            float speed = 26f; // cells per second
            for (int i = 0; i < reelStrips.Count; i++)
            {
                reelCellPos[i] = Mathf.MoveTowards(reelCellPos[i], reelTargetCell[i], speed * Time.deltaTime);
                reelStrips[i].anchoredPosition = new Vector2(0f, reelCellPos[i] * cellHeight);
                if (Mathf.Approximately(reelCellPos[i], reelTargetCell[i]))
                {
                    int p = reelTargetCell[i];
                    if (p < 10 || p > 19) { int np = 10 + (((p % 10) + 10) % 10); reelCellPos[i] = np; reelTargetCell[i] = np; reelStrips[i].anchoredPosition = new Vector2(0f, np * cellHeight); }
                }
                foreach (var cell in reelCells[i]) if (cell != null) cell.color = c;
            }
        }

        // ---- build ------------------------------------------------------------------
        [ContextMenu("Rebuild now")]
        public void Build()
        {
            ClearGenerated();
            reelStrips.Clear(); reelCells.Clear();

            RectTransform root = (RectTransform)transform;
            Stretch(root); // full-screen container: cluster sits at the bottom, route strip at the top

            // cluster panel — flush against the bottom edge of the screen
            RectTransform panel = NewRect(root, "Cluster");
            panel.anchorMin = new Vector2(0.5f, 0f); panel.anchorMax = new Vector2(0.5f, 0f); panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = panelSize; panel.anchoredPosition = new Vector2(0f, Mathf.Min(bottomMargin, 0f)); // never floats above the bottom edge
            Image pimg = panel.gameObject.AddComponent<Image>();
            pimg.color = panelColour; pimg.sprite = RoundedTopSprite(28); pimg.type = Image.Type.Sliced; pimg.raycastTarget = false;

            // Inset, rounded accent stripe — clears the panel's rounded corners instead of cutting across them.
            Image lip = AddImage(panel, "AccentLip", accent, PillSprite());
            lip.rectTransform.anchorMin = new Vector2(0f, 1f); lip.rectTransform.anchorMax = new Vector2(1f, 1f);
            lip.rectTransform.pivot = new Vector2(0.5f, 1f); lip.rectTransform.sizeDelta = new Vector2(-90f, 5f); lip.rectTransform.anchoredPosition = new Vector2(0f, -4f);

            float w = panelSize.x;
            BuildTopStrip(root, w);
            BuildOdometer(panel, new Vector2(-w * 0.30f, 4f));
            BuildSpeedometer(panel, new Vector2(0f, 0f));
            BuildBattery(panel, new Vector2(w * 0.24f, -4f));
            BuildLimit(panel, new Vector2(w * 0.36f, 0f));
            BuildWarning(root);

            // red screen-edge flash on a crash (on top, edge-only so the centre stays clear)
            vignette = AddImage(root, "CrashVignette", danger, VignetteSprite());
            Stretch(vignette.rectTransform);
            vignette.color = new Color(danger.r, danger.g, danger.b, 0f);

            built = true;
        }

        // The route + day strip lives at the TOP of the screen (not on the cluster).
        private void BuildTopStrip(RectTransform root, float w)
        {
            RectTransform strip = NewRect(root, "TopStrip");
            strip.anchorMin = new Vector2(0.5f, 1f); strip.anchorMax = new Vector2(0.5f, 1f); strip.pivot = new Vector2(0.5f, 1f);
            strip.sizeDelta = new Vector2(w, 40f); strip.anchoredPosition = new Vector2(0f, -22f);

            TMP_Text day = AddText(strip, "DayLabel", "ASUBUHI", 24, accent, TextAlignmentOptions.Left);
            Anchor(day.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(170f, 30f), new Vector2(90f, 0f));
            dayLabel = day;

            Image bar = AddImage(strip, "RouteBar", trackDim, PillSprite());
            Anchor(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(w * 0.64f, 6f), new Vector2(40f, 0f));
            Image fill = AddImage(bar.rectTransform, "RouteFill", accent, PillSprite());
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 0.4f;
            Stretch(fill.rectTransform);
            routeFill = fill;
            routeBarWidth = w * 0.64f;

            // charge-station / goal marker at the end of the route
            Image charge = AddImage(bar.rectTransform, "Charge", gold, BoltSprite());
            Anchor(charge.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(22f, 22f), new Vector2(0f, 0f));

            // rider marker rides the head of the fill (stand-in for a scooter sprite)
            Image rider = AddImage(bar.rectTransform, "Rider", ink, CircleSprite());
            rider.rectTransform.anchorMin = new Vector2(0f, 0.5f); rider.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rider.rectTransform.pivot = new Vector2(0.5f, 0.5f); rider.rectTransform.sizeDelta = new Vector2(20f, 20f); rider.rectTransform.anchoredPosition = Vector2.zero;
            Image riderDot = AddImage(rider.rectTransform, "Dot", accent, CircleSprite());
            Anchor(riderDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10f, 10f), Vector2.zero);
            routeMarker = rider;
        }

        private void BuildOdometer(RectTransform parent, Vector2 pos)
        {
            RectTransform block = NewRect(parent, "Odometer");
            Anchor(block, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 150f), pos);

            TMP_Text label = AddText(block, "Label", "ALAMA · KM", 18, muted, TextAlignmentOptions.Center);
            Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(300f, 22f), new Vector2(0f, -6f));

            // dark window holding the reels, framed by a lighter bezel so it stands out from the panel
            Image bezel = AddImage(block, "WindowBezel", Hex("#3C2D22"), RoundedSprite(13));
            Anchor(bezel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(376f, 112f), new Vector2(0f, -2f));
            Image window = AddImage(block, "Window", Hex("#0B0806"), RoundedSprite(10));
            Anchor(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 96f), new Vector2(0f, -2f));

            float cellW = 56f; cellHeight = 90f;
            reelCellPos = new float[digits]; reelTargetCell = new int[digits];
            float totalW = digits * cellW + 18f; // gap
            float startX = -totalW * 0.5f + cellW * 0.5f;
            for (int i = 0; i < digits; i++)
            {
                float x = startX + i * cellW + (i >= digits - 3 ? 18f : 0f);
                BuildReel(window.rectTransform, new Vector2(x, 0f), cellW);
                reelCellPos[i] = 10f; reelTargetCell[i] = 10;
            }

            // team multiplier badge — always visible (×1 minimum), brightens as the shared streak climbs
            RectTransform badge = NewRect(block, "StreakBadge");
            Anchor(badge, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(66f, 42f), new Vector2(-2f, -2f));
            Image bg = badge.gameObject.AddComponent<Image>(); bg.sprite = RoundedSprite(12); bg.type = Image.Type.Sliced; bg.color = muted;
            streakBg = bg;
            streakText = AddText(badge, "x", "×1", 26, panelColour, TextAlignmentOptions.Center);
            Stretch(streakText.rectTransform);
            streakBadge = badge;

            TMP_Text sub = AddText(block, "Sub", "TEAM SCORE", 15, muted, TextAlignmentOptions.Center);
            Anchor(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(300f, 20f), new Vector2(0f, 4f));
        }

        private void BuildReel(RectTransform parent, Vector2 pos, float cellW)
        {
            RectTransform reel = NewRect(parent, "Reel");
            Anchor(reel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cellW, cellHeight), pos);
            reel.gameObject.AddComponent<RectMask2D>();

            RectTransform strip = NewRect(reel, "Strip");
            strip.anchorMin = new Vector2(0.5f, 0.5f); strip.anchorMax = new Vector2(0.5f, 0.5f); strip.pivot = new Vector2(0.5f, 0.5f);
            strip.sizeDelta = new Vector2(cellW, cellHeight * 30f); strip.anchoredPosition = new Vector2(0f, 10f * cellHeight);

            var cells = new TMP_Text[30];
            for (int k = 0; k < 30; k++)
            {
                TMP_Text c = AddText(strip, "c" + k, (k % 10).ToString(), 70, accent, TextAlignmentOptions.Center);
                Anchor(c.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cellW, cellHeight), new Vector2(0f, -k * cellHeight));
                cells[k] = c;
            }
            reelStrips.Add(strip); reelCells.Add(cells);
        }

        private void BuildSpeedometer(RectTransform parent, Vector2 pos)
        {
            RectTransform block = NewRect(parent, "Speedometer");
            Anchor(block, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260f, 200f), pos);

            const float gd = 150f;                 // gauge diameter
            Vector2 hub = new Vector2(0f, -6f);     // lowered so the whole gauge sits further down in the panel

            // Top semicircle: origin Left + clockwise + 0.5 fills Left→Top→Right.
            Image track = AddImage(block, "Track", trackDim, RingSprite(0.72f));
            track.type = Image.Type.Filled; track.fillMethod = Image.FillMethod.Radial360; track.fillOrigin = (int)Image.Origin360.Left; track.fillClockwise = true; track.fillAmount = 0.5f;
            Anchor(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(gd, gd), hub);

            Image fill = AddImage(block, "Fill", accent, RingSprite(0.72f));
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Radial360; fill.fillOrigin = (int)Image.Origin360.Left; fill.fillClockwise = true; fill.fillAmount = 0.25f;
            Anchor(fill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(gd, gd), hub);
            speedFill = fill;

            // needle: bar with its base pinned at the hub, pointing up at half-speed
            Image n = AddImage(block, "Needle", ink, null);
            n.rectTransform.anchorMin = new Vector2(0.5f, 0.5f); n.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            n.rectTransform.pivot = new Vector2(0.5f, 0f);
            n.rectTransform.sizeDelta = new Vector2(5f, gd * 0.5f - 6f);
            n.rectTransform.anchoredPosition = hub;
            needle = n;

            Image hubCap = AddImage(block, "Hub", ink, CircleSprite());
            Anchor(hubCap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(16f, 16f), hub);

            // km/h number sits in the lower gap of the dial, just under the hub
            kmhText = AddText(block, "Kmh", "64", 44, ink, TextAlignmentOptions.Center);
            Anchor(kmhText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180f, 48f), new Vector2(0f, hub.y - 34f));
            TMP_Text unit = AddText(block, "Unit", "speedometer", 15, muted, TextAlignmentOptions.Center);
            Anchor(unit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180f, 18f), new Vector2(0f, hub.y - 64f));

            ledLeft = AddImage(block, "LED_Left", ledOff, CircleSprite());
            Anchor(ledLeft.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 16f), new Vector2(12f, hub.y));
            ledRight = AddImage(block, "LED_Right", ledOff, CircleSprite());
            Anchor(ledRight.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(16f, 16f), new Vector2(-12f, hub.y));
        }

        private void BuildBattery(RectTransform parent, Vector2 pos)
        {
            RectTransform block = NewRect(parent, "Battery");
            Anchor(block, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70f, 150f), pos);

            int n = 10; batterySegments = new Image[n];
            float segH = 9f, gap = 4f; float start = -(n * (segH + gap)) * 0.5f + segH * 0.5f + 6f;
            for (int i = 0; i < n; i++)
            {
                Image seg = AddImage(block, "Seg" + i, ledOff, RoundedSprite(2));
                Anchor(seg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(44f, segH), new Vector2(0f, start + i * (segH + gap)));
                batterySegments[i] = seg;
            }
            TMP_Text sub = AddText(block, "Sub", "MUDA", 15, muted, TextAlignmentOptions.Center);
            Anchor(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(90f, 18f), new Vector2(0f, 2f));
        }

        private void BuildLimit(RectTransform parent, Vector2 pos)
        {
            RectTransform block = NewRect(parent, "SpeedLimit");
            Anchor(block, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(110f, 150f), pos);

            Image disc = AddImage(block, "Disc", ink, CircleSprite());
            Anchor(disc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(96f, 96f), new Vector2(0f, 6f));
            Image ring = AddImage(block, "Ring", danger, RingSprite(0.78f));
            Anchor(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(96f, 96f), new Vector2(0f, 6f));
            limitRing = ring;
            limitText = AddText(block, "Number", "80", 46, panelColour, TextAlignmentOptions.Center);
            Anchor(limitText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90f, 56f), new Vector2(0f, 6f));

            TMP_Text sub = AddText(block, "Sub", "speed limit", 15, muted, TextAlignmentOptions.Center);
            Anchor(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(110f, 18f), new Vector2(0f, 2f));
        }

        // Rule-warning banner — floats just above the cluster, centred over the speedometer.
        private void BuildWarning(RectTransform root)
        {
            RectTransform banner = NewRect(root, "RuleWarning");
            banner.anchorMin = new Vector2(0.5f, 0f); banner.anchorMax = new Vector2(0.5f, 0f); banner.pivot = new Vector2(0.5f, 0f);
            banner.sizeDelta = new Vector2(640f, 56f);
            banner.anchoredPosition = new Vector2(0f, panelSize.y + 16f); // just above the panel, over the gauge

            Image bg = banner.gameObject.AddComponent<Image>();
            bg.sprite = RoundedSprite(24); bg.type = Image.Type.Sliced; bg.raycastTarget = false;
            bg.color = new Color(danger.r, danger.g, danger.b, 0.22f);
            warningBg = bg;

            warningText = AddText(banner, "Text", "VERKEERDE WEGHELFT", 26, ink, TextAlignmentOptions.Center);
            Stretch(warningText.rectTransform);

            warningRoot = banner.gameObject;
            warningRoot.SetActive(false);
        }

        // ---- UI helpers --------------------------------------------------------------
        private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        private RectTransform NewRect(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private Image AddImage(RectTransform parent, string name, Color colour, Sprite sprite)
        {
            var rt = NewRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = colour; img.sprite = sprite; img.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced; // 9-slice rounded sprites
            return img;
        }

        private TMP_Text AddText(RectTransform parent, string name, string text, float size, Color colour, TextAlignmentOptions align)
        {
            var rt = NewRect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = colour; t.alignment = align;
            t.raycastTarget = false; t.fontStyle = FontStyles.Bold;
            return t;
        }

        private static void Stretch(RectTransform rt)
        { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f); rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }

        private static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos)
        { rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = pos; }

        private void ClearGenerated()
        {
            var rt = (RectTransform)transform;
            for (int i = rt.childCount - 1; i >= 0; i--)
            {
                var child = rt.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        // ---- procedural sprites ------------------------------------------------------
        private Sprite _circle, _bolt;
        private readonly Dictionary<int, Sprite> _rounded = new();
        private readonly Dictionary<float, Sprite> _ring = new();
        private Sprite _pill;

        private Sprite CircleSprite()
        {
            if (_circle != null) return _circle;
            int s = 64; var tex = NewTex(s, s);
            float r = s * 0.5f, cx = r, cy = r;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            { float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)); tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d))); }
            tex.Apply(); _circle = ToSprite(tex); return _circle;
        }

        private Sprite RingSprite(float innerFrac)
        {
            if (_ring.TryGetValue(innerFrac, out var cached)) return cached;
            int s = 128; var tex = NewTex(s, s);
            float r = s * 0.5f, cx = r, cy = r, inner = r * innerFrac;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            { float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)); float a = Mathf.Clamp01(r - d) * Mathf.Clamp01(d - inner); tex.SetPixel(x, y, new Color(1, 1, 1, a)); }
            tex.Apply(); var sp = ToSprite(tex); _ring[innerFrac] = sp; return sp;
        }

        private Sprite RoundedSprite(int radius)
        {
            if (_rounded.TryGetValue(radius, out var cached)) return cached;
            int s = radius * 2 + 4; var tex = NewTex(s, s);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Max(radius - x, x - (s - radius), 0f);
                float dy = Mathf.Max(radius - y, y - (s - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
            }
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            _rounded[radius] = sp; return sp;
        }

        private Sprite PillSprite() { if (_pill == null) _pill = RoundedSprite(3); return _pill; }

        // Transparent in the middle, opaque toward the edges/corners — tinted red and stretched full-screen.
        private Sprite _vignette;
        private Sprite VignetteSprite()
        {
            if (_vignette != null) return _vignette;
            int s = 128; var tex = NewTex(s, s);
            float c = s * 0.5f;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0.65f, 1.15f, d)));
            }
            tex.Apply(); _vignette = ToSprite(tex); return _vignette;
        }

        // Rounded TOP corners, square bottom — so the panel can sit flush against the screen's bottom edge.
        private Sprite _roundedTop;
        private Sprite RoundedTopSprite(int radius)
        {
            if (_roundedTop != null) return _roundedTop;
            int s = radius * 2 + 4; var tex = NewTex(s, s);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Max(radius - x, x - (s - radius), 0f);
                float dy = Mathf.Max(y - (s - radius), 0f); // only the top edge rounds; bottom stays square
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
            }
            tex.Apply();
            _roundedTop = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, 2, radius, radius));
            return _roundedTop;
        }

        private Sprite BoltSprite()
        {
            if (_bolt != null) return _bolt;
            int s = 64; var tex = NewTex(s, s);
            Vector2[] pts = { new(0.55f,0.95f), new(0.30f,0.50f), new(0.48f,0.50f), new(0.42f,0.05f), new(0.70f,0.55f), new(0.52f,0.55f) };
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            { bool inside = PointInPoly(new Vector2((float)x / s, (float)y / s), pts); tex.SetPixel(x, y, new Color(1, 1, 1, inside ? 1f : 0f)); }
            tex.Apply(); _bolt = ToSprite(tex); return _bolt;
        }

        private static bool PointInPoly(Vector2 p, Vector2[] v)
        { bool c = false; for (int i = 0, j = v.Length - 1; i < v.Length; j = i++) if (((v[i].y > p.y) != (v[j].y > p.y)) && (p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x)) c = !c; return c; }

        private static Texture2D NewTex(int w, int h) { var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; return t; }
        private static Sprite ToSprite(Texture2D tex) => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
