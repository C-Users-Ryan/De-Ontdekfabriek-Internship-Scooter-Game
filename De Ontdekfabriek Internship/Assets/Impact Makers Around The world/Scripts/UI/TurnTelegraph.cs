using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.Roads;

namespace KenyaScooter.UI
{
    /// <summary>
    /// Telegraphs an upcoming bend. From the elderly menu/comprehension play-test (2026-07-05): a first-time
    /// player was taken by surprise by a turn because nothing announced it — the bend simply arrived. This
    /// watches the road ahead through <see cref="RoadSequencer.TryGetTurnAhead"/> and, the moment a turn sharp
    /// enough to notice enters the lead window, raises <see cref="GameEvents.TurnAhead"/> ONCE for that bend.
    /// It rides the very same calm-caution channel the pedestrian-crossing telegraph uses (the DiegeticHud
    /// caution banner and the WarningSystem one-shot both already listen), so the player is told
    /// "BOCHT · ← LINKS" / "BOCHT · RECHTS →" a beat before the road turns — a gotcha becomes a readable,
    /// anticipated turn. Telegraph only: the road, the turn geometry and the scoring are all unchanged.
    ///
    /// Self-bootstraps after scene load (like TiltSteerHint and the crossing spawner), so it needs no scene
    /// wiring and is a no-op wherever no RoadSequencer exists (a bare test scene). Direction: TryGetTurnAhead
    /// reports + for a road bending to the player's right, − to the left (see RoadTile's swing sign) — worth a
    /// quick confirm on the first drive test that the LEFT/RIGHT labels read the right way round.
    /// </summary>
    public sealed class TurnTelegraph : MonoBehaviour
    {
        [Tooltip("How far ahead (metres of road) a bend is announced — the warning shows this far before the turn STARTS. " +
                 "Bigger = more reaction time; too big and it fires while the bend is still out of sight.")]
        [SerializeField] private float leadDistance = 45f;
        [Tooltip("Ignore bends gentler than this heading swing (degrees) — only telegraph turns a player must actually read.")]
        [SerializeField] private float minSwingDegrees = 22f;
        [Tooltip("Re-arm only once the announced bend is at least this far (metres) from the last one, so each distinct turn warns exactly once.")]
        [SerializeField] private float rearmGap = 8f;

        // Absolute road-arc of the bend last announced. A turn keeps the same arc as the player approaches it,
        // so it is announced once; the next distinct bend has a different arc and re-arms the telegraph.
        private float lastAnnouncedArc = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<TurnTelegraph>() != null)
                return;
            var go = new GameObject("TurnTelegraph (auto)");
            go.AddComponent<TurnTelegraph>();
            DontDestroyOnLoad(go);
        }

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        private void Update()
        {
            if (GameManager.State != GameState.Playing)
                return;
            RoadSequencer seq = RoadSequencer.Instance;
            if (seq == null)
                return;

            if (!seq.TryGetTurnAhead(leadDistance, minSwingDegrees, out float distance, out float swing))
                return;

            // Same bend as last time (its arc has barely moved) — already announced, so stay quiet.
            float turnArc = seq.PlayerArc + distance;
            if (Mathf.Abs(turnArc - lastAnnouncedArc) <= rearmGap)
                return;

            lastAnnouncedArc = turnArc;
            GameEvents.RaiseTurnAhead(swing >= 0f ? "WARN_TURN_RIGHT" : "WARN_TURN_LEFT", distance);
        }

        private void HandleSessionReset() => lastAnnouncedArc = float.NegativeInfinity;
    }
}
