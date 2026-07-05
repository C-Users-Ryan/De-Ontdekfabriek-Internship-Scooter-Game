using UnityEngine;
using UnityEngine.Rendering;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Settings;

namespace KenyaScooter.Session
{
    /// <summary>
    /// The day cycle (M25): phases defined in DayCycleConfig advance AUTOMATICALLY with session time,
    /// driving the sun's rotation, colour and intensity, the fog colour and the procedural skybox from a
    /// cool morning through to a starry night — the session's narrative arc (MDA A4). Raises
    /// DayPhaseChanged for the HUD label and audio.
    ///
    /// Runs with ZERO scene setup. It self-bootstraps after scene load (like WarmGrade / HorizonBackdrop /
    /// AmbientSoundscape) and resolves its config and the sun in code, so the cycle is decided purely by
    /// TIME, not by hand-placed objects. The whole day-to-night look rides on the sun, fog and sky, which
    /// is also LIGHTER on tablets than a stack of per-phase post-processing Volumes. Per-phase Volumes
    /// remain supported as OPTIONAL extra grading if a scene wires them into <see cref="phaseVolumes"/>,
    /// but nothing depends on them — leave it empty and the cycle still runs.
    /// </summary>
    public sealed class DayCycleManager : MonoBehaviour
    {
        [Tooltip("Optional. Left empty, the shared DayCycleConfig asset is found in code (or a default is built).")]
        [SerializeField] private DayCycleConfig config;
        [Tooltip("Optional extra grading only: one URP global Volume per config phase, same order. Leave empty and " +
                 "the cycle still runs automatically on the sun, fog and sky alone (and lighter on tablets).")]
        [SerializeField] private Volume[] phaseVolumes;
        [Tooltip("Optional. The sun (a directional light). Left empty it is resolved in code: RenderSettings.sun, " +
                 "then the scene's directional light, else one is created.")]
        [SerializeField] private Light sun;

        public int CurrentPhase { get; private set; } = -1;
        public string CurrentLabel =>
            config != null && CurrentPhase >= 0 && CurrentPhase < config.phases.Length ? config.phases[CurrentPhase].label : string.Empty;

        /// <summary>How dark it is right now, blended continuously between phases: 0 = broad daylight, 1 = full
        /// night (each phase's DayCycleConfig.Phase.artificialLight). Read by ScooterHeadlight (and anything else
        /// that wants to fade in at dusk) so artificial light tracks the SAME timeline as the sky. Static so a
        /// consumer needs no reference to the auto-spawned manager.</summary>
        public static float NightFactor01 { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => NightFactor01 = 0f;

        // Procedural-skybox driving (the SKY shifts per phase too). We mutate an INSTANCE of the scene skybox so the
        // shared asset is never written, and only engage if the skybox actually exposes the procedural properties,
        // so any other skybox (or the built-in default) is left exactly as authored.
        private Material skybox;
        private bool skyboxResolved;
        private bool skyDriven;
        private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
        private static readonly int GroundColourId = Shader.PropertyToID("_GroundColor");
        private static readonly int AtmosphereId = Shader.PropertyToID("_AtmosphereThickness");
        private static readonly int SkyExposureId = Shader.PropertyToID("_Exposure");

        // The atmosphere changes slowly, so we recompute it ~15×/s instead of every frame. The sun arcs at roughly
        // 1.5°/s over the session, well below what the eye resolves, so this cadence is invisible yet cuts the
        // per-frame cost to near zero on the tablet. On top of that we skip the writes whenever neither the phase nor
        // the blend moved enough to see (a paused game, or the day settled into night), so a static sky costs nothing.
        private const float EvalInterval = 1f / 15f;
        private float evalAccum;
        private int lastLo = -1;
        private float lastEase = -1f;

        /// <summary>Self-activates after scene load so the day cycle always runs automatically, with no manager to
        /// place or wire in the scene. Skips if a manager is already present (a hand-placed one still wins).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<DayCycleManager>() != null)
                return;
            new GameObject("Day Cycle (auto)").AddComponent<DayCycleManager>();
        }

