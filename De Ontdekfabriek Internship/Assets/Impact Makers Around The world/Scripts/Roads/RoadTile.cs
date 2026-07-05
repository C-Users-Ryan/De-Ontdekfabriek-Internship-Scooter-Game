using System.Collections.Generic;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;
using KenyaScooter.SafetyNet;

namespace KenyaScooter.Roads
{
    /// <summary>Tile difficulty metadata (Req §4.1) — used by designers when authoring sequences.</summary>
    public enum TileDifficulty { Clear, Low, Medium, High }

    /// <summary>What the road on a tile is made of (2026-07-05). Dirt = a murram road: the scooter visual
    /// rumbles (DirtRumble), every dust layer kicks up more (WeatherConfig.dirtDustMultiplier), and a
    /// CurvedRoadMesh tile tints its strip red-brown. Pure feel + FX — speed, scoring and steering are
    /// identical on both surfaces, so a dirt zone is atmosphere, never a difficulty knob.</summary>
    public enum RoadSurfaceType { Paved, Dirt }

    /// <summary>
    /// THE road tile. Everything a tile is, is defined right here — and the road is authored with POINTS
    /// you place in the tile, not with numbers:
    ///
    ///  • BEGIN POINT (blue ball) — where the road enters the tile.
    ///  • TURNS — each turn is a PAIR of points: the GREEN ball is where the turn STARTS, the RED ball is
    ///    where it ENDS. Place both anywhere in the tile: the road runs straight to the green ball, curves
    ///    from green to red, and leaves the red ball heading for the next turn's green ball (or the exit).
    ///    Add as many turns as you want, in driving order.
    ///  • EXIT POINT (orange ball) — where the road leaves the tile. The next tile attaches here.
    ///  • STOP POINT (purple ball, checkpoint tiles only) — where the scooter rests at the charge station.
    ///
    /// How much each turn bends falls out of where its two balls sit — there are no angle or metre fields.
    /// Length is measured from the points automatically. Select the tile and every ball is visible AND
    /// draggable in the Scene view (RoadTileEditor); the cyan line is exactly what the scooter will drive.
    /// The sequencer, the generated road mesh, the scooter lean and spawn gating all read this tile's own
    /// evaluator, so what you see is what you ride.
    /// </summary>
    public sealed class RoadTile : MonoBehaviour, IRewindable
    {
        /// <summary>One turn: it STARTS at the green ball and ENDS at the red ball. The road arrives straight
        /// at the start, bends between the two points, and leaves the end aimed at whatever comes next.</summary>
        [System.Serializable]
        public struct Turn
        {
            [Tooltip("Where the turn STARTS — the GREEN ball. The road runs straight until it reaches this point.")]
            public Vector3 start;
            [Tooltip("Where the turn ENDS — the RED ball. The bend finishes here, aimed at the next point / the exit.")]
            public Vector3 end;
        }

        [Header("Tile")]
        [Tooltip("Metres of driven road on this tile — measured automatically from the points (begin → turns → exit).")]
        public float length = 30f;
        public TileDifficulty difficulty = TileDifficulty.Clear;
        [Tooltip("What this tile's road is made of. DIRT = a murram road: the ride rumbles, vehicles kick up " +
                 "more dust, and a generated (CurvedRoadMesh) road strip is tinted red-brown automatically. " +
                 "Feel and FX only — speed and scoring stay exactly the same as on tarmac.")]
        public RoadSurfaceType surface = RoadSurfaceType.Paved;

        [Header("Hazards")]
        [Tooltip("Which hazards MAY spawn on this tile. Leave EMPTY to allow all hazards. Drag in the " +
                 "HazardSpawnConfig assets (pothole, rock, speed bump, ...) you want to permit here.")]
        public HazardSpawnConfig[] allowedHazards;

