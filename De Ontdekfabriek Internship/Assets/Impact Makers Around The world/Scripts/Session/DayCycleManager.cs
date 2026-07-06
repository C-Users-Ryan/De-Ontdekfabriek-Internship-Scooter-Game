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

        // The phase blend changes slowly, so we recompute the TARGET look ~15×/s instead of every frame. The applied
        // look then eases toward that target every frame (EaseAndApply); the sun arcs at ~1.5°/s, far below what the
        // eye resolves at this cadence.
        private const float EvalInterval = 1f / 15f;
        private float evalAccum;

        // The applied look eases toward the target every frame (EaseAndApply) so slow day changes track with no lag.
        // The turn-to-turn reset from night back to morning is the "sudden brightening" — that is handled separately:
        // it is snapped to morning BEHIND the relay hand-off screen (OnCheckpointReached) and HELD there, so the
        // tap-to-start screen and the next turn both open at dawn and the player never watches night brighten in view.
        [Tooltip("Seconds the look takes to glide for in-play changes. The turn reset is hidden behind the hand-off screen, not eased.")]
        [SerializeField] private float transitionSmoothing = 0.6f;
        private bool targetValid, hasApplied, holdMorning;
        private Quaternion tSunRot, aSunRot;
        private Color tSunCol, aSunCol, tFog, aFog, tSkyTint, aSkyTint, tGround, aGround;
        private float tSunInt, aSunInt, tAtmo, aAtmo, tExp, aExp, tNight;

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

        private void OnEnable()
        {
            GameEvents.SessionReset += HandleSessionReset;
            GameEvents.CheckpointReached += OnCheckpointReached;
        }

        private void OnDisable()
        {
            GameEvents.SessionReset -= HandleSessionReset;
            GameEvents.CheckpointReached -= OnCheckpointReached;
        }

        /// <summary>The turn is over (bike at the charge station) and the relay hand-off screen is taking the world.
        /// Snap the day to morning NOW, behind that screen, and HOLD it there until the next turn starts — so the
        /// tap-to-start screen and the next turn both open at dawn and the player never watches the night brighten
        /// back to morning in view. Snapped directly (not via Update) so it lands even if the state leaves
        /// Playing/AtCheckpoint immediately after.</summary>
        private void OnCheckpointReached()
        {
            if (config == null || config.phases.Length == 0 || !config.cycleEnabled)
                return;
            holdMorning = true;
            SnapToPhase(0);
        }

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

            // Recompute the TARGET look at the eval cadence (cheap). The phase blend is what actually moves.
            evalAccum += Time.deltaTime;
            if (!targetValid || evalAccum >= EvalInterval)
            {
                evalAccum = 0f;
                targetValid = true;
                ComputeTarget();
            }

            // Glide the applied look toward the target EVERY frame: slow day changes track exactly, but a sudden jump
            // (the turn-to-turn reset from night back to morning) eases over transitionSmoothing so it reads as a
            // gentle dawn instead of a flash.
            EaseAndApply(Time.deltaTime);
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

        /// <summary>Recomputes the TARGET look for the current moment on the timeline (the phase blend). Stored, not
        /// written to the scene directly — EaseAndApply glides the actual look toward it so the turn reset doesn't flash.</summary>
        private void ComputeTarget()
        {
            float elapsed = holdMorning ? 0f
                : config.cycleEnabled ? TimerManager.Instance.Elapsed
                : config.phases[Mathf.Clamp(config.fixedPhaseIndex, 0, config.phases.Length - 1)].startTime;

            // Fit the authored arc to the facilitator's actual session length.
            if (config.cycleEnabled && config.scaleToSessionLength)
            {
                float duration = TimerManager.Instance.Duration;
                if (duration > 0.01f && config.referenceSessionSeconds > 0.01f)
                    elapsed *= config.referenceSessionSeconds / duration;
            }

            ResolveBlend(elapsed, out int lo, out int hi, out float frac);
            float ease = Mathf.SmoothStep(0f, 1f, frac);

            if (lo != CurrentPhase)
            {
                CurrentPhase = lo;
                GameEvents.RaiseDayPhaseChanged(lo, config.phases[lo].label);
                if (skyDriven)
                    DynamicGI.UpdateEnvironment(); // refresh skybox-derived ambient on the (few) phase changes only
            }

            DayCycleConfig.Phase a = config.phases[lo];
            DayCycleConfig.Phase b = config.phases[hi];

            tNight = Mathf.Lerp(a.artificialLight, b.artificialLight, ease);
            tSunRot = Quaternion.Slerp(Quaternion.Euler(a.sunEuler), Quaternion.Euler(b.sunEuler), ease);
            tSunCol = Color.Lerp(a.sunColour, b.sunColour, ease);
            tSunInt = Mathf.Lerp(a.sunIntensity, b.sunIntensity, ease);
            tFog = Color.Lerp(a.fogColour, b.fogColour, ease);
            tSkyTint = Color.Lerp(a.skyTint, b.skyTint, ease);
            tGround = Color.Lerp(a.groundColour, b.groundColour, ease);
            tAtmo = Mathf.Lerp(a.atmosphereThickness, b.atmosphereThickness, ease);
            tExp = Mathf.Lerp(a.skyExposure, b.skyExposure, ease);

            int volumeCount = phaseVolumes == null ? 0 : Mathf.Min(phaseVolumes.Length, config.phases.Length);
            for (int i = 0; i < volumeCount; i++)
            {
                if (phaseVolumes[i] == null)
                    continue;
                phaseVolumes[i].weight = i == lo ? 1f - ease : i == hi ? ease : 0f;
            }
        }

        /// <summary>Eases the applied sun/fog/sky toward the target and writes it. The first apply snaps (no dawn fade
        /// on boot); after that a time-constant ease smooths any jump (the turn reset) while tracking the slow day arc
        /// with no visible lag. NightFactor01 rides along, so the headlight/car glows ease with it.</summary>
        private void EaseAndApply(float dt)
        {
            float k = hasApplied ? 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, transitionSmoothing)) : 1f;
            hasApplied = true;

            aSunRot = Quaternion.Slerp(aSunRot, tSunRot, k);
            aSunCol = Color.Lerp(aSunCol, tSunCol, k);
            aSunInt = Mathf.Lerp(aSunInt, tSunInt, k);
            aFog = Color.Lerp(aFog, tFog, k);
            aSkyTint = Color.Lerp(aSkyTint, tSkyTint, k);
            aGround = Color.Lerp(aGround, tGround, k);
            aAtmo = Mathf.Lerp(aAtmo, tAtmo, k);
            aExp = Mathf.Lerp(aExp, tExp, k);
            NightFactor01 = Mathf.Lerp(NightFactor01, tNight, k);

            if (sun != null)
            {
                sun.transform.rotation = aSunRot;
                sun.color = aSunCol;
                sun.intensity = aSunInt;
            }
            RenderSettings.fogColor = aFog;
            if (skyDriven && skybox != null)
            {
                skybox.SetColor(SkyTintId, aSkyTint);
                skybox.SetColor(GroundColourId, aGround);
                skybox.SetFloat(AtmosphereId, aAtmo);
                skybox.SetFloat(SkyExposureId, aExp);
            }
        }

        /// <summary>Instantly sets the applied look to a phase and writes it to the scene, bypassing the ease — used
        /// to reset the day to morning behind the hand-off screen, so there is no visible glide.</summary>
        private void SnapToPhase(int idx)
        {
            idx = Mathf.Clamp(idx, 0, config.phases.Length - 1);
            DayCycleConfig.Phase p = config.phases[idx];
            aSunRot = Quaternion.Euler(p.sunEuler);
            aSunCol = p.sunColour;
            aSunInt = p.sunIntensity;
            aFog = p.fogColour;
            aSkyTint = p.skyTint;
            aGround = p.groundColour;
            aAtmo = p.atmosphereThickness;
            aExp = p.skyExposure;
            NightFactor01 = p.artificialLight;
            hasApplied = true;
            targetValid = false; // ComputeTarget re-runs next update (reads morning while holdMorning)
            CurrentPhase = idx;

            EnsureSkybox();
            if (sun != null)
            {
                sun.transform.rotation = aSunRot;
                sun.color = aSunCol;
                sun.intensity = aSunInt;
            }
            RenderSettings.fogColor = aFog;
            if (skyDriven && skybox != null)
            {
                skybox.SetColor(SkyTintId, aSkyTint);
                skybox.SetColor(GroundColourId, aGround);
                skybox.SetFloat(AtmosphereId, aAtmo);
                skybox.SetFloat(SkyExposureId, aExp);
                DynamicGI.UpdateEnvironment();
            }
        }

        private void HandleSessionReset()
        {
            if (config == null || config.phases.Length == 0)
                return;

            // The new turn's timer is now 0, so the day reads morning on its own. The look is ALREADY at morning if a
            // checkpoint preceded this (OnCheckpointReached snapped it behind the hand-off screen) — so clearing the
            // hold here leaves nothing to brighten in view. On the rare no-checkpoint restart, snap morning too so the
            // turn opens at dawn rather than easing up from the frozen night.
            if (!holdMorning)
                SnapToPhase(config.cycleEnabled ? 0 : Mathf.Clamp(config.fixedPhaseIndex, 0, config.phases.Length - 1));
            holdMorning = false;
            targetValid = false;
            evalAccum = 0f;
        }
    }
}
