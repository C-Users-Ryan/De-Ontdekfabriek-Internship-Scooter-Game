using UnityEngine;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// Per-vehicle engine loop — the audible half of "different vehicle types" (companion to <see cref="TrafficHorn"/>,
    /// Req §11.4). Each traffic PREFAB carries its own engine clip, so a truck rumbles, a boda-boda buzzes and a
    /// matatu hums; combined with the spawner's separate same-direction / oncoming prefab pools, each direction can
    /// run its own mix of vehicles and each vehicle type sounds like itself.
    ///
    /// Drop this on a traffic prefab next to <see cref="TrafficVehicle"/>/<see cref="TrafficHorn"/> and assign the
    /// type's engine clip. It owns a dedicated CHILD AudioSource (3D, looping) so the continuous engine never fights
    /// the horn's one-shot source on the same object. The loop starts when the pooled vehicle is activated and stops
    /// when it is recycled, so cars sitting in the pool are silent. Optionally the pitch revs with the vehicle's own
    /// driving speed, with a small per-spawn jitter so two identical vehicles don't drone in unison.
    ///
    /// Fully null-safe: no clip assigned = silent, matching the rest of the audio system (which stays quiet until the
    /// clips are imported — a known backlog item). No edits to the traffic core are needed; this is purely additive.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrafficEngine : MonoBehaviour
    {
        [Header("Engine loop (this vehicle type's sound)")]
        [Tooltip("Looping engine clip for THIS vehicle type. Empty = silent (the game runs silent until audio is imported).")]
        [SerializeField] private AudioClip engineClip;
        [Range(0f, 1f)]
        [Tooltip("Base loudness of the engine loop, before the 3D distance rolloff below.")]
        [SerializeField] private float volume = 0.6f;

        [Header("3D distance falloff")]
        [Tooltip("Within this distance (m) the engine is at full volume.")]
        [SerializeField] private float minDistance = 5f;
        [Tooltip("Beyond this distance (m) the engine is inaudible, so far-off cars don't muddy the mix.")]
        [SerializeField] private float maxDistance = 45f;

        [Header("Rev with speed (optional)")]
        [Tooltip("If on, the pitch rises with the vehicle's own driving speed (reads TrafficVehicle.CurrentSpeed).")]
        [SerializeField] private bool pitchWithSpeed = true;
        [Tooltip("Pitch at a standstill.")]
        [SerializeField] private float idlePitch = 0.8f;
        [Tooltip("Pitch reached at refSpeed and above.")]
        [SerializeField] private float revPitch = 1.25f;
        [Tooltip("Own driving speed (m/s) at which revPitch is reached.")]
        [SerializeField] private float refSpeed = 12f;
        [Tooltip("Random pitch spread applied per spawn, so two of the same vehicle don't sound identical.")]
        [SerializeField] private float pitchJitter = 0.06f;
        [Tooltip("How quickly the pitch chases its target (per second). Higher = snappier revs.")]
        [SerializeField] private float pitchLerp = 4f;

        private AudioSource source;       // on a dedicated child, so it never collides with the horn's source
        private TrafficVehicle vehicle;   // optional — used only to read CurrentSpeed for the rev
        private float pitchOffset;        // this instance's random pitch bias
        private float currentPitch;

        private void Awake()
        {
            vehicle = GetComponent<TrafficVehicle>();

            // Own child source: the engine is a continuous loop, the horn is one-shots — keeping them on separate
            // AudioSources means the loop's pitch/volume never warps the horn (and vice versa). Created once per
            // pooled instance (Awake runs once), not per spawn.
            var child = new GameObject("EngineAudio");
            child.transform.SetParent(transform, false);
            source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 1f;            // 3D — engines are positioned in the world (left/right, near/far)
            source.dopplerLevel = 0.2f;          // a hint of doppler as cars pass, not a siren swoop
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.clip = engineClip;
        }

        private void OnEnable()
        {
            if (source == null || engineClip == null)
                return; // silent until a clip is assigned (consistent with the rest of the audio system)
            pitchOffset = Random.Range(-pitchJitter, pitchJitter);
            currentPitch = idlePitch + pitchOffset;
            source.pitch = currentPitch;
            source.volume = volume;
            source.Play();
        }

        private void OnDisable()
        {
            if (source != null)
                source.Stop(); // recycled to the pool — go quiet so parked cars never drone
        }

        private void Update()
        {
            if (!pitchWithSpeed || source == null || engineClip == null || !source.isPlaying)
                return;

            float speed01 = vehicle != null && refSpeed > 0.01f
                ? Mathf.Clamp01(vehicle.CurrentSpeed / refSpeed)
                : 0f;
            float targetPitch = Mathf.Lerp(idlePitch, revPitch, speed01) + pitchOffset;
            currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, pitchLerp * Time.deltaTime);
            source.pitch = currentPitch;
        }
    }
}
