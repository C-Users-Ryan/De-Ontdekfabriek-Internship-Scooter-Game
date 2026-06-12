using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace OvertakeGame
{
    // ══════════════════════════════════════════════════════════════════════════
    //  AudioManager  —  v2  (Sprint 8 sound design redesign)
    //
    //  LAYER STACK
    //  ───────────
    //  Looping / always-on:
    //    engine        Electric motor whine. Pitch + volume scale with speed via
    //                  AnimationCurves. Clip should be a high-frequency electric
    //                  motor hum — NOT a combustion engine sound.
    //    wind          Wind noise. Volume scales with speed. Near-silent at idle;
    //                  dominant above ~70% max speed.
    //    roadSurface   Tyre-on-road texture. Volume scales with speed + tile type.
    //                  Call OnRoadSurfaceChanged(float roughness 0–1) from
    //                  RoadTileRecycler when a new tile activates.
    //    ambience      Context-aware ambient layer. Crossfades between clips when
    //                  the road sequence context tag changes. Call OnZoneChanged().
    //    music         Background music. Ducks on crash. Fades in on restart.
    //
    //  One-shot SFX — pooled, no Instantiate/Destroy at runtime:
    //    Gameplay:  PlayOvertakeReward, OnCrash, PlayNearMiss, OnPotholeHit,
    //               PlayWrongLane
    //    Traffic:   PlayHorn  (3 variants, random pick + random pitch)
    //    Animals:   PlayHadadaCall, PlayGoatScatter, PlayElephant
    //    Atmosphere: PlayDustDevil
    //    Session:   OnSessionStart, OnSessionEnd
    //
    //  WIRING — what calls what
    //  ────────────────────────
    //  WorldSpeed.LateUpdate()           → AudioManager.I.OnSpeedChanged(t)
    //  RoadSequencer.OnSequenceStarted   → AudioManager.I.OnZoneChanged(tag)
    //  DayCycleManager.OnPhaseChanged    → AudioManager.I.OnTimeOfDayChanged(phase)
    //  RoadTileRecycler (on tile swap)   → AudioManager.I.OnRoadSurfaceChanged(roughness)
    //  TrafficHorn.OnTriggerEnter        → AudioManager.I.PlayHorn()
    //  RewardSystem.OnOvertakeCompleted  → AudioManager.I.PlayOvertakeReward()
    //  GameManager.OnPlayerHitTraffic    → AudioManager.I.OnCrash()
    //  GameManager.OnPlayerHitPothole    → AudioManager.I.OnPotholeHit()
    //  GameManager.OnPlayerWrongLane     → AudioManager.I.PlayWrongLane()
    //  GameManager.OnNearMiss            → AudioManager.I.PlayNearMiss()
    //  GameManager.StartGame             → AudioManager.I.OnSessionStart()
    //  GameManager.TriggerGameOver/End   → AudioManager.I.OnSessionEnd()
    //  WrongLaneDetector (on break)      → AudioManager.I.ResetOvertakeChain()
    //  OvertakeCollisionHandler          → AudioManager.I.ResetOvertakeChain()
    //
    //  SETUP
    //  ─────
    //  1. Attach to a persistent GameObject (DontDestroyOnLoad).
    //  2. Add 5 child AudioSources for the looping layers (engine, wind,
    //     roadSurface, ambience, music). Assign each in Inspector.
    //     All looping sources: Loop = true, Play On Awake = true.
    //  3. Assign AudioMixerGroups if using an AudioMixer (optional but
    //     recommended — lets you set master volume from settings).
    //  4. Assign all SFX AudioClip fields in Inspector.
    //  5. The SFX pool (sfxPoolSize child AudioSources) is created at Awake —
    //     no manual setup needed.
    //  6. For AnimationCurves: right-click any curve field → "Add Key" to shape
    //     the response. Defaults are set in Reset() below. Good starting shapes:
    //       enginePitchCurve  — fast rise 0→0.5, plateau, sharp rise 0.8→1
    //       windVolumeCurve   — flat near 0, then exponential rise after 0.4
    // ══════════════════════════════════════════════════════════════════════════

    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        // ── Looping layers ─────────────────────────────────────────────────
        [Header("Looping Layers")]
        [Tooltip("Electric motor whine. Use a high-frequency electric hum clip — not combustion.")]
        public AudioSource engine;
        [Tooltip("Wind noise. Scales with speed. Near-silent at idle.")]
        public AudioSource wind;
        [Tooltip("Tyre-on-road surface sound. Scales with speed and road roughness.")]
        public AudioSource roadSurface;
        [Tooltip("Context-aware ambient layer. Crossfades between clips on zone change.")]
        public AudioSource ambience;
        [Tooltip("Background music. Ducks during crash.")]
        public AudioSource music;

        // ── Ambient clips (context-switched) ───────────────────────────────
        [Header("Ambient Clips")]
        [Tooltip("Open savanna/highway: light wind, sparse distant birds.")]
        public AudioClip ambienceSavanna;
        [Tooltip("Township/market: human activity, distant vehicles, market sounds.")]
        public AudioClip ambienceTownship;
        [Tooltip("Wildlife zone: insects, quieter human activity, nature sounds.")]
        public AudioClip ambienceWildlife;
        [Tooltip("Highland: morning birds, fresh quality. Also used for ASUBUHI phase.")]
        public AudioClip ambienceHighland;

        // ── Speed → audio curves ────────────────────────────────────────────
        [Header("Speed Curves  (x = speed ratio 0–1, y = output)")]
        [Tooltip("Engine pitch vs speed. Shape: fast rise, plateau at mid, sharp at top.")]
        public AnimationCurve enginePitchCurve   = AnimationCurve.Linear(0f, 0.6f, 1f, 1.3f);
        [Tooltip("Engine volume vs speed.")]
        public AnimationCurve engineVolumeCurve  = AnimationCurve.Linear(0f, 0.2f, 1f, 0.7f);
        [Tooltip("Wind volume vs speed. Should be near-zero below 0.4, then rise steeply.")]
        public AnimationCurve windVolumeCurve    = AnimationCurve.Linear(0f, 0f,   1f, 0.65f);
        [Tooltip("Road surface volume vs speed.")]
        public AnimationCurve roadSurfaceCurve   = AnimationCurve.Linear(0f, 0.1f, 1f, 0.35f);

        // ── Music settings ─────────────────────────────────────────────────
        [Header("Music")]
        public float normalMusicVolume  = 0.35f;
        public float crashMusicVolume   = 0.08f;
        public float musicFadeDuration  = 0.5f;

        // ── Ambience crossfade ──────────────────────────────────────────────
        [Header("Ambience")]
        [Tooltip("Seconds to crossfade between ambient clips on zone change.")]
        public float ambienceCrossfadeDuration = 2.5f;

        // ── SFX pool ────────────────────────────────────────────────────────
        [Header("SFX Pool")]
        [Tooltip("Pre-allocated one-shot AudioSources. 8 handles all simultaneous SFX in this game.")]
        public int sfxPoolSize = 8;

        // ── SFX clips — traffic ─────────────────────────────────────────────
        [Header("SFX — Traffic")]
        [Tooltip("2–3 horn variants. Random pick + random pitch per play.")]
        public AudioClip[] hornClips;
        [SerializeField] private float hornVolume = 0.70f;

        // ── SFX clips — gameplay ────────────────────────────────────────────
        [Header("SFX — Gameplay")]
        [Tooltip("Overtake reward sting. Pitch rises per consecutive overtake in chain.")]
        public AudioClip overtakeClip;
        [SerializeField] private float overtakeVolume = 0.80f;

        public AudioClip crashClip;
        [SerializeField] private float crashVolume = 0.90f;

        [Tooltip("Whoosh/screech when player passes oncoming vehicle very closely.")]
        public AudioClip nearMissClip;
        [SerializeField] private float nearMissVolume = 0.65f;

        [Tooltip("Thud/wobble sound on pothole or rock hit. Different from crash.")]
        public AudioClip potholeHitClip;
        [SerializeField] private float potholeHitVolume = 0.60f;

        [Tooltip("Audio cue for sustained wrong-lane driving. Short, sharp warning tone.")]
        public AudioClip wrongLaneClip;
        [SerializeField] private float wrongLaneVolume = 0.55f;

        // ── SFX clips — animals ─────────────────────────────────────────────
        [Header("SFX — Animals")]
        [Tooltip("Hadada ibis call — the defining East African dawn sound. Plays in ASUBUHI phase.")]
        public AudioClip hadadaCallClip;
        [SerializeField] private float hadadaVolume = 0.75f;

        [Tooltip("Goat herd scatter — bleating, hooves, sudden movement.")]
        public AudioClip goatScatterClip;
        [SerializeField] private float goatVolume = 0.55f;

        [Tooltip("Elephant crossing — low rumble/trumpet. Plays once on A04 event.")]
        public AudioClip elephantClip;
        [SerializeField] private float elephantVolume = 0.80f;

        // ── SFX clips — atmosphere ──────────────────────────────────────────
        [Header("SFX — Atmosphere")]
        [Tooltip("Dust devil whoosh as player drives through ATM01 event.")]
        public AudioClip dustDevilClip;
        [SerializeField] private float dustDevilVolume = 0.50f;

        // ── SFX clips — session ─────────────────────────────────────────────
        [Header("SFX — Session")]
        [Tooltip("Short sting on session start — optional, can be null for silent start.")]
        public AudioClip sessionStartClip;
        [Tooltip("End-of-session sting before score screen.")]
        public AudioClip sessionEndClip;
        [SerializeField] private float sessionStingVolume = 0.70f;

        // ── Optional AudioMixer routing ─────────────────────────────────────
        [Header("AudioMixer (optional)")]
        [Tooltip("Route SFX pool sources through this group for master volume control.")]
        public AudioMixerGroup sfxMixerGroup;

        // ── Private state ───────────────────────────────────────────────────
        private AudioSource[] _sfxPool;
        private int           _sfxPoolIndex;

        // Overtake chain pitch — resets when chain breaks or session ends
        private float _overtakeChainPitch    = 1f;
        private const float OVERTAKE_PITCH_STEP = 0.05f;
        private const float OVERTAKE_PITCH_MAX  = 1.50f;

        // Road roughness (0 = smooth tarmac, 1 = rough murram)
        private float _roadRoughness = 0f;

        private Coroutine _musicFadeCoroutine;
        private Coroutine _ambienceCrossfadeCoroutine;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            BuildSfxPool();
        }

        void Start()
        {
            if (music    != null) music.volume    = normalMusicVolume;
            if (ambience != null) ambience.volume = 0.55f;
            // Engine, wind, roadSurface start at idle — OnSpeedChanged(0) sets them.
            OnSpeedChanged(0f);
        }

        // Resets default curve shapes when the component is first added in Editor.
        void Reset()
        {
            enginePitchCurve  = new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.5f, 0.9f), new Keyframe(1f, 1.3f));
            engineVolumeCurve = new AnimationCurve(
                new Keyframe(0f, 0.2f), new Keyframe(1f, 0.70f));
            windVolumeCurve   = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.4f, 0.05f), new Keyframe(1f, 0.65f));
            roadSurfaceCurve  = new AnimationCurve(
                new Keyframe(0f, 0.10f), new Keyframe(1f, 0.35f));
        }

        // ══════════════════════════════════════════════════════════════════
        //  Speed binding  (called every frame by WorldSpeed.LateUpdate)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by WorldSpeed.LateUpdate with t = Current / maxSpeed (0–1).
        /// Updates engine, wind, and road surface layers via AnimationCurves.
        /// </summary>
        public void OnSpeedChanged(float t)
        {
            if (engine != null)
            {
                engine.pitch  = enginePitchCurve.Evaluate(t);
                engine.volume = engineVolumeCurve.Evaluate(t);
            }
            if (wind != null)
                wind.volume = windVolumeCurve.Evaluate(t);
            if (roadSurface != null)
                // Road roughness boosts base volume; speed controls the multiplier.
                roadSurface.volume = roadSurfaceCurve.Evaluate(t) * (1f + _roadRoughness * 0.4f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Context events  (zone, surface, time of day)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by RoadSequencer when a new sequence starts.
        /// Crossfades the ambient layer to the clip matching the context tag.
        /// </summary>
        public void OnZoneChanged(string contextTag)
        {
            AudioClip target = contextTag switch
            {
                "township"        or "township_market" or
                "township_civic"  or "township_school"  => ambienceTownship,
                "tsavo_wildlife"  or "tsavo"             => ambienceWildlife,
                "highland"        or "highland_start"    => ambienceHighland,
                _                                        => ambienceSavanna
            };

            if (ambience == null || target == null || target == ambience.clip) return;
            if (_ambienceCrossfadeCoroutine != null) StopCoroutine(_ambienceCrossfadeCoroutine);
            _ambienceCrossfadeCoroutine = StartCoroutine(CrossfadeAmbience(target));
        }

        /// <summary>
        /// Called by RoadTileRecycler when a new tile activates.
        /// roughness: 0 = smooth tarmac, 1 = rough murram / rocky surface.
        /// </summary>
        public void OnRoadSurfaceChanged(float roughness)
        {
            _roadRoughness = Mathf.Clamp01(roughness);
        }

        /// <summary>
        /// Called by DayCycleManager when the time-of-day phase changes.
        /// Triggers one-shot atmosphere sounds appropriate to the phase.
        /// </summary>
        public void OnTimeOfDayChanged(string phase)
        {
            // ASUBUHI (morning): Hadada ibis call — the defining East African dawn sound.
            if (phase == "ASUBUHI") PlayHadadaCall();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Session lifecycle
        // ══════════════════════════════════════════════════════════════════

        public void OnSessionStart()
        {
            ResetOvertakeChain();
            PlaySFX(sessionStartClip, sessionStingVolume);
            SetMusicVolume(normalMusicVolume, musicFadeDuration);
        }

        public void OnSessionEnd()
        {
            PlaySFX(sessionEndClip, sessionStingVolume);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Gameplay SFX
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Play overtake reward sting. Pitch rises per consecutive overtake in the
        /// current chain. Call ResetOvertakeChain() when the chain breaks (collision,
        /// wrong lane) or the session ends.
        /// </summary>
        public void PlayOvertakeReward()
        {
            PlaySFX(overtakeClip, overtakeVolume, _overtakeChainPitch);
            _overtakeChainPitch = Mathf.Min(
                _overtakeChainPitch + OVERTAKE_PITCH_STEP, OVERTAKE_PITCH_MAX);
        }

        public void ResetOvertakeChain() => _overtakeChainPitch = 1f;

        /// <summary>Called by GameManager on traffic collision. Plays crash SFX and ducks music.</summary>
        public void OnCrash()
        {
            PlaySFX(crashClip, crashVolume);
            ResetOvertakeChain();
            SetMusicVolume(crashMusicVolume, musicFadeDuration);
        }

        /// <summary>Called by GameManager on restart. Restores music volume.</summary>
        public void OnRestart()
        {
            ResetOvertakeChain();
            SetMusicVolume(normalMusicVolume, musicFadeDuration);
        }

        public void PlayNearMiss() => PlaySFX(nearMissClip, nearMissVolume,
            0.95f + UnityEngine.Random.value * 0.10f);

        public void OnPotholeHit() => PlaySFX(potholeHitClip, potholeHitVolume,
            0.9f + UnityEngine.Random.value * 0.15f);

        public void PlayWrongLane() => PlaySFX(wrongLaneClip, wrongLaneVolume);

        // ══════════════════════════════════════════════════════════════════
        //  Traffic SFX
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Play a random horn variant. Called by TrafficHorn on proximity trigger.
        /// Each call picks a random clip variant and applies a slight random pitch
        /// for variety — prevents all horns from sounding identical.
        /// </summary>
        public void PlayHorn()
        {
            if (hornClips == null || hornClips.Length == 0) return;
            var clip  = hornClips[UnityEngine.Random.Range(0, hornClips.Length)];
            float pitch = 0.88f + UnityEngine.Random.value * 0.24f;
            PlaySFX(clip, hornVolume, pitch);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Animal SFX
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Hadada ibis call. Plays automatically in ASUBUHI phase via OnTimeOfDayChanged.
        /// Can also be called from an AnimalManager for ATM04 (Hadada flyover event).
        /// </summary>
        public void PlayHadadaCall() => PlaySFX(hadadaCallClip, hadadaVolume);

        /// <summary>Called by AnimalManager when a goat herd scatters (A01 event).</summary>
        public void PlayGoatScatter() => PlaySFX(goatScatterClip, goatVolume,
            0.95f + UnityEngine.Random.value * 0.10f);

        /// <summary>Called by AnimalManager on A04 elephant crossing event.</summary>
        public void PlayElephant() => PlaySFX(elephantClip, elephantVolume);

        // ══════════════════════════════════════════════════════════════════
        //  Atmosphere SFX
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Called when player enters ATM01 dust devil.</summary>
        public void PlayDustDevil() => PlaySFX(dustDevilClip, dustDevilVolume);

        // ══════════════════════════════════════════════════════════════════
        //  Internal — SFX pool
        // ══════════════════════════════════════════════════════════════════

        private void BuildSfxPool()
        {
            _sfxPool = new AudioSource[sfxPoolSize];
            for (int i = 0; i < sfxPoolSize; i++)
            {
                var child = new GameObject($"SFX_Pool_{i}");
                child.transform.SetParent(transform);
                var src           = child.AddComponent<AudioSource>();
                src.playOnAwake   = false;
                src.spatialBlend  = 0f;   // 2D — all SFX are non-spatial
                src.loop          = false;
                if (sfxMixerGroup != null) src.outputAudioMixerGroup = sfxMixerGroup;
                _sfxPool[i] = src;
            }
        }

        // Round-robin pool assignment. If the chosen source is still playing,
        // it is stolen — acceptable at pool size 8 for this game's SFX density.
        private void PlaySFX(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null) return;
            _sfxPoolIndex        = (_sfxPoolIndex + 1) % _sfxPool.Length;
            var src              = _sfxPool[_sfxPoolIndex];
            src.clip             = clip;
            src.volume           = volume;
            src.pitch            = pitch;
            src.Play();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internal — music fade + ambience crossfade
        // ══════════════════════════════════════════════════════════════════

        private void SetMusicVolume(float target, float duration)
        {
            if (music == null) return;
            if (_musicFadeCoroutine != null) StopCoroutine(_musicFadeCoroutine);
            _musicFadeCoroutine = StartCoroutine(FadeSource(music, target, duration));
        }

        private IEnumerator FadeSource(AudioSource src, float target, float duration)
        {
            float start = src.volume;
            float t     = 0f;
            while (t < 1f)
            {
                t          += Time.deltaTime / duration;
                src.volume  = Mathf.Lerp(start, target, t);
                yield return null;
            }
            src.volume = target;
        }

        private IEnumerator CrossfadeAmbience(AudioClip target)
        {
            float halfDuration = ambienceCrossfadeDuration * 0.5f;

            // Fade out current clip
            yield return FadeSource(ambience, 0f, halfDuration);

            // Swap clip and fade back in
            ambience.clip = target;
            ambience.Play();
            yield return FadeSource(ambience, 0.55f, halfDuration);
        }
    }
}
