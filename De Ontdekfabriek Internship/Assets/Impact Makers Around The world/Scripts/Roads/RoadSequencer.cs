using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.SafetyNet;
using KenyaScooter.Session;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// Builds and rides the endless road (M1, M3, M23, M24). Rebuilt 2026-06-17 around a
    /// constant travel frame: instead of rotating the world's compass on a turn, the road is
    /// laid out head-to-tail in a stable "road space" (each tile bends the road by its
    /// curveAngle), and every frame the chain is re-placed so the player's current point on the
    /// road sits at the world origin facing +Z. So the player always rides "forward" while the
    /// world curves around them — a turn is just a curve tile, no axis snap, no re-centring, and
    /// nothing for the rest of the game (which projects onto the now-constant RoadDirection axes)
    /// to desync against. Sequences are still chosen through the weighted tag grammar; all tiles
    /// come from per-prefab pools built at Start.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class RoadSequencer : MonoBehaviour, IRewindable
    {
        public static RoadSequencer Instance { get; private set; }

        [SerializeField] private RoadSequence openingSequence;
        [SerializeField] private RoadSequence[] sequences;
        [Tooltip("The relay checkpoint tile (charge station). Appended to the road's end when the timer expires; " +
                 "leave empty to end the session on the Finish path instead.")]
        [SerializeField] private RoadTile checkpointTile;
        [Tooltip("Road is kept built this far ahead of the player.")]
        [SerializeField] private float spawnHorizon = 160f;
        [SerializeField] private float despawnBehind = 35f;
        [SerializeField] private int poolSizePerTile = 4;

        [Header("Sharp-curve overlap guard")]
        [Tooltip("A tile whose |curveAngle| reaches this many degrees counts as a \"sharp\" curve for the overlap " +
                 "guard. Gentle bends below this are always laid out as authored.")]
        [SerializeField] private float sharpCurveAngleThreshold = 45f;
        [Tooltip("Minimum metres of road kept between two sharp curves. While a sharp curve sits closer than this " +
                 "behind the build cursor, the next sharp curve is laid out flat (softened to straight) so two sharp " +
                 "bends can never be live within one draw distance and fold the road over itself. " +
                 "Keep this >= Spawn Horizon (the draw distance).")]
        [SerializeField] private float minSharpCurveSpacing = 160f;

        public RoadSequence CurrentSequence { get; private set; }

        /// <summary>The live checkpoint tile once it has capped the road, else null. CheckpointController brakes into its StopPosition.</summary>
        public RoadTile ActiveCheckpointTile { get; private set; }

        /// <summary>True when a checkpoint tile is configured, so timer expiry can route to the checkpoint instead of finishing.</summary>
        public bool HasCheckpointTile => checkpointTile != null;

        /// <summary>Signed degrees the road bends across the tile under the player right now (for hazard/traffic spawn gating).</summary>
        public float CurrentTileCurveAngle { get; private set; }

        /// <summary>Largest signed curveAngle of any tile within the spawn horizon ahead — lets spawners pause through a bend.</summary>
        public float UpcomingCurveAngle { get; private set; }

        /// <summary>How far the player has travelled along the road centreline this turn, in metres of road. This is
        /// the road's own odometer (it is rewound, unlike WorldSpeed.DistanceTravelled). Spawners place objects
        /// relative to this so placement shares the road's frame and stays consistent across a rewind.</summary>
        public float PlayerArc => playerArc;

        private readonly Dictionary<RoadTile, ObjectPool<RoadTile>> pools =
            new Dictionary<RoadTile, ObjectPool<RoadTile>>();
        private readonly List<RoadTile> activeTiles = new List<RoadTile>(16);
        private readonly Queue<RoadTile> prefabQueue = new Queue<RoadTile>(16);
        private readonly List<RoadTile> tileEnqueueBuffer = new List<RoadTile>(16);
        private readonly Dictionary<RoadSequence, long> lastUsedAtTile = new Dictionary<RoadSequence, long>();

        // Tiles on screen when a rewind began, plus scratch space for putting their prefabs back on the
        // queue once the rewind lands — see HandleRewindStarted / RequeueRewoundTiles.
        private readonly List<RoadTile> preRewindActiveTiles = new List<RoadTile>(16);
        private readonly List<RoadTile> requeueBuffer = new List<RoadTile>(16);

        private long tilesSpawnedTotal;

        // The road is measured purely by arc length (metres of road). The player rides forward along
        // it (playerArc grows with the world speed); the build cursor sits at the far end of the chain.
        private float playerArc;          // how far the player has travelled along the road
        private float buildArc;           // arc length at the end of the last built tile (the build cursor)
        private Vector3 buildRoadPosition; // road-space position of the build cursor
        private Quaternion buildRoadRotation = Quaternion.identity; // road-space heading of the build cursor
        private bool buildHalted;         // stop appending road once the checkpoint tile caps the chain (Req §9.2)

        // Cached each RenderChain so other systems can map a point on the road (arc + lateral) into the same
        // player-anchored world frame the tiles are placed in — see TryGetRoadPose. Lets hazards ride the curve.
        private Vector3 anchorRoadPosition;                         // road-space position of the player's current point
        private Quaternion anchorRoadInverse = Quaternion.identity; // inverse of that point's heading (road space → world)
        private bool chainRendered;                                 // false until RenderChain has placed the chain at least once

        // Arc-length (StartArc) of the last tile actually ridden as a sharp curve. The overlap guard keeps
        // the next sharp curve at least minSharpCurveSpacing of road past this, so two sharp bends are never
        // live within one draw distance. NegativeInfinity = none placed yet, so the first sharp curve is free.
        private float lastSharpCurveArc = float.NegativeInfinity;

        // Hard cap on tiles built in one pass — a backstop if a tile ever fails to add road length
        // (a zero-length tile) so the build loop can never freeze the game.
        private const int MaxTilesPerFill = 128;
        private const float StraightEpsilon = 0.01f; // |curveAngle| below this is treated as a straight tile

        private void Awake() => Instance = this;

        private void Start()
        {
            BuildPoolsFor(openingSequence);
            for (int i = 0; i < sequences.Length; i++)
                BuildPoolsFor(sequences[i]);
            if (checkpointTile != null)
                EnsurePool(checkpointTile);

            // The player's arc-length is part of the rewind state, so a rewind across a curve
            // resumes from the right point on the road (the constant frame means there is no
            // compass to restore — just this one number).
            if (RewindSystem.Instance != null)
                RewindSystem.Instance.Register(this);
        }

        private void OnEnable()
        {
            GameEvents.SessionReset += HandleSessionReset;
            GameEvents.RewindStarted += HandleRewindStarted;
            GameEvents.RewindCompleted += HandleRewindCompleted;
        }

        private void OnDisable()
        {
            GameEvents.SessionReset -= HandleSessionReset;
            GameEvents.RewindStarted -= HandleRewindStarted;
            GameEvents.RewindCompleted -= HandleRewindCompleted;
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (state != GameState.Playing && state != GameState.AtCheckpoint)
                return;

            // The player advanced this many metres along the road this frame (M1).
            playerArc += WorldSpeed.Instance.Current * Time.deltaTime;

            DespawnBehind();
            BuildAhead();
            RenderChain();
        }

        // ---- Road-space geometry -----------------------------------------------------

        /// <summary>
        /// Pose at arc-length <paramref name="u"/> into a tile, in road space. A straight tile runs
        /// along its heading; a curve tile follows a circular arc of total turn <c>curveAngle</c> over
        /// its length, so the heading and position bend smoothly (no exit anchor needed — the shape is
        /// fully defined by length + curveAngle).
        /// </summary>
        private static void EvaluatePose(RoadTile tile, float u, out Vector3 position, out Quaternion rotation)
        {
            // EffectiveCurveAngle, not the authored curveAngle: a curve softened by the overlap guard
            // must lay out (and therefore chain) as a straight, or the geometry would disagree with the guard.
            if (Mathf.Abs(tile.EffectiveCurveAngle) < StraightEpsilon || tile.length <= 0.0001f)
            {
                position = tile.RoadPosition + tile.RoadRotation * new Vector3(0f, 0f, u);
                rotation = tile.RoadRotation;
                return;
            }

            float phi = tile.EffectiveCurveAngle * Mathf.Deg2Rad;   // total signed turn across the tile
            float theta = (u / tile.length) * phi;          // turn taken so far
            float radius = tile.length / phi;               // signed radius (sign carries left/right)
            float localX = radius * (1f - Mathf.Cos(theta));
            float localZ = radius * Mathf.Sin(theta);
            position = tile.RoadPosition + tile.RoadRotation * new Vector3(localX, 0f, localZ);
            rotation = tile.RoadRotation * Quaternion.AngleAxis(theta * Mathf.Rad2Deg, Vector3.up);
        }

        /// <summary>The active tile whose span contains <paramref name="arc"/>, clamped to the ends of the chain.</summary>
        private RoadTile TileAtArc(float arc, out float localU)
        {
            for (int i = 0; i < activeTiles.Count; i++)
            {
                RoadTile tile = activeTiles[i];
                if (arc >= tile.StartArc && arc < tile.StartArc + tile.length)
                {
                    localU = arc - tile.StartArc;
                    return tile;
                }
            }

            if (activeTiles.Count == 0)
            {
                localU = 0f;
                return null;
            }

            RoadTile first = activeTiles[0];
            if (arc < first.StartArc)
            {
                localU = 0f;
                return first;
            }
            RoadTile last = activeTiles[activeTiles.Count - 1];
            localU = last.length;
            return last;
        }

        /// <summary>
        /// Places every active tile so the player's current point on the road is at the world origin
        /// facing +Z. As the player rides through a curve tile, that anchor pose rotates, swinging the
        /// whole world around the stationary player — which is the turn.
        /// </summary>
        private void RenderChain()
        {
            RoadTile anchorTile = TileAtArc(playerArc, out float anchorU);
            if (anchorTile == null)
                return;

            EvaluatePose(anchorTile, anchorU, out Vector3 anchorPos, out Quaternion anchorRot);
            Quaternion inverse = Quaternion.Inverse(anchorRot);

            // Cache the player-anchored mapping so TryGetRoadPose can place hazards on the same curved frame.
            anchorRoadPosition = anchorPos;
            anchorRoadInverse = inverse;
            chainRendered = true;

            for (int i = 0; i < activeTiles.Count; i++)
            {
                RoadTile tile = activeTiles[i];
                tile.transform.SetPositionAndRotation(
                    inverse * (tile.RoadPosition - anchorPos),
                    inverse * tile.RoadRotation);
            }

            // Publish the live bend so the camera bank, the scooter lean and the collision grace can read it.
            // Effective, not authored — a softened curve is ridden straight, so nothing should bank into it.
            CurrentTileCurveAngle = anchorTile.EffectiveCurveAngle;
            float degPerMetre = anchorTile.length > 0.0001f ? anchorTile.EffectiveCurveAngle / anchorTile.length : 0f;
            RoadDirection.SetCurveRate(degPerMetre * WorldSpeed.Instance.Current);

            UpcomingCurveAngle = SharpestCurveWithin(spawnHorizon);
        }

        /// <summary>
        /// The sharpest signed curveAngle of any tile within <paramref name="metresAhead"/> of the player
        /// (including the tile under them). TrafficSpawner calls this to pause placing traffic through a
        /// bend — on a curve the road leaves the straight +Z spawn line, so a car placed there would float
        /// off the road. (Hazards instead ride the curve via <see cref="TryGetRoadPose"/>, so they no longer
        /// pause.) Returns 0 on a clear straight stretch.
        /// </summary>
        public float SharpestCurveWithin(float metresAhead)
        {
            float sharpest = 0f;
            float limit = playerArc + metresAhead;
            for (int i = 0; i < activeTiles.Count; i++)
            {
                RoadTile tile = activeTiles[i];
                if (tile.StartArc + tile.length < playerArc || tile.StartArc > limit)
                    continue;
                // Effective, not authored: a softened curve is straight road, so spawners may resume on it.
                if (Mathf.Abs(tile.EffectiveCurveAngle) > Mathf.Abs(sharpest))
                    sharpest = tile.EffectiveCurveAngle;
            }
            return sharpest;
        }

        /// <summary>
        /// Maps a point on the road — <paramref name="arc"/> metres along the centreline plus a
        /// <paramref name="lateral"/> offset (road-local +X, the player's right) — to its current world pose,
        /// using the very same player-anchored mapping that places the tiles in <see cref="RenderChain"/>. This
        /// lets hazards (and any point object) ride the curve exactly instead of a straight +Z line (M1, M3).
        /// Returns false until the chain has been rendered once, so callers can fall back.
        /// </summary>
        public bool TryGetRoadPose(float arc, float lateral, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            if (!chainRendered || activeTiles.Count == 0)
                return false;

            RoadTile tile = TileAtArc(arc, out float u);
            if (tile == null)
                return false;

            EvaluatePose(tile, u, out Vector3 roadPos, out Quaternion roadRot);
            // Lateral rides the road's local right, so a lane offset follows the bend instead of staying on +X.
            roadPos += roadRot * new Vector3(lateral, 0f, 0f);
            // Same transform RenderChain applies to every tile, so the hazard lands exactly on the tile surface.
            worldPosition = anchorRoadInverse * (roadPos - anchorRoadPosition);
            worldRotation = anchorRoadInverse * roadRot;
            return true;
        }

        // ---- Chain building ----------------------------------------------------------

        /// <summary>Builds road forward until at least <see cref="spawnHorizon"/> metres of road sit ahead of the player.</summary>
        private void BuildAhead()
        {
            int built = 0;
            while (!buildHalted && buildArc - playerArc < spawnHorizon)
            {
                int before = activeTiles.Count;
                SpawnNextTile();
                if (activeTiles.Count == before || ++built >= MaxTilesPerFill)
                    break;
            }
        }

        /// <summary>Releases the oldest tile once it sits fully <see cref="despawnBehind"/> metres of road behind the player.</summary>
        private void DespawnBehind()
        {
            while (activeTiles.Count > 1 &&
                   playerArc - (activeTiles[0].StartArc + activeTiles[0].length) >= despawnBehind)
            {
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

            // Stamp the tile's place in road space, then advance the build cursor to this tile's exit.
            tile.RoadPosition = buildRoadPosition;
            tile.RoadRotation = buildRoadRotation;
            tile.StartArc = buildArc;
            tile.EffectiveCurveAngle = ResolveCurveAngle(tile);
            tile.gameObject.SetActive(true);
            activeTiles.Add(tile);

            if (tile.IsCheckpoint)
                ActiveCheckpointTile = tile;

            EvaluatePose(tile, tile.length, out buildRoadPosition, out buildRoadRotation);
            buildArc += tile.length;
            tilesSpawnedTotal++;
        }

        /// <summary>
        /// The bend this tile should actually be laid out with — the sharp-curve overlap guard
        /// (README "Known limitations"). Two sharp bends inside one draw distance can fold the road
        /// over itself, and there is no other in-engine guard. A "sharp" tile (|curveAngle| at or above
        /// <see cref="sharpCurveAngleThreshold"/>) is ridden flat whenever the last sharp curve sits less
        /// than <see cref="minSharpCurveSpacing"/> of road behind the build cursor, so at most one sharp
        /// bend is ever live within the draw distance. The first sharp curve, gentle bends and straights
        /// always pass through unchanged. Spacing is measured start-to-start in arc length, which is
        /// robust to tiles of differing length.
        /// </summary>
        private float ResolveCurveAngle(RoadTile tile)
        {
            bool isSharp = Mathf.Abs(tile.curveAngle) >= sharpCurveAngleThreshold;
            if (isSharp && buildArc - lastSharpCurveArc < minSharpCurveSpacing)
                return 0f; // soften to straight: a sharp bend is already too close behind

            if (isSharp)
                lastSharpCurveArc = buildArc; // this one rides as a curve; the next must clear the spacing
            return tile.curveAngle;
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

        /// <summary>
        /// Caps the road with the checkpoint tile (Req §9.2): clears any pending tiles, appends the
        /// checkpoint at the current chain end, and halts further building so nothing spawns past it.
        /// The player keeps riding into it; CheckpointController brakes to a stop at the tile's marker.
        /// Called once by CheckpointController when the timer expires.
        /// </summary>
        public void SpawnCheckpoint()
        {
            if (checkpointTile == null || ActiveCheckpointTile != null)
                return;

            prefabQueue.Clear();
            buildHalted = false; // let this one spawn through
            prefabQueue.Enqueue(checkpointTile);
            SpawnNextTile();      // appends the checkpoint at the chain end and sets ActiveCheckpointTile
            buildHalted = true;   // nothing builds past the checkpoint
        }

        private void HandleSessionReset()
        {
            for (int i = 0; i < activeTiles.Count; i++)
                activeTiles[i].SourcePool.Release(activeTiles[i]);
            activeTiles.Clear();
            prefabQueue.Clear();
            preRewindActiveTiles.Clear(); // drop any snapshot from a rewind cut short by the reset
            lastUsedAtTile.Clear();
            tilesSpawnedTotal = 0;

            // Road space starts at the origin facing +Z, half a despawn gap behind the player so the
            // first tile already extends under the player. The player rides forward from arc 0.
            playerArc = 0f;
            buildArc = -despawnBehind * 0.5f;
            buildRoadPosition = new Vector3(0f, 0f, buildArc);
            buildRoadRotation = Quaternion.identity;
            buildHalted = false;
            lastSharpCurveArc = float.NegativeInfinity; // first sharp curve of the run is unguarded
            ActiveCheckpointTile = null;
            CurrentTileCurveAngle = 0f;
            UpcomingCurveAngle = 0f;
            RoadDirection.SetCurveRate(0f);

            // Opening is forced, never weighted (Req §4.3).
            CurrentSequence = openingSequence;
            lastUsedAtTile[openingSequence] = 0;
            EnqueueTiles(openingSequence);

            BuildAhead();
            RenderChain(); // place the road for the Ready state before play begins
        }

        private void HandleRewindStarted()
        {
            // Snapshot the tiles on screen at the moment the rewind begins. The rewind will switch off any
            // of these that were spawned during the rewound window (they were not active at the frame play
            // resumes from); RequeueRewoundTiles then puts their prefabs back so the replay is identical.
            preRewindActiveTiles.Clear();
            preRewindActiveTiles.AddRange(activeTiles);
        }

        private void HandleRewindCompleted()
        {
            // The rewind toggled tile actives/positions directly; rebuild the chain bookkeeping from the
            // actual scene state. playerArc was restored from the rewind buffer (ApplySample below); the
            // road-space pose of each tile is stable (stamped at spawn), so we just re-derive the order
            // and the build cursor.
            foreach (ObjectPool<RoadTile> pool in pools.Values)
                pool.ReconcileAvailability();

            activeTiles.Clear();
            foreach (ObjectPool<RoadTile> pool in pools.Values)
                for (int i = 0; i < pool.AllInstances.Count; i++)
                    if (pool.AllInstances[i].gameObject.activeInHierarchy)
                        activeTiles.Add(pool.AllInstances[i]);

            activeTiles.Sort((a, b) => a.StartArc.CompareTo(b.StartArc));

            // Re-derive the overlap-guard cursor from the tiles that survived the rewind. Rewinding
            // backward shrinks buildArc, so a lastSharpCurveArc recorded further ahead would be stale;
            // the surviving tiles keep their (pooling-stable) EffectiveCurveAngle, so the last sharp
            // one among them — they are now sorted, so the highest StartArc wins — is the truth.
            lastSharpCurveArc = float.NegativeInfinity;
            for (int i = 0; i < activeTiles.Count; i++)
                if (Mathf.Abs(activeTiles[i].EffectiveCurveAngle) >= sharpCurveAngleThreshold)
                    lastSharpCurveArc = activeTiles[i].StartArc;

            if (activeTiles.Count > 0)
            {
                RoadTile last = activeTiles[activeTiles.Count - 1];
                EvaluatePose(last, last.length, out buildRoadPosition, out buildRoadRotation);
                buildArc = last.StartArc + last.length;
            }

            // Put the prefabs consumed during the rewound window back at the front of the queue, so the
            // road replays the identical upcoming tiles instead of jumping to the next ones in the
            // sequence (README "Known limitations": rewind vs tile queue).
            RequeueRewoundTiles();

            RenderChain();
        }

        /// <summary>
        /// Restores the prefab-sequence position after a rewind. A tile that was on screen when the rewind
        /// began (<see cref="preRewindActiveTiles"/>) but is now switched off was spawned during the rewound
        /// window, so its prefab had been dequeued from <see cref="prefabQueue"/>. Those prefabs are pushed
        /// back to the FRONT of the queue, near-to-far, so the road re-spawns the identical tiles in the
        /// identical order. Tiles that stayed active were consumed before the resume point and are left
        /// alone. Any refill those window tiles triggered (a new sequence) is already physically carried in
        /// the surviving queue, so no sequence selection is re-run.
        /// </summary>
        private void RequeueRewoundTiles()
        {
            requeueBuffer.Clear();
            // preRewindActiveTiles is in ascending StartArc order, so the window-spawned tiles come out
            // near-to-far — exactly the order they should re-spawn in.
            for (int i = 0; i < preRewindActiveTiles.Count; i++)
            {
                RoadTile tile = preRewindActiveTiles[i];
                if (tile != null && tile.SourcePool != null && !tile.gameObject.activeInHierarchy)
                    requeueBuffer.Add(tile.SourcePool.Prefab);
            }
            preRewindActiveTiles.Clear();
            if (requeueBuffer.Count == 0)
                return;

            // Queue has no push-front: append the queue's current tail after the re-queued prefabs, then
            // refill the (now drained) queue from the buffer so the window prefabs sit at the front.
            while (prefabQueue.Count > 0)
                requeueBuffer.Add(prefabQueue.Dequeue());
            for (int i = 0; i < requeueBuffer.Count; i++)
                prefabQueue.Enqueue(requeueBuffer[i]);
        }

        // ---- Rewind (player arc-length) ---------------------------------------------

        public void CaptureSample(ref RewindSample sample)
        {
            sample.Position = Vector3.zero;
            sample.Rotation = Quaternion.identity;
            sample.Aux = playerArc;
            sample.Active = true;
        }

        public void ApplySample(in RewindSample sample) => playerArc = sample.Aux;

        // ---- Pools -------------------------------------------------------------------

        private void BuildPoolsFor(RoadSequence sequence)
        {
            if (sequence == null || sequence.tiles == null)
                return;

            for (int i = 0; i < sequence.tiles.Length; i++)
                EnsurePool(sequence.tiles[i]);
        }

        /// <summary>Builds the pool for one tile prefab if it does not already have one (shared by sequences and the checkpoint tile).</summary>
        private void EnsurePool(RoadTile prefab)
        {
            if (prefab == null || pools.ContainsKey(prefab))
                return;

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