        [Header("The road, as points (drag the balls in the Scene view)")]
        [Tooltip("BEGIN POINT (blue ball): where the road enters the tile. The game attaches this point to " +
                 "the previous tile's exit point, so tiles line up perfectly.")]
        public Vector3 beginPoint = Vector3.zero;
        [Tooltip("EXIT POINT (orange ball): where the road leaves the tile. Always yours to place — the road " +
                 "runs begin → turns → here, and the next tile attaches here.")]
        public Vector3 exitPoint = new Vector3(0f, 0f, 30f);
        [Tooltip("The turns, in driving order. Each has a START (green ball) and an END (red ball) — place " +
                 "both anywhere in the tile; the road bends between them and is straight everywhere else.")]
        public Turn[] turns;

        [Header("Checkpoint (charge station) — optional")]
        [Tooltip("Tick if this tile is the relay checkpoint tile: the world brakes to a stop here.")]
        public bool isCheckpoint;
        [Tooltip("STOP POINT (purple ball): where the scooter comes to rest at the charge station. Drag it " +
                 "in the Scene view like the other points.")]
        public Vector3 stopPoint = Vector3.zero;

        [System.NonSerialized] public ObjectPool<RoadTile> SourcePool;

        // Road-space identity, written by RoadSequencer when the tile is appended to the chain. The road is
        // laid out head-to-tail in a stable "road space" and re-placed every frame relative to the player,
        // so a turn bends the road around the player. These survive pooling.
        [System.NonSerialized] public Vector3 RoadPosition;     // road-space position of this tile's begin point
        [System.NonSerialized] public Quaternion RoadRotation;  // road-space heading at the begin point
        [System.NonSerialized] public float StartArc;           // cumulative road metres at this tile's begin

        // ---- The driven line, built from the points --------------------------------------------------------
        // The path is a list of straights and small arc pieces in TILE LOCAL space, rebuilt whenever the
        // points change (editor) and once per instance at runtime (the authored points never change in play).

        private struct PathSegment
        {
            public float startMetre;     // cumulative metres at this segment's start
            public float length;         // metres of this segment
            public Vector3 startPos;     // local position at segment start
            public Quaternion startRot;  // local heading at segment start
            public float degrees;        // 0 = straight; else the signed bend across this piece
        }

        private struct TurnInfo       // one authored turn's place on the driven line, for gating and gizmos
        {
            public float startMetre;
            public float endMetre;
            public float swingDegrees; // the turn's largest signed heading swing away from its entry heading
        }

        [System.NonSerialized] private PathSegment[] pathSegments;
        [System.NonSerialized] private TurnInfo[] turnInfos;
        [System.NonSerialized] private float pathLength;
        [System.NonSerialized] private Quaternion pathFacing = Quaternion.identity; // heading of the first segment
        [System.NonSerialized] private bool pathBuilt;

        // NaN until the points have been synced once. Lets the first-ever build recognise a pre-rebuild
        // prefab (real length, factory-default points) and push the exit point out instead of shrinking the
        // tile; afterwards it just remembers the last derived length so typing a new one can stretch a
        // straight tile.
        [SerializeField, HideInInspector] private float syncedLength = float.NaN;

        private const int PiecesPerTurn = 16; // curve smoothness; built once, so cost is irrelevant

        private static readonly List<PathSegment> buildBuffer = new List<PathSegment>(64);
        private static readonly List<TurnInfo> infoBuffer = new List<TurnInfo>(8);

        private void Awake() => EnsurePath(); // length/heading are correct before the sequencer reads them

        private void EnsurePath()
        {
            if (!pathBuilt)
                BuildPath();
        }

        // ---- Scale awareness --------------------------------------------------------------------------------
        // Tile art often lives on a SCALED root (the ground tiles are ×20), so a point stored in local space
        // is much further away in the world. All road maths runs in real WORLD METRES: authored points are
        // scaled up on the way in, and converted back when something needs the tile's local space.

