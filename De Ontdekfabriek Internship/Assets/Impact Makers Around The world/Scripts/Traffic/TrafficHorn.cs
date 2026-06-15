using UnityEngine;

namespace KenyaScooter.Traffic
{
    /// <summary>
    /// Per-vehicle horn (Req §11.4): fires when the player comes close, with a
    /// per-vehicle cooldown and re-arm so it cannot spam. Ticked by TrafficVehicle
    /// (no own Update). Distance-based volume comes from the AudioSource's authored
    /// 3D rolloff — matatus get louder as the player approaches and fade past.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class TrafficHorn : MonoBehaviour
    {
        [SerializeField] private AudioClip[] hornClips;
        [SerializeField] private float triggerDistance = 12f;
        [SerializeField] private float maxLateralGap = 4f;
        [SerializeField] private float cooldownSeconds = 6f;
        [Range(0f, 1f)]
        [Tooltip("Not every driver honks every time.")]
        [SerializeField] private float hornChance = 0.5f;

        private AudioSource source;
        private bool armed = true;
        private float cooldownUntil;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
        }

        private void OnEnable() => armed = true;

        /// <summary>Called by TrafficVehicle.Tick with player-relative distances.</summary>
        public void Tick(float aheadDistance, float lateralGap)
        {
            if (Mathf.Abs(aheadDistance) > triggerDistance * 2f)
            {
                armed = true;
                return;
            }

            if (!armed || Time.time < cooldownUntil)
                return;

            if (Mathf.Abs(aheadDistance) < triggerDistance && Mathf.Abs(lateralGap) < maxLateralGap)
            {
                armed = false;
                cooldownUntil = Time.time + cooldownSeconds;

                if (Random.value < hornChance && hornClips != null && hornClips.Length > 0)
                {
                    AudioClip clip = hornClips[Random.Range(0, hornClips.Length)];
                    if (clip != null)
                        source.PlayOneShot(clip);
                }
            }
        }
    }
}
