using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;
using KenyaScooter.Traffic;

namespace KenyaScooter.Audio
{
    /// <summary>
    /// All game audio except per-vehicle horns (Req §11): the three-layer engine
    /// (motor + wind + road surface, curve-driven by speed ratio), the ambient
    /// crossfade following the road zone's context tag, music ducking on crashes,
    /// and an 8-source SFX pool — all sources created once in Awake, nothing
    /// allocated during play. Every clip is optional: with no audio imported yet
    /// (known backlog) the game simply runs silent.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioConfig config;

        private AudioSource motorSource;
        private AudioSource windSource;
        private AudioSource surfaceSource;
        private AudioSource ambientA;
        private AudioSource ambientB;
        private AudioSource musicSource;
        private AudioSource[] sfxPool;

        private bool ambientAIsFront;
        private float ambientFade; // 1 = front source fully in
        private float ambientVolA = 1f; // per-zone level of whatever clip ambientA currently holds
        private float ambientVolB = 1f;
        private float musicDuck = 1f;
        private int nextSfx;

        // 1 = world beds fully audible, → menuWorldDuck while a framing/menu screen is up. Smoothed so the
        // savanna/village ambience fades under a menu instead of cutting, and comes back when play resumes.
        private float worldGate = 1f;
        private const float WorldGateFade = 0.35f; // seconds to duck / restore the beds

        private void Awake()
        {
            Instance = this;

            // No config asset assigned → every source/level below dereferences null. Class contract is "run silent"
            // when audio is missing, so honour that for a missing CONFIG too: warn once, disable, and bail before any
            // deref (sfxPool stays null, so the event handlers that reach PlaySfx must also guard on config == null).
            if (config == null)
            {
                Debug.LogWarning("[AudioManager] No AudioConfig assigned, so all game audio is disabled. " +
                                 "Assign one in the Inspector.", this);
                enabled = false;
                return;
            }

            motorSource = CreateLoopSource("Engine_Motor", config.motorLoop);
            windSource = CreateLoopSource("Engine_Wind", config.windLoop);
            surfaceSource = CreateLoopSource("Engine_Surface", config.surfaceLoop);
            ambientA = CreateLoopSource("Ambient_A", null);
            ambientB = CreateLoopSource("Ambient_B", null);
            musicSource = CreateLoopSource("Music", config.musicLoop);
            if (musicSource.clip != null)
                musicSource.Play();

            sfxPool = new AudioSource[Mathf.Max(1, config.sfxPoolSize)];
            for (int i = 0; i < sfxPool.Length; i++)
            {
                var go = new GameObject("SFX_" + i);
                go.transform.SetParent(transform, false);
                sfxPool[i] = go.AddComponent<AudioSource>();
                sfxPool[i].playOnAwake = false;
            }
        }

        private void OnEnable()
        {
            GameEvents.SessionStarted += HandleSessionStarted;
            GameEvents.StateChanged += HandleStateChanged;
            GameEvents.SequenceChanged += HandleSequenceChanged;
            GameEvents.DayPhaseChanged += HandleDayPhaseChanged;
            GameEvents.OvertakeCompleted += HandleOvertake;
            GameEvents.NearMiss += HandleNearMiss;
            GameEvents.CollisionOccurred += HandleCollision;
            GameEvents.HazardHit += HandleHazard;
            GameEvents.WrongLaneTick += HandleWrongLane;
            GameEvents.RewindStarted += HandleRewindStarted;
            GameEvents.CheckpointReached += HandleCheckpoint;
        }

        private void OnDisable()
        {
            GameEvents.SessionStarted -= HandleSessionStarted;
            GameEvents.StateChanged -= HandleStateChanged;
            GameEvents.SequenceChanged -= HandleSequenceChanged;
            GameEvents.DayPhaseChanged -= HandleDayPhaseChanged;
            GameEvents.OvertakeCompleted -= HandleOvertake;
            GameEvents.NearMiss -= HandleNearMiss;
            GameEvents.CollisionOccurred -= HandleCollision;
            GameEvents.HazardHit -= HandleHazard;
            GameEvents.WrongLaneTick -= HandleWrongLane;
            GameEvents.RewindStarted -= HandleRewindStarted;
            GameEvents.CheckpointReached -= HandleCheckpoint;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateWorldGate(dt);
            UpdateEngine(dt);
            UpdateAmbientCrossfade(dt);
            UpdateMusicDuck(dt);
        }

