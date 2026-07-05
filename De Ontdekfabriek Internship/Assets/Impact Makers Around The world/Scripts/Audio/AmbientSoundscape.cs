using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.Roads;
using KenyaScooter.Session;
using KenyaScooter.Settings;

namespace KenyaScooter.Audio
{
    /// <summary>
    /// A layered Kenyan AMBIENT SOUNDSCAPE bed — the "make it sound like Kenya" pass. It plays a continuous
    /// environmental backdrop on a handful of looping AudioSources and cross-fades them by day phase and a
    /// simple town-vs-country flag. It is deliberately SEPARATE from AudioManager (which owns the engine, the
    /// per-zone ambient swap and one-shot SFX); this manager only adds the soft, always-on bed underneath.
    ///
    /// Layers (all optional, see <see cref="SoundscapeConfig"/>):
    ///  - NATURAL — daytime savanna insects/cicadas + doves (phases ASUBUHI/MCHANA), cross-fading to night
    ///    crickets at dusk/evening (phases ALASIRI/JIONI). Driven by DayCycleManager.CurrentPhase (0..3).
    ///  - TOWN — distant market chatter, a tinny benga-style radio loop, and occasional matatu HOOTING
    ///    one-shots, fading in over "town" zones (RoadSequence carrying a town context tag), and idling at a
    ///    gentle base level (or a simple pulse) in the country when no zone signal is available.
    ///
    /// Cheap and dormant-by-default:
    ///  - All sources are created ONCE at startup; nothing is allocated per frame. Cross-fades are
    ///    Mathf.MoveTowards on each source's volume.
    ///  - Every level is multiplied by SoundscapeConfig.soundscapeVolume and, when an AudioConfig is present,
    ///    by AudioConfig.ambientVolume (and by AudioListener.volume implicitly), so it honours the master mix.
    ///  - SILENT IF UNCONFIGURED: if no SoundscapeConfig asset is found, or a clip slot is empty, that layer is
    ///    simply silent. Self-bootstraps after scene load via ConfigLocator, so it needs zero scene wiring and
    ///    ships dormant until clips are dropped in.
    /// </summary>
    public sealed class AmbientSoundscape : MonoBehaviour
    {
        private SoundscapeConfig config;
        private AudioConfig audioConfig; // cached once; supplies the shared ambient master and the menu-duck level
        private DayCycleManager dayCycle;

        // 1 = soundscape audible in-world, → menuWorldDuck while a framing/menu screen is up, so this bed does not
        // drone behind the title / hand-off / eindstand / game-over the way it used to. Smoothed like the crossfades.
        private float worldGate = 1f;
        private const float WorldGateFade = 0.35f;

        // Natural layer.
        private AudioSource dayInsectsSource;
        private AudioSource dovesSource;
        private AudioSource cricketsSource;

        // Town layer.
        private AudioSource marketSource;
        private AudioSource radioSource;
        private AudioSource hootSource;

        // Targets in 0..1 that the per-source volumes chase each frame.
        private float naturalDayTarget;   // 1 during day phases, 0 at night/dusk
        private float naturalNightTarget; // inverse of the above
        private float townTarget;         // 1 in town zones, base level otherwise

        private float nextHootAt;
        private float pulsePhase;

        // True once any SequenceChanged has arrived, i.e. a RoadSequencer exists and is driving the town layer.
        // The fallback pulse is ONLY for the "no zone signal ever arrives" case (see SoundscapeConfig tooltip),
        // so once we've seen one signal we must stop pulsing or we'd stomp the real event-driven townTarget.
        private bool zoneSignalSeen;

        // Self-bootstrap: create the soundscape manager after scene load if a config can be located, mirroring
        // PedestrianCrossingSpawner / the audio + scoring managers. Zero scene wiring; if no SoundscapeConfig is
        // found, nothing is created and the game is silent on this layer (the feature ships dormant).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<AmbientSoundscape>() != null)
                return;
            if (ConfigLocator.Find<SoundscapeConfig>() == null)
                return; // feature not configured — do nothing, gameplay/audio are unaffected
            var go = new GameObject("AmbientSoundscape (auto)");
            go.AddComponent<AmbientSoundscape>();
        }

