using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Darkens what stands on a square that a pick on the board leaves out (the owner's wish, 2026-11-17: the squares
    /// that may be picked no longer light up; the rest go dark, their buildings with them). The models keep their
    /// materials: each renderer is handed its own colours, darkened, and gets them back untouched when the pick is over.
    /// </summary>
    public static class SquareShade
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int PlainColor = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock _block;

        /// <summary>A colour <paramref name="amount"/> of the way to black; it stays as see-through as it was.</summary>
        public static Color Darkened(Color color, float amount)
        {
            float keep = 1f - Mathf.Clamp01(amount);
            return new Color(color.r * keep, color.g * keep, color.b * keep, color.a);
        }

        /// <summary>The colour a material is drawn in: the render pipeline's or the plain one; -1 for a material with neither.</summary>
        public static int ColorProperty(Material material) =>
            material == null ? -1 : material.HasProperty(BaseColor) ? BaseColor : material.HasProperty(PlainColor) ? PlainColor : -1;

        /// <summary>
        /// Draws every part under <paramref name="root"/> (hidden ones too) <paramref name="amount"/> of the way to black;
        /// zero gives each its material's own colours back.
        /// </summary>
        public static void Apply(GameObject root, float amount)
        {
            if (root == null) return;
            foreach (Renderer part in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = part.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (amount <= 0f)
                    {
                        part.SetPropertyBlock(null, i);
                        continue;
                    }

                    int property = ColorProperty(materials[i]);
                    if (property == -1) continue;
                    if (_block == null) _block = new MaterialPropertyBlock();
                    _block.Clear();
                    _block.SetColor(property, Darkened(materials[i].GetColor(property), amount));
                    part.SetPropertyBlock(_block, i);
                }
            }
        }

        /// <summary>Whether a part is drawn darkened now.</summary>
        public static bool IsDark(Renderer part)
        {
            if (part == null) return false;
            int count = part.sharedMaterials.Length;
            for (int i = 0; i < count; i++)
            {
                if (Shown(part, i).HasValue) return true;
            }

            return false;
        }

        /// <summary>The darkened colour a part's material is drawn in; null while it is drawn as it is.</summary>
        public static Color? Shown(Renderer part, int materialIndex)
        {
            if (part == null) return null;
            Material[] materials = part.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= materials.Length) return null;
            int property = ColorProperty(materials[materialIndex]);
            if (property == -1) return null;
            var block = new MaterialPropertyBlock();
            part.GetPropertyBlock(block, materialIndex);
            return block.isEmpty ? (Color?)null : block.GetColor(property);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _block = null;
    }
}