        private Vector3 RootScale
        {
            get
            {
                Vector3 s = transform.localScale;
                return new Vector3(
                    Mathf.Abs(s.x) < 0.0001f ? 1f : s.x,
                    Mathf.Abs(s.y) < 0.0001f ? 1f : s.y,
                    Mathf.Abs(s.z) < 0.0001f ? 1f : s.z);
            }
        }

        private Vector3 ToMetres(Vector3 localPoint) => Vector3.Scale(localPoint, RootScale);

        /// <summary>A metres-space point converted back into the tile's (possibly scaled) local space — what
        /// you would type into a point field.</summary>
        public Vector3 MetresToTileLocal(Vector3 metres)
        {
            Vector3 s = RootScale;
            return new Vector3(metres.x / s.x, metres.y / s.y, metres.z / s.z);
        }

        /// <summary>A metres-space point in world space, for gizmos and handles (position + rotation only —
        /// metres are already real size, so the transform's scale must not touch them).</summary>
        public Vector3 MetresToWorld(Vector3 metres) => transform.position + transform.rotation * metres;

        /// <summary>The begin point in real metres — the sequencer anchors the tile's transform with this.</summary>
        public Vector3 BeginPointMetres => ToMetres(beginPoint);

        /// <summary>
        /// Builds the driven line from the points: straight to each turn's START (green ball), a smooth
        /// curve from START to END (red ball) that leaves aimed at the next point, straight again, and a
        /// final straight into the exit point. Also derives the tile's driven <see cref="length"/>.
        /// </summary>
        private void BuildPath()
        {
            pathBuilt = true;

            // Migration (first build ever, sentinel NaN): a prefab saved before the points model carries a
            // real length but factory-default points — trust the LENGTH (it was authored in world metres)
            // and push the exit point out, so a 200 m tile can never shrink to the 30 m default exit.
            if (float.IsNaN(syncedLength) && (turns == null || turns.Length == 0))
            {
                Vector3 migrate = Flat(ToMetres(exitPoint) - ToMetres(beginPoint));
                float migrateDist = migrate.magnitude;
                if (length > migrateDist + 0.01f)
                    exitPoint = beginPoint + MetresToTileLocal(
                        (migrateDist > 0.001f ? migrate / migrateDist : Vector3.forward) * length);
            }

            buildBuffer.Clear();
            infoBuffer.Clear();

            // Everything below is in real WORLD METRES (authored points scaled by the root), so a ball on a
            // ×20 ground tile counts its true distance and tiles space out correctly.
            Vector3 beginM = ToMetres(beginPoint);
            float y = beginM.y;                 // the road is flat: everything rides at the begin point's height
            Vector3 exit = FlatTo(ToMetres(exitPoint), y);
            Vector3 cursor = beginM;
            Quaternion headingRot = Quaternion.identity;
            Vector3 heading = Vector3.forward;
            bool headingKnown = false;
            float metres = 0f;

            int turnCount = turns != null ? turns.Length : 0;
            for (int i = 0; i < turnCount; i++)
            {
                Vector3 start = FlatTo(ToMetres(turns[i].start), y);
                Vector3 end = FlatTo(ToMetres(turns[i].end), y);
                if ((end - start).sqrMagnitude < 0.25f)
                    continue; // start and end on top of each other: not a turn, skip the row

                // Straight up to the turn's START (green ball).
                Vector3 toStart = Flat(start - cursor);
                if (toStart.sqrMagnitude > 0.0001f)
                {
                    heading = toStart.normalized;
                    headingRot = Quaternion.LookRotation(heading);
                    headingKnown = true;
                    AddStraight(ref metres, ref cursor, headingRot, toStart.magnitude);
                }
                else if (!headingKnown)
                {
                    heading = Flat(end - start).normalized; // turn starts right at the begin point
                    headingRot = Quaternion.LookRotation(heading);
                    headingKnown = true;
                }

                // Where should the turn LEAVE towards? The next turn's start ball, or the exit point.
                Vector3 nextTarget = exit;
                for (int j = i + 1; j < turnCount; j++)
                {
                    Vector3 candidate = FlatTo(ToMetres(turns[j].start), y);
                    if ((FlatTo(ToMetres(turns[j].end), y) - candidate).sqrMagnitude >= 0.25f)
                    {
                        nextTarget = candidate;
                        break;
                    }
                }
                Vector3 outDir = Flat(nextTarget - end);
                Vector3 exitTangent = outDir.sqrMagnitude > 0.0001f ? outDir.normalized : heading;

                EmitCurve(ref metres, ref cursor, ref headingRot, ref heading, start, end, exitTangent);
            }

            // Final straight into the exit point.
            Vector3 toExit = Flat(exit - cursor);
            if (toExit.sqrMagnitude > 0.0001f)
            {
                headingRot = Quaternion.LookRotation(toExit.normalized);
                if (!headingKnown)
                    headingKnown = true;
                AddStraight(ref metres, ref cursor, headingRot, toExit.magnitude);
            }

            if (buildBuffer.Count == 0)
            {
                // No usable points at all: a half-metre stub so the chain maths never divides by zero.
                buildBuffer.Add(new PathSegment
                {
                    startMetre = 0f, length = 0.5f, startPos = beginM,
                    startRot = Quaternion.identity, degrees = 0f
                });
                metres = 0.5f;
            }

            pathSegments = buildBuffer.ToArray();
            turnInfos = infoBuffer.ToArray();
            pathLength = metres;
            pathFacing = pathSegments[0].startRot;
            length = pathLength; // the tile's length IS the driven line through the points
            syncedLength = pathLength;
        }

