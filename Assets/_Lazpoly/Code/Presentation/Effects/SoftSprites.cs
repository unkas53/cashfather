using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>The soft shapes an effect is drawn with (<see cref="SoftSprites"/>).</summary>
    public enum SoftSprite
    {
        /// <summary>A lumpy cloud with a feathered edge: dust and smoke.</summary>
        Puff,

        /// <summary>A four-pointed glint with a bright heart.</summary>
        Star,

        /// <summary>A thin ring with soft sides: a wave spreading over the ground.</summary>
        Ring,

        /// <summary>A round glow, brightest in the middle.</summary>
        Dot,
    }

    /// <summary>
    /// The pictures behind the game's soft effects (the owner's wish, 2026-11-17: the leaving of a building should look
    /// professional): four see-through shapes drawn in code on one sheet, their edges feathered so that a quad wearing
    /// one never shows its corners. They are drawn with the board's print shader, which blends over what is behind,
    /// tests depth (a house hides the dust behind it) and takes its colour from the quad's corners.
    /// </summary>
    public static class SoftSprites
    {
        /// <summary>Each shape's square on the sheet, in pixels; the sheet is two by two of them.</summary>
        public const int Cell = 128;

        /// <summary>The effects draw after the board's own print and its lettering.</summary>
        public const int Layer = 40;

        private static Texture2D _sheet;

        /// <summary>
        /// How solid <paramref name="sprite"/> is at a point of its square, (-1,-1) to (1,1) with the middle at 0: 0 clear
        /// to 1 solid. Every shape is clear at the edge of its square.
        /// </summary>
        public static float Alpha(SoftSprite sprite, float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            // Nothing reaches the edge, so neighbours on the sheet never bleed into each other.
            float edge = Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(x), Mathf.Abs(y))) / 0.08f);
            float alpha;
            switch (sprite)
            {
                case SoftSprite.Puff:
                    alpha = Puff(x, y);
                    break;
                case SoftSprite.Star:
                {
                    // Two thin crossed rays that taper to their tips, and a round heart.
                    float along = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                    float across = Mathf.Min(Mathf.Abs(x), Mathf.Abs(y));
                    float width = 0.15f * (1f - along / 0.92f);
                    float ray = width <= 0f ? 0f : Mathf.Clamp01(1f - across / width);
                    float heart = Mathf.Exp(-r * r / 0.045f);
                    alpha = Mathf.Clamp01(ray * ray * (1f - along * 0.5f) + heart);
                    break;
                }

                case SoftSprite.Ring:
                {
                    float off = (r - 0.8f) / 0.07f;
                    alpha = Mathf.Exp(-off * off);
                    break;
                }

                default:
                    alpha = Mathf.Exp(-r * r / 0.16f) * Mathf.Clamp01((1f - r) / 0.25f);
                    break;
            }

            return Mathf.Clamp01(alpha) * edge;
        }

        /// <summary>The cloud: a heart and five lobes round it, each a soft ball, so its outline is lumpy rather than round.</summary>
        private static float Puff(float x, float y)
        {
            float clear = 1f - Lobe(x, y, 0f, 0f, 0.52f);
            for (int i = 0; i < 5; i++)
            {
                float angle = 0.7f + i * (2f * Mathf.PI / 5f);
                float reach = i % 2 == 0 ? 0.36f : 0.3f;
                clear *= 1f - 0.85f * Lobe(x, y, Mathf.Cos(angle) * reach, Mathf.Sin(angle) * reach, i % 2 == 0 ? 0.36f : 0.42f);
            }

            return 1f - clear;
        }

        private static float Lobe(float x, float y, float cx, float cy, float radius)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
            if (d >= 1f) return 0f;
            float soft = 1f - d * d;
            return soft * soft;
        }

        /// <summary>Where <paramref name="sprite"/> lies on the sheet: its lower left corner and its size, in texture coordinates.</summary>
        public static Rect Uv(SoftSprite sprite)
        {
            int index = (int)sprite;
            return new Rect((index % 2) * 0.5f, (index / 2) * 0.5f, 0.5f, 0.5f);
        }

        /// <summary>The sheet itself: white, with the shapes in its alpha. Drawn once and kept.</summary>
        public static Texture2D Sheet()
        {
            if (_sheet != null) return _sheet;
            int side = Cell * 2;
            var pixels = new Color32[side * side];
            for (int index = 0; index < 4; index++)
            {
                int left = (index % 2) * Cell, bottom = (index / 2) * Cell;
                for (int py = 0; py < Cell; py++)
                {
                    for (int px = 0; px < Cell; px++)
                    {
                        float x = (px + 0.5f) / Cell * 2f - 1f;
                        float y = (py + 0.5f) / Cell * 2f - 1f;
                        byte alpha = (byte)Mathf.RoundToInt(Alpha((SoftSprite)index, x, y) * 255f);
                        pixels[(bottom + py) * side + left + px] = new Color32(255, 255, 255, alpha);
                    }
                }
            }

            _sheet = new Texture2D(side, side, TextureFormat.RGBA32, false, true)
            {
                name = "Soft sprites",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            _sheet.SetPixels32(pixels);
            _sheet.Apply(false, true);
            return _sheet;
        }

        /// <summary>The one material every soft effect shares (its colours ride on the quads); null when the print shader is not in the build.</summary>
        public static Material Material() => PrintMaterial.ForPicture(Sheet(), Layer);
    }
}