        private void Awake() => Resolve();

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        /// <summary>Fills any reference left unwired, so the cycle runs with zero scene setup. Prefers the shared
        /// config asset (so the facilitator's Dag-nacht toggle keeps steering the SAME instance); only if none is
        /// loaded does it fall back to a code default that carries the full Kenyan phase arc.</summary>
        private void Resolve()
        {
            if (config == null)
                config = ConfigLocator.DayCycle;
            if (config == null)
                config = ScriptableObject.CreateInstance<DayCycleConfig>();
            if (sun == null)
                sun = ResolveSun();
        }

        /// <summary>Finds the sun without scene wiring: the lighting sun, else any directional light, else a fresh
        /// one so the sun arc still reads in a scene that ships none.</summary>
        private static Light ResolveSun()
        {
            if (RenderSettings.sun != null)
                return RenderSettings.sun;
            Light[] lights = FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional)
                    return lights[i];
            Light created = new GameObject("Sun (auto)").AddComponent<Light>();
            created.type = LightType.Directional;
            return created;
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (state != GameState.Playing && state != GameState.AtCheckpoint)
                return;
            if (config == null || config.phases.Length == 0 || TimerManager.Instance == null)
                return;

            EnsureSkybox();

            // Throttle: most frames just accumulate time and return (see EvalInterval above).
            evalAccum += Time.deltaTime;
            if (evalAccum < EvalInterval)
                return;
            evalAccum = 0f;

            // Where are we on the authored timeline? lo = the phase we are in, hi = the next one, frac = progress
            // between them, measured against the REAL time each phase spans — so the fast equatorial dusk stays fast.
            float elapsed = config.cycleEnabled
                ? TimerManager.Instance.Elapsed
                : config.phases[Mathf.Clamp(config.fixedPhaseIndex, 0, config.phases.Length - 1)].startTime;

            // Fit the authored arc to the facilitator's actual session length: convert real elapsed into the
            // reference timeline the start times were authored on, so the full day always spans the whole run.
            if (config.cycleEnabled && config.scaleToSessionLength)
            {
                float duration = TimerManager.Instance.Duration;
                if (duration > 0.01f && config.referenceSessionSeconds > 0.01f)
                    elapsed *= config.referenceSessionSeconds / duration;
            }

            ResolveBlend(elapsed, out int lo, out int hi, out float frac);
            float ease = Mathf.SmoothStep(0f, 1f, frac); // ease the colour/light glide in and out of each phase

            if (lo != CurrentPhase)
            {
                CurrentPhase = lo;
                GameEvents.RaiseDayPhaseChanged(lo, config.phases[lo].label);
                // Refresh skybox-derived ambient on the (few) phase changes, not every frame.
                if (skyDriven)
                    DynamicGI.UpdateEnvironment();
            }

            // Nothing moved enough to be visible: leave the sun, fog, sky and volumes exactly as they are.
            if (lo == lastLo && Mathf.Abs(ease - lastEase) < 0.004f)
                return;
            lastLo = lo;
            lastEase = ease;

            ApplyBlend(lo, hi, ease);
        }

        /// <summary>Resolves the scene skybox once. If it is a Skybox/Procedural material (has the tint + atmosphere
        /// properties), we swap in an instance so per-phase edits never write the shared asset; otherwise sky driving
        /// stays off and the skybox is untouched.</summary>
        private void EnsureSkybox()
        {
            if (skyboxResolved)
                return;
            skyboxResolved = true;
            if (!config.driveSkybox)
                return;
            Material shared = RenderSettings.skybox;
            if (shared != null && shared.HasProperty(SkyTintId) && shared.HasProperty(AtmosphereId))
            {
                // A procedural sky is already assigned: instance it so per-phase edits never write the asset.
                skybox = new Material(shared);
            }
            else
            {
                // No procedural sky (the scene ships the default blue skybox): build one at runtime so the warm
                // Kenyan sky and its ambient show by default. Reversible via driveSkybox. Skybox/Procedural is
                // a built-in shader, so this needs no project asset.
                Shader proc = Shader.Find("Skybox/Procedural");
                if (proc == null)
                    return; // cannot build one — leave the scene's sky untouched
                skybox = new Material(proc) { name = "Kenya Sky (runtime)" };
            }
            RenderSettings.skybox = skybox;
            skyDriven = true;
        }

