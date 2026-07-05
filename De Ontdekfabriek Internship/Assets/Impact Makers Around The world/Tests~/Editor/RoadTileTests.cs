using NUnit.Framework;
using UnityEngine;
using KenyaScooter.Roads;

namespace KenyaScooter.RoadsTests
{
    /// <summary>
    /// EditMode tests for the point-based RoadTile (2026-07-05): the road is authored as PLACED POINTS —
    /// begin point → per-turn START/END ball pairs → exit point — and everything (bend angles, tile length,
    /// lean rates) is derived from where the points sit. These lock the geometry contract: straight
    /// begin→exit with no turns, a turn bending exactly between its two balls by the angle its placement
    /// implies, multiple turns chaining, the driven line arriving at the exit point, the lean being
    /// non-zero only inside a turn, the checkpoint stop point, and the hazard allow-list.
    /// </summary>
    public sealed class RoadTileTests
    {
        private const float Tol = 1e-2f;
        private const float GeoTol = 0.3f; // the turn curve is sampled, so end-point drift up to ~centimetres

        private GameObject go;
        private RoadTile tile;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("road tile under test");
            tile = go.AddComponent<RoadTile>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        private static RoadTile.Turn T(Vector3 start, Vector3 end) =>
            new RoadTile.Turn { start = start, end = end };

        /// <summary>The driven line's position at u metres in — what should land on the authored points.
        /// Tests run at root scale 1, so metres space and local space coincide.</summary>
        private Vector3 LocalAt(float u)
        {
            tile.EvaluateRun(u, out Vector3 run, out _);
            return tile.RunToMetres(run);
        }

        // ---- Straight tiles: begin → exit ---------------------------------------------------------------

        [Test]
        public void NoTurns_RoadRunsStraightFromBeginToExit()
        {
            tile.beginPoint = new Vector3(2f, 0f, 5f);
            tile.exitPoint = new Vector3(2f, 0f, 125f);
            tile.EvaluateRun(0f, out _, out _); // builds the path, derives length

            Assert.That(tile.length, Is.EqualTo(120f).Within(Tol), "length is the begin-to-exit distance");
            Vector3 mid = LocalAt(60f);
            Assert.That(mid.x, Is.EqualTo(2f).Within(Tol), "the line starts at the begin point");
            Assert.That(mid.z, Is.EqualTo(65f).Within(Tol), "and advances toward the exit point");
            Assert.That((LocalAt(tile.length) - tile.exitPoint).magnitude, Is.LessThan(Tol),
                "the road ends exactly at the exit point");
        }

        [Test]
        public void ExitPoint_CanAimTheRoadSideways()
        {
            tile.beginPoint = Vector3.zero;
            tile.exitPoint = new Vector3(70.7107f, 0f, 70.7107f); // 45° off to the right
            tile.EvaluateRun(0f, out _, out _);
            Assert.That((LocalAt(tile.length) - tile.exitPoint).magnitude, Is.LessThan(Tol),
                "a straight tile drives wherever the exit point is placed");
        }

        // ---- Turns: a START ball and an END ball each ----------------------------------------------------

