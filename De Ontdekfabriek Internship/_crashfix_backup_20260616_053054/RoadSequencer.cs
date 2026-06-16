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
        }

        private void OnDisable()
        {
            GameEvents.SessionReset -= HandleSessionReset;
            GameEvents.RewindCompleted -= HandleRewindCompleted;
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (state != GameState.Playing && state != GameState.AtCheckpoint)
                return;

            // Central scroll (M1): one delta, applied to every tile and the chain end.
            Vector3 delta = -RoadDirection.Current * (WorldSpeed.Instance.Current * Time.deltaTime);
            for (int i = 0; i < activeTiles.Count; i++)
                activeTiles[i].transform.position += delta;
            chainPosition += delta;

            float playerLong = RoadDirection.Longitudinal(player.position);

            while (activeTiles.Count > 0 &&
                   RoadDirection.Longitudinal(activeTiles[0].ExitPosition) < playerLong - despawnBehind)
            {
                activeTiles[0].SourcePool.Release(activeTiles[0]);
                activeTiles.RemoveAt(0);
            }

            while (RoadDirection.Longitudinal(chainPosition) < playerLong + spawnHorizon)
                SpawnNextTile();
        }

        // ---- Chain building ---------------------------------------------------------

        private void SpawnNextTile()
        {
            if (prefabQueue.Count == 0)
                AdvanceSequence();

            RoadTile prefab = prefabQueue.Dequeue();
            RoadTile tile = pools[prefab].Get();
            tile.transform.SetPositionAndRotation(chainPosition, chainRotation);
            tile.gameObject.SetActive(true);
            activeTiles.Add(tile);

            chainPosition = tile.ExitPosition;
            chainRotation = tile.ExitRotation;
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

            // Opening is forced, never weighted (Req §4.3).
            CurrentSequence = openingSequence;
            lastUsedAtTile[openingSequence] = 0;
            EnqueueTiles(openingSequence);
            GameEvents.RaiseSequenceChanged(openingSequence);

            while (RoadDirection.Longitudinal(chainPosition) < playerLong + spawnHorizon)
                SpawnNextTile();
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
