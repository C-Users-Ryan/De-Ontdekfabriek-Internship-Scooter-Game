using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.SafetyNet;
using KenyaScooter.Session;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// Builds and scrolls the endless road (M1, M23, M24). Maintains a geometric
    /// chain — each tile snaps to the previous tile's exit anchor — fed by sequences
    /// chosen through the grammar: weighted random over unlocked sequences, hard
    /// forbidden-previous-tag filter, 2× preferred-previous-tag boost, tile-count
    /// cooldown. The opening sequence is forced, never weighted (Req §4.3).
    /// All tiles come from per-prefab pools built at Start.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class RoadSequencer : MonoBehaviour
    {
        public static RoadSequencer Instance { get; private set; }

        [SerializeField] private RoadSequence openingSequence;
        [SerializeField] private RoadSequence[] sequences;
        [SerializeField] private Transform player;
        [Tooltip("Road is kept built this far ahead of the player.")]
        [SerializeField] private float spawnHorizon = 160f;
        [SerializeField] private float despawnBehind = 35f;
        [SerializeField] private int poolSizePerTile = 4;

        public RoadSequence CurrentSequence { get; private set; }

        private readonly Dictionary<RoadTile, ObjectPool<RoadTile>> pools =
            new Dictionary<RoadTile, ObjectPool<RoadTile>>();
        private readonly List<RoadTile> activeTiles = new List<RoadTile>(16);
        private readonly Queue<RoadTile> prefabQueue = new Queue<RoadTile>(16);
        private readonly List<RoadTile> tileEnqueueBuffer = new List<RoadTile>(16);
        private readonly Dictionary<RoadSequence, long> lastUsedAtTile = new Dictionary<RoadSequence, long>();

        private long tilesSpawnedTotal;
        private Vector3 chainPosition;
        private Quaternion chainRotation;

        // The road is measured by arc length (metres of road), not by world position. This is what
        // keeps turns safe: a turn tile bends the chain in world space, but the amount of road we
        // keep ahead of and behind the player is counted along the road itself. The corridor stays
        // short, so it can never spiral back over itself into a loop (M3) — and the build loop can
        // never spin forever, because every tile adds a fixed length.
        private float aheadArc;   // metres of road built ahead of the player
        private float behindArc;  // metres of road still kept behind the player

        // Hard cap on tiles built in one pass — a backstop if a tile ever fails to add road length
        // (a zero-length tile) so the build loop can never freeze the game.
        private const int MaxTilesPerFill = 128;

        private void Awake() => Instance = this;

        private void Start()
        {
            BuildPoolsFor(openingSequence);
            for (int i = 0; i < sequences.Length; i++)
                BuildPoolsFor(sequences[i]);
        }

        private void OnEnable()
        {
            GameEvents.SessionReset += HandleSessionReset;
            GameEvents.RewindCompleted += HandleRewindCompleted;
            RoadDirection.DirectionChanged += HandleDirectionChanged;
        }

        private void OnDisable()
        {
            GameEvents.SessionReset -= HandleSessionReset;
            GameEvents.RewindCompleted -= HandleRewindCompleted;
            RoadDirection.DirectionChanged -= HandleDirectionChanged;
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (state != GameState.Playing && state != GameState.AtCheckpoint)
                return;

            // Central scroll (M1): one delta, applied to every tile and the chain end.
            float move = WorldSpeed.Instance.Current * Time.deltaTime;
            Vector3 delta = -RoadDirection.Current * move;
            for (int i = 0; i < activeTiles.Count; i++)
                activeTiles[i].transform.position += delta;
            chainPosition += delta;

            // The player advanced 'move' metres along the road this frame.
            aheadArc -= move;
            behindArc += move;

            DespawnBehind();
            BuildAhead();
        }

        // ---- Chain building ---------------------------------------------------------

        /// <summary>
        /// Builds road forward until at least <see cref="spawnHorizon"/> metres of road sit ahead
        /// of the player. Progress is counted in road length (arc), not world distance, so a chain
        /// that bends through a turn tile (M3) still measures correctly and we only ever keep a
        /// short corridor ahead — it can never spiral into a self-crossing loop or spin forever.
        /// </summary>
        private void BuildAhead()
        {
            int built = 0;
            while (aheadArc < spawnHorizon)
            {
                int before = activeTiles.Count;
                SpawnNextTile();
                if (activeTiles.Count == before || ++built >= MaxTilesPerFill)
                    break;
            }
        }

        /// <summary>
        /// Releases the oldest tile once it sits fully <see cref="despawnBehind"/> metres behind the
        /// player, so the road behind never piles up — including after a turn, where the old
        /// straight section would otherwise linger off to the side.
        /// </summary>
        private void DespawnBehind()
        {
            while (activeTiles.Count > 1 &&
                   behindArc - activeTiles[0].length >= despawnBehind)
            {
                behindArc -= activeTiles[0].length;
                activeTiles[0].SourcePool.Release(activeTiles[0]);
                activeTiles.RemoveAt(0);
            }
        }

        private void SpawnNextTile()
        {
            if (prefabQueue.Count == 0)
                AdvanceSequence();
            if (prefabQueue.Count == 0)
                return; // no eligible sequence yielded tiles — never Dequeue an empty queue

            RoadTile prefab = prefabQueue.Dequeue();
            RoadTile tile = pools[prefab].Get();
            tile.transform.SetPositionAndRotation(chainPosition, chainRotation);
            tile.gameObject.SetActive(true);
            activeTiles.Add(tile);

            chainPosition = tile.ExitPosition;
            chainRotation = tile.ExitRotation;
            aheadArc += tile.length;
            tilesSpawnedTotal++;
        }

        private void AdvanceSequence()
        {
            RoadSequence next = PickNextSequence();
            CurrentSequence = next;
            lastUsedAtTile[next] = tilesSpawnedTotal;

            EnqueueTiles(next);

            GameEvents.RaiseSequenceChanged(next);
        }

        /// <summary>
        /// Queues a sequence's tiles for spawning. Authored order by default; when the
        /// sequence opts into <see cref="RoadSequence.shuffleTiles"/> a fresh Fisher-Yates
        /// shuffle is queued instead, so the zone is remixed every time it plays (M23).
        /// The buffer is reused, so there is no per-spawn allocation.
        /// </summary>
        private void EnqueueTiles(RoadSequence sequence)
        {
            RoadTile[] tiles = sequence.tiles;
            if (tiles == null || tiles.Length == 0)
                return;

            if (!sequence.shuffleTiles)
            {
                for (int i = 0; i < tiles.Length; i++)
                    prefabQueue.Enqueue(tiles[i]);
                return;
            }

            tileEnqueueBuffer.Clear();
            for (int i = 0; i < tiles.Length; i++)
                tileEnqueueBuffer.Add(tiles[i]);

            // Fisher-Yates — unbiased in-place shuffle.
            for (int i = tileEnqueueBuffer.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (tileEnqueueBuffer[i], tileEnqueueBuffer[j]) = (tileEnqueueBuffer[j], tileEnqueueBuffer[i]);
            }

            for (int i = 0; i < tileEnqueueBuffer.Count; i++)
                prefabQueue.Enqueue(tileEnqueueBuffer[i]);
        }

        private RoadSequence PickNextSequence()
        {
            float elapsed = TimerManager.Instance != null ? TimerManager.Instance.Elapsed : 0f;

            float total = TotalEligibleWeight(elapsed, respectCooldown: true);
            bool respectCooldown = total > 0f;
            if (!respectCooldown)
                total = TotalEligibleWeight(elapsed, respectCooldown: false);
            if (total <= 0f)
                return CurrentSequence != null ? CurrentSequence : openingSequence;

            float roll = Random.value * total;
            for (int i = 0; i < sequences.Length; i++)
            {
                float weight = EligibleWeight(sequences[i], elapsed, respectCooldown);
                if (weight <= 0f)
                    continue;
                roll -= weight;
                if (roll < 0f)
                    return sequences[i];
            }
            return CurrentSequence != null ? CurrentSequence : openingSequence;
        }

        private float TotalEligibleWeight(float elapsed, bool respectCooldown)
        {
            float total = 0f;
            for (int i = 0; i < sequences.Length; i++)
                total += EligibleWeight(sequences[i], elapsed, respectCooldown);
            return total;
        }

        private float EligibleWeight(RoadSequence candidate, float elapsed, bool respectCooldown)
        {
            if (candidate == null || candidate.tiles == null || candidate.tiles.Length == 0)
                return 0f;
            if (candidate.unlockAtTime > elapsed)
                return 0f;

            if (respectCooldown && lastUsedAtTile.TryGetValue(candidate, out long usedAt)
                && tilesSpawnedTotal - usedAt < candidate.cooldownTiles)
                return 0f;

            RoadSequence previous = CurrentSequence;
            if (previous != null && previous.HasAnyTag(candidate.forbiddenPrevTags))
                return 0f;

            float weight = candidate.weight;
            if (previous != null && previous.HasAnyTag(candidate.preferredPrevTags))
                weight *= 2f;
            return weight;
        }

        // ---- Session flow ------------------------------------------------------------

        private void HandleSessionReset()
        {
            for (int i = 0; i < activeTiles.Count; i++)
                activeTiles[i].SourcePool.Release(activeTiles[i]);
            activeTiles.Clear();
            prefabQueue.Clear();
            lastUsedAtTile.Clear();
            tilesSpawnedTotal = 0;

            float playerLong = player != null ? RoadDirection.Longitudinal(player.position) : 0f;
            chainPosition = RoadDirection.Current * (playerLong - despawnBehind * 0.5f);
            chainRotation = Quaternion.LookRotation(RoadDirection.Current);

            // The first tile starts half the despawn gap behind the player; the rest builds ahead.
            behindArc = despawnBehind * 0.5f;
            aheadArc = -despawnBehind * 0.5f;

            // Opening is forced, never weighted (Req §4.3).
            CurrentSequence = openingSequence;
            lastUsedAtTile[openingSequence] = 0;
            EnqueueTiles(openingSequence);
            GameEvents.RaiseSequenceChanged(openingSequence);

            BuildAhead();
        }

        private void HandleRewindCompleted()
        {
            // Rewind restored tile actives/positions directly — rebuild the chain
            // bookkeeping from the actual scene state.
            foreach (ObjectPool<RoadTile> pool in pools.Values)
                pool.ReconcileAvailability();

            activeTiles.Clear();
            foreach (ObjectPool<RoadTile> pool in pools.Values)
                for (int i = 0; i < pool.AllInstances.Count; i++)
                    if (pool.AllInstances[i].gameObject.activeInHierarchy)
                        activeTiles.Add(pool.AllInstances[i]);

            activeTiles.Sort((a, b) =>
                RoadDirection.Longitudinal(a.transform.position).CompareTo(
                    RoadDirection.Longitudinal(b.transform.position)));

            if (activeTiles.Count > 0)
            {
                RoadTile last = activeTiles[activeTiles.Count - 1];
                chainPosition = last.ExitPosition;
                chainRotation = last.ExitRotation;
            }

            RecomputeArcFromScene();
        }

        /// <summary>
        /// A turn just fired. The new road can sit off to one side of the player's axis (it depends
        /// on where the turn tile's exit anchor is), which is what threw the player to the kerb.
        /// Slide the whole chain sideways so the road is centred on the player again, then re-sync the
        /// arc counters to the moved tiles so the despawn keeps clearing the pre-turn road instead of
        /// letting it pile up at the corner.
        /// </summary>
        private void HandleDirectionChanged(Vector3 previous, Vector3 next)
        {
            float roadLateral = RoadDirection.Lateral(chainPosition);
            Vector3 shift = -RoadDirection.SteerAxis * roadLateral;
            for (int i = 0; i < activeTiles.Count; i++)
                activeTiles[i].transform.position += shift;
            chainPosition += shift;

            RecomputeArcFromScene();
        }

        /// <summary>
        /// After a rewind the tiles were restored directly, so recover the ahead/behind arc
        /// counters from where the tiles actually sit relative to the player.
        /// </summary>
        private void RecomputeArcFromScene()
        {
            aheadArc = 0f;
            behindArc = 0f;
            if (player == null)
                return;

            float playerLong = RoadDirection.Longitudinal(player.position);
            for (int i = 0; i < activeTiles.Count; i++)
            {
                if (RoadDirection.Longitudinal(activeTiles[i].ExitPosition) > playerLong)
                    aheadArc += activeTiles[i].length;
                else
                    behindArc += activeTiles[i].length;
            }
        }

        private void BuildPoolsFor(RoadSequence sequence)
        {
            if (sequence == null || sequence.tiles == null)
                return;

            for (int i = 0; i < sequence.tiles.Length; i++)
            {
                RoadTile prefab = sequence.tiles[i];
                if (prefab == null || pools.ContainsKey(prefab))
                    continue;

                ObjectPool<RoadTile> pool = null;
                pool = new ObjectPool<RoadTile>(prefab, transform, poolSizePerTile,
                    created =>
                    {
                        // Covers pool expansion mid-game (see the note in HazardSpawner): an
                        // expanded tile with no SourcePool throws when it despawns.
                        if (pool != null)
                            created.SourcePool = pool;
                        if (RewindSystem.Instance != null)
                            RewindSystem.Instance.Register(created);
                    });
                for (int j = 0; j < pool.AllInstances.Count; j++)
                    pool.AllInstances[j].SourcePool = pool;
                pools.Add(prefab, pool);
            }
        }
    }
}
