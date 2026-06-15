using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.SafetyNet
{
    /// <summary>
    /// The rewind (M17, Req §8.2): a ring buffer records snapshots of every
    /// registered participant (player, traffic, hazards, tiles) plus the world speed,
    /// every rewindSnapshotInterval while playing. On a hard crash GameManager sets
    /// the state to Rewinding and calls BeginRewind: B&amp;W ramp in, reverse playback
    /// through the buffered frames, exact restore of the oldest frame, ramp out,
    /// RewindCompleted. The penalty and streak reset hang off that event in
    /// ScoreManager; spawners reconcile their pools off it too.
    /// All frame storage is preallocated — zero allocation during recording.
    /// </summary>
    public sealed class RewindSystem : MonoBehaviour
    {
        public static RewindSystem Instance { get; private set; }

        [SerializeField] private SafetyNetConfig config;
        [SerializeField] private RewindVisuals visuals;

        public int UsedThisTurn { get; private set; }
        public bool CanRewind => UsedThisTurn < config.rewindsPerTurn && frameCount > 1;

        private sealed class RewindFrame
        {
            public float worldSpeed;
            public int sampleCount;
            public RewindSample[] samples;
        }

        private readonly List<IRewindable> participants = new List<IRewindable>(192);
        private RewindFrame[] frames;
        private int capacity;
        private int head;       // next write index
        private int frameCount;
        private float recordTimer;

        private void Awake()
        {
            Instance = this;
            capacity = Mathf.CeilToInt(config.rewindWindowSeconds / config.rewindSnapshotInterval) + 1;
            frames = new RewindFrame[capacity];
            for (int i = 0; i < capacity; i++)
                frames[i] = new RewindFrame { samples = new RewindSample[config.maxRewindParticipants] };
        }

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        /// <summary>Called once per instance at creation (pools never destroy, so indices stay stable).</summary>
        public void Register(IRewindable participant)
        {
            if (participants.Count >= config.maxRewindParticipants)
            {
                Debug.LogWarning("[RewindSystem] Participant cap reached — increase maxRewindParticipants in SafetyNetConfig.");
                return;
            }
            participants.Add(participant);
        }

        private void Update()
        {
            if (GameManager.State != GameState.Playing)
                return;

            recordTimer += Time.deltaTime;
            while (recordTimer >= config.rewindSnapshotInterval)
            {
                recordTimer -= config.rewindSnapshotInterval;
                CaptureFrame();
            }
        }

        /// <summary>Starts the rewind. GameManager has already set the state to Rewinding.</summary>
        public void BeginRewind()
        {
            UsedThisTurn++;
            GameEvents.RaiseRewindStarted();
            StartCoroutine(RewindRoutine());
        }

        private void CaptureFrame()
        {
            RewindFrame frame = frames[head];
            head = (head + 1) % capacity;
            frameCount = Mathf.Min(frameCount + 1, capacity);

            frame.worldSpeed = WorldSpeed.Instance.Current;
            frame.sampleCount = Mathf.Min(participants.Count, frame.samples.Length);
            for (int i = 0; i < frame.sampleCount; i++)
                participants[i].CaptureSample(ref frame.samples[i]);
        }

        private IEnumerator RewindRoutine()
        {
            yield return Fade(0f, 1f);

            // Reverse playback: newest frame back to oldest, interpolated for smoothness.
            float elapsed = 0f;
            int lastIndex = frameCount - 1;
            while (elapsed < config.rewindPlaybackSeconds)
            {
                elapsed += Time.deltaTime;
                float framePos = Mathf.Clamp01(elapsed / config.rewindPlaybackSeconds) * lastIndex;
                int step = Mathf.Min(Mathf.FloorToInt(framePos), lastIndex - 1 >= 0 ? lastIndex - 1 : 0);
                float t = framePos - step;
                ApplyInterpolated(FrameFromNewest(step), FrameFromNewest(Mathf.Min(step + 1, lastIndex)), t);
                yield return null;
            }

            // Exact restore of the oldest frame — the position play resumes from.
            RewindFrame oldest = FrameFromNewest(lastIndex);
            ApplyInterpolated(oldest, oldest, 0f);
            WorldSpeed.Instance.SetCurrent(oldest.worldSpeed);

            yield return Fade(1f, 0f);

            ClearBuffer();
            GameEvents.RaiseRewindCompleted();
        }

        private IEnumerator Fade(float from, float to)
        {
            if (visuals == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < config.rewindFadeSeconds)
            {
                elapsed += Time.deltaTime;
                visuals.SetWeight(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / config.rewindFadeSeconds)));
                yield return null;
            }
            visuals.SetWeight(to);
        }

        /// <summary>frames, counted back from the most recent capture (0 = newest).</summary>
        private RewindFrame FrameFromNewest(int stepsBack)
        {
            int index = (head - 1 - stepsBack + capacity * 2) % capacity;
            return frames[index];
        }

        private void ApplyInterpolated(RewindFrame a, RewindFrame b, float t)
        {
            int count = Mathf.Min(participants.Count, Mathf.Min(a.sampleCount, b.sampleCount));
            RewindSample blended;
            for (int i = 0; i < count; i++)
            {
                ref RewindSample sa = ref a.samples[i];
                ref RewindSample sb = ref b.samples[i];
                blended.Position = Vector3.LerpUnclamped(sa.Position, sb.Position, t);
                blended.Rotation = Quaternion.SlerpUnclamped(sa.Rotation, sb.Rotation, t);
                blended.Aux = Mathf.LerpUnclamped(sa.Aux, sb.Aux, t);
                blended.Active = sb.Active;
                participants[i].ApplySample(in blended);
            }
        }

        private void ClearBuffer()
        {
            frameCount = 0;
            head = 0;
            recordTimer = 0f;
        }

        private void HandleSessionReset()
        {
            UsedThisTurn = 0;
            ClearBuffer();
            if (visuals != null)
                visuals.SetWeight(0f);
        }
    }
}
