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
        public struct AmbientVariant
        {
            [Tooltip("RoadSequence context tag this ambience belongs to (savanna, township, wildlife, highland).")]
            public string contextTag;
            public AudioClip clip;
        }

        [Header("Engine layers (Req §11.1) — curves map speed ratio 0–1")]
        public AudioClip motorLoop;
        public AnimationCurve motorVolume = AnimationCurve.EaseInOut(0f, 0.25f, 1f, 0.9f);
        public AnimationCurve motorPitch = AnimationCurve.Linear(0f, 0.85f, 1f, 1.6f);
        public AudioClip windLoop;
        public AnimationCurve windVolume = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.4f, 0.1f), new Keyframe(1f, 0.8f));
        public AudioClip surfaceLoop;
        public AnimationCurve surfaceVolume = AnimationCurve.Linear(0f, 0.05f, 1f, 0.6f);

        [Header("Ambient variants (Req §11.2)")]
        public AmbientVariant[] ambientVariants;
        public float ambientCrossfadeSeconds = 2.5f;
        public float ambientVolume = 0.6f;

        [Header("Music ducking (Req §11.3)")]
        public AudioClip musicLoop;
        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [Range(0f, 1f)] public float duckLevel = 0.35f;
        public float duckRecoverSeconds = 2f;

        [Header("SFX (Req §11.3)")]
        public int sfxPoolSize = 8;
        public AudioClip overtakeSting;
        public AudioClip nearMissScreech;
        public AudioClip crashHard;
        public AudioClip crashLight;
        public AudioClip potholeBump;
        public AudioClip wrongLaneBuzz;
        public AudioClip graceSaved;
        public AudioClip rewindWhoosh;
        public AudioClip checkpointArrive;
        [Tooltip("Hadada ibis call, played once at the ASUBUHI (dawn) phase (Req §10).")]
        public AudioClip hadadaIbis;
    }
}
