using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.Session
{
    /// <summary>
    /// The class's collective JOURNEY down the Nairobi–Mombasa road (A109): every committed turn's driven
    /// distance is added to one shared total, which the relay/eindstand screens draw as a marker moving along
    /// a stylised route map. Score becomes geography — "JULLIE KWAMEN TOT MTITO ANDEI" — which gives the relay
    /// a destination and the "Impact Makers Around The World" frame an actual world.
    ///
    /// Purely additive: it mirrors GameManager.CommitTurn's semantics by listening to the SAME events
    /// (CheckpointReached, StateChanged→Finished/GameOver commit a turn once; SessionReset re-arms;
    /// GroupReset starts a fresh journey), so no core file changes. Real metres are scaled by
    /// <see cref="MapScale"/> (default ×10) so a typical class (10–15 two-minute turns ≈ 20–27 real km)
    /// genuinely travels the 480 km map instead of never leaving Nairobi. Persisted like the group score.
    /// </summary>
    public static class JourneyProgress
    {
        private const string SaveKey = "ksg.journey.metres";

        /// <summary>Game-metres → map-kilometre exaggeration. ×10: one turn ≈ 18 map-km, a class ≈ 200–300 km.</summary>
        public static float MapScale = 10f;

        /// <summary>The A109 stops drawn on the route strip: label + real kilometre mark from Nairobi.</summary>
        public static readonly (string name, float km)[] Towns =
        {
            ("NAIROBI", 0f), ("ATHI RIVER", 25f), ("EMALI", 125f),
            ("MTITO ANDEI", 233f), ("VOI", 329f), ("MOMBASA", 480f)
        };

        public static float TotalKm => 480f;

        private static float metres;     // raw driven metres, all committed turns of this group
        private static bool turnAdded;   // one add per turn, mirroring GameManager.turnCommitted
        private static bool hooked;

        /// <summary>Scaled map-kilometres travelled so far (clamped to the route).</summary>
        public static float MapKm => Mathf.Min(metres * MapScale / 1000f, TotalKm);

        /// <summary>0 at Nairobi, 1 at Mombasa.</summary>
        public static float Progress01 => MapKm / TotalKm;

        /// <summary>The last town reached (for the eindstand: "JULLIE KWAMEN TOT …").</summary>
        public static string ReachedTown()
        {
            string reached = Towns[0].name;
            for (int i = 0; i < Towns.Length; i++)
                if (MapKm >= Towns[i].km) reached = Towns[i].name;
            return reached;
        }

        /// <summary>The next town ahead (for the hand-off: "ONDERWEG NAAR …"). Mombasa once arrived.</summary>
        public static string NextTown()
        {
            for (int i = 0; i < Towns.Length; i++)
                if (MapKm < Towns[i].km) return Towns[i].name;
            return Towns[Towns.Length - 1].name;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            metres = PlayerPrefs.GetFloat(SaveKey, 0f);
            turnAdded = false;
            if (hooked) return;
            hooked = true;
            GameEvents.CheckpointReached += CommitTurn;
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.SessionReset += () => turnAdded = false;
            GameEvents.GroupReset += ResetJourney;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { hooked = false; metres = 0f; turnAdded = false; }

        private static void OnStateChanged(GameState from, GameState to)
        {
            if (to == GameState.Finished || to == GameState.GameOver)
                CommitTurn();
        }

        private static void CommitTurn()
        {
            if (turnAdded || GameManager.Instance == null)
                return;
            turnAdded = true;
            metres += Mathf.Max(0f, GameManager.Instance.Stats.DistanceMetres);
            PlayerPrefs.SetFloat(SaveKey, metres);
        }

        private static void ResetJourney()
        {
            metres = 0f;
            turnAdded = false;
            PlayerPrefs.SetFloat(SaveKey, 0f);
        }
    }
}
