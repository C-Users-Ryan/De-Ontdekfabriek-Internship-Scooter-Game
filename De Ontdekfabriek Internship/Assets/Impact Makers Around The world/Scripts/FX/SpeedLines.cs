using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.FX
{
    /// <summary>
    /// Speed-line particle emission scaled by world speed (MDA A1 — sensation).
    /// Silent below base speed, ramping to full at max. The emission module is cached
    /// once; no per-frame allocation.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SpeedLines : MonoBehaviour
    {
        [SerializeField] private float maxEmissionRate = 60f;
        [Tooltip("Speed ratio (0–1 of max) to emission fraction. Flat zero up to base speed.")]
        [SerializeField] private AnimationCurve emissionBySpeed = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.35f, 0f), new Keyframe(1f, 1f));

        private ParticleSystem.EmissionModule emission;

        private void Awake()
        {
            emission = GetComponent<ParticleSystem>().emission;
        }

        private void Update()
        {
            float rate = GameManager.State == GameState.Playing || GameManager.State == GameState.AtCheckpoint
                ? emissionBySpeed.Evaluate(WorldSpeed.Instance.SpeedRatio) * maxEmissionRate
                : 0f;
            emission.rateOverTime = rate;
        }
    }
}
