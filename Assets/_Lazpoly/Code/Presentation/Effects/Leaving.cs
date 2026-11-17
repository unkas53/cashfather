using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// How a building leaves its square (the owner's wishes, 2026-11-17: a fine animation when a town is sold and when it
    /// is torn down, and a professional one). Sold, it swells a touch, then is drawn up into a point of light and is gone
    /// in a flash with glints flying. Torn down, it is jolted, gives way and drops into its own rolling cloud of dust
    /// (going down into the ground as much as it is crushed, so it never flattens like a box) while its rubble is thrown
    /// out, bounces and lies.
    /// </summary>
    public enum Leaving
    {
        Sold,
        Demolished,
    }

    /// <summary>Where a leaving building is at one moment: on top of the place, turn and size it rests at.</summary>
    public struct LeavingPose
    {
        /// <summary>How far it has risen, 0 to 1 of the whole way up.</summary>
        public float Lift;

        /// <summary>How far it has gone down into the ground, as a share of its own height.</summary>
        public float Sink;

        /// <summary>Degrees turned about its own upright.</summary>
        public float Turn;

        /// <summary>Degrees it leans forward.</summary>
        public float Tilt;

        /// <summary>How far it is shaken to its side, -1 to 1 of the tremble's reach.</summary>
        public float Shake;

        /// <summary>Its size along its own axes, as a share of its resting size.</summary>
        public Vector3 Scale;
    }

    /// <summary>The two leavings as pure curves over 0..1 of their time, so they can be tested without a table.</summary>
    public static class LeavingMotion
    {
        /// <summary>How long a sold building takes to go, and a torn-down one. Their effects (<see cref="LeavingBurst"/>) outlast them.</summary>
        public const float SoldSeconds = 0.7f;
        public const float DemolishedSeconds = 0.9f;

        /// <summary>A sold building swells by <see cref="SwellBy"/> over this share of its time, then is drawn up thin and tall, turning a little, into nothing.</summary>
        public const float Swell = 0.25f;
        public const float SwellBy = 0.08f;
        public const float Thin = 0.3f;
        public const float Tall = 0.35f;
        public const float Twist = 90f;

        /// <summary>The share of a sold building's time at which its light flashes: just before the last of it is gone.</summary>
        public const float FlashAt = 0.84f;

        /// <summary>A torn-down building is jolted for this share of its time, settling by <see cref="Settle"/>; then it drops, leaning and rumbling.</summary>
        public const float Jolt = 0.2f;
        public const float JoltBeats = 3f;
        public const float Settle = 0.04f;

        /// <summary>By the end of the drop it stands this much of its height, and has gone as far into the ground: its roof is down at the tile, in the dust.</summary>
        public const float Crush = 0.5f;
        public const float Lean = 6f;
        public const float Bulge = 0.07f;
        public const float Rumble = 0.35f;
        public const float RumbleBeats = 14f;

        /// <summary>The smallest a leaving building is drawn, so its size never turns to nothing while it is still shown.</summary>
        public const float Least = 0.02f;

        /// <summary>The share of its time at which what stays behind (a lower house, the plot) shows again: behind the flash, or inside the dust.</summary>
        public const float UncoverAt = 0.88f;

        public static float Seconds(Leaving how) => how == Leaving.Demolished ? DemolishedSeconds : SoldSeconds;

        public static LeavingPose At(Leaving how, float t) => how == Leaving.Demolished ? Demolished(t) : Sold(t);

        /// <summary>Sold: a small swell, as a breath taken, then up, thinner and taller, turning, to a point.</summary>
        public static LeavingPose Sold(float t)
        {
            t = Mathf.Clamp01(t);
            var pose = new LeavingPose();
            if (t < Swell)
            {
                pose.Scale = Vector3.one * (1f + SwellBy * Ease.OutCubic(t / Swell));
                return pose;
            }

            float away = (t - Swell) / (1f - Swell);
            float size = (1f + SwellBy) * (1f - Ease.InCubic(away));
            float thin = 1f - Thin * away * away;
            float tall = 1f + Tall * away * away;
            pose.Lift = Ease.InOutCubic(away);
            pose.Turn = Twist * Ease.InCubic(away);
            pose.Scale = new Vector3(Mathf.Max(Least, size * thin), Mathf.Max(Least, size * tall), Mathf.Max(Least, size * thin));
            return pose;
        }

        /// <summary>Torn down: a sharp jolt that dies away as the walls settle, then the drop, ever faster, leaning, bulging and rumbling.</summary>
        public static LeavingPose Demolished(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < Jolt)
            {
                float jolt = t / Jolt;
                float give = Ease.OutCubic(jolt);
                return new LeavingPose
                {
                    Shake = Mathf.Sin(jolt * JoltBeats * 2f * Mathf.PI) * (1f - jolt),
                    Scale = new Vector3(1f + Settle * 0.3f * give, 1f - Settle * give, 1f + Settle * 0.3f * give),
                };
            }

            float fall = (t - Jolt) / (1f - Jolt);
            float width = 1f + Settle * 0.3f + Bulge * Ease.Bump(fall);
            return new LeavingPose
            {
                Shake = Rumble * Mathf.Sin(fall * RumbleBeats * 2f * Mathf.PI) * Ease.Bump(fall),
                Tilt = Lean * fall * fall,
                Sink = (1f - Settle) * Crush * fall * fall,
                Scale = new Vector3(width, (1f - Settle) * (1f - (1f - Crush) * fall * fall), width),
            };
        }
    }
}
