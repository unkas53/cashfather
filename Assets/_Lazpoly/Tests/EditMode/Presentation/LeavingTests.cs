using System;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// The owner's wish (2026-11-17): a fine animation for the buildings on a town when it is sold and when it is torn
    /// down. Sold, a building crouches, springs up turning once and is gone in a twinkle of gold; torn down, it trembles
    /// and falls in on itself in dust and rubble.
    /// </summary>
    public class LeavingTests
    {
        private const int Samples = 200;

        [Test]
        public void ASoldBuildingCrouchesSpringsUpTurningOnceAndIsGoneAtTheTop()
        {
            LeavingPose start = LeavingMotion.Sold(0f);
            AssertSame(Vector3.one, start.Scale, "it starts at its own size");
            Assert.AreEqual(0f, start.Lift, 1e-4f);
            Assert.AreEqual(0f, start.Turn, 1e-4f);
            Assert.AreEqual(0f, start.Tilt, 1e-4f);
            Assert.AreEqual(0f, start.Shake, 1e-4f);

            LeavingPose crouched = LeavingMotion.Sold(LeavingMotion.Crouch);
            Assert.AreEqual(1f - LeavingMotion.CrouchDepth, crouched.Scale.y, 1e-3f, "it crouches before it springs");
            Assert.Greater(crouched.Scale.x, 1f, "what it loses in height it gains in width");
            Assert.AreEqual(0f, crouched.Lift, 1e-3f, "it is still on its tile at the bottom of the crouch");

            LeavingPose end = LeavingMotion.Sold(1f);
            Assert.AreEqual(1f, end.Lift, 1e-4f);
            Assert.AreEqual(360f, end.Turn, 1e-2f, "it turns round exactly once, so it ends facing as it stood");
            Assert.LessOrEqual(end.Scale.y, LeavingMotion.Least + 1e-4f, "it is gone at the top");
            AssertSame(end.Scale, LeavingMotion.Sold(2f).Scale, "past its time it stays as it ended");

            LeavingPose before = start;
            for (int i = 1; i <= Samples; i++)
            {
                LeavingPose pose = LeavingMotion.Sold(i / (float)Samples);
                Assert.GreaterOrEqual(pose.Lift, before.Lift - 1e-5f, $"it never comes back down (sample {i})");
                Assert.GreaterOrEqual(pose.Turn, before.Turn - 1e-3f, $"it never turns back (sample {i})");
                Assert.Less(Mathf.Abs(pose.Lift - before.Lift), 0.03f, $"it jumps at sample {i}");
                Assert.Less((pose.Scale - before.Scale).magnitude, 0.08f, $"its size jumps at sample {i}");
                Assert.Greater(pose.Scale.x, 0f, "its size never turns to nothing while it is shown");
                Assert.Greater(pose.Scale.y, 0f, "its size never turns to nothing while it is shown");
                before = pose;
            }
        }

        [Test]
        public void ATornDownBuildingTremblesThenFallsInOnItselfWhereItStands()
        {
            LeavingPose start = LeavingMotion.Demolished(0f);
            AssertSame(Vector3.one, start.Scale, "it starts at its own size");
            Assert.AreEqual(0f, start.Shake, 1e-4f);
            Assert.AreEqual(0f, start.Tilt, 1e-4f);

            float left = 0f, right = 0f;
            LeavingPose before = start;
            for (int i = 1; i <= Samples; i++)
            {
                float t = i / (float)Samples;
                LeavingPose pose = LeavingMotion.Demolished(t);
                Assert.AreEqual(0f, pose.Lift, 1e-5f, "it never leaves the ground");
                Assert.AreEqual(0f, pose.Turn, 1e-5f, "it does not turn");
                Assert.LessOrEqual(Mathf.Abs(pose.Shake), 1f + 1e-4f);
                Assert.LessOrEqual(pose.Scale.y, before.Scale.y + 1e-5f, $"it never grows back (sample {i})");
                Assert.GreaterOrEqual(pose.Scale.y, LeavingMotion.Least - 1e-5f);
                Assert.Less(before.Scale.y - pose.Scale.y, 0.03f, $"it drops at once at sample {i}");
                if (t <= LeavingMotion.Tremble) AssertSame(Vector3.one, pose.Scale, "it keeps its size while it only trembles");
                left = Mathf.Min(left, pose.Shake);
                right = Mathf.Max(right, pose.Shake);
                before = pose;
            }

            Assert.Less(left, -0.5f, "it does not shake to one side");
            Assert.Greater(right, 0.5f, "it does not shake to the other side");

            LeavingPose end = LeavingMotion.Demolished(1f);
            Assert.AreEqual(LeavingMotion.Least, end.Scale.y, 1e-4f, "it is flat at the end");
            Assert.AreEqual(1f, end.Scale.x, 1e-3f, "the bulge is over");
            Assert.AreEqual(LeavingMotion.Lean, end.Tilt, 1e-3f);
            Assert.AreEqual(0f, end.Shake, 1e-3f, "the tremble has died away");
            Assert.Greater(LeavingMotion.Demolished(0.6f).Scale.x, 1f, "it bulges as it sinks");
        }

        [Test]
        public void EachLeavingHasItsTimeAndItsBurstStartsBeforeItIsOver()
        {
            Assert.AreEqual(LeavingMotion.SoldSeconds, LeavingMotion.Seconds(Leaving.Sold));
            Assert.AreEqual(LeavingMotion.DemolishedSeconds, LeavingMotion.Seconds(Leaving.Demolished));
            foreach (Leaving how in Enum.GetValues(typeof(Leaving)))
            {
                Assert.That(LeavingMotion.Seconds(how), Is.InRange(0.6f, 1.6f), $"{how}: short enough not to hold the game up, long enough to be seen");
                Assert.That(LeavingMotion.BurstAt(how), Is.InRange(0.1f, 0.9f), $"{how}: the burst flies while the building is still going");
                AssertSame(LeavingMotion.At(how, 0.5f).Scale, (how == Leaving.Sold ? LeavingMotion.Sold(0.5f) : LeavingMotion.Demolished(0.5f)).Scale, $"{how}");
            }

            Assert.Greater(LeavingMotion.UncoverAt, LeavingMotion.BurstAt(Leaving.Sold), "what stays behind shows after the twinkle starts");
            Assert.Less(LeavingMotion.UncoverAt, 1f);
        }

        [Test]
        public void OnlyTheDemolitionCardsTearDown()
        {
            foreach (CardKind kind in Enum.GetValues(typeof(CardKind)))
            {
                bool tears = kind == CardKind.Demolish || kind == CardKind.IllegalFloor;
                Assert.AreEqual(tears, CardPlay.TearsDown(kind), $"{kind}");
            }
        }

        [Test]
        public void APoseIsLaidOnTopOfWhereThePieceRests()
        {
            var piece = new GameObject("Leaving Test");
            try
            {
                var rest = new Vector3(1f, 2f, 3f);
                var size = new Vector3(2f, 2f, 2f);
                Quaternion facing = Quaternion.Euler(0f, 90f, 0f);
                const float width = 2f;

                BoardView.PoseLeaving(piece.transform, rest, size, facing, LeavingMotion.Sold(0f), width);
                AssertSame(rest, piece.transform.localPosition, "at the start it stands where it rests");
                AssertSame(size, piece.transform.localScale, "at the start it has its own size");
                Assert.Less(Quaternion.Angle(facing, piece.transform.localRotation), 0.1f);

                var pose = new LeavingPose { Lift = 0.5f, Turn = 90f, Shake = 1f, Scale = new Vector3(1f, 0.5f, 1f) };
                BoardView.PoseLeaving(piece.transform, rest, size, facing, pose, width);
                Vector3 side = facing * Vector3.right * (width * BoardView.LeaveShake);
                AssertSame(rest + Vector3.up * (0.5f * width * BoardView.LeaveLift) + side, piece.transform.localPosition, "lifted and shaken to its own side");
                AssertSame(new Vector3(2f, 1f, 2f), piece.transform.localScale, "its size along its own axes");
                Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 180f, 0f), piece.transform.localRotation), 0.1f, "turned about its own upright");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(piece);
            }
        }

        [Test]
        public void TheGoldTwinkleWaitsFliesOutAndClearsItselfAway()
        {
            var holder = new GameObject("Burst Test");
            try
            {
                var center = new Vector3(1f, 2f, 3f);
                const float size = 2f;
                LeavingBurst burst = LeavingBurst.Play(holder.transform, center, size, Leaving.Sold, Color.red, 0.5f);
                Assert.AreEqual(Leaving.Sold, burst.How);
                Assert.AreEqual(LeavingBurst.TwinkleSeconds, burst.Seconds);
                Assert.AreEqual(LeavingBurst.Twinkles, burst.Pieces.Count);
                foreach (Transform piece in burst.Pieces) Assert.IsFalse(piece.gameObject.activeSelf, "a twinkle shows before its time");

                burst.Step(0.25f);
                foreach (Transform piece in burst.Pieces) Assert.IsFalse(piece.gameObject.activeSelf, "a twinkle shows before its time");

                burst.Step(0.3f);
                float near = 0f;
                foreach (Transform piece in burst.Pieces)
                {
                    Assert.IsTrue(piece.gameObject.activeSelf, "a twinkle is hidden in its time");
                    Assert.Greater(piece.localScale.x, 0f);
                    near = Mathf.Max(near, (piece.localPosition - center).magnitude);
                }

                burst.Step(0.25f);
                float far = 0f;
                foreach (Transform piece in burst.Pieces) far = Mathf.Max(far, (piece.localPosition - center).magnitude);
                Assert.Greater(far, near, "the twinkles do not fly out");
                Assert.LessOrEqual(far, size * (LeavingBurst.TwinkleReach + 0.12f) + 1e-3f, "a twinkle flies past its reach");

                burst.Step(1f);
                Assert.IsTrue(burst == null, "the burst stays after its time");
                Assert.AreEqual(0, holder.transform.childCount, "the burst leaves something behind");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheDustRisesFromTheFootWithRubbleAndClearsItselfAway()
        {
            var holder = new GameObject("Burst Test");
            try
            {
                var center = new Vector3(0f, 0.5f, 0f);
                const float size = 2f;
                LeavingBurst burst = LeavingBurst.Play(holder.transform, center, size, Leaving.Demolished, Color.red, 0f);
                Assert.AreEqual(Leaving.Demolished, burst.How);
                Assert.AreEqual(LeavingBurst.DustSeconds, burst.Seconds);
                Assert.AreEqual(LeavingBurst.Puffs + LeavingBurst.Rubble, burst.Pieces.Count);

                burst.Step(LeavingBurst.DustSeconds * 0.3f);
                float early = 0f;
                for (int i = 0; i < burst.Pieces.Count; i++)
                {
                    Transform piece = burst.Pieces[i];
                    Assert.IsTrue(piece.gameObject.activeSelf, $"piece {i} is hidden");
                    Assert.GreaterOrEqual(piece.localPosition.y, center.y - 1e-4f, $"piece {i} is under the ground");
                    if (i < LeavingBurst.Puffs) early = Mathf.Max(early, piece.localPosition.y);
                    else Assert.Greater(piece.localPosition.y, center.y + size * LeavingBurst.RubbleSize * 0.35f, $"rubble {i} is not thrown up");
                }

                burst.Step(LeavingBurst.DustSeconds * 0.4f);
                float late = 0f;
                for (int i = 0; i < LeavingBurst.Puffs; i++) late = Mathf.Max(late, burst.Pieces[i].localPosition.y);
                Assert.Greater(late, early, "the dust does not rise");
                for (int i = LeavingBurst.Puffs; i < burst.Pieces.Count; i++)
                {
                    Assert.AreEqual(center.y + size * LeavingBurst.RubbleSize * 0.35f, burst.Pieces[i].localPosition.y, 1e-3f, $"rubble {i} has not landed");
                }

                burst.Step(LeavingBurst.DustSeconds * 0.4f);
                Assert.IsTrue(burst == null, "the burst stays after its time");
                Assert.AreEqual(0, holder.transform.childCount, "the burst leaves something behind");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void SpokesGoRoundLevelAndASwellRisesAndDwindles()
        {
            AssertSame(Vector3.right, LeavingBurst.Spoke(0, 4, 0f), "the first spoke");
            AssertSame(Vector3.forward, LeavingBurst.Spoke(1, 4, 0f), "a quarter round");
            for (int i = 0; i < 7; i++)
            {
                Vector3 spoke = LeavingBurst.Spoke(i, 7, 0.4f);
                Assert.AreEqual(1f, spoke.magnitude, 1e-4f);
                Assert.AreEqual(0f, spoke.y, 1e-6f, "a spoke is level with the ground");
            }

            Assert.AreEqual(0f, LeavingBurst.Swell(0f, 0.2f), 1e-4f);
            Assert.AreEqual(1f, LeavingBurst.Swell(0.2f, 0.2f), 1e-4f);
            Assert.AreEqual(0f, LeavingBurst.Swell(1f, 0.2f), 1e-4f);
            Assert.Greater(LeavingBurst.Swell(0.5f, 0.2f), LeavingBurst.Swell(0.9f, 0.2f));
        }

        [Test]
        public void WithoutAnimationBuildingsAndTheMarkerSimplyGo()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                int town = -1;
                for (int i = 0; i < data.Board.Count && town < 0; i++)
                {
                    if (data.Board[i].Type == SquareType.Property && data.Board[i].IsDevelopable) town = i;
                }

                board.SetOwner(town, Color.red);
                Assert.IsTrue(board.MarkerShown(town), "a town just bought shows its plot");
                board.SetBuildingLevel(town, 2);
                Assert.IsFalse(board.MarkerShown(town), "the plot gives way to the house");

                // A floor torn off: the lower house stands at once.
                board.SetBuildingLevel(town, 1, animate: false, leaving: Leaving.Demolished);
                for (int i = 0; i < board.BuildingPieces(town).Count; i++) Assert.AreEqual(i == 0, board.BuildingPieces(town)[i].activeSelf, $"house {i + 1}");

                // The last building goes: the plot is back; the town goes: nothing stands on it.
                board.SetBuildingLevel(town, 0, animate: false, leaving: Leaving.Demolished);
                foreach (GameObject piece in board.BuildingPieces(town)) Assert.IsFalse(piece.activeSelf, "a building stays on a town with none");
                Assert.IsTrue(board.MarkerShown(town), "the plot is not back");
                board.ClearOwner(town, animate: false, leaving: Leaving.Demolished);
                Assert.IsFalse(board.MarkerShown(town), "the plot stays on a town nobody owns");
                Assert.AreEqual(0, board.LeavingCount);
                Assert.AreEqual(0, holder.GetComponentsInChildren<LeavingBurst>(true).Length, "a burst flies with no animation asked");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static void AssertSame(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-3f, what);
            Assert.AreEqual(expected.y, actual.y, 1e-3f, what);
            Assert.AreEqual(expected.z, actual.z, 1e-3f, what);
        }
    }
}