        /// <summary>
        /// The curve of one turn: a smooth Hermite bend from <paramref name="start"/> (entered along the
        /// current heading) to <paramref name="end"/> (leaving along <paramref name="exitTangent"/>),
        /// emitted as small arc pieces so the ride, the lean and the drawn road all follow it exactly.
        /// </summary>
        private void EmitCurve(ref float metres, ref Vector3 cursor, ref Quaternion headingRot, ref Vector3 heading,
            Vector3 start, Vector3 end, Vector3 exitTangent)
        {
            float chord = Vector3.Distance(start, end);
            Vector3 m0 = heading * chord;
            Vector3 m1 = exitTangent * chord;

            float turnStartMetre = metres;
            float swing = 0f;      // largest signed heading deviation from the entry heading
            float cumulative = 0f; // running signed bend across the pieces

            Vector3 prevTangent = heading;
            for (int p = 0; p < PiecesPerTurn; p++)
            {
                float tA = (float)p / PiecesPerTurn;
                float tB = (float)(p + 1) / PiecesPerTurn;
                Vector3 sampleA = Hermite(start, m0, end, m1, tA);
                Vector3 sampleB = Hermite(start, m0, end, m1, tB);
                float pieceChord = Vector3.Distance(sampleA, sampleB);
                if (pieceChord < 0.001f)
                    continue;

                Vector3 tangentB = Flat(HermiteTangent(start, m0, end, m1, tB));
                Vector3 nextTangent = tangentB.sqrMagnitude > 0.0001f ? tangentB.normalized : prevTangent;

                float bend = Vector3.SignedAngle(prevTangent, nextTangent, Vector3.up);
                float bendRad = Mathf.Abs(bend) * Mathf.Deg2Rad;
                // The arc length whose chord matches the sample spacing, so the pieces chain seamlessly.
                float pieceLength = bendRad < 0.0001f ? pieceChord
                    : pieceChord * (bendRad * 0.5f) / Mathf.Sin(bendRad * 0.5f);

                buildBuffer.Add(new PathSegment
                {
                    startMetre = metres, length = pieceLength, startPos = cursor,
                    startRot = headingRot, degrees = bend
                });
                metres += pieceLength;
                EvaluateSegment(buildBuffer[buildBuffer.Count - 1], pieceLength, out cursor, out headingRot);
                prevTangent = nextTangent;

                cumulative += bend;
                if (Mathf.Abs(cumulative) > Mathf.Abs(swing))
                    swing = cumulative;
            }

            heading = prevTangent;
            infoBuffer.Add(new TurnInfo
            {
                startMetre = turnStartMetre,
                endMetre = metres,
                swingDegrees = swing
            });
        }

