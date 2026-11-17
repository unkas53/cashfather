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
    /// down, and (the same evening) a professional one. Sold, a building swells a touch and is drawn up into a flash of
    /// light with glints flying; torn down, it is jolted and drops into a rolling cloud of dust, its rubble thrown out.
    /// </summary>
    public class LeavingTests
    {
        private const int Samples = 200;

        [Test]
        public void ASoldBuildingSwellsThenIsDrawnUpThinAndTallToAPoint()
        {
            LeavingPose start = LeavingMotion.Sold(0f);
            AssertSame(Vector3.one, start.Scale, "it starts at its own size");
            Assert.AreEqual(0f, start.Lift, 1e-4f);
            Assert.AreEqual(0f, start.Turn, 1e-4f);
            Assert.AreEqual(0f, start.Tilt, 1e-4f);
            Assert.AreEqual(0f, start.Shake, 1e-4f);
            Assert.AreEqual(0f, start.Sink, 1e-4f);

            LeavingPose swollen = LeavingMotion.Sold(LeavingMotion.Swell);
            AssertSame(Vector3.one * (1f + LeavingMotion.SwellBy), swollen.Scale, "it swells evenly before it goes");
            Assert.AreEqual(0f, swollen.Lift, 1e-3f, "it is still on its tile at the top of the swell");

            LeavingPose going = LeavingMotion.Sold(0.8f);
            Assert.Greater(going.Scale.y, going.Scale.x * 1.1f, "it is drawn up taller than it is wide");
            Assert.Less(going.Scale.x, 1f, "it is not getting thinner");

            LeavingPose end = LeavingMotion.Sold(1f);
            Assert.AreEqual(1f, end.Lift, 1e-4f);
            Assert.AreEqual(LeavingMotion.Twist, end.Turn, 1e-2f);
            Assert.LessOrEqual(end.Scale.y, LeavingMotion.Least + 1e-4f, "it is gone at the top");
            Assert.LessOrEqual(end.Scale.x, LeavingMotion.Least + 1e-4f, "it is gone at the top");
            AssertSame(end.Scale, LeavingMotion.Sold(2f).Scale, "past its time it stays as it ended");

            LeavingPose before = start;
            for (int i = 1; i <= Samples; i++)
            {
                LeavingPose pose = LeavingMotion.Sold(i / (float)Samples);
                Assert.GreaterOrEqual(pose.Lift, before.Lift - 1e-5f, $"it never comes back down (sample {i})");
                Assert.AreEqual(0f, pose.Sink, 1e-6f, "a sold building does not go into the ground");
                Assert.GreaterOrEqual(pose.Turn, before.Turn - 1e-3f, $"it never turns back (sample {i})");
                Assert.Less(Mathf.Abs(pose.Lift - before.Lift), 0.03f, $"it jumps at sample {i}");
                Assert.Less((pose.Scale - before.Scale).magnitude, 0.08f, $"its size jumps at sample {i}");
                Assert.GreaterOrEqual(pose.Scale.x, LeavingMotion.Least - 1e-5f, "its size never turns to nothing while it is shown");
                Assert.GreaterOrEqual(pose.Scale.y, LeavingMotion.Least - 1e-5f, "its size never turns to nothing while it is shown");
                before = pose;
            }
        }

        [Test]
        public void ATornDownBuildingIsJoltedThenDropsEverFasterWhereItStands()
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
                Assert.GreaterOrEqual(pose.Sink, before.Sink - 1e-6f, $"it never comes back up (sample {i})");
                if (t < LeavingMotion.Jolt) Assert.AreEqual(0f, pose.Sink, 1e-6f, "the jolt does not sink it");
                Assert.GreaterOrEqual(pose.Scale.y, LeavingMotion.Least - 1e-5f);
                Assert.Less(before.Scale.y - pose.Scale.y, 0.03f, $"it drops at once at sample {i}");
                Assert.Less(Mathf.Abs(pose.Shake - before.Shake), 0.6f, $"its shake jumps at sample {i}");
                if (t < LeavingMotion.Jolt) Assert.GreaterOrEqual(pose.Scale.y, 1f - LeavingMotion.Settle - 1e-4f, "the jolt only settles it");
                if (t < LeavingMotion.Jolt)
                {
                    left = Mathf.Min(left, pose.Shake);
                    right = Mathf.Max(right, pose.Shake);
                }

                before = pose;
            }

            Assert.Less(left, -0.5f, "the jolt does not shake it to one side");
            Assert.Greater(right, 0.5f, "the jolt does not shake it to the other side");

            // The drop is a fall: little in its first half, most in its second.
            float half = LeavingMotion.Jolt + (1f - LeavingMotion.Jolt) * 0.5f;
            float firstHalf = LeavingMotion.Demolished(LeavingMotion.Jolt).Scale.y - LeavingMotion.Demolished(half).Scale.y;
            float secondHalf = LeavingMotion.Demolished(half).Scale.y - LeavingMotion.Demolished(1f).Scale.y;
            Assert.Greater(secondHalf, firstHalf * 2f, "it does not fall faster as it goes");

            LeavingPose end = LeavingMotion.Demolished(1f);
            Assert.AreEqual((1f - LeavingMotion.Settle) * LeavingMotion.Crush, end.Scale.y, 1e-4f, "it is crushed, not flattened like a box");
            Assert.AreEqual(end.Scale.y, end.Sink, 1e-4f, "its roof is down at the ground at the end, in the dust");
            Assert.Greater(LeavingMotion.Demolished(half).Scale.y - LeavingMotion.Demolished(half).Sink, 0.5f, "half way down it still stands out of the ground");
            Assert.AreEqual(LeavingMotion.Lean, end.Tilt, 1e-3f);
            Assert.AreEqual(0f, end.Shake, 1e-3f, "the rumble has died away");
            Assert.Greater(LeavingMotion.Demolished(half).Scale.x, 1.05f, "it bulges as it drops");
        }

        [Test]
        public void EachLeavingHasItsTimeAndWhatStaysBehindShowsAtItsEnd()
        {
            Assert.AreEqual(LeavingMotion.SoldSeconds, LeavingMotion.Seconds(Leaving.Sold));
            Assert.AreEqual(LeavingMotion.DemolishedSeconds, LeavingMotion.Seconds(Leaving.Demolished));
            foreach (Leaving how in Enum.GetValues(typeof(Leaving)))
            {
                Assert.That(LeavingMotion.Seconds(how), Is.InRange(0.5f, 1.6f), $"{how}: short enough not to hold the game up, long enough to be seen");
                AssertSame(LeavingMotion.At(how, 0.5f).Scale, (how == Leaving.Sold ? LeavingMotion.Sold(0.5f) : LeavingMotion.Demolished(0.5f)).Scale, $"{how}");
            }

            Assert.That(LeavingMotion.FlashAt, Is.InRange(LeavingMotion.Swell, 1f), "the flash opens while the last of the building goes");
            Assert.That(LeavingMotion.UncoverAt, Is.InRange(LeavingMotion.FlashAt, 1f), "what stays behind shows behind the flash");
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

                const float height = 3f;
                BoardView.PoseLeaving(piece.transform, rest, size, facing, LeavingMotion.Sold(0f), width, height);
                AssertSame(rest, piece.transform.localPosition, "at the start it stands where it rests");
                AssertSame(size, piece.transform.localScale, "at the start it has its own size");
                Assert.Less(Quaternion.Angle(facing, piece.transform.localRotation), 0.1f);

                var pose = new LeavingPose { Lift = 0.5f, Turn = 90f, Shake = 1f, Scale = new Vector3(1f, 0.5f, 1f) };
                BoardView.PoseLeaving(piece.transform, rest, size, facing, pose, width, height);
                Vector3 side = facing * Vector3.right * (width * BoardView.LeaveShake);
                AssertSame(rest + Vector3.up * (0.5f * width * BoardView.LeaveLift) + side, piece.transform.localPosition, "lifted and shaken to its own side");
                AssertSame(new Vector3(2f, 1f, 2f), piece.transform.localScale, "its size along its own axes");
                Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 180f, 0f), piece.transform.localRotation), 0.1f, "turned about its own upright");

                var sunk = new LeavingPose { Sink = 0.5f, Scale = Vector3.one };
                BoardView.PoseLeaving(piece.transform, rest, size, facing, sunk, width, height);
                AssertSame(rest + Vector3.down * (0.5f * height), piece.transform.localPosition, "gone into the ground by a share of its own height");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(piece);
            }
        }

        [Test]
        public void ABurstIsFittedToTheBoxOfItsBuilding()
        {
            BoardView.Fit(new Vector3(0f, 0.1f, 0f), new Vector3(0.6f, 1.1f, 0.4f), 0.1f, 1f, out Vector3 foot, out float across, out float high);
            AssertSame(new Vector3(0.3f, 0.1f, 0.2f), foot, "the middle of its foot, on the ground");
            Assert.AreEqual(0.5f, across, 1e-4f, "the mean of its two sides");
            Assert.AreEqual(1f, high, 1e-4f, "its height above the ground");

            BoardView.Fit(Vector3.zero, new Vector3(0.01f, 0.01f, 0.01f), 0f, 1f, out _, out across, out high);
            Assert.AreEqual(0.2f, across, 1e-4f, "a speck still gets a burst that can be seen");
            Assert.AreEqual(0.15f, high, 1e-4f);
            BoardView.Fit(Vector3.zero, new Vector3(5f, 9f, 5f), 0f, 1f, out _, out across, out high);
            Assert.AreEqual(0.9f, across, 1e-4f, "nothing on a tile is wider than the tile");
            Assert.AreEqual(2f, high, 1e-4f);
        }

        [Test]
        public void TheSoftShapesAreClearAtTheirEdgesAndSolidWhereTheyShouldBe()
        {
            foreach (SoftSprite sprite in Enum.GetValues(typeof(SoftSprite)))
            {
                for (int i = 0; i <= 40; i++)
                {
                    float along = i / 20f - 1f;
                    Assert.AreEqual(0f, SoftSprites.Alpha(sprite, along, 1f), 1e-5f, $"{sprite} shows at its top edge");
                    Assert.AreEqual(0f, SoftSprites.Alpha(sprite, -1f, along), 1e-5f, $"{sprite} shows at its left edge");
                    for (int j = 0; j <= 40; j++) Assert.That(SoftSprites.Alpha(sprite, along, j / 20f - 1f), Is.InRange(0f, 1f), $"{sprite}");
                }
            }

            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Puff, 0f, 0f), 1e-4f, "a cloud is solid at its heart");
            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Dot, 0f, 0f), 1e-4f, "a glow is brightest in the middle");
            Assert.AreEqual(1f, SoftSprites.Alpha(SoftSprite.Star, 0f, 0f), 1e-4f, "a glint has a bright heart");
            Assert.Less(SoftSprites.Alpha(SoftSprite.Ring, 0f, 0f), 0.01f, "a ring is open in the middle");
            Assert.Greater(SoftSprites.Alpha(SoftSprite.Ring, 0.8f, 0f), 0.95f, "a ring is solid on its line");
            Assert.Greater(SoftSprites.Alpha(SoftSprite.Star, 0.5f, 0f), 0.5f, "a glint has rays along its cross");
            Assert.Greater(SoftSprites.Alpha(SoftSprite.Star, 0f, -0.5f), 0.5f, "a glint has rays along its cross");
            Assert.Less(SoftSprites.Alpha(SoftSprite.Star, 0.5f, 0.3f), 0.01f, "a glint is clear between its rays");
            Assert.Greater(SoftSprites.Alpha(SoftSprite.Dot, 0.3f, 0f), SoftSprites.Alpha(SoftSprite.Dot, 0.6f, 0f), "a glow thins outwards");

            // A cloud's outline is lumpy: further out along a lobe than between two.
            float lobe = 0.7f, between = 0.7f + Mathf.PI / 5f;
            Assert.Greater(SoftSprites.Alpha(SoftSprite.Puff, Mathf.Cos(lobe) * 0.62f, Mathf.Sin(lobe) * 0.62f),
                SoftSprites.Alpha(SoftSprite.Puff, Mathf.Cos(between) * 0.62f, Mathf.Sin(between) * 0.62f) + 0.1f, "a cloud is a plain round ball");

            // Each shape has its own quarter of one sheet.
            var seen = new System.Collections.Generic.HashSet<Vector2>();
            foreach (SoftSprite sprite in Enum.GetValues(typeof(SoftSprite)))
            {
                Rect cell = SoftSprites.Uv(sprite);
                Assert.AreEqual(0.5f, cell.width);
                Assert.AreEqual(0.5f, cell.height);
                Assert.That(cell.xMin, Is.InRange(0f, 0.5f));
                Assert.That(cell.yMin, Is.InRange(0f, 0.5f));
                Assert.IsTrue(seen.Add(cell.position), $"{sprite} shares its place on the sheet");
            }

            Texture2D sheet = SoftSprites.Sheet();
            Assert.AreEqual(SoftSprites.Cell * 2, sheet.width);
            Assert.AreEqual(SoftSprites.Cell * 2, sheet.height);
            Assert.AreSame(sheet, SoftSprites.Sheet(), "the sheet is drawn once");
        }

        [Test]
        public void ASoftPieceComesInMovesSlowsAndGoesOutByPlainArithmetic()
        {
            var piece = new SoftParticle
            {
                Born = 1f, Life = 2f, From = new Vector3(1f, 2f, 3f), Drift = new Vector3(2f, 0f, 0f), Rise = 1f, Fall = 0.5f,
                SizeFrom = 1f, SizeTo = 3f, Alpha = 0.8f, FadeIn = 0.25f, FadeFrom = 0.5f, Turn = 10f, Spin = 80f,
            };
            Assert.IsFalse(piece.Alive(0.5f), "it shows before it is born");
            Assert.AreEqual(0f, piece.Opacity(piece.Age(0.5f)));
            Assert.IsTrue(piece.Alive(1f));
            Assert.IsTrue(piece.Alive(2f));
            Assert.IsFalse(piece.Alive(3f), "it stays after its life");
            Assert.AreEqual(0f, piece.Opacity(piece.Age(3f)));

            Assert.AreEqual(0f, piece.Opacity(0f), 1e-5f, "it pops in");
            Assert.AreEqual(0.4f, piece.Opacity(0.125f), 1e-4f, "half way in");
            Assert.AreEqual(0.8f, piece.Opacity(0.4f), 1e-5f, "at its fullest");
            Assert.AreEqual(0.4f, piece.Opacity(0.75f), 1e-4f, "half way out");
            Assert.Less(piece.Opacity(0.99f), 0.01f, "it snaps out");

            AssertSame(new Vector3(1f, 2f, 3f), piece.Position(0f), "it starts where it was put");
            AssertSame(new Vector3(3f, 2.5f, 3f), piece.Position(1f), "carried, risen and fallen by the end");
            Assert.Greater(piece.Position(0.5f).x - 1f, 1f, "it is carried fast at first and slows");
            Assert.AreEqual(1f, piece.Size(0f), 1e-5f);
            Assert.AreEqual(3f, piece.Size(1f), 1e-5f);
            Assert.AreEqual(10f, piece.Angle(0f), 1e-4f);
            Assert.AreEqual(90f, piece.Angle(1f), 1e-4f);

            piece.Sit = 0.5f;
            Assert.AreEqual(2.5f, piece.Position(0f).y, 1e-5f, "a cloud is held up by a share of its size");
            piece.Twinkle = 0.5f;
            piece.Beats = 1f;
            Assert.AreEqual(0.5f * Mathf.Lerp(1f, 3f, Ease.OutCubic(0.5f)), piece.Size(0.5f), 1e-4f, "a twinkle dips its size");
            Assert.AreEqual(3f, piece.Size(1f), 1e-4f, "and gives it back");
        }

        [Test]
        public void ASoftPiecesQuadFacesTheEyeOrLiesOnTheGround()
        {
            var piece = new SoftParticle { Life = 1f, SizeFrom = 2f, SizeTo = 2f, Alpha = 1f };
            LeavingBurst.Quad(piece, 0.5f, Vector3.right, Vector3.up, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);
            AssertSame(new Vector3(-1f, -1f, 0f), a, "lower left");
            AssertSame(new Vector3(1f, -1f, 0f), b, "lower right");
            AssertSame(new Vector3(1f, 1f, 0f), c, "upper right");
            AssertSame(new Vector3(-1f, 1f, 0f), d, "upper left");

            piece.Turn = 90f;
            LeavingBurst.Quad(piece, 0.5f, Vector3.right, Vector3.up, out a, out _, out c, out _);
            AssertSame(new Vector3(1f, -1f, 0f), a, "turned a quarter");
            AssertSame(new Vector3(-1f, 1f, 0f), c, "turned a quarter");

            piece.Turn = 0f;
            piece.Flat = true;
            LeavingBurst.Quad(piece, 0.5f, new Vector3(0.7f, 0f, 0.7f), new Vector3(0f, 0.7f, -0.7f), out a, out _, out c, out _);
            AssertSame(new Vector3(-1f, 0f, -1f), a, "a ring lies on the ground whatever the eye");
            AssertSame(new Vector3(1f, 0f, 1f), c, "a ring lies on the ground whatever the eye");
        }

        [Test]
        public void ASaleLaysRingsMotesAFlashAndGlintsAndClearsItselfAway()
        {
            var holder = new GameObject("Burst Test");
            try
            {
                var foot = new Vector3(1f, 0.5f, 3f);
                const float width = 0.8f, height = 1.2f, tile = 2f, lift = 0.6f;
                LeavingBurst burst = LeavingBurst.Play(holder.transform, Leaving.Sold, foot, width, height, tile, lift, Color.red, 7);
                Assert.AreEqual(Leaving.Sold, burst.How);
                Assert.AreEqual(LeavingBurst.SoldParticles, burst.Particles.Count);
                Assert.AreEqual(0, burst.RubbleCount, "a sold building leaves rubble");
                Assert.That(burst.Seconds, Is.InRange(LeavingMotion.SoldSeconds, 2.2f), "the light outlasts the building, but not by long");
                Assert.Greater(burst.Showing(0f), 0, "nothing shows as the building starts to go");

                float flash = LeavingMotion.SoldSeconds * LeavingMotion.FlashAt;
                int glints = 0;
                foreach (SoftParticle particle in burst.Particles)
                {
                    if (particle.Sprite == SoftSprite.Star)
                    {
                        glints++;
                        Assert.GreaterOrEqual(particle.Born, flash - 0.1f, "a glint flies before the flash");
                        Assert.Greater(particle.From.y, foot.y + lift, "a glint does not start where the building vanishes");
                    }

                    Assert.Greater(particle.Life, 0f);
                    for (int i = 0; i <= 20; i++)
                    {
                        Vector3 at = particle.Position(i / 20f);
                        Assert.GreaterOrEqual(at.y, foot.y - 1e-4f, $"a {particle.Sprite} goes under the ground");
                        Assert.Less(new Vector2(at.x - foot.x, at.z - foot.z).magnitude, tile, $"a {particle.Sprite} flies a tile away");
                    }
                }

                Assert.AreEqual(LeavingBurst.Glints + 1, glints);

                // The same building leaves the same way; another scatters otherwise.
                LeavingBurst again = LeavingBurst.Play(holder.transform, Leaving.Sold, foot, width, height, tile, lift, Color.red, 7);
                LeavingBurst other = LeavingBurst.Play(holder.transform, Leaving.Sold, foot, width, height, tile, lift, Color.red, 8);
                bool differs = false;
                for (int i = 0; i < burst.Particles.Count; i++)
                {
                    AssertSame(burst.Particles[i].Drift, again.Particles[i].Drift, "the same seed scatters differently");
                    differs |= (burst.Particles[i].Drift - other.Particles[i].Drift).magnitude > 1e-3f;
                }

                Assert.IsTrue(differs, "another seed scatters the same");
                UnityEngine.Object.DestroyImmediate(again.gameObject);
                UnityEngine.Object.DestroyImmediate(other.gameObject);

                // One mesh carries every soft piece; when its time is up the burst is gone.
                burst.Step(burst.Seconds * 0.5f);
                Assert.IsFalse(burst == null, "the burst goes before its time");
                MeshFilter soft = holder.GetComponentInChildren<MeshFilter>();
                Assert.AreEqual(LeavingBurst.SoldParticles * 4, soft.sharedMesh.vertexCount);
                Assert.Greater(burst.Showing(burst.Age), 0);
                burst.Step(burst.Seconds);
                Assert.IsTrue(burst == null, "the burst stays after its time");
                Assert.AreEqual(0, holder.transform.childCount, "the burst leaves something behind");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void ADemolitionRollsDustOutAndThrowsRubbleThatLandsOnTheTable()
        {
            var holder = new GameObject("Burst Test");
            try
            {
                var foot = new Vector3(3.4f, 0.5f, 0f);
                const float width = 0.8f, height = 1.2f, tile = 2f, bound = 4f;
                LeavingBurst burst = LeavingBurst.Play(holder.transform, Leaving.Demolished, foot, width, height, tile, 0f, Color.red, 3, bound);
                Assert.AreEqual(Leaving.Demolished, burst.How);
                Assert.AreEqual(LeavingBurst.DemolishedParticles, burst.Particles.Count);
                Assert.AreEqual(LeavingBurst.Rubble, burst.RubbleCount);
                Assert.That(burst.Seconds, Is.InRange(LeavingMotion.DemolishedSeconds + 0.8f, 3f), "the dust hangs a while after the building, but not long");
                for (int i = 0; i < burst.RubbleCount; i++) Assert.IsFalse(burst.RubbleShown(i), "rubble shows before the walls give way");

                float drop = LeavingMotion.DemolishedSeconds * LeavingMotion.Jolt;
                int clouds = 0;
                foreach (SoftParticle particle in burst.Particles)
                {
                    if (particle.Sprite == SoftSprite.Puff && particle.Born >= drop) clouds++;
                    for (int i = 0; i <= 20; i++)
                    {
                        Assert.GreaterOrEqual(particle.Position(i / 20f).y, foot.y - 1e-4f, $"a {particle.Sprite} goes under the ground");
                    }
                }

                Assert.AreEqual(LeavingBurst.Clouds + LeavingBurst.Plumes + LeavingBurst.Haze, clouds);
                Assert.Greater(burst.Showing(drop + 0.6f), burst.Showing(0.1f), "the cloud does not grow as the building drops");

                // Thrown: in the air soon after the walls give way.
                burst.Step(drop + 0.45f);
                float highest = 0f;
                for (int i = 0; i < burst.RubbleCount; i++)
                {
                    Assert.IsTrue(burst.RubbleShown(i), $"rubble {i} is hidden");
                    Assert.GreaterOrEqual(burst.RubblePlace(i).y, foot.y, $"rubble {i} is under the ground");
                    highest = Mathf.Max(highest, burst.RubblePlace(i).y - foot.y);
                }

                Assert.Greater(highest, height * 0.5f, "no rubble is thrown up");

                // Landed: near the ground, near the building, and none past the table's edge (the building stands by it).
                burst.Step(1.65f - burst.Age);
                for (int i = 0; i < burst.RubbleCount; i++)
                {
                    Vector3 place = burst.RubblePlace(i);
                    Assert.That(place.y - foot.y, Is.InRange(0f, tile * 0.25f), $"rubble {i} has not come down");
                    Assert.LessOrEqual(Mathf.Abs(place.x), bound, $"rubble {i} is off the table");
                    Assert.LessOrEqual(Mathf.Abs(place.z), bound, $"rubble {i} is off the table");
                    Assert.Less(new Vector2(place.x - foot.x, place.z - foot.z).magnitude, tile, $"rubble {i} is thrown a tile away");
                }

                Assert.AreEqual(1f, LeavingBurst.RubbleSize(LeavingBurst.RubbleSeconds), 1e-5f, "rubble dwindles while it should lie");
                Assert.AreEqual(0f, LeavingBurst.RubbleSize(LeavingBurst.RubbleSeconds + LeavingBurst.RubbleGone), 1e-5f, "rubble stays for good");
                Assert.AreEqual(0f, LeavingBurst.RubbleSize(-0.1f));

                burst.Step(burst.Seconds);
                Assert.IsTrue(burst == null, "the burst stays after its time");
                Assert.AreEqual(0, holder.transform.childCount, "the burst leaves something behind");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
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
