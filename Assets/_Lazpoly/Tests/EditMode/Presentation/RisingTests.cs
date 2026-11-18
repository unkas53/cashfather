using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// The owner's wishes (2026-11-18): a building moment in the same cartoon manner as the sale and the tearing down
    /// (a puff of dust, the storey rising, in time with the hammer), and a bankrupt player's towns leaving one after
    /// another, as a wave, not all at once.
    /// </summary>
    public class RisingTests
    {
        private const int Samples = 240;

        [Test]
        public void ABuildingGoesUpInTwoBlowsOfTheHammer()
        {
            // The first blow: out of the tile to about half its height, and held there until the second.
            Assert.AreEqual(RisingMotion.Least, RisingMotion.Height(0f), 1e-4f, "it starts flat on the tile");
            float justBefore = RisingMotion.Height(RisingMotion.SecondAt - 0.001f);
            Assert.AreEqual(RisingMotion.Half, justBefore, 0.01f, "it waits at half its height for the second blow");
            Assert.That(RisingMotion.Half, Is.InRange(0.4f, 0.7f));
            Assert.Less(RisingMotion.FirstUp, RisingMotion.KnockGap, "the first blow's rise is over before the second falls");
            Assert.Less(RisingMotion.KnockGap + RisingMotion.SecondUp, RisingMotion.Seconds, "it has time to settle");

            // The blows are those of the building sound: the second knock of each take is this long after the first.
            Assert.That(RisingMotion.KnockGap, Is.InRange(0.15f, 0.17f), "the hammer in build.wav, build_2.wav and hotel.wav");
            Assert.AreEqual(RisingMotion.KnockGap / RisingMotion.Seconds, RisingMotion.SecondAt, 1e-6f);

            float highest = 0f, before = 0f;
            bool dipped = false;
            for (int i = 0; i <= Samples; i++)
            {
                float t = i / (float)Samples;
                float height = RisingMotion.Height(t);
                Assert.GreaterOrEqual(height, RisingMotion.Least, $"it turns to nothing at {t}");
                Assert.LessOrEqual(height, 1.12f, $"it shoots far past its height at {t}");
                Assert.Less(Mathf.Abs(height - before), 0.25f, $"it jumps at {t}");
                if (t < RisingMotion.SecondAt) Assert.LessOrEqual(height, RisingMotion.Half * 1.15f, $"it is past its first storeys before the second blow, at {t}");
                if (t > RisingMotion.SecondAt + 0.25f) Assert.GreaterOrEqual(height, 0.9f, $"it is still low long after the second blow, at {t}");
                if (highest > 1f && height < 1f - RisingMotion.Settle * 0.5f) dipped = true;
                highest = Mathf.Max(highest, height);
                before = height;

                // What it lacks in height it has in width; a building at its height is at its width.
                Vector3 size = RisingMotion.Scale(t);
                Assert.AreEqual(height, size.y, 1e-6f);
                Assert.AreEqual(size.x, size.z, 1e-6f);
                Assert.AreEqual(1f + RisingMotion.Wide * (1f - height), size.x, 1e-5f);
            }

            Assert.Greater(highest, 1.01f, "it never springs past its height");
            Assert.IsTrue(dipped, "it never settles back");
            Assert.AreEqual(1f, RisingMotion.Height(1f), 1e-4f);
            Assert.Less((RisingMotion.Scale(1f) - Vector3.one).magnitude, 1e-4f, "it does not end at its own size");
            Assert.AreEqual(RisingMotion.Height(0f), RisingMotion.Height(-1f), 1e-6f);
            Assert.AreEqual(RisingMotion.Height(1f), RisingMotion.Height(2f), 1e-6f);

            // What it takes the place of ducks away before the second blow.
            Assert.AreEqual(1f, RisingMotion.Duck(0f), 1e-6f);
            Assert.AreEqual(RisingMotion.Least, RisingMotion.Duck(1f), 1e-6f);
            Assert.Less(RisingMotion.Duck(0.6f), RisingMotion.Duck(0.3f));
            Assert.Less(RisingMotion.DuckSeconds, RisingMotion.KnockGap);
        }

        [Test]
        public void ARisingBurstsDustAtEachBlowLeavesTheBuildingInTheOpenAndClearsItselfAway()
        {
            var holder = new GameObject("Burst Test");
            try
            {
                var foot = new Vector3(1f, 0.5f, 3f);
                const float width = 0.8f, height = 1.2f, tile = 2f;
                RisingBurst burst = RisingBurst.Play(holder.transform, foot, width, height, tile, false, 7);
                Assert.IsFalse(burst.Grand);
                Assert.AreEqual(RisingBurst.PlainParticles, burst.Particles.Count);
                Assert.That(burst.Seconds, Is.InRange(RisingMotion.Seconds, 1.2f), "the dust outlasts the rise, but not by long");
                Assert.Greater(burst.Showing(0.05f), RisingBurst.Puffs, "no dust at the first blow");
                Assert.Greater(burst.Showing(RisingMotion.KnockGap + 0.1f), burst.Showing(RisingMotion.KnockGap - 0.02f), "nothing more at the second blow");

                int clouds = 0, rings = 0, chips = 0, early = 0;
                for (int index = 0; index < burst.Particles.Count; index++)
                {
                    SoftParticle particle = burst.Particles[index];
                    Assert.Greater(particle.Life, 0f);
                    Assert.GreaterOrEqual(particle.Born, 0f);
                    Assert.AreEqual(0f, particle.Fore, "dust is drawn before the building: its going up is what is shown");
                    if (particle.Born < RisingMotion.KnockGap) early++;
                    Vector3 off = particle.From - foot;
                    float reach = new Vector2(off.x, off.z).magnitude;
                    switch (particle.Sprite)
                    {
                        case SoftSprite.Cloud:
                            clouds++;
                            Assert.AreEqual(width * RisingBurst.Reach, reach, 1e-3f, "a cloud starts inside the walls, which cut it");
                            Assert.Greater(RisingBurst.Reach, 0.71f, "the corners of the building are that far from its middle");
                            Assert.AreEqual(foot.y, particle.From.y, 1e-4f, "dust starts off the ground");
                            Assert.Greater(particle.Sit, 0f, "a cloud sits in the ground");
                            Assert.Greater(particle.Pop, 0f);
                            Assert.Greater(particle.ShrinkFrom, 0f, "a cartoon cloud dwindles away");
                            Assert.Less(particle.SizeTo, tile * 0.4f, "a cloud as large as the building");
                            Assert.Greater(Vector3.Dot(particle.Drift, new Vector3(off.x, 0f, off.z)), 0f, "dust drifts in towards the building");
                            Assert.AreEqual(SoftSprite.CloudLight, burst.Particles[index + 1].Sprite, "a cloud without its lit side");
                            break;
                        case SoftSprite.Ring:
                            rings++;
                            Assert.IsTrue(particle.Flat);
                            Assert.AreEqual(RisingMotion.KnockGap, particle.Born, 1e-4f, "the ring runs out at the second blow");
                            Assert.Greater(particle.SizeTo, particle.SizeFrom);
                            Assert.LessOrEqual(particle.SizeTo, tile * 1.15f + 1e-4f, "the ring runs far over the neighbours");
                            Assert.Less(particle.Alpha, 0.8f, "a ring of dust is faint");
                            break;
                        case SoftSprite.Dot:
                            chips++;
                            Assert.GreaterOrEqual(particle.Born, RisingMotion.KnockGap, "chips fly before the walls go up");
                            Assert.AreEqual(foot.y + tile * 0.02f, particle.Position(1f).y, 1e-3f, "a chip does not land on the tile");
                            Assert.Greater(particle.Position(0.3f).y, particle.From.y, "a chip is not thrown up");
                            break;
                    }
                }

                Assert.AreEqual(RisingBurst.Puffs + RisingBurst.Skirts, clouds);
                Assert.AreEqual(1, rings);
                Assert.AreEqual(RisingBurst.Chips, chips);
                Assert.AreEqual(2 * RisingBurst.Puffs, early, "only the first blow's dust comes before the second");

                // The same building always goes up the same way; another one differently.
                RisingBurst again = RisingBurst.Play(holder.transform, foot, width, height, tile, false, 7);
                RisingBurst other = RisingBurst.Play(holder.transform, foot, width, height, tile, false, 8);
                bool differs = false;
                for (int i = 0; i < burst.Particles.Count; i++)
                {
                    Assert.AreEqual(burst.Particles[i].From, again.Particles[i].From);
                    Assert.AreEqual(burst.Particles[i].Born, again.Particles[i].Born);
                    differs |= burst.Particles[i].From != other.Particles[i].From;
                }

                Assert.IsTrue(differs, "every building goes up in the same dust");
                UnityEngine.Object.DestroyImmediate(again.gameObject);
                UnityEngine.Object.DestroyImmediate(other.gameObject);

                // A hotel is a greater thing: larger dust, gold rings, and a wreath of white cloud as it stands.
                RisingBurst grand = RisingBurst.Play(holder.transform, foot, width, height, tile, true, 7);
                Assert.IsTrue(grand.Grand);
                Assert.AreEqual(RisingBurst.GrandParticles, grand.Particles.Count);
                Assert.GreaterOrEqual(grand.Seconds, burst.Seconds);
                int gold = 0, white = 0;
                float largest = 0f, plainLargest = 0f;
                foreach (SoftParticle particle in burst.Particles) plainLargest = Mathf.Max(plainLargest, particle.Sprite == SoftSprite.Cloud ? particle.SizeTo : 0f);
                foreach (SoftParticle particle in grand.Particles)
                {
                    Assert.AreEqual(0f, particle.Fore);
                    if (particle.Sprite == SoftSprite.Ring)
                    {
                        gold++;
                        Assert.IsTrue(Same(particle.Color, SoftBurst.Gold) || Same(particle.Color, SoftBurst.PaleGold), "a hotel's ring is not gold");
                    }

                    if (particle.Sprite != SoftSprite.Cloud) continue;
                    if (Same(particle.Color, SoftBurst.Puff))
                    {
                        white++;
                        Assert.GreaterOrEqual(particle.Born, RisingBurst.GrandBeat, "the wreath bursts out before the hotel stands");
                    }
                    else
                    {
                        largest = Mathf.Max(largest, particle.SizeTo);
                    }
                }

                Assert.AreEqual(2, gold);
                Assert.AreEqual(RisingBurst.Wreath, white);
                Assert.Greater(largest, plainLargest * 1.1f, "a hotel's dust is no larger than a house's");
                Assert.That(RisingBurst.GrandBeat, Is.InRange(RisingMotion.KnockGap + 0.1f, RisingMotion.Seconds), "the wreath comes as it reaches its height");
                UnityEngine.Object.DestroyImmediate(grand.gameObject);

                // One mesh carries every soft piece; when its time is up the burst is gone.
                burst.Step(burst.Seconds * 0.5f);
                Assert.IsFalse(burst == null, "the burst goes before its time");
                MeshFilter soft = holder.GetComponentInChildren<MeshFilter>();
                Assert.AreEqual(RisingBurst.PlainParticles * 4, soft.sharedMesh.vertexCount);
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
        public void TheHammerIsHeardAsTheBuildingRisesAndItsPriceAsMoney()
        {
            // The payment flies before the building is shown, so the hammer waits for the building and the payment
            // sounds as any payment to the bank does.
            Assert.AreEqual(SoundCue.Build, SoundCues.ForRising(false));
            Assert.AreEqual(SoundCue.Hotel, SoundCues.ForRising(true), "a hotel has its own blows and its tune");
            Assert.AreEqual(SoundCue.MoneyOut, SoundCues.For(new MoneyChangedEvent(1, -50, MoneyReason.Building)));
            Assert.AreEqual(SoundCue.None, SoundCues.For(new BuildingLevelChangedEvent(3, 2)), "the level's event is silent: the board tells the scene when the building starts to rise");
            foreach (SoundCue cue in new[] { SoundCue.Build, SoundCue.Hotel })
            {
                for (int take = 0; take < SoundCues.Variants(cue); take++)
                {
                    AudioClip clip = Resources.Load<AudioClip>(SoundCues.VariantPath(cue, take));
                    Assert.IsNotNull(clip, $"{cue} take {take + 1}");
                    Assert.Greater(clip.length, RisingMotion.KnockGap + 0.1f, $"{cue} take {take + 1} has no second blow");
                }
            }
        }

        [Test]
        public void WithoutAnimationABuildingSimplyStands()
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

                int risings = 0;
                board.RisingStarted += _ => risings++;
                board.SetOwner(town, Color.red);
                Vector3[] sizes = new Vector3[board.BuildingPieces(town).Count];
                for (int i = 0; i < sizes.Length; i++) sizes[i] = board.BuildingPieces(town)[i].transform.localScale;
                for (int level = 1; level <= SquareDefinition.MaxBuildingLevel; level++)
                {
                    board.SetBuildingLevel(town, level);
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        GameObject piece = board.BuildingPieces(town)[i];
                        Assert.AreEqual(BoardView.Shows(i, level), piece.activeSelf, $"house {i + 1} at level {level}");
                        Assert.Less((piece.transform.localScale - sizes[i]).magnitude, 1e-4f, $"house {i + 1} is not at its size at level {level}");
                    }
                }

                Assert.AreEqual(0, board.RisingCount);
                Assert.AreEqual(0, risings, "the hammer is heard with no animation asked");
                Assert.AreEqual(0, holder.GetComponentsInChildren<RisingBurst>(true).Length, "dust flies with no animation asked");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void ABankruptPlayersTownsLeaveOneAfterAnother()
        {
            // A bankruptcy as the rules send it: each town's buildings come down, then the town goes; then the money.
            const int bankrupt = 2, bank = GameState.NoOwner;
            var first = new PropertyOwnerChangedEvent(3, bankrupt, bank);
            var second = new PropertyOwnerChangedEvent(7, bankrupt, bank);
            var third = new PropertyOwnerChangedEvent(12, bankrupt, bank);
            var events = new List<GameEvent>
            {
                new BuildingLevelChangedEvent(3, 0), first, second, new BuildingLevelChangedEvent(12, 0), third,
                new MoneyChangedEvent(bankrupt, 100, MoneyReason.BuildingSale), new MoneyChangedEvent(bankrupt, 240, MoneyReason.PropertySale),
                new PlayerBankruptedEvent(bankrupt, 1),
            };
            Assert.AreEqual(2, LeavingWave.Left(first, events.GetRange(2, events.Count - 2)));
            Assert.AreEqual(1, LeavingWave.Left(second, events.GetRange(3, events.Count - 3)), "a town's own buildings coming down do not break the wave");
            Assert.AreEqual(0, LeavingWave.Left(third, events.GetRange(5, events.Count - 5)), "the last town waits for nothing");

            // Towns sold one by one (each with its money), another player's town, a town bought, a house sold: no wave.
            var sale = new PropertyOwnerChangedEvent(3, bankrupt, bank);
            Assert.AreEqual(0, LeavingWave.Left(sale, new GameEvent[] { new MoneyChangedEvent(bankrupt, 60, MoneyReason.PropertySale), new PropertyOwnerChangedEvent(7, bankrupt, bank) }));
            Assert.AreEqual(0, LeavingWave.Left(sale, new GameEvent[] { new PropertyOwnerChangedEvent(7, 1, bank) }), "another player's town");
            Assert.AreEqual(0, LeavingWave.Left(sale, new GameEvent[] { new PropertyOwnerChangedEvent(7, bankrupt, 1) }), "a town changing hands");
            Assert.AreEqual(0, LeavingWave.Left(sale, new GameEvent[] { new BuildingLevelChangedEvent(7, 1), new PropertyOwnerChangedEvent(7, bankrupt, bank) }), "a floor coming off is not a town going");
            Assert.AreEqual(0, LeavingWave.Left(new PropertyOwnerChangedEvent(3, bank, 1), new GameEvent[] { new PropertyOwnerChangedEvent(7, bank, bank) }), "a town bought");
            Assert.AreEqual(0, LeavingWave.Left(sale, new GameEvent[0]));
            Assert.AreEqual(0, LeavingWave.Left(sale, null));
            Assert.AreEqual(0, LeavingWave.Left(null, events));

            // A step apart, so each town is seen going; however many, the wave is soon over.
            Assert.AreEqual(0f, LeavingWave.Step(0));
            Assert.AreEqual(0f, LeavingWave.Step(1), "one town is no wave");
            Assert.AreEqual(LeavingWave.StepSeconds, LeavingWave.Step(2), 1e-6f);
            Assert.AreEqual(LeavingWave.StepSeconds, LeavingWave.Step(8), 1e-6f);
            Assert.That(LeavingWave.StepSeconds, Is.InRange(0.15f, LeavingMotion.SoldSeconds * 0.5f), "the next town starts while the one before is still going: a wave, not a queue");
            for (int towns = 2; towns <= 28; towns++)
            {
                float step = LeavingWave.Step(towns);
                Assert.Greater(step, 0.08f, $"{towns} towns go all but together");
                Assert.LessOrEqual(step * (towns - 1), LeavingWave.MaxSeconds + 1e-4f, $"{towns} towns take too long");
                Assert.AreEqual(0f, LeavingWave.StartOf(0, towns));
                Assert.AreEqual(step * (towns - 1), LeavingWave.StartOf(towns - 1, towns), 1e-5f);
                Assert.AreEqual(LeavingWave.StartOf(towns - 1, towns), LeavingWave.StartOf(towns + 5, towns), 1e-6f);
            }

            Assert.Less(LeavingWave.Step(28), LeavingWave.StepSeconds, "many towns go faster");

            // The table waits a step after each town but the last, and a long wave keeps the step it started with.
            float running = 0f;
            Assert.AreEqual(LeavingWave.StepSeconds, LeavingWave.Wait(first, events.GetRange(2, events.Count - 2), ref running), 1e-6f);
            Assert.AreEqual(LeavingWave.StepSeconds, running, 1e-6f);
            Assert.AreEqual(LeavingWave.StepSeconds, LeavingWave.Wait(second, events.GetRange(3, events.Count - 3), ref running), 1e-6f);
            Assert.AreEqual(0f, LeavingWave.Wait(third, events.GetRange(5, events.Count - 5), ref running), "the money waits for the last town");
            Assert.AreEqual(0f, running, "the wave is over");
            Assert.AreEqual(0f, LeavingWave.Wait(sale, new GameEvent[0], ref running), "a town sold alone");

            var many = new List<GameEvent>();
            for (int square = 1; square <= 26; square++) many.Add(new PropertyOwnerChangedEvent(square, bankrupt, bank));
            float total = 0f, kept = 0f;
            for (int i = 0; i < many.Count; i++)
            {
                float wait = LeavingWave.Wait((PropertyOwnerChangedEvent)many[i], many.GetRange(i + 1, many.Count - i - 1), ref running);
                if (i == 0) kept = wait;
                if (i < many.Count - 1) Assert.AreEqual(kept, wait, 1e-6f, $"town {i + 1} of a long wave waits another time than the first");
                total += wait;
            }

            Assert.AreEqual(LeavingWave.Step(26), kept, 1e-6f);
            Assert.AreEqual(LeavingWave.MaxSeconds, total, 1e-3f, "a long wave lasts as long as the longest may");
            Assert.AreEqual(0f, running);
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;
    }
}
