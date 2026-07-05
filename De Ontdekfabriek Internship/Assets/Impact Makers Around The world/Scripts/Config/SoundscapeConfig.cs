using UnityEngine;

namespace KenyaScooter.Config
{
    /// <summary>
    /// The layered Kenyan AMBIENT SOUNDSCAPE bed (the "make it sound like Kenya" pass) — a separate, dormant
    /// feature from <see cref="AudioConfig"/> and the AudioManager engine/SFX. Where AudioConfig drives the
    /// motor, the per-zone ambient swap and one-shots, this asset describes a continuous environmental bed that
    /// AmbientSoundscape cross-fades by day phase and a simple town-vs-country flag:
    ///
    ///  - NATURAL layer  — day savanna insects/cicadas + doves, fading to night crickets after dusk.
    ///  - TOWN layer     — distant market chatter, occasional matatu HOOTING and a tinny benga-style radio,
    ///                     fading in over "town" zones (nganya/matatu culture is loud, hooting and bass-heavy).
    ///
    /// SILENT IF UNCONFIGURED: every clip slot is optional. If no SoundscapeConfig asset exists, or a slot is
    /// empty, that layer is simply silent — so the feature ships dormant until Ryan drops in royalty-free or
    /// commissioned clips (see the clip spec doc). Nothing here writes to AudioConfig or AudioManager.
    /// </summary>
    [CreateAssetMenu(menuName = "Kenya Scooter/Soundscape Config", fileName = "SoundscapeConfig")]
    public sealed class SoundscapeConfig : ScriptableObject
    {
        [Header("Master")]
        [Tooltip("Whole-soundscape level, multiplied on top of AudioConfig.ambientVolume and each per-layer " +
                 "volume below. Keep it gentle so the bed sits UNDER the engine, ambience swap and SFX.")]
        [Range(0f, 1f)] public float soundscapeVolume = 0.5f;
        [Tooltip("Seconds for a layer to cross-fade in/out on a day-phase or town/country change. " +
                 "Long and slow so the world breathes rather than snaps.")]
        public float crossfadeSeconds = 3f;

        [Header("NATURAL — daytime (savanna insects/cicadas + doves)")]
        [Tooltip("Looping savanna insect/cicada bed for the daytime phases (ASUBUHI / MCHANA). Empty = silent.")]
        public AudioClip dayInsects;
        [Range(0f, 1f)] public float dayInsectsVolume = 0.6f;
        [Tooltip("Looping dove/bird coo bed, layered with the day insects. Empty = silent.")]
        public AudioClip doves;
        [Range(0f, 1f)] public float dovesVolume = 0.45f;

        [Header("NATURAL — night/dusk (crickets)")]
        [Tooltip("Looping cricket bed for the dusk/evening phases (ALASIRI / JIONI). Cross-fades against the " +
                 "day insects+doves by day phase. Empty = silent.")]
        public AudioClip nightCrickets;
        [Range(0f, 1f)] public float nightCricketsVolume = 0.6f;

        [Header("TOWN — market + matatu + radio")]
        [Tooltip("Looping distant market hum / bargaining chatter. Empty = silent.")]
        public AudioClip marketChatter;
        [Range(0f, 1f)] public float marketChatterVolume = 0.5f;
        [Tooltip("Matatu HOOTING one-shots, fired at random intervals while the town layer is up (nganya culture " +
                 "is loud and hooting). Several variants keep it from sounding repetitive. Empty = no hoots.")]
        public AudioClip[] matatuHoots;
        [Range(0f, 1f)] public float matatuHootVolume = 0.55f;
        [Tooltip("Shortest gap (seconds) between matatu hoots while the town layer is audible.")]
        public float matatuHootMinGap = 6f;
        [Tooltip("Longest gap (seconds) between matatu hoots while the town layer is audible.")]
        public float matatuHootMaxGap = 16f;
        [Tooltip("Looping tinny radio playing benga/gengetone-style music — bass-heavy, distant, like a stall " +
                 "speaker. Empty = silent.")]
        public AudioClip bengaRadioLoop;
        [Range(0f, 1f)] public float bengaRadioVolume = 0.4f;

        [Header("Town zoning (town-vs-country flag)")]
        [Tooltip("A RoadSequence carrying ANY of these context tags counts as a TOWN zone, so the town layer " +
                 "fades in (e.g. township, town, market). Matched against RoadSequence.contextTags.")]
        public string[] townContextTags = new[] { "township", "town", "market" };
        [Tooltip("Level the town layer idles at OUTSIDE town zones (0 = country is fully natural-only). A small " +
                 "value keeps a faint hum of distant life on the horizon everywhere.")]
        [Range(0f, 1f)] public float townBaseLevelOutsideTown = 0f;
        [Tooltip("If no zone signal ever arrives (e.g. a bare test scene with no RoadSequencer), pulse the town " +
                 "layer on/off on this period (seconds) so the town bed is still demonstrable. 0 = stay at the " +
                 "base level and never pulse.")]
        public float townFallbackPulseSeconds = 0f;

        [Header("Reference only — owned by the traffic work, listed here for the clip spec")]
        [Tooltip("Old-truck/lorry diesel engine loop. NOT played by AmbientSoundscape — the traffic vehicles own " +
                 "their own engine/horn audio. This slot exists purely so the clip is catalogued in one place for " +
                 "sourcing (see Kenya Soundscape — Audio Clip Spec.md).")]
        public AudioClip oldTruckEngineReference;
    }
}