        // ---- World-vs-menu gate ------------------------------------------------------------------
        // The game world is only heard in-world. While a framing screen (title/hand-off/eindstand/game over)
        // is up — even the relay screen reached mid-AtCheckpoint — the ambience/soundscape must not drone
        // behind the menu. The engine stays on through AtCheckpoint (for the charge-in), so it also rides this
        // gate: it ducks with the beds once the hand-off screen raises, not just off-road.

        private void UpdateWorldGate(float dt)
        {
            float target = WorldBedsAudible() ? 1f : Mathf.Clamp01(config.menuWorldDuck);
            worldGate = Mathf.MoveTowards(worldGate, target, dt / WorldGateFade);
        }

        private static bool WorldBedsAudible()
        {
            GameState s = GameManager.State;
            bool inWorld = s == GameState.Playing || s == GameState.Rewinding || s == GameState.AtCheckpoint;
            return inWorld && !GameEvents.MenuScreenVisible;
        }

        // ---- Engine layers (Req §11.1) ---------------------------------------------------

        private void UpdateEngine(float dt)
        {
            GameState state = GameManager.State;
            bool engineOn = state == GameState.Playing || state == GameState.AtCheckpoint;
            float ratio = engineOn ? WorldSpeed.Instance.SpeedRatio : 0f;

            // No engine audio during rewind or game over — fade hard to silence.
            float fadeRate = engineOn ? 2f : 6f;
            // Ride the menu gate too: an idling motor at AtCheckpoint must duck behind the relay hand-off screen,
            // not drone under it. worldGate is ~1 during the charge-in (no screen up) and ducks once it raises.
            SetLayer(motorSource, config.motorVolume.Evaluate(ratio) * config.engineVolume * config.motorLevel * worldGate, engineOn, fadeRate, dt);
            SetLayer(windSource, config.windVolume.Evaluate(ratio) * config.engineVolume * config.windLevel * worldGate, engineOn, fadeRate, dt);
            SetLayer(surfaceSource, config.surfaceVolume.Evaluate(ratio) * config.engineVolume * config.surfaceLevel * worldGate, engineOn, fadeRate, dt);
            if (motorSource.clip != null)
                motorSource.pitch = config.motorPitch.Evaluate(ratio);
        }

        private static void SetLayer(AudioSource source, float targetVolume, bool on, float fadeRate, float dt)
        {
            if (source.clip == null)
                return;
            if (on && !source.isPlaying)
                source.Play();
            source.volume = Mathf.MoveTowards(source.volume, on ? targetVolume : 0f, fadeRate * dt);
        }

        // ---- Ambient (Req §11.2) ------------------------------------------------------------

        private void HandleSequenceChanged(RoadSequence sequence)
        {
            if (config == null)
                return; // event fired despite the disabled flag (OnEnable subscribes regardless) — no config, no ambient
            AudioConfig.AmbientVariant target = FindAmbient(sequence);
            AudioClip targetClip = target != null ? target.clip : null;
            AudioSource front = ambientAIsFront ? ambientA : ambientB;
            if (front.clip == targetClip)
                return;

            // Swap: the back source takes the new clip and fades in over the old.
            ambientAIsFront = !ambientAIsFront;
            AudioSource newFront = ambientAIsFront ? ambientA : ambientB;
            newFront.clip = targetClip;
            newFront.volume = 0f;
            // Remember the new front clip's own level so the crossfade can honour each zone's slider.
            float newVolume = target != null ? target.volume : 1f;
            if (ambientAIsFront) ambientVolA = newVolume; else ambientVolB = newVolume;
            if (targetClip != null)
                newFront.Play();
            ambientFade = 0f;
        }

        private AudioConfig.AmbientVariant FindAmbient(RoadSequence sequence)
        {
            if (sequence == null || sequence.contextTags == null || config.ambientVariants == null)
                return null;
            for (int i = 0; i < config.ambientVariants.Length; i++)
            {
                string variantTag = config.ambientVariants[i].contextTag;
                if (string.IsNullOrEmpty(variantTag))
                    continue;
                for (int j = 0; j < sequence.contextTags.Length; j++)
                    if (sequence.contextTags[j] == variantTag)
                        return config.ambientVariants[i];
            }
            return null;
        }

        private void UpdateAmbientCrossfade(float dt)
        {
            // Advance the crossfade (if any is running)...
            if (ambientFade < 1f)
                ambientFade = Mathf.MoveTowards(ambientFade, 1f, dt / Mathf.Max(0.1f, config.ambientCrossfadeSeconds));

            // ...but re-apply the volumes EVERY frame, so worldGate keeps ducking the bed under a menu even after
            // the crossfade has settled (the old early-return froze the volume and let ambience leak behind menus).
            AudioSource front = ambientAIsFront ? ambientA : ambientB;
            AudioSource back = ambientAIsFront ? ambientB : ambientA;
            float frontVolume = ambientAIsFront ? ambientVolA : ambientVolB;
            float backVolume = ambientAIsFront ? ambientVolB : ambientVolA;
            front.volume = config.ambientVolume * frontVolume * ambientFade * worldGate;
            back.volume = config.ambientVolume * backVolume * (1f - ambientFade) * worldGate;
            if (ambientFade >= 1f && back.isPlaying)
                back.Stop();
        }

