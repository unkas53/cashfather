using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// What flies about as a building leaves: a ring of gold twinkles where a sold one vanished, or dust puffs rolling
    /// out from the foot of a torn-down one with a few pieces of rubble in its wall's colour. Plain meshes moved in code,
    /// as the confetti and the chimney smoke are; it clears itself away.
    /// </summary>
    public sealed class LeavingBurst : MonoBehaviour
    {
        public const int Twinkles = 8;
        public const int Puffs = 7;
        public const int Rubble = 6;

        public const float TwinkleSeconds = 0.55f;
        public const float DustSeconds = 1.05f;

        /// <summary>Sizes and reaches, in widths of the square the building stood on.</summary>
        public const float TwinkleReach = 0.42f;
        public const float TwinkleSize = 0.035f;
        public const float DustReach = 0.4f;
        public const float DustRise = 0.22f;
        public const float DustSize = 0.17f;
        public const float RubbleSize = 0.045f;
        public const float RubbleReach = 0.45f;
        public const float RubbleHop = 0.38f;

        public static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        public static readonly Color Dust = new Color(0.8f, 0.77f, 0.71f);

        private readonly List<Transform> _pieces = new List<Transform>();
        private readonly List<Material> _materials = new List<Material>();
        private Leaving _how;
        private Vector3 _center;
        private float _size;
        private float _delay;
        private float _age;

        /// <summary>The kind of burst and how long it lasts once it has started.</summary>
        public Leaving How => _how;
        public float Seconds => _how == Leaving.Demolished ? DustSeconds : TwinkleSeconds;

        /// <summary>The pieces flying now (hidden until the burst starts).</summary>
        public IReadOnlyList<Transform> Pieces => _pieces;

        /// <summary>
        /// Starts a burst under <paramref name="parent"/> at <paramref name="center"/> (in its frame), sized for a square
        /// <paramref name="size"/> wide, after <paramref name="delay"/> seconds; the rubble takes <paramref name="wall"/>'s colour.
        /// </summary>
        public static LeavingBurst Play(Transform parent, Vector3 center, float size, Leaving how, Color wall, float delay)
        {
            var root = new GameObject(how == Leaving.Demolished ? "Demolition dust" : "Sale twinkle");
            root.transform.SetParent(parent, false);
            var burst = root.AddComponent<LeavingBurst>();
            burst._how = how;
            burst._center = center;
            burst._size = size;
            burst._delay = Mathf.Max(0f, delay);
            if (how == Leaving.Demolished)
            {
                for (int i = 0; i < Puffs; i++) burst.Add("Dust", MeshFactory.Sphere(10), Dust, 0.05f);
                Mesh stone = MeshFactory.RoundedBox(new Vector3(1f, 0.7f, 0.8f), 0.08f, 1);
                Color rubble = Color.Lerp(wall, Dust, 0.35f);
                for (int i = 0; i < Rubble; i++) burst.Add("Rubble", stone, rubble, 0.1f);
            }
            else
            {
                for (int i = 0; i < Twinkles; i++) burst.Add("Twinkle", MeshFactory.Sphere(10), Gold, 0.8f);
            }

            return burst;
        }

        private void Add(string name, Mesh mesh, Color color, float smoothness)
        {
            GameObject part = ModelParts.Create(name, transform, mesh, color, smoothness);
            part.SetActive(false);
            _pieces.Add(part.transform);
            Material material = part.GetComponent<Renderer>().sharedMaterial;
            if (material != null) _materials.Add(material);
        }

        private void Update() => Step(Time.deltaTime);

        /// <summary>Moves the burst on by <paramref name="seconds"/> (public so a test can run it without the clock); it is gone once its time is up.</summary>
        public void Step(float seconds)
        {
            _age += seconds;
            float t = (_age - _delay) / Seconds;
            if (t >= 1f)
            {
                Clear();
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            if (t < 0f) return;
            for (int i = 0; i < _pieces.Count; i++)
            {
                Transform piece = _pieces[i];
                if (!piece.gameObject.activeSelf) piece.gameObject.SetActive(true);
                if (_how == Leaving.Sold) Twinkle(piece, i, t);
                else if (i < Puffs) Puff(piece, i, t);
                else Stone(piece, i - Puffs, t);
            }
        }

        /// <summary>A twinkle flies out from the middle on its spoke, a little upwards, swelling at once and dwindling to nothing.</summary>
        private void Twinkle(Transform piece, int index, float t)
        {
            Vector3 spoke = Spoke(index, Twinkles, 0f);
            float flown = Ease.OutCubic(t);
            piece.localPosition = _center + (spoke * TwinkleReach + Vector3.up * (0.12f * (index % 2 == 0 ? 1f : -0.4f))) * (_size * flown);
            piece.localScale = Vector3.one * (_size * TwinkleSize * (index % 2 == 0 ? 1f : 0.65f) * Swell(t, 0.2f));
        }

        /// <summary>A dust puff rolls out along the ground on its spoke and rises, growing while it is young and thinning away.</summary>
        private void Puff(Transform piece, int index, float t)
        {
            // Each puff starts a moment after the one before, so the cloud rolls rather than pops.
            float own = Mathf.Clamp01(t * 1.25f - 0.25f * index / Puffs);
            Vector3 spoke = Spoke(index, Puffs, 0.4f);
            float reach = index == 0 ? 0f : DustReach * (0.55f + 0.45f * ((index * 3) % Puffs) / (float)Puffs);
            piece.localPosition = _center + (spoke * (reach * Ease.OutCubic(own)) + Vector3.up * (DustRise * own)) * _size;
            piece.localScale = Vector3.one * (_size * DustSize * (index == 0 ? 1.25f : 1f) * Mathf.Lerp(0.45f, 1f, Mathf.Sqrt(own)) * (1f - own * own));
        }

        /// <summary>A piece of rubble hops out on its spoke, tumbling, lands and dwindles where it lies.</summary>
        private void Stone(Transform piece, int index, float t)
        {
            float flight = Mathf.Clamp01(t / 0.6f);
            Vector3 spoke = Spoke(index, Rubble, 1.1f);
            float reach = RubbleReach * (0.6f + 0.4f * ((index * 5) % Rubble) / (float)Rubble);
            float hop = RubbleHop * (0.6f + 0.4f * ((index * 2) % Rubble) / (float)Rubble) * 4f * flight * (1f - flight);
            piece.localPosition = _center + (spoke * (reach * flight) + Vector3.up * (hop + RubbleSize * 0.35f)) * _size;
            piece.localRotation = Quaternion.Euler(540f * flight * (index % 2 == 0 ? 1f : -1f), 70f * index, 360f * flight);
            piece.localScale = Vector3.one * (_size * RubbleSize * (0.7f + 0.3f * (index % 3) / 2f) * (t < 0.7f ? 1f : Mathf.Max(0f, 1f - (t - 0.7f) / 0.3f)));
        }

        /// <summary>The way spoke <paramref name="index"/> of <paramref name="count"/> points, level with the ground; <paramref name="turn"/> turns the whole wheel.</summary>
        public static Vector3 Spoke(int index, int count, float turn)
        {
            float angle = turn + 2f * Mathf.PI * index / Mathf.Max(1, count);
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        /// <summary>Up to full size in the first <paramref name="rise"/> of the time, then away to nothing.</summary>
        public static float Swell(float t, float rise)
        {
            t = Mathf.Clamp01(t);
            return t < rise ? Ease.OutCubic(t / rise) : 1f - Ease.InCubic((t - rise) / (1f - rise));
        }

        private void OnDestroy() => Clear();

        /// <summary>Throws away the materials its pieces were given (each piece has its own).</summary>
        private void Clear()
        {
            foreach (Material material in _materials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }

            _materials.Clear();
        }
    }
}
