using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// Vehicle lighting behaviour — the readable, teachable layer on top of the traffic AI (Req §5, §10).
    /// It gives each car the signals a real road has, which is both atmosphere and road-safety teaching:
    ///   • <b>Brake lights</b> glow when the vehicle slows or stops (yielding, car-following, a verge stop) — so
    ///     the player learns to read "the car ahead is braking" instead of only reacting to a collision.
    ///   • <b>Tail / running lights</b> sit dim, and <b>headlights</b> come on, at dusk and in the evening
    ///     (driven by the day cycle, M25) — the road reads differently late in a run.
    ///   • <b>Turn indicators</b> blink toward a lane change (inferred from the car's own lateral movement); a
    ///     broken-down vehicle blinks <b>both</b> as hazard flashers, telegraphing the obstacle ahead.
    ///
    /// It is a self-contained companion to <see cref="TrafficVehicle"/>/<see cref="TrafficHorn"/>/<see cref="TrafficEngine"/>:
    /// it only READS the vehicle's public state (speed, lateral, broken-down flag) and the day-phase event, so it adds
    /// no coupling to and needs no change in the traffic core. Lights are driven through a MaterialPropertyBlock on the
    /// assigned light renderers (same no-instancing approach as the near-miss highlight), so the light meshes need
    /// materials with Emission enabled. Every field is optional — unassigned renderers are simply skipped, so a prefab
    /// with no light meshes costs nothing and never errors.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrafficVehicleLights : MonoBehaviour
    {
        [Header("Rear lights (brake = bright, tail = dim at night)")]
        [Tooltip("Rear light renderers. Bright when braking; dim when it is dusk/night; off otherwise.")]
        [SerializeField] private Renderer[] rearLights;
        [SerializeField] private Color brakeColour = new Color(1f, 0.12f, 0.06f);
        [SerializeField] private Color tailColour = new Color(0.35f, 0.03f, 0.02f);
        [Tooltip("Deceleration (m/s²) above which the brake lights come on. Lower = more sensitive.")]
        [SerializeField] private float brakeDecel = 1.5f;
        [Tooltip("Brake lights also come on below this speed (m/s), so a stopped/crawling car reads as braking.")]
        [SerializeField] private float brakeCrawlSpeed = 0.4f;

        [Header("Headlights (on at dusk / evening)")]
        [SerializeField] private Renderer[] headlights;
        [SerializeField] private Color headlightColour = new Color(1f, 0.93f, 0.75f);
        [Tooltip("Day-cycle phase index at/after which it counts as 'night' for the lights (0 ASUBUHI dawn, " +
                 "1 MCHANA midday, 2 ALASIRI afternoon, 3 JIONI dusk, 4 MAGHARIBI blue hour, 5 USIKU night). " +
                 "Default 2 = on from the afternoon.")]
        [SerializeField] private int nightFromPhaseIndex = 2;

        [Header("Turn indicators (blink toward a lane change; both = hazards)")]
        [SerializeField] private Renderer leftIndicator;
        [SerializeField] private Renderer rightIndicator;
        [SerializeField] private Color indicatorColour = new Color(1f, 0.55f, 0.05f);
        [Tooltip("Blink rate (full on/off cycles per second).")]
        [SerializeField] private float blinkHz = 2.5f;
        [Tooltip("Lateral speed (m/s) the car must be moving sideways before its indicator on that side blinks. " +
                 "A small deadzone so normal lane-keeping wobble doesn't flicker the indicators.")]
        [SerializeField] private float indicatorLateralRate = 0.35f;
        [Tooltip("A broken-down / static-obstacle vehicle blinks BOTH indicators as hazard flashers.")]
        [SerializeField] private bool hazardsWhenBrokenDown = true;

        [Header("Emission")]
        [Tooltip("Multiplier on the light colours for an HDR glow (Bloom picks it up if present).")]
        [SerializeField] private float emissionBoost = 1.5f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color Off = Color.black;

        // Day phase is global; cache the last broadcast so a car spawned between phase changes lights correctly at once.
        private static int lastPhase = 1; // default midday (lights off)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => lastPhase = 1;

        private TrafficVehicle vehicle;
        private MaterialPropertyBlock mpb;
        private int phase;
        private float prevSpeed;
        private float prevLateral;
        // Last-applied colours: SetPropertyBlock has no built-in equality skip, so without these every vehicle
        // rewrites 4+ renderers' blocks every frame. Steady-state lights (cruising, no blink edge) now cost zero.
        private Color lastRear, lastLeft, lastRight;

        private void Awake()
        {
            vehicle = GetComponent<TrafficVehicle>();
            mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            GameEvents.DayPhaseChanged += OnDayPhase;
            phase = lastPhase;
            prevSpeed = vehicle != null ? vehicle.CurrentSpeed : 0f;
            prevLateral = vehicle != null ? vehicle.RoadLateral : 0f;
            ApplyHeadlights();
            // Start with rear lights and indicators off; Update drives them from here.
            SetEmission(rearLights, Off);
            SetEmission(leftIndicator, Off);
            SetEmission(rightIndicator, Off);
            lastRear = lastLeft = lastRight = Off;
        }

        private void OnDisable()
        {
            GameEvents.DayPhaseChanged -= OnDayPhase;
            // Pooled / recycled: make sure nothing glows while the car sits in the pool.
            SetEmission(rearLights, Off);
            SetEmission(headlights, Off);
            SetEmission(leftIndicator, Off);
            SetEmission(rightIndicator, Off);
        }

        private void OnDayPhase(int index, string label)
        {
            phase = index;
            lastPhase = index;
            ApplyHeadlights();
        }

        private bool IsNight => phase >= nightFromPhaseIndex;

        private void ApplyHeadlights() => SetEmission(headlights, IsNight ? headlightColour * emissionBoost : Off);

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            // ---- Rear lights: brake (bright) vs tail (dim at night) vs off --------------------------------
            float speed = vehicle != null ? vehicle.CurrentSpeed : 0f;
            float decel = (prevSpeed - speed) / dt; // positive = slowing down
            prevSpeed = speed;
            bool braking = decel > brakeDecel || speed < brakeCrawlSpeed;
            Color rear = braking ? brakeColour * emissionBoost
                       : IsNight ? tailColour * emissionBoost
                       : Off;
            if (rear != lastRear) { lastRear = rear; SetEmission(rearLights, rear); }

            // ---- Indicators: blink toward the side the car is moving; both for a breakdown -----------------
            float lateral = vehicle != null ? vehicle.RoadLateral : 0f;
            float lateralRate = (lateral - prevLateral) / dt; // +X = moving to the car's right
            prevLateral = lateral;

            bool hazards = hazardsWhenBrokenDown && vehicle != null && (vehicle.isStaticObstacle || vehicle.HazardFlashers);
            bool wantLeft = hazards || lateralRate < -indicatorLateralRate;
            bool wantRight = hazards || lateralRate > indicatorLateralRate;
            bool blinkOn = Mathf.Repeat(Time.time * blinkHz, 1f) < 0.5f;

            Color left = wantLeft && blinkOn ? indicatorColour * emissionBoost : Off;
            Color right = wantRight && blinkOn ? indicatorColour * emissionBoost : Off;
            if (left != lastLeft) { lastLeft = left; SetEmission(leftIndicator, left); }
            if (right != lastRight) { lastRight = right; SetEmission(rightIndicator, right); }
        }

        // ---- Emission helpers (MaterialPropertyBlock — no material instancing) ----------------------------
        private void SetEmission(Renderer[] renderers, Color colour)
        {
            if (renderers == null)
                return;
            for (int i = 0; i < renderers.Length; i++)
                SetEmission(renderers[i], colour);
        }

        private void SetEmission(Renderer renderer, Color colour)
        {
            if (renderer == null)
                return;
            renderer.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionId, colour);
            renderer.SetPropertyBlock(mpb);
        }
    }
}
