using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.SafetyNet;
using KenyaScooter.Session;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// Builds and rides the endless road (M1, M3, M23, M24). Rebuilt clean 2026-07-04: a tile IS its
    /// <see cref="RoadTile"/> — length, begin/exit points and a list of turns — and this sequencer does
    /// exactly one job with it: lay tiles head-to-tail (each tile's BEGIN point attached to the previous
    /// tile's EXIT point) in a stable "road space", then re-place the chain every frame so the player's
    /// current point on the road sits at the world origin facing +Z. The world bends around the stationary
    /// player — that is the turn. No turn components, no schedulers, no junction gates, no silent curve
    /// softening: what is authored on the tiles is what is ridden.
    ///
    /// Sequences are chosen through the weighted tag grammar; all tiles come from per-prefab pools built
    /// at Start. The player's arc-length is the one number the rewind restores.
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

        [Header("Facilitator zone bias (2026-06-28)")]
        [Tooltip("How strongly a zone chosen in the settings menu (OMGEVING > Omgeving kiezen) is preferred. It " +
                 "MULTIPLIES that zone's selection weight rather than hard-locking it, so the Journey Arc still " +
                 "surfaces other zones now and then. Higher = the chosen zone dominates more. Has no effect while the " +
                 "menu choice is AUTOMATISCH (the default).")]
        [SerializeField] private float zoneBiasMultiplier = 8f;

        public RoadSequence CurrentSequence { get; private set; }

        /// <summary>The live checkpoint tile once it has capped the road, else null. CheckpointController brakes into its StopPosition.</summary>
        public RoadTile ActiveCheckpointTile { get; private set; }

        /// <summary>True when a checkpoint tile is configured, so timer expiry can route to the checkpoint instead of finishing.</summary>
        public bool HasCheckpointTile => checkpointTile != null;

        /// <summary>How far the player has travelled along the road centreline this turn, in metres of road. This is
        /// the road's own odometer (it is rewound, unlike WorldSpeed.DistanceTravelled). Spawners place objects
        /// relative to this so placement shares the road's frame and stays consistent across a rewind.</summary>
        public float PlayerArc => playerArc;

        // ---- Facilitator zone bias (2026-06-28) --------------------------------------
        // A scalar bridge between the settings menu and the weighted grammar: the menu writes a chosen zone index
        // here, EligibleWeight reads it live and weights that zone up.

        /// <summary>PlayerPrefs key for the facilitator's chosen zone bias: 0 = AUTOMATISCH (pure grammar),
        /// 1..N = strongly prefer the Nth serialized sequence. Written by SettingsCatalog "env.zone", read by EligibleWeight.</summary>
        public const string ZoneBiasPrefKey = "ksg.zoneBias";

        /// <summary>PlayerPrefs key under which the count of selectable zones is published each run, so the settings
        /// menu (built before this scene loads) can size its zone picker from the previous run's count.</summary>
        public const string ZoneCountPrefKey = "ksg.zoneCount";

        /// <summary>PlayerPrefs key for the REGIO-REIS mode (2026-07-05, Ryan's design): 1 = the serialized
        /// sequences array is ridden as an ORDERED ROUTE of regions (1 → 2 → 3 → …, wrapping), so the world
        /// visibly changes as the ride progresses and every region keeps its own tiles. 0 (default) = the
        /// weighted random Journey-Arc grammar. Authoring the route = ordering the sequences on this
        /// component. Written by SettingsCatalog "env.regionJourney".</summary>
        public const string RegionJourneyPrefKey = "ksg.regionJourney";

        /// <summary>How many zones a facilitator can bias towards — the serialized grammar pool. The opening
        /// sequence is excluded: it is forced at the start of every run (Req §4.3), so it is not a pickable zone.</summary>
        public int SelectableZoneCount => sequences != null ? sequences.Length : 0;

        /// <summary>Display name of the 1-based selectable zone for the settings-menu label (its zoneName, falling
        /// back to the asset name); empty string if the index is out of range.</summary>
        public string SelectableZoneName(int oneBasedIndex)
        {
            int i = oneBasedIndex - 1;
            if (sequences == null || i < 0 || i >= sequences.Length || sequences[i] == null)
                return "";
            return string.IsNullOrEmpty(sequences[i].zoneName) ? sequences[i].name : sequences[i].zoneName;
        }

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

        // Hard cap on tiles built in one pass — a backstop if a tile ever fails to add road length
        // (a zero-length tile) so the build loop can never freeze the game.
        private const int MaxTilesPerFill = 128;

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

            // Publish how many zones the menu's "Omgeving kiezen" picker may offer. The settings catalog is built
            // before this scene loads, so it sizes the picker from the value written here on the PREVIOUS run; the
            // first ever run falls back to a small default. Write only when it actually changed.
            if (PlayerPrefs.GetInt(ZoneCountPrefKey, -1) != SelectableZoneCount)
            {
                PlayerPrefs.SetInt(ZoneCountPrefKey, SelectableZoneCount);
                PlayerPrefs.Save();
            }
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
        /// Pose at arc-length <paramref name="u"/> into a tile, in road space: the tile's own driven line
        /// (<see cref="RoadTile.EvaluateRun"/>) chained off the road-space pose its begin point was stamped
        /// with at spawn.
        /// </summary>
        private static void EvaluatePose(RoadTile tile, float u, out Vector3 position, out Quaternion rotation)
        {
            tile.EvaluateRun(u, out Vector3 runPos, out Quaternion runRot);
            position = tile.RoadPosition + tile.RoadRotation * runPos;
            rotation = tile.RoadRotation * runRot;
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

        /// <summary>The active tile under road-metre <paramref name="arc"/>, or null before the road exists.
        /// HazardSpawner asks this so a tile's own allowed-hazards list decides what may spawn on it.</summary>
        public RoadTile TileAt(float arc)
        {
            if (activeTiles.Count == 0)
                return null;
            return TileAtArc(arc, out _);
        }

        /// <summary>
        /// Places every active tile so the player's current point on the road is at the world origin
        /// facing +Z. Each tile is positioned by its BEGIN point (the point the chain attached), so tiles
        /// always seam begin-to-exit. As the player rides through a turn, the anchor pose rotates, swinging
        /// the whole world around the stationary player — which is the turn.
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
                // The tile transform maps its LOCAL space to the world such that its begin point lands on the
                // stamped road pose and its driven line lies along the road (RoadTile.ArtFacing). The begin
                // point is taken in real METRES (the authored point scaled by the root), so scaled tile art
                // (the ×20 ground tiles) anchors correctly.
                Quaternion rot = inverse * tile.RoadRotation * Quaternion.Inverse(tile.ArtFacing);
                Vector3 pos = inverse * (tile.RoadPosition - anchorPos) - rot * tile.BeginPointMetres;
                tile.transform.SetPositionAndRotation(pos, rot);
            }

            // Publish the live bend so the camera bank and the scooter lean can read it: the per-metre bend
            // at the player's exact point — non-zero only between a turn's green and red ball.
            RoadDirection.SetCurveRate(anchorTile.CurveDegreesPerMetreAt(anchorU) * WorldSpeed.Instance.Current);

            // Publish the surface under the tyres the same way, so the dirt-road feel (DirtRumble, the dust
            // boost) eases in and out with the tile actually being ridden.
            RoadSurfaceFeel.Publish(anchorTile.surface, Time.deltaTime);
        }

        /// <summary>
        /// The sharpest signed turn (degrees) on any tile within <paramref name="metresAhead"/> of the player
        /// (including the tile under them). Spawners call this to pause placing objects through a bend.
        /// Returns 0 on a clear straight stretch.
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
                float tileCurve = tile.SharpestBendDegrees;
                if (Mathf.Abs(tileCurve) > Mathf.Abs(sharpest))
                    sharpest = tileCurve;
            }
            return sharpest;
        }

        /// <summary>
        /// The nearest authored turn that BEGINS ahead of the player within <paramref name="metresAhead"/> and
        /// bends at least <paramref name="minSwingDegrees"/>: its distance from the player (out) and signed
        /// swing (out; + = the road bends right, − = left). Gentle kinks below the threshold are ignored so the
        /// telegraph only fires for turns a player actually has to read. Returns false on clear straight road.
        /// TurnTelegraph calls this each frame to warn the player before a bend arrives.
        /// </summary>
        public bool TryGetTurnAhead(float metresAhead, float minSwingDegrees, out float distance, out float swingDegrees)
        {
            distance = 0f;
            swingDegrees = 0f;
            float limit = playerArc + metresAhead;
            float nearestArc = float.MaxValue;
            for (int i = 0; i < activeTiles.Count; i++)
            {
                RoadTile tile = activeTiles[i];
                if (tile.StartArc > limit || tile.StartArc + tile.length < playerArc)
                    continue; // tile is wholly beyond the window, or wholly behind the player

                // Only consider turns starting at or after the player's own point on this tile.
                float fromU = Mathf.Max(0f, playerArc - tile.StartArc);
                if (!tile.TryGetNextTurn(fromU, minSwingDegrees, out float startMetre, out float swing))
                    continue;

                float turnArc = tile.StartArc + startMetre;
                if (turnArc < playerArc || turnArc > limit || turnArc >= nearestArc)
                    continue;

                nearestArc = turnArc;
                distance = turnArc - playerArc;
                swingDegrees = swing;
            }
            return nearestArc < float.MaxValue;
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

            if (!pools.ContainsKey(prefab))
                EnsurePool(prefab);
            RoadTile tile = pools[prefab].Get();

            // Stamp the tile's place in road space: its BEGIN point sits at the build cursor, then the
            // cursor advances to its driven end — which is its EXIT point. Tiles line up by construction.
            tile.RoadPosition = buildRoadPosition;
            tile.RoadRotation = buildRoadRotation;
            tile.StartArc = buildArc;

            // If the tile generates its road surface (CurvedRoadMesh), rebuild it so the painted road is the
            // exact line the tile drives. Tiles with hand-modelled road meshes are untouched.
            CurvedRoadMesh curvedMesh = tile.GetComponent<CurvedRoadMesh>();
            if (curvedMesh != null)
                curvedMesh.BuildFromTile(tile);

            tile.gameObject.SetActive(true);
            activeTiles.Add(tile);

            if (tile.isCheckpoint)
                ActiveCheckpointTile = tile;

            EvaluatePose(tile, tile.length, out buildRoadPosition, out buildRoadRotation);
            buildArc += tile.length;
            tilesSpawnedTotal++;
        }

        private void AdvanceSequence()
        {
            RoadSequence next = RegionJourneyEnabled ? PickNextRegion() : PickNextSequence();
            CurrentSequence = next;
            lastUsedAtTile[next] = tilesSpawnedTotal;

            EnqueueTiles(next);

            GameEvents.RaiseSequenceChanged(next);
        }

        // ---- Regio-reis: the sequences array as an ordered route (2026-07-05) ---------
        // Instead of the weighted random mix, the player TRAVERSES the regions in the order they are
        // listed on this component — region 1, then 2, then 3 … wrapping at the end — so the environment
        // genuinely changes along the ride and each region contributes its own tiles. A region that wants
        // to last longer simply lists more tiles. Every turn starts back at region 1 (predictable for the
        // relay: every student gets the same journey). The facilitator zone bias is ignored in this mode —
        // the authored order IS the choice.

        private int regionIndex; // next region to ride in Regio-reis mode (index into sequences, pre-wrap)

        private bool RegionJourneyEnabled =>
            PlayerPrefs.GetInt(RegionJourneyPrefKey, 0) == 1 && sequences != null && sequences.Length > 0;

        private RoadSequence PickNextRegion()
        {
            for (int step = 0; step < sequences.Length; step++) // skip null/empty entries defensively
            {
                RoadSequence candidate = sequences[regionIndex % sequences.Length];
                regionIndex++;
                if (candidate != null && candidate.tiles != null && candidate.tiles.Length > 0)
                    return candidate;
            }
            return CurrentSequence != null ? CurrentSequence : openingSequence; // no usable region at all
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
                    if (tiles[i] != null) // a sequence slot whose prefab was deleted must never reach the pools
                        prefabQueue.Enqueue(tiles[i]);
                return;
            }

            tileEnqueueBuffer.Clear();
            for (int i = 0; i < tiles.Length; i++)
                if (tiles[i] != null)
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

            // Facilitator soft zone bias: a zone chosen in the settings menu is weighted up rather than hard-locked,
            // so the Journey Arc still surfaces other zones occasionally. Read live so a menu change applies at the
            // next sequence pick; 0 / out of range = AUTOMATISCH (no bias). The cooldown above still applies, so even
            // a biased zone never repeats back-to-back — it dominates the rotation without becoming the only zone.
            int biasedZone = PlayerPrefs.GetInt(ZoneBiasPrefKey, 0);
            if (biasedZone >= 1 && biasedZone <= sequences.Length && sequences[biasedZone - 1] == candidate)
                weight *= zoneBiasMultiplier;

            return weight;
        }

        // ---- Session flow ------------------------------------------------------------

        /// <summary>
        /// Caps the road with the checkpoint tile (Req §9.2): clears any pending tiles, appends the
        /// checkpoint at the current chain end, and halts further building so nothing spawns past it.
        /// The player keeps riding into it; CheckpointController brakes to a stop at the tile's stop point.
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
            ActiveCheckpointTile = null;
            RoadDirection.SetCurveRate(0f);
            RoadSurfaceFeel.Reset(); // a new group's turn starts on tarmac, not the previous run's blend

            // Opening is forced, never weighted (Req §4.3).
            CurrentSequence = openingSequence;
            lastUsedAtTile[openingSequence] = 0;
            regionIndex = 0; // Regio-reis: every turn rides the route from region 1 — same journey for every student
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

        public void ApplySample(in RewindSample sample)
        {
            playerArc = sample.Aux;
            // Keep the player-anchored curve mapping live as the road rewinds. RenderChain (which normally caches
            // it) does not run while the game is Rewinding, so without this the anchor would stay frozen at the
            // crash point and anything that re-derives its pose from road space during the rewind — the traffic —
            // would drift off the rewinding road. The tiles' own road-space data is stable through pooling, so the
            // mapping is well-defined for any restored arc. RoadSequencer registers before the vehicles (execution
            // order -50), so this runs first in the rewind apply pass and they read the fresh anchor.
            CacheAnchorAtPlayer();
        }

        /// <summary>Recomputes the cached player-anchor mapping (road space → world) from the current playerArc,
        /// without moving any tile. Shared by the rewind apply path; RenderChain computes the same values inline
        /// while it also re-places the tiles.</summary>
        private void CacheAnchorAtPlayer()
        {
            RoadTile anchorTile = TileAtArc(playerArc, out float anchorU);
            if (anchorTile == null)
                return;
            EvaluatePose(anchorTile, anchorU, out Vector3 anchorPos, out Quaternion anchorRot);
            anchorRoadPosition = anchorPos;
            anchorRoadInverse = Quaternion.Inverse(anchorRot);
            chainRendered = true;
        }

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
