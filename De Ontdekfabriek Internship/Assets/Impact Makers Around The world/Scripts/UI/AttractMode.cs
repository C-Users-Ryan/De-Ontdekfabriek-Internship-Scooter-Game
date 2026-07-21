using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using KenyaScooter.Core;
using KenyaScooter.Config;
using KenyaScooter.Settings; // ConfigLocator (the live SessionConfig, for the facilitator-tunable session length)
using KenyaScooter.Player;
using KenyaScooter.Roads;
using KenyaScooter.Traffic;
using KenyaScooter.Hazards;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Arcade ATTRACT MODE for the kiosk: when the Ready screen sits untouched for a while, the game demos
    /// itself — a scripted autopilot cruises the scooter down the living road (traffic, dust, day cycle all
    /// running) under a pulsing "TIK OM TE STARTEN" banner, so an idle tablet draws the next group in instead
    /// of showing a static title. Any touch/key instantly ends the demo back to the title.
    ///
    /// Safety by construction:
    ///  - The demo is a REAL session started via GameManager.StartSession, but it always ends through
    ///    ForceReset(), which never commits a score — and it self-ends well before the 120 s timer, so the
    ///    checkpoint/finish commit path can never fire. The group score and journey cannot be polluted.
    ///  - Driving reuses PlayerController's existing scripted-pose mode (the charge-station API): the bike
    ///    cruises at base speed while this component steers the lateral target with a simple road-space
    ///    dodge brain (pull toward the oncoming lane to pass slower cars/obstacles, only when oncoming is clear,
    ///    and ease around hazards). During the demo the player is COLLISION-IMMUNE (DemoActive, honoured by
    ///    PlayerCollisionHandler), so a missed dodge simply glides through instead of hard-crashing and snapping
    ///    the kiosk back to the title after a few seconds — the demo keeps playing and keeps attracting.
    ///  - GameManager's own tap-to-start runs earlier in the frame (execution order -100) and only acts in
    ///    Ready, so the tap that ends the demo can never double as a game start.
    /// Self-bootstraps like the other kiosk helpers; disable by deleting the component or via idleSeconds = 0.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttractMode : MonoBehaviour
    {
        [Tooltip("Seconds the Ready screen must sit untouched before the demo starts. 0 = attract mode off.")]
        [SerializeField] private float idleSeconds = 60f;
        [Tooltip("How long one demo runs before returning to the title. Keep well under the 120 s session timer so the demo can never reach the checkpoint/commit path.")]
        [SerializeField] private float demoSeconds = 70f;
        [Tooltip("Look-ahead (m of road) for a slower car worth pulling out around.")]
        [SerializeField] private float passLookahead = 26f;
        [Tooltip("The oncoming lane must be clear this far ahead before the autopilot borrows it.")]
        [SerializeField] private float oncomingClear = 70f;

        /// <summary>Facilitator override (SettingsCatalog): -1 = use the serialized default; 0 = attract off; else seconds.</summary>
        public static float IdleSecondsOverride = -1f;

        private float IdleSeconds => IdleSecondsOverride >= 0f ? IdleSecondsOverride : idleSeconds;

        /// <summary>True only while the self-play demo is running. During the demo the player is collision-immune
        /// (<see cref="KenyaScooter.Player.PlayerCollisionHandler"/> checks this), so a missed dodge can never
        /// hard-crash the run and snap the kiosk back to the title — the demo can only ever end on its own timer
        /// or a real human tap. This is what makes the attract loop actually attract: it keeps playing.</summary>
        public static bool DemoActive { get; private set; }

        private float idleTimer;
        private float demoTimer;
        private float demoRunSeconds;   // effective demo length for THIS run — capped under the (facilitator-tunable) session length
        private bool demoRunning;
        private PlayerController player;
        private GameObject banner;
        private CanvasGroup bannerGroup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<AttractMode>() != null)
                return;
            var go = new GameObject("AttractMode (auto)");
            DontDestroyOnLoad(go);
            go.AddComponent<AttractMode>();
        }

        private void Update()
        {
            if (IdleSeconds <= 0f)
                return;

            bool anyInput = AnyHumanInput();

            if (demoRunning)
            {
                demoTimer += Time.deltaTime;
                if (anyInput || demoTimer >= demoRunSeconds || GameManager.State != GameState.Playing || Time.timeScale == 0f)
                    EndDemo();
                else
                    DriveAutopilot();
                return;
            }

            // Counting idle time on the Ready screen only (not mid-game, not while the facilitator menu is open).
            if (GameManager.State != GameState.Ready || Time.timeScale == 0f || anyInput)
            {
                idleTimer = 0f;
                return;
            }

            idleTimer += Time.deltaTime;
            if (idleTimer >= IdleSeconds)
                BeginDemo();
        }

        private static bool AnyHumanInput()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) return true;
            if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
            if (Gamepad.current != null && (Gamepad.current.buttonSouth.isPressed || Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.04f)) return true;
            return false;
        }

        private void BeginDemo()
        {
            if (GameManager.Instance == null)
                return;
            player = FindObjectOfType<PlayerController>();
            if (player == null)
                return;

            DemoActive = true;                              // collision-immune for the whole demo (see PlayerCollisionHandler)
            GameManager.Mode = GameMode.GroupRelay;         // the demo is always a normal timed run, never a leftover endless one
            GameManager.Instance.StartSession();            // real world, real traffic — the demo IS the game
            float lane = RoadSideConfig.Active != null ? RoadSideConfig.Active.OwnLaneCentre : 2f;
            player.BeginScriptedPose(lane, 0f, 2.5f, 120f); // cruise in-lane at base speed (throttle is scripted idle)

            // End the demo comfortably BEFORE the turn timer, using the LIVE session length (session.length is
            // facilitator-tunable down to 45 s), not the 120 s the serialized demoSeconds ceiling assumes — otherwise
            // a shortened timer could expire mid-demo and (with no checkpoint tile) commit a demo score. demoSeconds
            // stays the ceiling; the cap leaves a 10 s margin before the timer, floored so it can never go silly-short.
            float cap = ConfigLocator.Session != null ? ConfigLocator.Session.sessionSeconds - 10f : demoSeconds;
            demoRunSeconds = Mathf.Min(demoSeconds, Mathf.Max(10f, cap));
            demoRunning = true;
            demoTimer = 0f;
            ShowBanner(true);
        }

        private void EndDemo()
        {
            DemoActive = false;
            demoRunning = false;
            idleTimer = 0f;
            if (player != null) player.EndScriptedPose();
            if (GameManager.Instance != null) GameManager.Instance.ForceReset(); // abandon WITHOUT commit (Req §16 escape hatch)
            ShowBanner(false);
        }

        /// <summary>The demo brain: hold the own lane; when a clearly-slower car sits ahead in-lane and the oncoming
        /// lane is clear, pull to the oncoming lane to pass, then ease back. All in road space (RoadArc/RoadLateral),
        /// the same frame the traffic AI itself reasons in.</summary>
        private void DriveAutopilot()
        {
            if (player == null || RoadSideConfig.Active == null)
                return;
            float ownLane = RoadSideConfig.Active.OwnLaneCentre;
            float passLane = RoadSideConfig.Active.OncomingLaneCentre * 0.85f; // borrow the lane, not its far edge
            float playerArc = RoadSequencer.Instance != null ? RoadSequencer.Instance.PlayerArc : 0f;

            bool wantPass = false;
            bool oncomingBlocked = false;
            var vehicles = TrafficVehicle.Active;
            for (int i = 0; i < vehicles.Count; i++)
            {
                TrafficVehicle v = vehicles[i];
                float gap = v.RoadArc - playerArc;

                // Pull out around anything sitting ahead in our lane: a slower same-direction car, OR any static
                // obstacle (a parked wreck / rock) whatever way it faces. Both are things a real rider would pass.
                bool aheadInLane = gap > 1f && gap < passLookahead && Mathf.Abs(v.RoadLateral - ownLane) < 1.6f;
                if (aheadInLane && (v.Direction == LaneDirection.SameDirection || v.isStaticObstacle))
                    wantPass = true;

                // Never borrow the oncoming lane while a (moving) car is coming down it.
                if (v.Direction == LaneDirection.Oncoming && !v.isStaticObstacle && gap > -6f && gap < oncomingClear)
                    oncomingBlocked = true;
            }

            // Ease around a pothole / rock sitting ahead in our lane too. Purely for looks — the demo is
            // collision-immune — but weaving around hazards reads as skilled play and shows the world off.
            var hazards = Hazard.Active;
            for (int i = 0; i < hazards.Count; i++)
            {
                Hazard h = hazards[i];
                float gap = h.RoadArc - playerArc;
                if (gap > 1f && gap < passLookahead && Mathf.Abs(h.RoadLateral - ownLane) < 1.4f)
                    wantPass = true;
            }

            float targetLane = wantPass && !oncomingBlocked ? passLane : ownLane;
            player.BeginScriptedPose(targetLane, 0f, 2.5f, 120f); // idempotent: just retargets the scripted pose
        }

        // ---- the pulsing "TIK OM TE STARTEN" banner ----------------------------------------------------------
        private void ShowBanner(bool on)
        {
            if (banner == null && on)
                BuildBanner();
            if (banner != null)
                banner.SetActive(on);
        }

        private void LateUpdate()
        {
            if (bannerGroup != null && banner != null && banner.activeSelf)
                bannerGroup.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
        }

        private void BuildBanner()
        {
            var canvasGO = new GameObject("AttractBanner", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95; // above the HUD, below the facilitator menu (100)
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2048f, 1536f);

            banner = canvasGO;
            bannerGroup = canvasGO.AddComponent<CanvasGroup>();
            bannerGroup.interactable = false;
            bannerGroup.blocksRaycasts = false; // taps go straight through; ending the demo is input-polled

            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(canvasGO.transform, false);
            text.text = "TIK OM TE STARTEN";
            text.fontSize = 72f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.96f, 0.92f, 1f);
            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(1400f, 110f); rt.anchoredPosition = new Vector2(0f, 330f);
        }
    }
}
