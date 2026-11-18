using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// What is in the air as a building goes up (the owner's wish, 2026-11-18), in the same clean cartoon shapes as its
    /// leaving (<see cref="LeavingBurst"/>). At the first blow of the hammer dust bursts out round its foot; at the
    /// second a ring of dust runs out over the ground, smaller puffs follow it and chips of wood fly off the walls.
    /// The building itself is never hidden: its going up is what is shown. A hotel is a greater thing: its clouds are
    /// larger, the rings are gold, and a wreath of white cloud bursts out round it as it stands. It clears itself away.
    /// </summary>
    public sealed class RisingBurst : SoftBurst
    {
        public const int Puffs = 6;
        public const int Skirts = 5;
        public const int Chips = 6;
        public const int Wreath = 8;

        /// <summary>
        /// The quads of a rising (a cloud is two: its body and its lit side): the first blow's puffs, the second's
        /// ring, puffs and chips; a hotel has a second ring and its wreath besides.
        /// </summary>
        public const int PlainParticles = 2 * Puffs + 1 + 2 * Skirts + Chips;
        public const int GrandParticles = PlainParticles + 1 + 2 * Wreath;

        /// <summary>When a hotel's wreath bursts out: as it reaches its height, where the tune of its sound begins.</summary>
        public const float GrandBeat = 0.32f;

        /// <summary>How far from the middle of the building its dust starts, in its widths: clear of its corners.</summary>
        public const float Reach = 0.75f;

        /// <summary>How much larger a hotel's dust is.</summary>
        public const float GrandSize = 1.25f;

        /// <summary>Chips of fresh wood.</summary>
        public static readonly Color32 Chip = new Color32(226, 188, 128, 255);

        /// <summary>Whether it is a hotel's.</summary>
        public bool Grand { get; private set; }

        /// <summary>
        /// Starts the dust of a building going up, under <paramref name="parent"/> and in its frame: the building will
        /// stand with the middle of its foot at <paramref name="foot"/>, <paramref name="width"/> across and
        /// <paramref name="height"/> high, on a square <paramref name="tile"/> wide. <paramref name="grand"/> for a
        /// hotel; <paramref name="seed"/> settles the scatter.
        /// </summary>
        public static RisingBurst Play(Transform parent, Vector3 foot, float width, float height, float tile, bool grand, int seed)
        {
            var root = new GameObject(grand ? "Hotel dust" : "Building dust");
            root.transform.SetParent(parent, false);
            var burst = root.AddComponent<RisingBurst>();
            burst.Grand = grand;
            burst.Begin(seed);
            burst.Lay(foot, width, height, tile, grand);
            burst.Ready("Rising burst");
            return burst;
        }

        private void Lay(Vector3 foot, float width, float height, float tile, bool grand)
        {
            float big = grand ? GrandSize : 1f;
            float knock = RisingMotion.KnockGap;

            // The first blow: dust bursts out all round, from beyond its corners (a cloud that starts inside the walls is
            // cut by them), and sits on the ground as it dwindles.
            for (int i = 0; i < Puffs; i++)
            {
                Vector3 way = Round(i, Puffs, 0.5f);
                Dust(new SoftParticle
                {
                    Born = Roll(0f, 0.04f), Life = Roll(0.5f, 0.65f),
                    From = foot + way * (width * Reach), Drift = way * (Roll(0.1f, 0.18f) * tile), Rise = Roll(0.03f, 0.1f) * tile,
                    SizeFrom = 0.14f * tile, SizeTo = Roll(0.28f, 0.36f) * tile * big, Sit = 0.3f, Pop = 0.25f, ShrinkFrom = 0.5f,
                }, i);
            }

            // The second blow: a ring runs out over the ground (gold, twice, for a hotel).
            for (int i = 0; i < (grand ? 2 : 1); i++)
            {
                Add(new SoftParticle
                {
                    Sprite = SoftSprite.Ring, Flat = true, Born = knock + 0.12f * i, Life = grand ? 0.55f : 0.45f,
                    From = foot + Vector3.up * (tile * 0.012f), SizeFrom = width * 0.9f, SizeTo = Mathf.Min(tile * 1.15f, Mathf.Max(width * 2.2f, tile * 0.8f)) * (grand ? 1f : 0.9f),
                    Color = !grand ? DustTones[1] : i == 0 ? Gold : PaleGold, Alpha = !grand ? 0.6f : i == 0 ? 1f : 0.65f, FadeIn = 0.1f, FadeFrom = 0.3f,
                });
            }

            // Smaller puffs after it, further out.
            for (int i = 0; i < Skirts; i++)
            {
                Vector3 way = Round(i, Skirts, 1.3f);
                Dust(new SoftParticle
                {
                    Born = knock + Roll(0f, 0.05f), Life = Roll(0.45f, 0.6f),
                    From = foot + way * (width * Reach), Drift = way * (Roll(0.16f, 0.26f) * tile), Rise = Roll(0.02f, 0.08f) * tile,
                    SizeFrom = 0.1f * tile, SizeTo = Roll(0.2f, 0.26f) * tile * big, Sit = 0.3f, Pop = 0.25f, ShrinkFrom = 0.5f,
                }, i + 2);
            }

            // Chips fly off the walls as they go up, and land as they fade.
            for (int i = 0; i < Chips; i++)
            {
                Vector3 way = Round(i, Chips, 2.1f);
                float start = Roll(0.45f, 0.9f) * height;
                float thrown = Roll(0.12f, 0.3f) * tile;
                Add(new SoftParticle
                {
                    Sprite = SoftSprite.Dot, Born = knock + Roll(0f, 0.08f), Life = Roll(0.4f, 0.6f),
                    From = foot + way * (width * 0.5f) + Vector3.up * start, Drift = way * (Roll(0.2f, 0.42f) * tile) + Vector3.up * thrown,
                    Fall = start + thrown - tile * 0.02f, SizeFrom = 0.085f * tile, SizeTo = 0.05f * tile,
                    Color = Chip, Alpha = 0.95f, FadeIn = 0.05f, FadeFrom = 0.8f,
                });
            }

            if (!grand) return;

            // A hotel stands: a wreath of white cloud bursts out round it.
            for (int i = 0; i < Wreath; i++)
            {
                Vector3 way = Round(i, Wreath, 0.2f);
                float size = Roll(0.2f, 0.28f) * tile;
                Pair(new SoftParticle
                {
                    Sprite = SoftSprite.Cloud, Born = GrandBeat + Roll(0f, 0.05f), Life = Roll(0.5f, 0.7f),
                    From = foot + way * (width * Reach), Drift = way * (Roll(0.2f, 0.32f) * tile), Rise = Roll(0.08f, 0.2f) * tile,
                    SizeFrom = size * 0.5f, SizeTo = size, Sit = 0.3f, Pop = 0.22f, ShrinkFrom = 0.4f,
                    Color = Puff, Alpha = 1f, FadeFrom = 0.9f, Turn = Roll(-18f, 18f),
                }, SoftSprite.CloudLight, PuffLight);
            }
        }
    }
}