        /// <summary>Finds the phase pair bracketing the current time: lo = the latest phase already begun, hi = the
        /// next phase, frac = 0..1 progress from lo's start to hi's start. Past the last phase it holds there.</summary>
        private void ResolveBlend(float elapsed, out int lo, out int hi, out float frac)
        {
            DayCycleConfig.Phase[] phases = config.phases;
            lo = 0;
            for (int i = 0; i < phases.Length; i++)
                if (phases[i].startTime <= elapsed)
                    lo = i;

            if (lo >= phases.Length - 1)
            {
                hi = lo;
                frac = 0f;
                return;
            }
            hi = lo + 1;
            float span = Mathf.Max(0.01f, phases[hi].startTime - phases[lo].startTime);
            frac = Mathf.Clamp01((elapsed - phases[lo].startTime) / span);
        }

        /// <summary>Writes the continuously interpolated look for the moment between phases lo and hi. This is the
        /// vibe upgrade over the old "snap toward the current phase then hold": the sun now genuinely ARCS and the sky
        /// keeps warming all through each phase, so the light feels alive and the equatorial dusk sweeps quickly to
        /// night. Cost is a handful of lerps, run at EvalInterval, so it stays cheap on the tablet.</summary>
        private void ApplyBlend(int lo, int hi, float ease)
        {
            DayCycleConfig.Phase a = config.phases[lo];
            DayCycleConfig.Phase b = config.phases[hi];

            // The artificial-light level rides the exact same blend as the sky, so the headlight fades in as it darkens.
            NightFactor01 = Mathf.Lerp(a.artificialLight, b.artificialLight, ease);

            int volumeCount = phaseVolumes == null ? 0 : Mathf.Min(phaseVolumes.Length, config.phases.Length);
            for (int i = 0; i < volumeCount; i++)
            {
                if (phaseVolumes[i] == null)
                    continue;
                // A true crossfade of the two active phases; every other phase Volume is off.
                phaseVolumes[i].weight = i == lo ? 1f - ease : i == hi ? ease : 0f;
            }

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(a.sunEuler), Quaternion.Euler(b.sunEuler), ease);
                sun.color = Color.Lerp(a.sunColour, b.sunColour, ease);
                sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, ease);
            }
            RenderSettings.fogColor = Color.Lerp(a.fogColour, b.fogColour, ease);

            if (skyDriven && skybox != null)
            {
                skybox.SetColor(SkyTintId, Color.Lerp(a.skyTint, b.skyTint, ease));
                skybox.SetColor(GroundColourId, Color.Lerp(a.groundColour, b.groundColour, ease));
                skybox.SetFloat(AtmosphereId, Mathf.Lerp(a.atmosphereThickness, b.atmosphereThickness, ease));
                skybox.SetFloat(SkyExposureId, Mathf.Lerp(a.skyExposure, b.skyExposure, ease));
            }
        }

        private void HandleSessionReset()
        {
            if (config == null || config.phases.Length == 0)
                return;

            // Start the turn at the cycle's first phase, or at the chosen fixed phase if the cycle is off.
            int start = config.cycleEnabled ? 0 : Mathf.Clamp(config.fixedPhaseIndex, 0, config.phases.Length - 1);
            CurrentPhase = start;
            evalAccum = 0f;
            lastLo = start;
            lastEase = 0f;
            DayCycleConfig.Phase phase = config.phases[start];
            NightFactor01 = phase.artificialLight;
            int volumeCount = phaseVolumes == null ? 0 : Mathf.Min(phaseVolumes.Length, config.phases.Length);
            for (int i = 0; i < volumeCount; i++)
                if (phaseVolumes[i] != null)
                    phaseVolumes[i].weight = i == start ? 1f : 0f;

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(phase.sunEuler);
                sun.color = phase.sunColour;
                sun.intensity = phase.sunIntensity;
            }
            RenderSettings.fogColor = phase.fogColour;

            EnsureSkybox();
            if (skyDriven && skybox != null)
            {
                skybox.SetColor(SkyTintId, phase.skyTint);
                skybox.SetColor(GroundColourId, phase.groundColour);
                skybox.SetFloat(AtmosphereId, phase.atmosphereThickness);
                skybox.SetFloat(SkyExposureId, phase.skyExposure);
                DynamicGI.UpdateEnvironment();
            }

            GameEvents.RaiseDayPhaseChanged(start, phase.label);
        }
    }
}