        // ---- Music ducking (Req §11.3) --------------------------------------------------------

        private void UpdateMusicDuck(float dt)
        {
            if (musicSource.clip == null)
                return;
            musicDuck = Mathf.MoveTowards(musicDuck, 1f, dt / Mathf.Max(0.1f, config.duckRecoverSeconds));
            // Music is the soundtrack across menus AND play, so it only follows the menu gate when opted in.
            float menuGate = config.menuDucksMusic ? worldGate : 1f;
            musicSource.volume = config.musicVolume * Mathf.Lerp(config.duckLevel, 1f, musicDuck) * menuGate;
        }

        // ---- SFX (Req §11.3) ---------------------------------------------------------------------

        // NOTE: these handlers read config.* in the PlaySfx ARGUMENTS, which are evaluated before PlaySfx's own
        // null-guard runs — so each needs its own early return when config == null (the events still fire even
        // though Awake disabled the component: OnEnable subscribes regardless of the enabled flag).
        private void HandleOvertake(TrafficVehicle vehicle) { if (config == null) return; PlaySfx(config.overtakeSting, config.overtakeStingVolume); }
        private void HandleNearMiss(TrafficVehicle vehicle) { if (config == null) return; PlaySfx(config.nearMissScreech, config.nearMissScreechVolume); }
        private void HandleHazard(HazardSpawnConfig definition, float playerKmh, Vector3 position) { if (config == null) return; PlaySfx(config.potholeBump, config.potholeBumpVolume); }
        private void HandleWrongLane() { if (config == null) return; PlaySfx(config.wrongLaneBuzz, config.wrongLaneBuzzVolume); }
        private void HandleRewindStarted() { if (config == null) return; PlaySfx(config.rewindWhoosh, config.rewindWhooshVolume); }
        private void HandleCheckpoint() { if (config == null) return; PlaySfx(config.checkpointArrive, config.checkpointArriveVolume); }

        // Engine spin-up / spin-down (Req §11.1): one-shots layered over the motor loop's fade so the
        // electric whine winds up as a run begins and winds down when it ends. Both are optional —
        // PlaySfx is null-safe, so leaving them empty just falls back to the plain loop fade.
        private void HandleSessionStarted() { if (config == null) return; PlaySfx(config.motorStart, config.motorStartVolume); }
        private void HandleStateChanged(GameState from, GameState to)
        {
            if (config == null)
                return;
            if (to == GameState.GameOver || to == GameState.Finished)
                PlaySfx(config.motorStop, config.motorStopVolume);
        }

        private void HandleCollision(CollisionSeverity severity, float relativeKmh, Vector3 position, bool absorbed)
        {
            if (config == null)
                return; // no config → the musicDuck write is meaningless and the PlaySfx args would NRE
            musicDuck = 0f;
            if (absorbed) PlaySfx(config.graceSaved, config.graceSavedVolume);
            else if (severity == CollisionSeverity.Hard) PlaySfx(config.crashHard, config.crashHardVolume);
            else PlaySfx(config.crashLight, config.crashLightVolume);
        }

        private void HandleDayPhaseChanged(int index, string label)
        {
            if (config == null)
                return;
            // Hadada ibis at dawn (Req §10) — the most Kenyan sound in the game.
            if (index == 0)
                PlaySfx(config.hadadaIbis, config.hadadaIbisVolume);
        }

        private void PlaySfx(AudioClip clip) => PlaySfx(clip, 1f);

        // perClipVolume is the sound's own slider (0–1); config.sfxVolume is the shared SFX master.
        private void PlaySfx(AudioClip clip, float perClipVolume)
        {
            // No config → Awake bailed and sfxPool is null; the SFX event handlers all funnel through here, so this
            // one guard keeps every one of them safe (OnEnable subscribes them even though the component is disabled).
            if (config == null || clip == null)
                return;
            AudioSource source = sfxPool[nextSfx];
            nextSfx = (nextSfx + 1) % sfxPool.Length;
            source.PlayOneShot(clip, config.sfxVolume * Mathf.Clamp01(perClipVolume));
        }

        private AudioSource CreateLoopSource(string sourceName, AudioClip clip)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            return source;
        }
    }
}
