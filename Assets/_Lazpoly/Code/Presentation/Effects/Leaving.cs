using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// How a building leaves its square (the owner's wishes, 2026-11-17 and 18: a fine animation when a town is sold and
    /// when it is torn down, professional, and in keeping with the game). Sold, it ducks, springs up and is gone in a
    /// puff of cloud out of which dollar signs rise. Torn down, it is jolted, gives way and drops into its own cloud of
    /// dust (going down into the ground as much as it is crushed, so it never flattens like a box) while its rubble is
    /// thrown out, bounces and lies.
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
        public const float SoldSeconds = 0.6f;
        public const float DemolishedSeconds = 1f;

        /// <summary>
        /// A sold building ducks by <see cref="DuckBy"/> for the first <see cref="Duck"/> of its time, springs up tall
        /// and narrow until <see cref="PopAt"/>, and is then gone at once, inside its puff.
        /// </summary>
        public const float Duck = 0.4f;
        public const float DuckBy = 0.12f;
        public const float Spring = 0.16f;
        public const float PopAt = 0.75f;

        /// <summary>How much of its way up it has gone when it pops; the rest it rises as it vanishes.</summary>
        public const float SpringLift = 0.6f;

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

        /// <summary>The share of its time at which what stays behind (a lower house, the plot) shows again: inside the puff, or inside the dust.</summary>
        public const float UncoverAt = 0.88f;

        public static float Seconds(Leaving how) => how == Leaving.Demolished ? DemolishedSeconds : SoldSeconds;

        public static LeavingPose At(Leaving how, float t) => how == Leaving.Demolished ? Demolished(t) : Sold(t);

        /// <summary>Sold: down into a duck, wider as it is lower; up out of it tall and narrow, off the tile; then gone in a blink.</summary>
        public static LeavingPose Sold(float t)
        {
            t = Mathf.Clamp01(t);
            var pose = new LeavingPose();
            float squash;
            float size = 1f;
            if (t < Duck)
            {
                squash = DuckBy * Ease.OutCubic(t / Duck);
            }
            else if (t < PopAt)
            {
                float up = Ease.OutCubic((t - Duck) / (PopAt - Duck));
                squash = Mathf.Lerp(DuckBy, -Spring, up);
                pose.Lift = SpringLift * up;
            }
            else
            {
                float gone = (t - PopAt) / (1f - PopAt);
                squash = -Spring;
                pose.Lift = SpringLift + (1f - SpringLift) * gone;
                size = Mathf.Max(Least, 1f - Ease.OutCubic(gone));
            }

            // What it loses in height it gains in width, and the other way round, as a soft thing would.
            pose.Scale = new Vector3(1f + squash * 0.6f, 1f - squash, 1f + squash * 0.6f) * size;
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
