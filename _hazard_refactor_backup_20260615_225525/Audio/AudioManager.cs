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
        private float musicDuck = 1f;
        private int nextSfx;

        private void Awake()
        {
            Instance = this;

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
            UpdateEngine(dt);
            UpdateAmbientCrossfade(dt);
            UpdateMusicDuck(dt);
        }

        // ---- Engine layers (Req §11.1) ---------------------------------------------------

        private void UpdateEngine(float dt)
        {
            GameState state = GameManager.State;
            bool engineOn = state == GameState.Playing || state == GameState.AtCheckpoint;
            float ratio = engineOn ? WorldSpeed.Instance.SpeedRatio : 0f;

            // No engine audio during rewind or game over — fade hard to silence.
            float fadeRate = engineOn ? 2f : 6f;
            SetLayer(motorSource, config.motorVolume.Evaluate(ratio), engineOn, fadeRate, dt);
            SetLayer(windSource, config.windVolume.Evaluate(ratio), engineOn, fadeRate, dt);
            SetLayer(surfaceSource, config.surfaceVolume.Evaluate(ratio), engineOn, fadeRate, dt);
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
            AudioClip target = FindAmbient(sequence);
            AudioSource front = ambientAIsFront ? ambientA : ambientB;
            if (front.clip == target)
                return;

            // Swap: the back source takes the new clip and fades in over the old.
            ambientAIsFront = !ambientAIsFront;
            AudioSource newFront = ambientAIsFront ? ambientA : ambientB;
            newFront.clip = target;
            newFront.volume = 0f;
            if (target != null)
                newFront.Play();
            ambientFade = 0f;
        }

        private AudioClip FindAmbient(RoadSequence sequence)
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
                        return config.ambientVariants[i].clip;
            }
            return null;
        }

        private void UpdateAmbientCrossfade(float dt)
        {
            if (ambientFade >= 1f)
                return;
            ambientFade = Mathf.MoveTowards(ambientFade, 1f, dt / Mathf.Max(0.1f, config.ambientCrossfadeSeconds));
            AudioSource front = ambientAIsFront ? ambientA : ambientB;
            AudioSource back = ambientAIsFront ? ambientB : ambientA;
            front.volume = config.ambientVolume * ambientFade;
            back.volume = config.ambientVolume * (1f - ambientFade);
            if (ambientFade >= 1f && back.isPlaying)
                back.Stop();
        }

        // ---- Music ducking (Req §11.3) --------------------------------------------------------

        private void UpdateMusicDuck(float dt)
        {
            if (musicSource.clip == null)
                return;
            musicDuck = Mathf.MoveTowards(musicDuck, 1f, dt / Mathf.Max(0.1f, config.duckRecoverSeconds));
            musicSource.volume = config.musicVolume * Mathf.Lerp(config.duckLevel, 1f, musicDuck);
        }

        // ---- SFX (Req §11.3) ---------------------------------------------------------------------

        private void HandleOvertake(TrafficVehicle vehicle) => PlaySfx(config.overtakeSting);
        private void HandleNearMiss(TrafficVehicle vehicle) => PlaySfx(config.nearMissScreech);
        private void HandleHazard(HazardKind kind, float playerKmh, Vector3 position) => PlaySfx(config.potholeBump);
        private void HandleWrongLane() => PlaySfx(config.wrongLaneBuzz);
        private void HandleRewindStarted() => PlaySfx(config.rewindWhoosh);
        private void HandleCheckpoint() => PlaySfx(config.checkpointArrive);

        private void HandleCollision(CollisionSeverity severity, float relativeKmh, Vector3 position, bool absorbed)
        {
            musicDuck = 0f;
            if (absorbed) PlaySfx(config.graceSaved);
            else PlaySfx(severity == CollisionSeverity.Hard ? config.crashHard : config.crashLight);
        }

        private void HandleDayPhaseChanged(int index, string label)
        {
            // Hadada ibis at dawn (Req §10) — the most Kenyan sound in the game.
            if (index == 0)
                PlaySfx(config.hadadaIbis);
        }

        private void PlaySfx(AudioClip clip)
        {
            if (clip == null)
                return;
            AudioSource source = sfxPool[nextSfx];
            nextSfx = (nextSfx + 1) % sfxPool.Length;
            source.PlayOneShot(clip);
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
