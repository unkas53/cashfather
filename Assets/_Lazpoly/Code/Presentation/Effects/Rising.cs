using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// How a building goes up on its square (the owner's wish, 2026-11-18: a building moment in the same cartoon manner
    /// as the sale and the tearing down, with dust, a storey rising, in time with the hammer). It is built in two blows
    /// of the hammer: at the first it comes up out of the tile to about half its height, in a burst of dust; at the
    /// second it goes up to its full height, a little past it and back, and settles. Pure curves over 0..1 of its time,
    /// so they can be tested without a table; the dust is <see cref="RisingBurst"/>.
    /// </summary>
    public static class RisingMotion
    {
        /// <summary>How long a building takes to stand. Its dust outlasts it.</summary>
        public const float Seconds = 0.6f;

        /// <summary>The time between the two blows: that of the hammer in the building sound (Tools/Audio/make_sfx_kenney.py, "build" and "hotel").</summary>
        public const float KnockGap = 0.17f;

        /// <summary>How long each blow takes to raise it: the first to <see cref="Half"/> of its height, the second to all of it.</summary>
        public const float FirstUp = 0.14f;
        public const float SecondUp = 0.22f;
        public const float Half = 0.55f;

        /// <summary>How much wider it is while it is still flat on the tile, and how far it dips as it settles.</summary>
        public const float Wide = 0.12f;
        public const float Settle = 0.04f;

        /// <summary>The smallest a rising building is drawn, so its size never turns to nothing while it is shown.</summary>
        public const float Least = 0.02f;

        /// <summary>What it takes the place of (the lower house) ducks into the dust in this time.</summary>
        public const float DuckSeconds = 0.12f;

        /// <summary>The share of its time at which the second blow falls.</summary>
        public const float SecondAt = KnockGap / Seconds;

        /// <summary>Its height at <paramref name="t"/> of its time, as a share of the height it stands at.</summary>
        public static float Height(float t)
        {
            float time = Mathf.Clamp01(t) * Seconds;
            if (time < KnockGap) return Mathf.Max(Least, Half * Ease.OutBack(time / FirstUp));
            float up = (time - KnockGap) / SecondUp;
            // Past its height and back, so no clamped blend.
            if (up < 1f) return Half + (1f - Half) * Ease.OutBack(up);
            return 1f - Settle * Ease.Bump((up - 1f) * SecondUp / (Seconds - KnockGap - SecondUp));
        }

        /// <summary>Its size along its own axes at <paramref name="t"/> of its time, about its foot: what it lacks in height it has in width, as a soft thing would.</summary>
        public static Vector3 Scale(float t)
        {
            float height = Height(t);
            float width = 1f + Wide * (1f - height);
            return new Vector3(width, height, width);
        }

        /// <summary>The height of what it takes the place of, <paramref name="t"/> of the way through its duck.</summary>
        public static float Duck(float t) => Mathf.Max(Least, 1f - Ease.InCubic(t));
    }
}
