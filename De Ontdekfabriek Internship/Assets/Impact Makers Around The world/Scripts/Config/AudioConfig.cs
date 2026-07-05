using System;
using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// Holds every audio clip and its routing: the three-layer engine, the ambient crossfade and the
    /// SFX pool (Req §11). Every clip is optional — AudioManager is null-safe, so the game runs silently
    /// until clips are dropped in (importing the audio is a known backlog item).
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Audio Config", fileName = "AudioConfig")]
    public sealed class AudioConfig : ScriptableObject
    {
        [Serializable]
        public sealed class AmbientVariant
        {
            [Tooltip("RoadSequence context tag this ambience belongs to (savanna, township, wildlife, highland).")]
            public string contextTag;
            public AudioClip clip;
            [Range(0f, 1f)]
            [Tooltip("Per-zone level for this bed, multiplied on top of the shared ambientVolume below.")]
            public float volume = 1f;
        }

        [Header("Engine layers (Req §11.1) — curves map speed ratio 0–1")]
        [Range(0f, 1f)]
        [Tooltip("Master volume for the WHOLE engine (motor + wind + surface), multiplied on top of the per-layer " +
                 "speed curves. Lower it if the engine drowns out the ambience and SFX.")]
        public float engineVolume = 0.35f;
        public AudioClip motorLoop;
        [Range(0f, 1f)] [Tooltip("Per-layer level for the motor whine, on top of engineVolume.")]
        public float motorLevel = 1f;
        public AnimationCurve motorVolume = AnimationCurve.EaseInOut(0f, 0.25f, 1f, 0.9f);
        public AnimationCurve motorPitch = AnimationCurve.Linear(0f, 0.85f, 1f, 1.6f);
        [Tooltip("Optional one-shot spin-up whir, played over the loop's fade-in when a run starts (SessionStarted). Trim it from the head of the motor recording. Empty = just fade the loop in.")]
        public AudioClip motorStart;
        [Range(0f, 1f)] public float motorStartVolume = 1f;
        [Tooltip("Optional one-shot spin-down, played over the loop's fade-out when a run ends (GameOver / Finished). Trim it from the tail of the motor recording.")]
        public AudioClip motorStop;
        [Range(0f, 1f)] public float motorStopVolume = 1f;
        public AudioClip windLoop;
        [Range(0f, 1f)] [Tooltip("Per-layer level for the wind rush, on top of engineVolume.")]
        public float windLevel = 1f;
        public AnimationCurve windVolume = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.4f, 0.1f), new Keyframe(1f, 0.8f));
        public AudioClip surfaceLoop;
        [Range(0f, 1f)] [Tooltip("Per-layer level for the road-surface rumble, on top of engineVolume.")]
        public float surfaceLevel = 1f;
        public AnimationCurve surfaceVolume = AnimationCurve.Linear(0f, 0.05f, 1f, 0.6f);

        [Header("Ambient variants (Req §11.2)")]
        public AmbientVariant[] ambientVariants;
        public float ambientCrossfadeSeconds = 2.5f;
        [Range(0f, 1f)]
        [Tooltip("Shared master for all ambience beds; each variant has its own volume on top.")]
        public float ambientVolume = 0.6f;

        [Header("Music ducking (Req §11.3)")]
        public AudioClip musicLoop;
        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [Range(0f, 1f)] public float duckLevel = 0.35f;
        public float duckRecoverSeconds = 2f;

        [Header("SFX (Req §11.3)")]
        [Range(0f, 1f)]
        [Tooltip("Master volume for one-shot SFX (overtake sting, crash, pothole, wrong-lane, rewind, etc.).")]
        public float sfxVolume = 0.5f;
        public int sfxPoolSize = 8;
        public AudioClip overtakeSting;
        [Range(0f, 1f)] public float overtakeStingVolume = 1f;
        public AudioClip nearMissScreech;
        [Range(0f, 1f)] public float nearMissScreechVolume = 1f;
        public AudioClip crashHard;
        [Range(0f, 1f)] public float crashHardVolume = 1f;
        public AudioClip crashLight;
        [Range(0f, 1f)] public float crashLightVolume = 1f;
        public AudioClip potholeBump;
        [Range(0f, 1f)] public float potholeBumpVolume = 1f;
        public AudioClip wrongLaneBuzz;
        [Range(0f, 1f)] public float wrongLaneBuzzVolume = 1f;
        public AudioClip graceSaved;
        [Range(0f, 1f)] public float graceSavedVolume = 1f;
        public AudioClip rewindWhoosh;
        [Range(0f, 1f)] public float rewindWhooshVolume = 1f;
        public AudioClip checkpointArrive;
        [Range(0f, 1f)] public float checkpointArriveVolume = 1f;
        [Tooltip("Hadada ibis call, played once at the ASUBUHI (dawn) phase (Req §10).")]
        public AudioClip hadadaIbis;
        [Range(0f, 1f)] public float hadadaIbisVolume = 1f;

        [Header("UI sounds (menu taps & screen cues) — a layer of their own")]
        [Tooltip("Master volume for interface sounds. These play on a SEPARATE, listener-pause-ignoring source " +
                 "(UiSoundDirector), so they belong to the UI, never to the game world, and still click while the " +
                 "facilitator menu has the world paused.")]
        [Range(0f, 1f)] public float uiVolume = 0.6f;
        [Tooltip("A soft tap, played on any button/toggle/chip press across every screen. One clip covers the whole UI.")]
        public AudioClip uiTap;
        [Range(0f, 1f)] public float uiTapVolume = 1f;
        [Tooltip("A short whoosh/chime, played once when a framing screen (title, hand-off, eindstand, game over) appears.")]
        public AudioClip uiScreenShow;
        [Range(0f, 1f)] public float uiScreenShowVolume = 1f;

        [Header("Menu ducking (keep the game from droning behind a menu screen)")]
        [Tooltip("While a framing/menu screen is up, the diegetic world beds (savanna/village ambience and the layered " +
                 "soundscape) fade to THIS level so only the UI — and, optionally, the music — is heard. 0 = fully " +
                 "silent behind menus (the default). The engine already silences itself off-road.")]
        [Range(0f, 1f)] public float menuWorldDuck = 0f;
        [Tooltip("Also fade the looping music down under menus. Usually OFF — the music doubles as the menu soundtrack, " +
                 "and it is not part of 'the game leaking' the way the savanna/traffic beds are.")]
        public bool menuDucksMusic = false;
    }
}
