using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// One soft piece of an effect (a dust cloud, a glint, a ring on the ground): everything about its short life is
    /// laid down when it is made, so where it is, how big and how clear at any moment is plain arithmetic that can be
    /// tested without a clock. Lengths are in the units of whatever it is drawn under.
    /// </summary>
    public struct SoftParticle
    {
        public SoftSprite Sprite;

        /// <summary>When it appears, in seconds from the start of its effect, and how long it lasts.</summary>
        public float Born;
        public float Life;

        /// <summary>Where it starts, and how far it is carried in all: fast at first and slowing, as through air.</summary>
        public Vector3 From;
        public Vector3 Drift;

        /// <summary>The height it gains steadily over its life, and the height it has lost by the end, falling ever faster.</summary>
        public float Rise;
        public float Fall;

        /// <summary>Its side at the start and at the end; it grows (or shrinks) fast at first.</summary>
        public float SizeFrom;
        public float SizeTo;

        /// <summary>How far above its own place it is held, as a share of its size: a cloud sits on the ground rather than in it.</summary>
        public float Sit;

        public Color32 Color;

        /// <summary>How solid it is at its fullest; it comes in over the first <see cref="FadeIn"/> of its life and goes out from <see cref="FadeFrom"/> on.</summary>
        public float Alpha;
        public float FadeIn;
        public float FadeFrom;

        /// <summary>Lies on the ground (a ring spreading) instead of facing the eye.</summary>
        public bool Flat;

        /// <summary>The angle it starts at and how far it turns in its life, in degrees.</summary>
        public float Turn;
        public float Spin;

        /// <summary>How deep its size flickers (0 not at all) and how many times in its life.</summary>
        public float Twinkle;
        public float Beats;

        /// <summary>How far through its life it is at <paramref name="time"/>: below 0 not yet there, 1 and above gone.</summary>
        public float Age(float time) => Life <= 0f ? 1f : (time - Born) / Life;

        public bool Alive(float time)
        {
            float age = Age(time);
            return age >= 0f && age < 1f;
        }

        public float Size(float age)
        {
            age = Mathf.Clamp01(age);
            float flicker = 1f - Twinkle * (0.5f - 0.5f * Mathf.Cos(age * Beats * 2f * Mathf.PI));
            return Mathf.Lerp(SizeFrom, SizeTo, Ease.OutCubic(age)) * flicker;
        }

        public Vector3 Position(float age)
        {
            age = Mathf.Clamp01(age);
            return From + Drift * Ease.OutCubic(age) + Vector3.up * (Rise * age - Fall * age * age + Sit * Size(age));
        }

        public float Opacity(float age)
        {
            if (age < 0f || age >= 1f) return 0f;
            float shown = 1f;
            if (FadeIn > 0f && age < FadeIn) shown = age / FadeIn;
            else if (FadeFrom < 1f && age > FadeFrom) shown = 1f - (age - FadeFrom) / (1f - FadeFrom);
            // Eased both ways, so nothing pops in or snaps out.
            return Alpha * shown * shown * (3f - 2f * shown);
        }

        public float Angle(float age) => Turn + Spin * Mathf.Clamp01(age);
    }
}
