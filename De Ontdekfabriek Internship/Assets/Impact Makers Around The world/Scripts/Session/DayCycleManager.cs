using UnityEngine;
using UnityEngine.Rendering;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.Session
{
    /// <summary>
    /// The day cycle (M25): phases defined in DayCycleConfig advance with session
    /// time, each crossfading a URP global Volume (matched by index) while the sun's
    /// rotation, colour, intensity and the fog colour lerp along. The phases map the
    /// 120 seconds onto a Kenyan day from cool morning to sunset — the session's
    /// narrative arc (MDA A4). Raises DayPhaseChanged for the HUD label and audio.
    /// </summary>
    public sealed class DayCycleManager : MonoBehaviour
    {
        [SerializeField] private DayCycleConfig config;
        [Tooltip("One URP global Volume per config phase, same order.")]
        [SerializeField] private Volume[] phaseVolumes;
        [SerializeField] private Light sun;

        public int CurrentPhase { get; private set; } = -1;
        public string CurrentLabel =>
            CurrentPhase >= 0 && CurrentPhase < config.phases.Length ? config.phases[CurrentPhase].label : string.Empty;

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        private void Update()
        {
            GameState state = GameManager.State;
            if (state != GameState.Playing && state != GameState.AtCheckpoint)
                return;
            if (TimerManager.Instance == null || config.phases.Length == 0)
                return;

            float elapsed = TimerManager.Instance.Elapsed;
            int target = 0;
            for (int i = 0; i < config.phases.Length; i++)
                if (config.phases[i].startTime <= elapsed)
                    target = i;

            if (target != CurrentPhase)
            {
                CurrentPhase = target;
                GameEvents.RaiseDayPhaseChanged(target, config.phases[target].label);
            }

            BlendTowardPhase(Time.deltaTime / Mathf.Max(0.1f, config.transitionSeconds));
        }

        private void BlendTowardPhase(float step)
        {
            int volumeCount = Mathf.Min(phaseVolumes.Length, config.phases.Length);
            for (int i = 0; i < volumeCount; i++)
            {
                if (phaseVolumes[i] == null)
                    continue;
                float target = i == CurrentPhase ? 1f : 0f;
                phaseVolumes[i].weight = Mathf.MoveTowards(phaseVolumes[i].weight, target, step);
            }

            DayCycleConfig.Phase phase = config.phases[CurrentPhase];
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Slerp(sun.transform.rotation, Quaternion.Euler(phase.sunEuler), step);
                sun.color = Color.Lerp(sun.color, phase.sunColour, step);
                sun.intensity = Mathf.MoveTowards(sun.intensity, phase.sunIntensity, step);
            }
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, phase.fogColour, step);
        }

        private void HandleSessionReset()
        {
            if (config.phases.Length == 0)
                return;

            CurrentPhase = 0;
            DayCycleConfig.Phase phase = config.phases[0];
            int volumeCount = Mathf.Min(phaseVolumes.Length, config.phases.Length);
            for (int i = 0; i < volumeCount; i++)
                if (phaseVolumes[i] != null)
                    phaseVolumes[i].weight = i == 0 ? 1f : 0f;

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(phase.sunEuler);
                sun.color = phase.sunColour;
                sun.intensity = phase.sunIntensity;
            }
            RenderSettings.fogColor = phase.fogColour;

            GameEvents.RaiseDayPhaseChanged(0, phase.label);
        }
    }
}
