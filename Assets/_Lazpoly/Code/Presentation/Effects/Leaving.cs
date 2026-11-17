using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// How a building leaves its square (the owner's wish, 2026-11-17: a fine animation when a town is sold and when it
    /// is torn down): sold, it crouches, springs up off the tile turning once and is gone in a twinkle of gold; torn
    /// down, it trembles and falls in on itself in a cloud of dust, its rubble thrown about.
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
        /// <summary>How long a sold building takes to go, and a torn-down one.</summary>
        public const float SoldSeconds = 0.95f;
        public const float DemolishedSeconds = 1.2f;

        /// <summary>A sold building crouches for this share of its time before it springs up; then it keeps its size until <see cref="ShrinkFrom"/> of the way up.</summary>
        public const float Crouch = 0.2f;
        public const float CrouchDepth = 0.14f;
        public const float ShrinkFrom = 0.45f;

        /// <summary>A torn-down building only trembles for this share of its time; then it falls in, leaning a little.</summary>
        public const float Tremble = 0.32f;
        public const float TrembleBeats = 13f;
        public const float Lean = 7f;
        public const float Bulge = 0.1f;

        /// <summary>The smallest a leaving building is drawn, so its size never turns to nothing while it is still shown.</summary>
        public const float Least = 0.02f;

        /// <summary>The share of its time at which the gold twinkles (sold) or the dust rises (torn down), and at which what stays behind shows again.</summary>
        public const float TwinkleAt = 0.6f;
        public const float DustAt = Tremble;
        public const float UncoverAt = 0.7f;

        public static float Seconds(Leaving how) => how == Leaving.Demolished ? DemolishedSeconds : SoldSeconds;

        /// <summary>When the burst of a leaving starts, as a share of its time.</summary>
        public static float BurstAt(Leaving how) => how == Leaving.Demolished ? DustAt : TwinkleAt;

        public static LeavingPose At(Leaving how, float t) => how == Leaving.Demolished ? Demolished(t) : Sold(t);

        /// <summary>Sold: down into a crouch, then up and round once, getting smaller on the way until it is gone at the top.</summary>
        public static LeavingPose Sold(float t)
        {
            t = Mathf.Clamp01(t);
            var pose = new LeavingPose();
            float stretch;
            float size = 1f;
            if (t < Crouch)
            {
                stretch = -CrouchDepth * Ease.OutCubic(t / Crouch);
            }
            else
            {
                float up = (t - Crouch) / (1f - Crouch);
                pose.Lift = Ease.OutCubic(up);
                pose.Turn = 360f * Ease.InOutCubic(up);
                // Out of the crouch it stretches a little past its height and settles.
                stretch = up < 0.3f ? Mathf.Lerp(-CrouchDepth, 0.08f, Ease.OutCubic(up / 0.3f)) : Mathf.Lerp(0.08f, 0f, (up - 0.3f) / 0.7f);
                if (up > ShrinkFrom) size = Mathf.Max(Least, 1f - Ease.InCubic((up - ShrinkFrom) / (1f - ShrinkFrom)));
            }

            // What it loses in height it gains in width, as a soft thing would.
            pose.Scale = new Vector3(1f - stretch / 2f, 1f + stretch, 1f - stretch / 2f) * size;
            return pose;
        }

        /// <summary>Torn down: a growing tremble, then it sinks into itself, bulging and leaning, the tremble dying away.</summary>
        public static LeavingPose Demolished(float t)
        {
            t = Mathf.Clamp01(t);
            float fall = t < Tremble ? 0f : (t - Tremble) / (1f - Tremble);
            float strength = t < Tremble ? t / Tremble : 1f - fall;
            float height = Mathf.Max(Least, 1f - fall * fall);
            float width = 1f + Bulge * Ease.Bump(fall);
            return new LeavingPose
            {
                Shake = Mathf.Sin(t * TrembleBeats * 2f * Mathf.PI) * strength,
                Tilt = Lean * Ease.OutCubic(fall),
                Scale = new Vector3(width, height, width),
            };
        }
    }
}