        private static Vector3 Hermite(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * p0 + (t3 - 2f * t2 + t) * m0
                 + (-2f * t3 + 3f * t2) * p1 + (t3 - t2) * m1;
        }

        private static Vector3 HermiteTangent(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float t)
        {
            float t2 = t * t;
            return (6f * t2 - 6f * t) * p0 + (3f * t2 - 4f * t + 1f) * m0
                 + (-6f * t2 + 6f * t) * p1 + (3f * t2 - 2f * t) * m1;
        }

        private static void AddStraight(ref float metres, ref Vector3 cursor, Quaternion rot, float len)
        {
            if (len <= 0.001f)
                return;
            buildBuffer.Add(new PathSegment
            {
                startMetre = metres, length = len, startPos = cursor, startRot = rot, degrees = 0f
            });
            metres += len;
            cursor += rot * new Vector3(0f, 0f, len);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static Vector3 FlatTo(Vector3 v, float y)
        {
            v.y = y;
            return v;
        }

        // ---- Evaluation (what everything else samples) ------------------------------------------------------

        /// <summary>
        /// Pose at <paramref name="u"/> metres into the driven road, in run space (u = 0 at the begin point,
        /// initial heading +Z). This is the ONE function that defines the tile's shape: the sequencer chains
        /// tiles with it, the road mesh samples it, the lean/bank read its bend.
        /// </summary>
        public void EvaluateRun(float u, out Vector3 position, out Quaternion rotation)
        {
            EnsurePath();
            EvaluateLocal(u, out Vector3 localPos, out Quaternion localRot);
            Quaternion inv = Quaternion.Inverse(pathFacing);
            position = inv * (localPos - PathOrigin);
            rotation = inv * localRot;
        }

        private Vector3 PathOrigin => pathSegments[0].startPos;

        // Local-space pose along the built path; u past the end continues straight from the exit.
        private void EvaluateLocal(float u, out Vector3 position, out Quaternion rotation)
        {
            u = Mathf.Max(0f, u);
            for (int i = 0; i < pathSegments.Length; i++)
            {
                PathSegment seg = pathSegments[i];
                if (u <= seg.startMetre + seg.length || i == pathSegments.Length - 1)
                {
                    EvaluateSegment(seg, u - seg.startMetre, out position, out rotation);
                    return;
                }
            }
            PathSegment last = pathSegments[pathSegments.Length - 1];
            EvaluateSegment(last, u - last.startMetre, out position, out rotation);
        }

        private static void EvaluateSegment(PathSegment seg, float local, out Vector3 position, out Quaternion rotation)
        {
            if (Mathf.Abs(seg.degrees) < 0.0001f || seg.length <= 0.0001f)
            {
                position = seg.startPos + seg.startRot * new Vector3(0f, 0f, local);
                rotation = seg.startRot;
                return;
            }
            float phi = seg.degrees * Mathf.Deg2Rad;      // total signed bend across the piece
            float theta = (Mathf.Min(local, seg.length) / seg.length) * phi;
            float radius = seg.length / phi;              // signed radius (sign carries left/right)
            Vector3 arcPos = new Vector3(radius * (1f - Mathf.Cos(theta)), 0f, radius * Mathf.Sin(theta));
            position = seg.startPos + seg.startRot * arcPos;
            rotation = seg.startRot * Quaternion.AngleAxis(theta * Mathf.Rad2Deg, Vector3.up);
            if (local > seg.length) // straight overshoot past the piece (only ever the final segment)
                position += rotation * new Vector3(0f, 0f, local - seg.length);
        }

        /// <summary>The rotation of the run's first segment — how the driven line lies in the tile's local
        /// space. The sequencer uses it to place the tile so its begin point and entry heading land on the
        /// chain.</summary>
        public Quaternion ArtFacing
        {
            get { EnsurePath(); return pathFacing; }
        }

        /// <summary>A run-space point mapped into the tile's METRES space (the inverse of what
        /// <see cref="EvaluateRun"/> does). Convert onward with <see cref="MetresToWorld"/> for gizmos or
        /// <see cref="MetresToTileLocal"/> for local-space vertices/points.</summary>
        public Vector3 RunToMetres(Vector3 runPosition)
        {
            EnsurePath();
            return PathOrigin + pathFacing * runPosition;
        }

        /// <summary>True when any authored turn actually bends the road.</summary>
        public bool HasTurn
        {
            get
            {
                EnsurePath();
                for (int i = 0; i < turnInfos.Length; i++)
                    if (Mathf.Abs(turnInfos[i].swingDegrees) >= 1f)
                        return true;
                return false;
            }
        }

        /// <summary>Degrees the road bends per metre at <paramref name="u"/> — non-zero only inside a turn
        /// (between its green and red ball), so the scooter leans and the camera banks exactly through each
        /// turn (easing in and out with the curve) and is upright on the straights.</summary>
        public float CurveDegreesPerMetreAt(float u)
        {
            EnsurePath();
            for (int i = 0; i < pathSegments.Length; i++)
            {
                PathSegment seg = pathSegments[i];
                if (u >= seg.startMetre && u <= seg.startMetre + seg.length)
                    return Mathf.Abs(seg.degrees) < 0.0001f || seg.length <= 0.0001f ? 0f : seg.degrees / seg.length;
            }
            return 0f;
        }

        /// <summary>The signed heading swing of this tile's sharpest turn (0 for a straight tile) — used by
        /// the spawners to hold traffic through an upcoming bend. An S-shaped turn reports its biggest
        /// deviation, not its (cancelling) net angle.</summary>
        public float SharpestBendDegrees
        {
            get
            {
                EnsurePath();
                float sharpest = 0f;
                for (int i = 0; i < turnInfos.Length; i++)
                    if (Mathf.Abs(turnInfos[i].swingDegrees) > Mathf.Abs(sharpest))
                        sharpest = turnInfos[i].swingDegrees;
                return sharpest;
            }
        }

        /// <summary>The tightest bend radius on this tile (metres), or float.MaxValue when straight — the
        /// road mesh caps its strip width at this so a sharp curve can never fold its inner edge.</summary>
        public float MinBendRadius()
        {
            EnsurePath();
            float minRadius = float.MaxValue;
            for (int i = 0; i < pathSegments.Length; i++)
            {
                PathSegment seg = pathSegments[i];
                if (Mathf.Abs(seg.degrees) < 0.0001f || seg.length <= 0.0001f)
                    continue;
                minRadius = Mathf.Min(minRadius, Mathf.Abs(seg.length / (seg.degrees * Mathf.Deg2Rad)));
            }
            return minRadius;
        }

        /// <summary>
        /// The nearest authored turn whose START lies at or beyond <paramref name="fromU"/> run-metres and
        /// bends at least <paramref name="minAbsSwingDegrees"/> — its start metre and signed swing (out;
        /// + bends the road to the player's right, − to the left). Turns already begun, or too gentle to be
        /// worth reading, are skipped. Returns false when no such turn remains on this tile. TurnTelegraph
        /// chains this across the active tiles to find the next bend to warn about.
        /// </summary>
        public bool TryGetNextTurn(float fromU, float minAbsSwingDegrees, out float startMetre, out float swingDegrees)
        {
            EnsurePath();
            startMetre = 0f;
            swingDegrees = 0f;
            float nearest = float.MaxValue;
            for (int i = 0; i < turnInfos.Length; i++)
            {
                if (turnInfos[i].startMetre < fromU || turnInfos[i].startMetre >= nearest)
                    continue;
                if (Mathf.Abs(turnInfos[i].swingDegrees) < minAbsSwingDegrees)
                    continue;
                nearest = turnInfos[i].startMetre;
                startMetre = turnInfos[i].startMetre;
                swingDegrees = turnInfos[i].swingDegrees;
            }
            return nearest < float.MaxValue;
        }

        /// <summary>Appends the metre marks where each turn begins and ends (its green and red ball), so the
        /// road mesh can put samples exactly on them.</summary>
        public void AppendTurnBoundaries(List<float> metres)
        {
            EnsurePath();
            for (int i = 0; i < turnInfos.Length; i++)
            {
                metres.Add(turnInfos[i].startMetre);
                metres.Add(turnInfos[i].endMetre);
            }
        }

        // ---- Checkpoint -----------------------------------------------------------------------------------

        /// <summary>World position where the scooter comes to rest on a checkpoint tile — the authored stop
        /// point (purple ball), wherever this tile currently sits in the world.</summary>
        public Vector3 StopPosition => transform.TransformPoint(stopPoint);

        // ---- Hazards ---------------------------------------------------------------------------------------

        /// <summary>True when <paramref name="hazard"/> may spawn on this tile. An empty list allows everything,
        /// so tiles do not need wiring to behave as before.</summary>
        public bool AllowsHazard(HazardSpawnConfig hazard)
        {
            if (allowedHazards == null || allowedHazards.Length == 0)
                return true;
            for (int i = 0; i < allowedHazards.Length; i++)
                if (allowedHazards[i] == hazard)
                    return true;
            return false;
        }

        // ---- Rewind ----------------------------------------------------------------------------------------

        public void CaptureSample(ref RewindSample sample)
        {
            sample.Position = transform.position;
            sample.Rotation = transform.rotation;
            sample.Aux = 0f;
            sample.Active = gameObject.activeInHierarchy;
        }

        public void ApplySample(in RewindSample sample)
        {
            transform.SetPositionAndRotation(sample.Position, sample.Rotation);
            if (gameObject.activeSelf != sample.Active)
                gameObject.SetActive(sample.Active);
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: re-derives the path (and length) after the points change — the scene-view
        /// handles call this so dragging a ball obeys exactly the same rules as typing the numbers.</summary>
        public void EditorValidate() => OnValidate();

        private void OnValidate()
        {
            // Typing a new Length on a STRAIGHT tile stretches it: the exit point is pushed out along the
            // road. (With turns, length is measured from the points and typing it has no effect.)
            if ((turns == null || turns.Length == 0) && !float.IsNaN(syncedLength)
                && Mathf.Abs(length - syncedLength) > 0.001f)
            {
                Vector3 dirM = Flat(ToMetres(exitPoint) - ToMetres(beginPoint));
                float distM = dirM.magnitude;
                exitPoint = beginPoint + MetresToTileLocal(
                    (distM > 0.001f ? dirM / distM : Vector3.forward) * Mathf.Max(0.5f, length));
            }

            // A freshly added turn row (both balls at zero) lands ON the tile's visible art: the green start
            // ball on the current road line, the red end ball ahead and off to the side — a visible bend to
            // grab immediately.
            if (turns != null)
            {
                for (int i = 0; i < turns.Length; i++)
                {
                    if (turns[i].start == Vector3.zero && turns[i].end == Vector3.zero)
                    {
                        Turn t = turns[i];
                        DefaultTurnPlacement(out t.start, out t.end);
                        turns[i] = t;
                    }
                }
            }

            // A checkpoint tile whose stop point was never placed gets it mid-road, visible and draggable.
            if (isCheckpoint && stopPoint == Vector3.zero)
            {
                pathBuilt = false;
                EnsurePath();
                EvaluateRun(pathLength * 0.5f, out Vector3 mid, out _);
                stopPoint = MetresToTileLocal(RunToMetres(mid));
            }

            pathBuilt = false; // points may have changed: rebuild (also re-derives `length`)
            EnsurePath();
        }

        // Where a brand-new turn lands: its green start ball on the current road line at the middle of the
        // visible art, its red end ball further along and pushed sideways, so the new bend is immediately
        // visible and both balls are grabbable.
        private void DefaultTurnPlacement(out Vector3 start, out Vector3 end)
        {
            pathBuilt = false;
            EnsurePath();
            float mid = pathLength * 0.5f;
            if (TryGetArtSpanMetres(out float artMin, out float artMax) && artMax - artMin >= 2f)
                mid = Mathf.Clamp((artMin + artMax) * 0.5f, 0f, pathLength);

            float reach = Mathf.Clamp(pathLength * 0.15f, 6f, 20f);
            EvaluateRun(Mathf.Max(0f, mid - reach), out Vector3 startRun, out _);
            EvaluateRun(Mathf.Min(pathLength, mid + reach), out Vector3 endRun, out Quaternion endRot);
            start = MetresToTileLocal(RunToMetres(startRun));
            end = MetresToTileLocal(RunToMetres(endRun + endRot * new Vector3(reach, 0f, 0f)));
        }

        // The span (in run metres) covered by this tile's visible art, measured along the driven line — so
        // defaults land on the tile you SEE.
        private bool TryGetArtSpanMetres(out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return false;

            EnsurePath();
            Quaternion invFacing = Quaternion.Inverse(pathFacing);
            Matrix4x4 toLocal = transform.worldToLocalMatrix;
            Vector3 origin = PathOrigin;
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds b = renderers[i].bounds; // world AABB — conservative is fine for a default
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = new Vector3(
                        (c & 1) == 0 ? b.min.x : b.max.x,
                        (c & 2) == 0 ? b.min.y : b.max.y,
                        (c & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 local = toLocal.MultiplyPoint3x4(corner);
                    float metre = (invFacing * (ToMetres(local) - origin)).z; // scaled: real metres along the run
                    if (metre < min) min = metre;
                    if (metre > max) max = metre;
                }
            }
            return max > min + 1f;
        }

        // Select the tile to see the whole road: BLUE ball = begin point, GREEN/RED balls = each turn's
        // start and end, ORANGE ball = exit point, PURPLE ball = checkpoint stop point, CYAN line = exactly
        // what the scooter drives.
        private void OnDrawGizmosSelected()
        {
            pathBuilt = false; // always preview the current numbers, even mid-edit
            EnsurePath();
            float len = Mathf.Max(pathLength, 0.01f);

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            EvaluateRun(0f, out Vector3 prevRun, out _);
            Vector3 prev = MetresToWorld(RunToMetres(prevRun));
            const int steps = 96;
            for (int i = 1; i <= steps; i++)
            {
                EvaluateRun((float)i / steps * len, out Vector3 p, out _);
                Vector3 w = MetresToWorld(RunToMetres(p));
                Gizmos.DrawLine(prev, w);
                prev = w;
            }

            Gizmos.color = new Color(0.25f, 0.55f, 1f);
            Gizmos.DrawSphere(transform.TransformPoint(beginPoint), 1.4f);
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            Gizmos.DrawSphere(transform.TransformPoint(exitPoint), 1.4f);

            if (turns != null)
            {
                for (int i = 0; i < turns.Length; i++)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawSphere(transform.TransformPoint(turns[i].start), 1.2f);
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(transform.TransformPoint(turns[i].end), 1.2f);
                }
            }

            if (isCheckpoint)
            {
                Gizmos.color = new Color(0.75f, 0.35f, 1f);
                Gizmos.DrawSphere(transform.TransformPoint(stopPoint), 1.3f);
            }
        }
#endif
    }
}