        private void Awake()
        {
            config = ConfigLocator.Find<SoundscapeConfig>();
            if (config == null)
            {
                enabled = false; // nothing to play — stay dormant
                return;
            }

            audioConfig = ConfigLocator.Find<AudioConfig>();
            dayCycle = FindObjectOfType<DayCycleManager>();

            dayInsectsSource = CreateLoopSource("Soundscape_DayInsects", config.dayInsects);
            dovesSource = CreateLoopSource("Soundscape_Doves", config.doves);
            cricketsSource = CreateLoopSource("Soundscape_NightCrickets", config.nightCrickets);
            marketSource = CreateLoopSource("Soundscape_MarketChatter", config.marketChatter);
            radioSource = CreateLoopSource("Soundscape_BengaRadio", config.bengaRadioLoop);

            // The matatu hoots are one-shots; one dedicated non-looping source plays them.
            var hootGo = new GameObject("Soundscape_MatatuHoots");
            hootGo.transform.SetParent(transform, false);
            hootSource = hootGo.AddComponent<AudioSource>();
            hootSource.playOnAwake = false;

            // Start aligned with whatever phase/zone is already active so we do not audibly snap on the first frame.
            ResolveNaturalTargets();
            townTarget = Mathf.Clamp01(config.townBaseLevelOutsideTown);
            ScheduleNextHoot();
        }

        private void OnEnable()
        {
            GameEvents.SequenceChanged += HandleSequenceChanged;
            GameEvents.DayPhaseChanged += HandleDayPhaseChanged;
        }

        private void OnDisable()
        {
            GameEvents.SequenceChanged -= HandleSequenceChanged;
            GameEvents.DayPhaseChanged -= HandleDayPhaseChanged;
        }

        private void Update()
        {
            if (config == null)
                return;

            float dt = Time.deltaTime;

            // Duck the whole bed under a menu screen so nothing from the game leaks behind the UI.
            float gateTarget = WorldBedsAudible() ? 1f : MenuDuckLevel();
            worldGate = Mathf.MoveTowards(worldGate, gateTarget, dt / WorldGateFade);

            // Country fallback: with no zone signal ever arriving (bare test scene), optionally pulse the town
            // layer so it is still demonstrable; otherwise it simply rests at the configured base level. Once a
            // real signal has arrived the sequencer exists, so the fallback is never needed again — stop pulsing
            // so we don't overwrite the event-driven townTarget HandleSequenceChanged sets.
            if (!zoneSignalSeen && config.townFallbackPulseSeconds > 0.01f)
            {
                pulsePhase += dt / config.townFallbackPulseSeconds;
                float pulse = 0.5f * (1f + Mathf.Sin(pulsePhase * Mathf.PI * 2f)); // 0..1
                townTarget = Mathf.Max(Mathf.Clamp01(config.townBaseLevelOutsideTown), pulse);
            }

            float fadeStep = dt / Mathf.Max(0.1f, config.crossfadeSeconds);
            float master = MasterLevel();

            DriveLayer(dayInsectsSource, naturalDayTarget * config.dayInsectsVolume * master, fadeStep, dt);
            DriveLayer(dovesSource, naturalDayTarget * config.dovesVolume * master, fadeStep, dt);
            DriveLayer(cricketsSource, naturalNightTarget * config.nightCricketsVolume * master, fadeStep, dt);
            DriveLayer(marketSource, townTarget * config.marketChatterVolume * master, fadeStep, dt);
            DriveLayer(radioSource, townTarget * config.bengaRadioVolume * master, fadeStep, dt);

            UpdateMatatuHoots(dt, master);
        }

        // ---- Targets -----------------------------------------------------------------------------

