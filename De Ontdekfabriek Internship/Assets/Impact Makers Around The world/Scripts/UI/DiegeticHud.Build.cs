// Construction half of DiegeticHud — split out 2026-06-27 for readability (no behaviour change).
// The runtime half (fields, lifecycle, per-frame drivers, event handlers) lives in DiegeticHud.cs.
// This file holds Build() and the procedural panel/sprite helpers that generate the instrument cluster.
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
    public sealed partial class DiegeticHud
    {
        // ---- build ------------------------------------------------------------------
        [ContextMenu("Rebuild now")]
        public void Build()
        {
            ClearGenerated();
            reelStrips.Clear(); reelCells.Clear();

            RectTransform root = (RectTransform)transform;
            Stretch(root); // full-screen container: cluster sits at the bottom, route strip at the top

            // Soft shadow that bleeds UPWARD from the cluster (the panel is pinned to the bottom edge), so the
            // instrument cluster reads as a solid console lifted off the road rather than a flat sticker.
            Image clusterShadow = AddImage(root, "ClusterShadow", new Color(0f, 0f, 0f, 0.34f), UiKit.SoftShadow(28));
            clusterShadow.rectTransform.anchorMin = new Vector2(0.5f, 0f); clusterShadow.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            clusterShadow.rectTransform.pivot = new Vector2(0.5f, 0f);
            clusterShadow.rectTransform.sizeDelta = panelSize + new Vector2(36f, 30f);
            clusterShadow.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Min(bottomMargin, 0f) + 8f);

            // cluster panel — flush against the bottom edge of the screen. v2: the panel Image itself is the
            // spec RUST HAIRLINE; a near-solid fill sits 2px inside it, so the console carries the same framed
            // edge as every spec panel (and the hairline can shift to danger red with the cluster).
            RectTransform panel = NewRect(root, "Cluster");
            panel.anchorMin = new Vector2(0.5f, 0f); panel.anchorMax = new Vector2(0.5f, 0f); panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = panelSize; panel.anchoredPosition = new Vector2(0f, Mathf.Min(bottomMargin, 0f)); // never floats above the bottom edge
            Image pimg = panel.gameObject.AddComponent<Image>();
            pimg.color = UiKit.WithAlpha(UiKit.Rust, 0.55f); pimg.sprite = RoundedTopSprite(28); pimg.type = Image.Type.Sliced; pimg.raycastTarget = false;
            clusterHair = pimg;
            Image pfill = AddImage(panel, "Fill", UiKit.WithAlpha(panelColour, 0.94f), RoundedTopSprite(28));
            pfill.rectTransform.anchorMin = Vector2.zero; pfill.rectTransform.anchorMax = Vector2.one;
            pfill.rectTransform.offsetMin = new Vector2(2f, 0f); pfill.rectTransform.offsetMax = new Vector2(-2f, -2f); // bottom stays flush

            // A faint top sheen across the console gives it a lit upper bezel instead of a flat fill.
            Image panelSheen = AddImage(panel, "Sheen", new Color(1f, 0.93f, 0.84f, 0.05f), UiKit.TopSheen());
            panelSheen.rectTransform.anchorMin = new Vector2(0f, 1f); panelSheen.rectTransform.anchorMax = new Vector2(1f, 1f);
            panelSheen.rectTransform.pivot = new Vector2(0.5f, 1f);
            panelSheen.rectTransform.sizeDelta = new Vector2(-12f, 120f); panelSheen.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            // Inset, rounded accent stripe — clears the panel's rounded corners instead of cutting across them.
            // A soft glow sits behind it so the brand orange edge looks lit, not painted on.
            Image lipGlow = AddImage(panel, "AccentLipGlow", UiKit.WithAlpha(accent, 0.35f), UiKit.SoftShadow(6));
            lipGlow.rectTransform.anchorMin = new Vector2(0f, 1f); lipGlow.rectTransform.anchorMax = new Vector2(1f, 1f);
            lipGlow.rectTransform.pivot = new Vector2(0.5f, 1f); lipGlow.rectTransform.sizeDelta = new Vector2(-78f, 16f); lipGlow.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            Image lip = AddImage(panel, "AccentLip", accent, PillSprite());
            lip.rectTransform.anchorMin = new Vector2(0f, 1f); lip.rectTransform.anchorMax = new Vector2(1f, 1f);
            lip.rectTransform.pivot = new Vector2(0.5f, 1f); lip.rectTransform.sizeDelta = new Vector2(-90f, 5f); lip.rectTransform.anchoredPosition = new Vector2(0f, -4f);
            clusterLip = lip; clusterLipGlow = lipGlow; // the danger-state driver recolours these as a set

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
            strip.sizeDelta = new Vector2(w, 52f); strip.anchoredPosition = new Vector2(0f, -22f);

            // A dark backing pill so the strip stays readable over any road colour. v2: more opaque (0.55 → 0.74)
            // so the route reads at arm's length over bright sand.
            Image stripBg = AddImage(strip, "StripBg", new Color(panelColour.r, panelColour.g, panelColour.b, 0.74f), RoundedSprite(26));
            Anchor(stripBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(w * 0.80f, 52f), Vector2.zero);

            // Little glowing sun dot ahead of the day label. Both are anchored from the PILL's left edge with
            // a left pivot (play-test: centre-pivoted boxes made ALASIRI/JIONI sit differently per word length).
            float pillLeft = w * 0.10f; // the backing pill is w*0.80 wide, centred
            Image sun = AddImage(strip, "SunDot", Hex("#F6A949"), CircleSprite());
            Anchor(sun.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 20f), new Vector2(pillLeft + 36f, 0f));
            sunDotGO = sun.gameObject; // hidden in endless — the heart pips take this corner of the strip

            TMP_Text day = AddText(strip, "DayLabel", "ASUBUHI", 24, accent, TextAlignmentOptions.Left);
            Anchor(day.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(230f, 30f), Vector2.zero);
            day.rectTransform.pivot = new Vector2(0f, 0.5f);
            day.rectTransform.anchoredPosition = new Vector2(pillLeft + 56f, 0f);
            UiKit.Caps(day, 0.12f);
            dayLabel = day;

            Image bar = AddImage(strip, "RouteBar", trackDim, PillSprite());
            Anchor(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(w * 0.58f, 10f), new Vector2(60f, 0f));
            Image fill = AddImage(bar.rectTransform, "RouteFill", accent, PillSprite());
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 0.4f;
            Stretch(fill.rectTransform);
            routeFill = fill;
            routeBarWidth = w * 0.58f;

            // Quarter ticks give the run a sense of distance-remaining at a glance (v2).
            for (int i = 1; i <= 3; i++)
            {
                Image tick = AddImage(bar.rectTransform, "Tick" + i, UiKit.WithAlpha(ink, 0.25f), null);
                tick.rectTransform.anchorMin = new Vector2(i * 0.25f, 0.5f); tick.rectTransform.anchorMax = new Vector2(i * 0.25f, 0.5f);
                tick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                tick.rectTransform.sizeDelta = new Vector2(3f, 16f); tick.rectTransform.anchoredPosition = Vector2.zero;
            }

            // Charge-station / goal marker at the end of the route — v2: the bolt sits on a gold roundel
            // end-cap, so the destination reads as a real place, not a stray glyph.
            Image chargeDisc = AddImage(bar.rectTransform, "ChargeDisc", gold, CircleSprite());
            Anchor(chargeDisc.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(32f, 32f), new Vector2(10f, 0f));
            Image charge = AddImage(chargeDisc.rectTransform, "Bolt", panelColour, BoltSprite());
            Anchor(charge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20f, 20f), Vector2.zero);
            chargeDiscGO = chargeDisc.gameObject; // hidden in endless (no charge-station goal there)

            // Endless-only DISTANCE readout, styled into the strip (accent, right-aligned). It sits roughly where the
            // charge-station goal marker is in the relay; ApplyModeLayout swaps the two by GameMode so only one shows.
            // Anchored INSIDE the backing pill (the pill's right edge sits w*0.10 in from the strip's).
            TMP_Text dist = AddText(strip, "Distance", "0 m", 22, accent, TextAlignmentOptions.Right);
            Anchor(dist.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(180f, 28f), new Vector2(-(w * 0.10f + 24f), 0f));
            dist.rectTransform.pivot = new Vector2(1f, 0.5f);
            dist.gameObject.SetActive(false);
            distanceText = dist;

            // Endless-only LIVES: one heart pip per life, in the day label's spot (ApplyModeLayout swaps them —
            // in a solo lives run the lives are the headline, the time of day is set dressing). Big, red and at
            // the top of the screen so a child reads "how am I doing" at a glance; the LEVENS battery gauge in
            // the cluster below mirrors the same count. DriveBattery paints them.
            heartPips = new Image[MaxHearts];
            for (int i = 0; i < MaxHearts; i++)
            {
                Image heart = AddImage(strip, "Heart" + i, danger, HeartSprite());
                Anchor(heart.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 30f), new Vector2(pillLeft + 36f + i * 34f, 0f));
                heart.gameObject.SetActive(false);
                heartPips[i] = heart;
            }

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
            Anchor(block, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(470f, 160f), pos);

            // v2: ONE caption ("Team score · Alama") above the window — the duplicate caption underneath is gone,
            // which pays for the bigger numerals in a shorter cluster.
            TMP_Text label = AddText(block, "Label", "TEAM SCORE · ALAMA", 18, muted, TextAlignmentOptions.Center);
            Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(340f, 22f), new Vector2(0f, -4f));
            UiKit.Caps(label, 0.12f);

            // dark window holding the reels, framed by a lighter bezel so it stands out from the panel
            Image bezel = AddImage(block, "WindowBezel", Hex("#46342A"), RoundedSprite(13));
            Anchor(bezel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 124f), new Vector2(0f, -10f));
            Image window = AddImage(block, "Window", Hex("#0B0806"), RoundedSprite(10));
            Anchor(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(404f, 108f), new Vector2(0f, -10f));
            // Inner top shadow so the readout looks recessed into the console (an inset well, not a flat patch).
            Image innerShade = AddImage(window.rectTransform, "InnerShade", new Color(0f, 0f, 0f, 0.55f), UiKit.TopSheen());
            innerShade.rectTransform.anchorMin = new Vector2(0f, 1f); innerShade.rectTransform.anchorMax = new Vector2(1f, 1f);
            innerShade.rectTransform.pivot = new Vector2(0.5f, 1f);
            innerShade.rectTransform.sizeDelta = new Vector2(0f, 30f); innerShade.rectTransform.anchoredPosition = Vector2.zero;

            float cellW = 64f; cellHeight = 100f; // numerals +18% for the 50–100ms glance (v2)
            reelCellPos = new float[digits]; reelTargetCell = new int[digits];
            reelSettle = new float[digits]; reelSettleVel = new float[digits];
            float totalW = digits * cellW + 18f; // gap
            float startX = -totalW * 0.5f + cellW * 0.5f;
            for (int i = 0; i < digits; i++)
            {
                float x = startX + i * cellW + (i >= digits - 3 ? 18f : 0f);
                BuildReel(window.rectTransform, new Vector2(x, 0f), cellW);
                reelCellPos[i] = 10f; reelTargetCell[i] = 10;
            }

            // team multiplier badge — always visible (×1 minimum). It rides the top-right CORNER of the score
            // window (play-test: floating beside the caption it stopped reading as part of the score). Parented
            // to the window and built after the reels so it draws on top of both.
            Image badgeShadow = AddImage(window.rectTransform, "StreakBadgeShadow", new Color(0f, 0f, 0f, 0.4f), UiKit.SoftShadow(12));
            Anchor(badgeShadow.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(80f, 54f), new Vector2(8f, 6f));
            RectTransform badge = NewRect(window.rectTransform, "StreakBadge");
            Anchor(badge, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(70f, 44f), new Vector2(8f, 10f));
            Image bg = badge.gameObject.AddComponent<Image>(); bg.sprite = RoundedSprite(12); bg.type = Image.Type.Sliced; bg.color = muted;
            streakBg = bg;
            streakText = AddText(badge, "x", "×1", 27, panelColour, TextAlignmentOptions.Center);
            Stretch(streakText.rectTransform);
            streakBadge = badge;

            // Tier-up celebration layers: an expanding ring plus a pooled spark burst, parented to the badge
            // so they fire from wherever it sits (and thump with it). Hidden until the multiplier climbs.
            tierRing = AddImage(badge, "TierRing", gold, RingSprite(0.86f));
            Anchor(tierRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(110f, 110f), Vector2.zero);
            tierRing.rectTransform.SetAsFirstSibling(); // behind the ×N text
            tierRing.gameObject.SetActive(false);
            const int sparkCount = 10;
            tierSparks = new Image[sparkCount]; tierSparkDirs = new Vector2[sparkCount];
            for (int i = 0; i < sparkCount; i++)
            {
                Color sparkCol = i % 3 == 0 ? gold : (i % 3 == 1 ? accent : ink);
                Image sp = AddImage(badge, "Spark" + i, sparkCol, CircleSprite());
                Anchor(sp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10f, 10f), Vector2.zero);
                sp.gameObject.SetActive(false);
                tierSparks[i] = sp;
            }

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
                TMP_Text c = AddText(strip, "c" + k, (k % 10).ToString(), 82, accent, TextAlignmentOptions.Center);
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
            Image track = AddImage(block, "Track", trackDim, RingSprite(0.70f));
            track.type = Image.Type.Filled; track.fillMethod = Image.FillMethod.Radial360; track.fillOrigin = (int)Image.Origin360.Left; track.fillClockwise = true; track.fillAmount = 0.5f;
            Anchor(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(gd, gd), hub);

            Image fill = AddImage(block, "Fill", accent, RingSprite(0.70f));
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

            // km/h number sits in the lower gap of the dial, just under the hub (v2: +18% for the glance)
            kmhText = AddText(block, "Kmh", "64", 54, ink, TextAlignmentOptions.Center);
            Anchor(kmhText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180f, 58f), new Vector2(0f, hub.y - 36f));
            TMP_Text unit = AddText(block, "Unit", "KM/H", 15, muted, TextAlignmentOptions.Center);
            Anchor(unit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180f, 18f), new Vector2(0f, hub.y - 68f));
            UiKit.Caps(unit, 0.2f);

            // Warning LEDs sit in soft dark wells so they read as recessed indicator lamps that light up, not
            // floating dots. The left lamp is the lane warning, the right the speeding warning (each also drives
            // text in the banner, so the cue is never colour-only).
            Image ledLeftWell = AddImage(block, "LED_Left_Well", new Color(0f, 0f, 0f, 0.45f), CircleSprite());
            Anchor(ledLeftWell.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 24f), new Vector2(14f, hub.y));
            ledLeft = AddImage(block, "LED_Left", ledOff, CircleSprite());
            Anchor(ledLeft.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(15f, 15f), new Vector2(14f, hub.y));
            Image ledRightWell = AddImage(block, "LED_Right_Well", new Color(0f, 0f, 0f, 0.45f), CircleSprite());
            Anchor(ledRightWell.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(24f, 24f), new Vector2(-14f, hub.y));
            ledRight = AddImage(block, "LED_Right", ledOff, CircleSprite());
            Anchor(ledRight.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(15f, 15f), new Vector2(-14f, hub.y));
        }

        private void BuildBattery(RectTransform parent, Vector2 pos)
        {
            RectTransform block = NewRect(parent, "Battery");
            Anchor(block, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70f, 150f), pos);

            // Battery silhouette: a little terminal cap on top, then a rounded body well the segments sit inside,
            // so the timer reads unmistakably as a draining battery (the segments are the charge bars).
            Image cap = AddImage(block, "Cap", muted, RoundedSprite(2));
            Anchor(cap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20f, 7f), new Vector2(0f, 70f));
            Image body = AddImage(block, "Body", Hex("#46342A"), RoundedSprite(8));
            Anchor(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(56f, 128f), new Vector2(0f, 0f));
            Image well = AddImage(block, "Well", new Color(0f, 0f, 0f, 0.55f), RoundedSprite(6));
            Anchor(well.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(48f, 120f), new Vector2(0f, 0f));

            int n = 10; batterySegments = new Image[n];
            float segH = 9f, gap = 4f; float start = -(n * (segH + gap)) * 0.5f + segH * 0.5f + 6f;
            for (int i = 0; i < n; i++)
            {
                Image seg = AddImage(block, "Seg" + i, ledOff, RoundedSprite(2));
                Anchor(seg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, segH), new Vector2(0f, start + i * (segH + gap)));
                batterySegments[i] = seg;
            }
            TMP_Text sub = AddText(block, "Sub", "MUDA", 15, muted, TextAlignmentOptions.Center);
            Anchor(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(90f, 18f), new Vector2(0f, 2f));
            UiKit.Caps(sub, 0.14f);
            batterySubLabel = sub; // relabelled "LEVENS" in endless (the battery becomes a lives gauge)
        }

        private void BuildLimit(RectTransform parent, Vector2 pos)
        {
            RectTransform block = NewRect(parent, "SpeedLimit");
            Anchor(block, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(110f, 150f), pos);

            // A proper European speed-sign: a white disc with a bold red rim, lifted off the console by a soft
            // shadow so it looks like a real roadside roundel mounted on the dash.
            Image discShadow = AddImage(block, "DiscShadow", new Color(0f, 0f, 0f, 0.4f), UiKit.SoftShadow(48));
            Anchor(discShadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(104f, 104f), new Vector2(0f, 2f));
            limitGlow = discShadow; // under danger the sign's soft shadow becomes a red glow (v2)
            Image disc = AddImage(block, "Disc", ink, CircleSprite());
            Anchor(disc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(96f, 96f), new Vector2(0f, 6f));
            Image ring = AddImage(block, "Ring", danger, RingSprite(0.80f));
            Anchor(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(96f, 96f), new Vector2(0f, 6f));
            limitRing = ring;
            limitText = AddText(block, "Number", "80", 48, panelColour, TextAlignmentOptions.Center);
            Anchor(limitText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90f, 58f), new Vector2(0f, 6f));

            TMP_Text sub = AddText(block, "Sub", "MAX", 15, muted, TextAlignmentOptions.Center);
            Anchor(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(110f, 18f), new Vector2(0f, 2f));
            UiKit.Caps(sub, 0.2f);
        }

        // Rule-warning banner — floats just above the cluster, centred over the speedometer.
        // v2 (screen 07): a filled danger icon block leads, the rule sits on line 1 and the FIX on line 2
        // ("← Blijf links"), and a soft ring around the pill pulses instead of washing the whole banner.
        private void BuildWarning(RectTransform root)
        {
            RectTransform banner = NewRect(root, "RuleWarning");
            banner.anchorMin = new Vector2(0.5f, 0f); banner.anchorMax = new Vector2(0.5f, 0f); banner.pivot = new Vector2(0.5f, 0f);
            banner.sizeDelta = new Vector2(560f, 92f);
            banner.anchoredPosition = new Vector2(0f, panelSize.y + 18f); // just above the panel, over the gauge

            // Pulsing ring: a slightly larger rounded rect BEHIND the solid pill — only its 5px rim shows,
            // which reads as the design's warnpulse halo without covering the text.
            Image ringPulse = AddImage(banner, "Ring", UiKit.WithAlpha(danger, 0.45f), RoundedSprite(26));
            ringPulse.rectTransform.anchorMin = Vector2.zero; ringPulse.rectTransform.anchorMax = Vector2.one;
            ringPulse.rectTransform.offsetMin = new Vector2(-5f, -5f); ringPulse.rectTransform.offsetMax = new Vector2(5f, 5f);
            warningRing = ringPulse;

            // A dark solid backing pill keeps the text crisp at arm's length regardless of the pulse.
            Image backing = AddImage(banner, "Backing", new Color(panelColour.r, panelColour.g, panelColour.b, 0.94f), RoundedSprite(24));
            Stretch(backing.rectTransform);

            // The filled icon block: rounded danger square with the warning triangle punched in warm white —
            // shape + text, never colour alone (colour-blind safety).
            Image block = AddImage(banner, "IconBlock", danger, RoundedSprite(14));
            Anchor(block.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62f, 62f), new Vector2(46f, 0f));
            warningIcon = block;
            Image glyph = AddImage(block.rectTransform, "Glyph", ink, WarnTriangleSprite());
            Anchor(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(38f, 36f), Vector2.zero);

            warningText = AddText(banner, "Rule", "VERKEERDE WEGHELFT", 28, ink, TextAlignmentOptions.Left);
            warningText.rectTransform.anchorMin = new Vector2(0f, 0.5f); warningText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            warningText.rectTransform.pivot = new Vector2(0f, 0.5f);
            warningText.rectTransform.offsetMin = new Vector2(96f, 4f); warningText.rectTransform.offsetMax = new Vector2(-20f, 40f);
            UiKit.Caps(warningText, 0.04f);

            warningSub = AddText(banner, "Fix", "← BLIJF LINKS", 19, Hex("#FFD9CF"), TextAlignmentOptions.Left);
            warningSub.rectTransform.anchorMin = new Vector2(0f, 0.5f); warningSub.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            warningSub.rectTransform.pivot = new Vector2(0f, 0.5f);
            warningSub.rectTransform.offsetMin = new Vector2(96f, -38f); warningSub.rectTransform.offsetMax = new Vector2(-20f, -4f);
            UiKit.Caps(warningSub, 0.10f);

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

        private Sprite _heart;
        // A filled heart (lobes up, point down) from the classic implicit curve (x²+y²−1)³ − x²y³ ≤ 0,
        // supersampled 2×2 for a soft edge — same no-art-asset approach as the other cluster sprites.
        private Sprite HeartSprite()
        {
            if (_heart != null) return _heart;
            int s = 64; var tex = NewTex(s, s);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < 2; sy++) for (int sx = 0; sx < 2; sx++)
                {
                    float u = (x + 0.25f + sx * 0.5f) / s * 2.9f - 1.45f;
                    float v = (y + 0.25f + sy * 0.5f) / s * 2.9f - 1.35f;
                    float a = u * u + v * v - 1f;
                    if (a * a * a - u * u * v * v * v < 0f) inside++;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, inside / 4f));
            }
            tex.Apply(); _heart = ToSprite(tex); return _heart;
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
                // t = distance remapped over [0.65, 1.15]: Mathf.SmoothStep(from,to,t) is a smoothed LERP whose
                // FROM/TO are output values — the old SmoothStep(0.65, 1.15, d) returned ≥0.65 at the screen
                // CENTRE, tinting the whole view red on a crash instead of just the edges (2026-07-14).
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, (d - 0.65f) / 0.5f)));
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

        // A warning triangle (rounded look via AA), with a punched-out exclamation, so the rule banner carries a
        // shape cue as well as text — colour is never the only signal.
        private Sprite _warnTri;
        private Sprite WarnTriangleSprite()
        {
            if (_warnTri != null) return _warnTri;
            int s = 64; var tex = NewTex(s, s);
            Vector2 apex = new(0.5f, 0.92f), b1 = new(0.08f, 0.12f), b2 = new(0.92f, 0.12f);
            Vector2 ai = new(0.5f, 0.78f), i1 = new(0.22f, 0.20f), i2 = new(0.78f, 0.20f); // inner triangle (the cut-out border)
            Vector2[] outer = { apex, b1, b2 };
            Vector2[] inner = { ai, i1, i2 };
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                var p = new Vector2((float)x / s, (float)y / s);
                bool onBorder = PointInPoly(p, outer) && !PointInPoly(p, inner);
                // the exclamation: a stem and a dot near the lower-centre of the triangle
                bool stem = p.x > 0.46f && p.x < 0.54f && p.y > 0.38f && p.y < 0.66f;
                bool dot = (new Vector2(p.x - 0.5f, p.y - 0.30f)).sqrMagnitude < 0.0026f;
                tex.SetPixel(x, y, new Color(1, 1, 1, (onBorder || stem || dot) ? 1f : 0f));
            }
            tex.Apply(); _warnTri = ToSprite(tex); return _warnTri;
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
