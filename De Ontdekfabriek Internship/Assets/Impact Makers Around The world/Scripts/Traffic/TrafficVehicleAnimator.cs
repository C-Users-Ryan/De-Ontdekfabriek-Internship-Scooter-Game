using UnityEngine;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// Procedural "game feel" for a traffic vehicle — the secondary motion that turns a sliding block into a car with
    /// weight, with no art or keyframe animation. Driven entirely by the motion the vehicle already has:
    ///   • <b>Lean</b> — the body rolls (weight transfer) when it changes lane / dodges / overtakes.
    ///   • <b>Dive &amp; squat</b> — it pitches nose-down when braking and tail-down when accelerating.
    ///   • <b>Engine bob</b> — a small vertical buzz that scales with speed.
    ///   • <b>Idle shake</b> — a stopped car (verge stop / breakdown) trembles like a running engine.
    ///   • <b>Wheel spin</b> — optional wheel transforms roll at the right rate for the speed.
    ///
    /// Self-contained companion to <see cref="TrafficEngine"/> / <see cref="TrafficVehicleLights"/>: it only READS the
    /// sibling <see cref="TrafficVehicle"/>'s public motion (speed + lateral) and animates a BODY child — never the
    /// root, which TrafficVehicle drives along the road — so it needs no change to the traffic core and is null-safe
    /// per part (no body assigned = it only spins wheels; no wheels = it only tilts the body; neither = harmless).
    ///
    /// Setup: the vehicle's mesh should sit on a CHILD of the root (so the root can follow the road while the child
    /// tilts). Assign that child as <see cref="body"/> (or it auto-finds the first child renderer); optionally assign
    /// the wheel transforms. Mirrors how the player scooter separates movement (PlayerController) from lean (ScooterLean).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrafficVehicleAnimator : MonoBehaviour
    {
        [Header("What to animate")]
        [Tooltip("The visual body to tilt/bob — a CHILD of the vehicle root (the root is driven along the road, so tilting it would fight the movement). Auto-finds the first child renderer if left empty.")]
        [SerializeField] private Transform body;

        [Header("Lean into a swerve (roll / weight transfer)")]
        [Tooltip("Degrees of body roll per m/s of sideways motion. Flip the sign if it leans the wrong way for your model.")]
        [SerializeField] private float leanPerLateral = 6f;
        [SerializeField] private float maxLean = 10f;
        [SerializeField] private float leanLerp = 8f;

        [Header("Dive / squat (pitch under braking & acceleration)")]
        [Tooltip("Degrees of pitch per m/s² of speed change — braking noses down, accelerating squats the tail. Flip the sign if reversed.")]
        [SerializeField] private float pitchPerAccel = 1.2f;
        [SerializeField] private float maxPitch = 6f;
        [SerializeField] private float pitchLerp = 6f;

        [Header("Engine bob & idle shake")]
        [Tooltip("Vertical bob (metres) while moving — the engine/road buzz. 0 = off.")]
        [SerializeField] private float bobAmplitude = 0.015f;
        [SerializeField] private float bobFrequency = 12f;
        [Tooltip("Extra tremble (metres) when stopped — a parked/stalled engine idling. 0 = off.")]
        [SerializeField] private float idleShake = 0.01f;
        [Tooltip("Road-roughness wobble (metres) while moving — the car jitters over the rough/dirt road, stronger the faster it drives. 0 = off.")]
        [SerializeField] private float roughness = 0.012f;

        [Header("Per-spawn variety (so cars aren't identical clones)")]
        [Tooltip("Random body size variation per spawn (± fraction). 0 = off.")]
        [SerializeField] private float scaleVariation = 0.08f;
        [Tooltip("Random variation in engine-bob strength per spawn, so idles differ slightly. 0 = off.")]
        [SerializeField] private float bobVariation = 0.25f;
        [Tooltip("Random per-spawn colour shift (± brightness + small hue jitter) so same-prefab cars aren't the same colour. 0 = off.")]
        [SerializeField] private float tintVariation = 0.12f;

        [Header("Roof load (matatu luggage — optional)")]
        [Tooltip("Optional strapped-luggage child that CHASES the body with lag and overshoot (a 1-DOF pendulum) — " +
                 "the lag is what makes roof luggage read as heavy (Levend Kenia §4.7). Auto-finds a child named " +
                 "'RoofLoad'; none found = off, harmless.")]
        [SerializeField] private Transform roofLoad;
        [Tooltip("Roof-load pendulum stiffness — softer than the body suspension, so it answers late.")]
        [SerializeField] private float loadStiffness = 26f;
        [Tooltip("Roof-load damping — light, so it overshoots before settling.")]
        [SerializeField] private float loadDamping = 3.6f;

        [Header("Wheels (optional)")]
        [SerializeField] private Transform[] wheels;
        [Tooltip("Wheel radius (m) — sets how fast the wheels spin for the speed.")]
        [SerializeField] private float wheelRadius = 0.35f;
        [Tooltip("Local axis the wheels spin around (X for a typical wheel).")]
        [SerializeField] private Vector3 wheelSpinAxis = Vector3.right;

        private TrafficVehicle vehicle;
        private Vector3 baseLocalPos;
        private Vector3 baseLocalScale = Vector3.one;
        private Quaternion baseLocalRot;
        private bool hasBody;
        private float prevSpeed, prevLateral;
        private float lean, pitch, wheelAngle;
        private float phase;          // per-spawn time offset — desyncs the bob/idle so cars don't pulse together
        private float bobScale = 1f;  // per-spawn engine-bob strength
        private float personalityAmp = 1f; // suspension amplitude by driver archetype: bold bounce, careful glide
        private float loadAngle, loadVel;  // the roof-load pendulum's state (degrees, deg/s)
        private Quaternion baseLoadRot;
        private bool hasLoad;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private Renderer tintRenderer;
        private MaterialPropertyBlock tintBlock;

        private void Awake()
        {
            vehicle = GetComponent<TrafficVehicle>();
            if (body == null)
            {
                // Prefer a child that actually has a renderer (skips e.g. the TrafficEngine audio child); never the root.
                Renderer r = GetComponentInChildren<Renderer>();
                if (r != null && r.transform != transform) body = r.transform;
            }
            hasBody = body != null;
            if (hasBody) { baseLocalPos = body.localPosition; baseLocalRot = body.localRotation; baseLocalScale = body.localScale; }

            if (roofLoad == null)
                roofLoad = FindChildNamed(transform, "RoofLoad");
            hasLoad = roofLoad != null;
            if (hasLoad) baseLoadRot = roofLoad.localRotation;

            tintRenderer = body != null ? body.GetComponent<Renderer>() : null;
            if (tintRenderer == null) tintRenderer = GetComponentInChildren<Renderer>();
            tintBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            prevSpeed = vehicle != null ? vehicle.CurrentSpeed : 0f;
            prevLateral = vehicle != null ? vehicle.RoadLateral : 0f;
            lean = pitch = 0f;
            loadAngle = loadVel = 0f;
            if (hasLoad) roofLoad.localRotation = baseLoadRot;
            phase = Random.value * 100f;                       // desync the bob/idle between cars
            bobScale = 1f + Random.Range(-bobVariation, bobVariation);

            // Suspension amplitude reads the driver archetype (Levend Kenia §4.7): bold drivers bounce,
            // careful ones glide — so the traffic personalities become VISIBLE without a line of new AI.
            switch (vehicle != null ? vehicle.Personality : DriverPersonality.Normal)
            {
                case DriverPersonality.Aggressive: personalityAmp = 1.35f; break;
                case DriverPersonality.Cautious: personalityAmp = 0.7f; break;
                case DriverPersonality.Distracted: personalityAmp = 1.15f; break;
                default: personalityAmp = 1f; break;
            }
            if (hasBody)
            {
                body.localPosition = baseLocalPos;
                body.localRotation = baseLocalRot;
                body.localScale = baseLocalScale * (1f + Random.Range(-scaleVariation, scaleVariation)); // per-spawn size variety
            }
            ApplyTint();
        }

        // Per-spawn colour shift via MaterialPropertyBlock (no material instancing): reads the material's base colour,
        // nudges its brightness and hue a little, and writes it back — so two of the same prefab aren't the same colour.
        // Uses GetPropertyBlock first so it composes with the near-miss highlight (which also drives an MPB).
        private void ApplyTint()
        {
            if (tintRenderer == null || tintVariation <= 0f) return;
            Color baseCol = Color.white;
            Material sm = tintRenderer.sharedMaterial;
            if (sm != null)
            {
                if (sm.HasProperty(BaseColorId)) baseCol = sm.GetColor(BaseColorId);
                else if (sm.HasProperty(ColorId)) baseCol = sm.GetColor(ColorId);
            }
            Color.RGBToHSV(baseCol, out float h, out float s, out float v);
            h = Mathf.Repeat(h + Random.Range(-0.03f, 0.03f), 1f);
            v = Mathf.Clamp01(v * (1f + Random.Range(-tintVariation, tintVariation)));
            Color tinted = Color.HSVToRGB(h, s, v);
            tinted.a = baseCol.a;
            tintRenderer.GetPropertyBlock(tintBlock);
            tintBlock.SetColor(BaseColorId, tinted);
            tintBlock.SetColor(ColorId, tinted);
            tintRenderer.SetPropertyBlock(tintBlock);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float speed = vehicle != null ? vehicle.CurrentSpeed : 0f;

            if (hasBody && vehicle != null)
            {
                float lateralRate = (vehicle.RoadLateral - prevLateral) / dt;
                prevLateral = vehicle.RoadLateral;
                float accel = (speed - prevSpeed) / dt;
                prevSpeed = speed;

                float targetLean = Mathf.Clamp(-lateralRate * leanPerLateral * personalityAmp, -maxLean, maxLean); // roll out of the slide (weight transfer)
                float targetPitch = Mathf.Clamp(-accel * pitchPerAccel * personalityAmp, -maxPitch, maxPitch);     // brake = nose down, accelerate = tail down
                lean = Mathf.Lerp(lean, targetLean, 1f - Mathf.Exp(-leanLerp * dt));
                pitch = Mathf.Lerp(pitch, targetPitch, 1f - Mathf.Exp(-pitchLerp * dt));

                float bob;
                if (speed > 0.3f)
                {
                    bob = Mathf.Sin((Time.time + phase) * bobFrequency) * bobAmplitude * bobScale * personalityAmp * Mathf.Clamp01(speed / 6f);
                    if (roughness > 0f) // jitter over the rough/dirt road, stronger the faster it drives
                        bob += (Mathf.PerlinNoise((Time.time + phase) * 7f, phase + 3.3f) - 0.5f) * 2f * roughness * personalityAmp * Mathf.Clamp01(speed / 6f);
                }
                else
                    bob = idleShake > 0f ? (Mathf.PerlinNoise((Time.time + phase) * 25f, phase) - 0.5f) * 2f * idleShake : 0f;

                body.localRotation = baseLocalRot * Quaternion.Euler(pitch, 0f, lean);
                body.localPosition = baseLocalPos + new Vector3(0f, bob, 0f);

                // The roof load chases the body with lag and overshoot — a 1-DOF pendulum, four floats, no
                // physics. The delay is what makes strapped luggage read as heavy (Levend Kenia §4.7).
                if (hasLoad)
                {
                    float target = Mathf.Clamp(lean * 0.6f + bob * 120f, -8f, 8f);
                    loadVel += ((target - loadAngle) * loadStiffness - loadVel * loadDamping) * dt;
                    loadAngle += loadVel * dt;
                    roofLoad.localRotation = baseLoadRot * Quaternion.Euler(0f, 0f, Mathf.Clamp(loadAngle, -10f, 10f));
                }
            }

            SpinWheels(speed, dt);
        }

        private static Transform FindChildNamed(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == name)
                    return t;
            return null;
        }

        private void SpinWheels(float speed, float dt)
        {
            if (wheels == null || wheels.Length == 0) return;
            float delta = (wheelRadius > 0.01f ? speed / wheelRadius : 0f) * Mathf.Rad2Deg * dt;
            if (delta == 0f) return; // stopped (verge stop / breakdown / pool): wheels hold their pose, skip the transform writes
            wheelAngle += delta;
            Quaternion spin = Quaternion.AngleAxis(wheelAngle, wheelSpinAxis);
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) wheels[i].localRotation = spin;
        }
    }
}