        [Test]
        public void OneTurn_BendsBetweenItsTwoBalls()
        {
            // Straight up to the green ball at z=50, bend to the red ball, leave aimed at the exit (right).
            tile.beginPoint = Vector3.zero;
            tile.turns = new[] { T(new Vector3(0f, 0f, 50f), new Vector3(10f, 0f, 60f)) };
            tile.exitPoint = new Vector3(60f, 0f, 60f);
            tile.EvaluateRun(0f, out _, out _);

            Assert.That(tile.SharpestBendDegrees, Is.EqualTo(90f).Within(1f),
                "the placement (in +Z, out +X) implies a 90° right turn");
            tile.EvaluateRun(tile.length, out _, out Quaternion endRot);
            Assert.That(Quaternion.Angle(endRot, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(1f),
                "the heading leaves the tile turned by the implied angle");
            Assert.That((LocalAt(tile.length) - tile.exitPoint).magnitude, Is.LessThan(GeoTol),
                "and the road still ends at the exit point");
        }

        [Test]
        public void TurnStart_IsWhereTheGreenBallSits()
        {
            tile.beginPoint = Vector3.zero;
            tile.turns = new[] { T(new Vector3(0f, 0f, 50f), new Vector3(10f, 0f, 60f)) };
            tile.exitPoint = new Vector3(60f, 0f, 60f);
            tile.EvaluateRun(0f, out _, out _);

            Assert.That((LocalAt(50f) - new Vector3(0f, 0f, 50f)).magnitude, Is.LessThan(GeoTol),
                "50 driven metres in, the road is exactly at the green ball");
            Assert.That(tile.CurveDegreesPerMetreAt(25f), Is.EqualTo(0f).Within(Tol),
                "and it is dead straight before that");
        }

        [Test]
        public void LeftTurn_ComesFromThePlacement()
        {
            tile.beginPoint = Vector3.zero;
            tile.turns = new[] { T(new Vector3(0f, 0f, 50f), new Vector3(-10f, 0f, 60f)) };
            tile.exitPoint = new Vector3(-60f, 0f, 60f); // exit off to the LEFT
            tile.EvaluateRun(0f, out _, out _);
            Assert.That(tile.SharpestBendDegrees, Is.EqualTo(-90f).Within(1f), "left = negative, from geometry");
        }

        [Test]
        public void BallsOnTheStraightLine_AddNoBend()
        {
            tile.beginPoint = Vector3.zero;
            tile.turns = new[] { T(new Vector3(0f, 0f, 40f), new Vector3(0f, 0f, 60f)) }; // dead on the line
            tile.exitPoint = new Vector3(0f, 0f, 120f);
            tile.EvaluateRun(0f, out _, out _);
            Assert.IsFalse(tile.HasTurn, "a turn whose balls sit on the straight line does nothing");
            Assert.That(tile.length, Is.EqualTo(120f).Within(0.05f));
        }

        [Test]
        public void TwoTurns_ChainIntoAnS()
        {
            tile.beginPoint = Vector3.zero;
            tile.turns = new[]
            {
                T(new Vector3(0f, 0f, 30f), new Vector3(10f, 0f, 45f)),
                T(new Vector3(20f, 0f, 60f), new Vector3(30f, 0f, 75f))
            };
            tile.exitPoint = new Vector3(30f, 0f, 120f);
            tile.EvaluateRun(0f, out _, out _);

            tile.EvaluateRun(tile.length, out _, out Quaternion endRot);
            Assert.That(Quaternion.Angle(endRot, Quaternion.identity), Is.LessThan(1f),
                "the second turn hands the road back to the original heading");
            Assert.That((LocalAt(tile.length) - tile.exitPoint).magnitude, Is.LessThan(GeoTol),
                "and the S still lands on the exit point");
        }

        // ---- What the lean and the spawners read ---------------------------------------------------------

        [Test]
        public void CurveRate_IsNonZeroOnlyBetweenTheBalls()
        {
            tile.beginPoint = Vector3.zero;
            tile.turns = new[] { T(new Vector3(0f, 0f, 50f), new Vector3(10f, 0f, 60f)) };
            tile.exitPoint = new Vector3(60f, 0f, 60f);
            tile.EvaluateRun(0f, out _, out _);

            Assert.That(tile.CurveDegreesPerMetreAt(20f), Is.EqualTo(0f).Within(Tol), "upright before the green ball");
            Assert.That(Mathf.Abs(tile.CurveDegreesPerMetreAt(55f)), Is.GreaterThan(0.5f), "leaning inside the turn");
            Assert.That(tile.CurveDegreesPerMetreAt(tile.length - 5f), Is.EqualTo(0f).Within(Tol),
                "upright again on the run-out to the exit");
        }

        // ---- Checkpoint stop point ------------------------------------------------------------------------

        [Test]
        public void StopPosition_IsTheAuthoredStopPointInWorldSpace()
        {
            go.transform.position = new Vector3(10f, 0f, -4f);
            tile.isCheckpoint = true;
            tile.stopPoint = new Vector3(3f, 0f, 12f);
            Assert.That((tile.StopPosition - new Vector3(13f, 0f, 8f)).magnitude, Is.LessThan(Tol),
                "the scooter rests exactly where the purple ball was placed");
        }

        // ---- Hazard permission ------------------------------------------------------------------------------

        [Test]
        public void EmptyHazardList_AllowsEverything()
        {
            Assert.IsTrue(tile.AllowsHazard(null), "no list means no restriction");
        }

        [Test]
        public void FilledHazardList_AllowsOnlyItsEntries()
        {
            var allowed = ScriptableObject.CreateInstance<KenyaScooter.Config.HazardSpawnConfig>();
            var forbidden = ScriptableObject.CreateInstance<KenyaScooter.Config.HazardSpawnConfig>();
            try
            {
                tile.allowedHazards = new[] { allowed };
                Assert.IsTrue(tile.AllowsHazard(allowed), "listed hazard may spawn");
                Assert.IsFalse(tile.AllowsHazard(forbidden), "unlisted hazard may not");
            }
            finally
            {
                Object.DestroyImmediate(allowed);
                Object.DestroyImmediate(forbidden);
            }
        }
    }
}