        private void HandleDayPhaseChanged(int index, string label) => ResolveNaturalTargets(index);

        /// <summary>Day phases (0 ASUBUHI, 1 MCHANA) = insects+doves; dusk/evening (2 ALASIRI, 3 JIONI) = crickets.</summary>
        private void ResolveNaturalTargets() => ResolveNaturalTargets(CurrentPhaseOrDefault());

        private void ResolveNaturalTargets(int phase)
        {
            bool isNight = phase >= 2; // ALASIRI/JIONI -> crickets; ASUBUHI/MCHANA -> day bed
            naturalDayTarget = isNight ? 0f : 1f;
            naturalNightTarget = isNight ? 1f : 0f;
        }

        private int CurrentPhaseOrDefault()
        {
            // DayCycleManager starts at -1 before the first phase is published; treat that as daytime.
            if (dayCycle == null)
                return 0;
            int phase = dayCycle.CurrentPhase;
            return phase < 0 ? 0 : phase;
        }

        private void HandleSequenceChanged(RoadSequence sequence)
        {
            zoneSignalSeen = true; // a sequencer is live and driving the town layer — the fallback pulse is retired
            bool inTown = sequence != null
                          && config.townContextTags != null
                          && sequence.HasAnyTag(config.townContextTags);
            townTarget = inTown ? 1f : Mathf.Clamp01(config.townBaseLevelOutsideTown);
        }

        // ---- Matatu hoots ------------------------------------------------------------------------

        private void UpdateMatatuHoots(float dt, float master)
        {
            if (config.matatuHoots == null || config.matatuHoots.Length == 0)
                return;
            // Only hoot while the town layer is meaningfully audible — no random horns out in empty savanna.
            if (townTarget <= 0.2f)
                return;

            nextHootAt -= dt;
            if (nextHootAt > 0f)
                return;

            AudioClip clip = config.matatuHoots[Random.Range(0, config.matatuHoots.Length)];
            if (clip != null)
                hootSource.PlayOneShot(clip, config.matatuHootVolume * townTarget * master);
            ScheduleNextHoot();
        }

        private void ScheduleNextHoot()
        {
            float min = Mathf.Max(0.5f, config.matatuHootMinGap);
            float max = Mathf.Max(min, config.matatuHootMaxGap);
            nextHootAt = Random.Range(min, max);
        }

        // ---- Mix ---------------------------------------------------------------------------------

        /// <summary>Whole-soundscape level: the config's own master times AudioConfig.ambientVolume when present
        /// (so the facilitator's ambient slider also rides this bed). AudioListener.volume applies implicitly.</summary>
        private float MasterLevel()
        {
            float ambient = audioConfig != null ? audioConfig.ambientVolume : 1f;
            return Mathf.Clamp01(config.soundscapeVolume) * ambient * worldGate;
        }

        // Where the bed sits while a menu screen is up (0 = silent). Shared with AudioManager's ambient duck.
        private float MenuDuckLevel() => audioConfig != null ? Mathf.Clamp01(audioConfig.menuWorldDuck) : 0f;

        // The soundscape is a world bed: audible only in-world, and never behind a framing/menu screen.
        private static bool WorldBedsAudible()
        {
            GameState s = GameManager.State;
            bool inWorld = s == GameState.Playing || s == GameState.Rewinding || s == GameState.AtCheckpoint;
            return inWorld && !GameEvents.MenuScreenVisible;
        }

        private static void DriveLayer(AudioSource source, float targetVolume, float fadeStep, float dt)
        {
            if (source == null || source.clip == null)
                return;
            bool wantsAudible = targetVolume > 0.0001f;
            if (wantsAudible && !source.isPlaying)
                source.Play();
            source.volume = Mathf.MoveTowards(source.volume, targetVolume, fadeStep);
            // Park a fully-faded source so it is not burning a voice on silence.
            if (!wantsAudible && source.volume <= 0.0001f && source.isPlaying)
                source.Stop();
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
