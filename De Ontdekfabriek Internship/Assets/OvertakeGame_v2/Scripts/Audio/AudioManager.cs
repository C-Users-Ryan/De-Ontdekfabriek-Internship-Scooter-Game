using System.Collections;
using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Manages all audio layers for Roads of Nairobi.
    ///
    /// LAYER STACK — all looping:
    ///   Music       — Nairobi track (Pixabay)         vol 0.35  fades to 0.1 on crash
    ///   Ambience    — General city ambience            vol 0.55  never muted
    ///   TrafficHum  — City traffic loop (Mixkit)       vol 0.40  pitch +0.2 as speed rises
    ///   Engine      — Scooter engine loop (Mixkit)     vol 0.60  pitch 0.8–1.4 mapped to speed
    ///
    /// ONE-SHOT SFX:
    ///   Horn        — 3 variants, random pitch 0.9–1.1    vol 0.70
    ///   Coin        — pitch rises +0.05 per chain         vol 0.80
    ///   CrashSFX    — crash + crowd gasp (Freesound)      vol 0.90
    ///   NearMiss    — whoosh / tyre screech (Mixkit)      vol 0.65
    ///   LaneChange  — short swipe click (Mixkit)          vol 0.35
    ///   MarketEnter — african market clip (Freesound)     vol 0.50
    ///
    /// HORN TRIGGER — add this to each traffic vehicle prefab trigger:
    ///   void OnTriggerEnter(Collider other) {
    ///       if (other.CompareTag("Player")) AudioManager.I.PlayHorn();
    ///   }
    ///
    /// SPEED BINDING — call from PlayerController or WorldSpeed each frame:
    ///   AudioManager.I.OnSpeedChanged(normalised 0–1 speed value);
    ///
    /// SETUP:
    ///   1. Attach to a DontDestroyOnLoad persistent GameObject.
    ///   2. Add 4 AudioSource components to the same GameObject for the layers.
    ///      Assign each to the corresponding field below.
    ///   3. Assign all SFX AudioClip assets in the Inspector.
    ///   4. All looping sources: Loop = true, Play On Awake = true.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        [Header("Always-On Looping Sources")]
        [Tooltip("Nairobi music track. Pixabay — licence-free.")]
        public AudioSource music;
        [Tooltip("City ambience layer. Never muted.")]
        public AudioSource ambience;
        [Tooltip("Traffic hum — pitch rises with world speed.")]
        public AudioSource trafficHum;
        [Tooltip("Scooter engine loop — pitch maps 0.8–1.4 to player speed.")]
        public AudioSource engine;

        [Header("SFX Clips")]
        [Tooltip("3 horn variants for variety. Random pick each call.")]
        public AudioClip[] hornClips;
        public AudioClip coinClip;
        public AudioClip crashClip;
        public AudioClip nearMissClip;
        public AudioClip laneChangeClip;
        public AudioClip marketEnterClip;

        [Header("SFX Volumes")]
        public float hornVolume       = 0.70f;
        public float coinVolume       = 0.80f;
        public float crashVolume      = 0.90f;
        public float nearMissVolume   = 0.65f;
        public float laneChangeVolume = 0.35f;
        public float marketEnterVolume= 0.50f;

        [Header("Music Fade")]
        [Tooltip("Music fades to this volume during crash state.")]
        public float crashMusicVolume  = 0.10f;
        [Tooltip("Normal music volume during play.")]
        public float normalMusicVolume = 0.35f;
        [Tooltip("Seconds to fade music in/out.")]
        public float musicFadeDuration = 0.40f;

        // Coin chain pitch — resets each run
        private float _coinChainPitch = 1f;
        private const float COIN_PITCH_INCREMENT = 0.05f;
        private const float COIN_PITCH_MAX       = 1.50f;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (music      != null) music.volume      = normalMusicVolume;
            if (ambience   != null) ambience.volume   = 0.55f;
            if (trafficHum != null) trafficHum.volume = 0.40f;
            if (engine     != null) engine.volume     = 0.60f;
        }

        // ── Speed binding ──────────────────────────────────────────────────────
        /// <summary>
        /// Call every frame with a normalised speed value (0 = stopped, 1 = max speed).
        /// Reads from WorldSpeed: AudioManager.I.OnSpeedChanged(WorldSpeed.Instance.Current / maxSpeed)
        /// </summary>
        public void OnSpeedChanged(float t)
        {
            if (trafficHum != null) trafficHum.pitch = Mathf.Lerp(1.0f, 1.2f, t);
            if (engine     != null) engine.pitch     = Mathf.Lerp(0.8f, 1.4f, t);
        }

        // ── One-shot SFX ───────────────────────────────────────────────────────
        /// <summary>
        /// Play a random horn variant. Add to traffic vehicle trigger:
        ///   void OnTriggerEnter(Collider c) {
        ///       if (c.CompareTag("Player")) AudioManager.I.PlayHorn();
        ///   }
        /// </summary>
        public void PlayHorn()
        {
            if (hornClips == null || hornClips.Length == 0) return;
            var clip  = hornClips[Random.Range(0, hornClips.Length)];
            float pitch = 0.9f + Random.value * 0.2f;
            PlayAtCamera(clip, hornVolume * pitch); // slight volume variance too
        }

        /// <summary>
        /// Play coin collect SFX. Pitch rises +0.05 per consecutive coin in a chain.
        /// Call ResetCoinChain() when the run ends or the chain breaks.
        /// </summary>
        public void PlayCoin()
        {
            if (coinClip == null) return;
            var src = PlayAtCamera(coinClip, coinVolume);
            if (src != null) src.pitch = _coinChainPitch;
            _coinChainPitch = Mathf.Min(_coinChainPitch + COIN_PITCH_INCREMENT, COIN_PITCH_MAX);
        }

        public void ResetCoinChain() => _coinChainPitch = 1f;

        /// <summary>Called on collision. Fades music and plays crash SFX.</summary>
        public void OnCrash()
        {
            if (crashClip != null) PlayAtCamera(crashClip, crashVolume);
            StartCoroutine(FadeMusic(crashMusicVolume, musicFadeDuration));
        }

        /// <summary>Called on game restart. Restores music to normal volume.</summary>
        public void OnRestart()
        {
            ResetCoinChain();
            StartCoroutine(FadeMusic(normalMusicVolume, musicFadeDuration));
        }

        public void PlayNearMiss()    => PlayAtCamera(nearMissClip,    nearMissVolume);
        public void PlayLaneChange()  => PlayAtCamera(laneChangeClip,  laneChangeVolume);
        public void PlayMarketEnter() => PlayAtCamera(marketEnterClip, marketEnterVolume);

        // ── Music fade ─────────────────────────────────────────────────────────
        private IEnumerator FadeMusic(float target, float duration)
        {
            if (music == null) yield break;
            float start = music.volume;
            float t     = 0f;
            while (t < 1f)
            {
                t           += Time.deltaTime / duration;
                music.volume = Mathf.Lerp(start, target, t);
                yield return null;
            }
            music.volume = target;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private AudioSource PlayAtCamera(AudioClip clip, float volume)
        {
            if (clip == null) return null;
            var go  = new GameObject("OneShotAudio");
            var src = go.AddComponent<AudioSource>();
            src.clip        = clip;
            src.volume      = volume;
            src.spatialBlend = 0f; // 2D
            src.Play();
            Destroy(go, clip.length + 0.1f);
            return src;
        }
    }
}
