using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// What a building's coming and going have in common (<see cref="RisingBurst"/>, <see cref="LeavingBurst"/>): soft
    /// pieces in the game's own kind (clean cartoon clouds of two tones, rings over the ground, dots) laid down once
    /// with all of their short lives (<see cref="SoftParticle"/>), drawn as the quads of one mesh that face the eye
    /// (<see cref="SoftSprites"/>), and cleared away when the last of them is gone.
    /// </summary>
    public abstract class SoftBurst : MonoBehaviour
    {
        /// <summary>Gold and money green, as the game's gold bars and banknotes, each with the dark rim that keeps a sign readable over any square.</summary>
        public static readonly Color32 Gold = new Color32(255, 204, 64, 255);
        public static readonly Color32 GoldRim = new Color32(124, 74, 10, 255);
        public static readonly Color32 Money = new Color32(126, 200, 110, 255);
        public static readonly Color32 MoneyRim = new Color32(28, 88, 44, 255);
        public static readonly Color32 PaleGold = new Color32(255, 232, 160, 255);
        public static readonly Color32 Light = new Color32(255, 247, 222, 255);

        /// <summary>A white cloud, a touch warm, and its lit side.</summary>
        public static readonly Color32 Puff = new Color32(232, 226, 214, 255);
        public static readonly Color32 PuffLight = new Color32(255, 253, 246, 255);

        /// <summary>Dust in three tones, and how far each tone's lit side is lifted towards <see cref="DustLight"/>.</summary>
        public static readonly Color32[] DustTones =
        {
            new Color32(186, 170, 148, 255), new Color32(168, 152, 132, 255), new Color32(200, 186, 166, 255),
        };

        public static readonly Color32 DustLight = new Color32(244, 236, 222, 255);
        public const float DustLit = 0.55f;
        public static readonly Color32 GritTone = new Color32(96, 84, 72, 255);

        private readonly List<SoftParticle> _particles = new List<SoftParticle>();
        private System.Random _dice;
        private Transform _view;
        private Mesh _mesh;
        private Vector3[] _corners;
        private Color32[] _colours;
        private float _age;
        private float _seconds;

        /// <summary>How far towards the eye the dust laid from now on is drawn (<see cref="Dust"/>, <see cref="SoftParticle.Fore"/>).</summary>
        protected float Near;

        /// <summary>How long it lasts, from its building's first move to its last piece gone.</summary>
        public float Seconds => _seconds;

        /// <summary>Its soft pieces, as they were laid down.</summary>
        public IReadOnlyList<SoftParticle> Particles => _particles;

        /// <summary>The time it has run.</summary>
        public float Age => _age;

        /// <summary>Before any piece is laid: <paramref name="seed"/> settles the scatter, so the same building always comes and goes the same way.</summary>
        protected void Begin(int seed)
        {
            _dice = new System.Random(seed);
            Camera eye = Camera.main;
            _view = eye != null ? eye.transform : null;
        }

        /// <summary>Once every piece is laid: it lasts as long as the longest-lived of them (and <see cref="Outlast"/>), and is drawn as it starts.</summary>
        protected void Ready(string meshName)
        {
            foreach (SoftParticle particle in _particles) _seconds = Mathf.Max(_seconds, particle.Born + particle.Life);
            BuildMesh(meshName);
            Draw();
        }

        /// <summary>Keeps it for at least <paramref name="seconds"/>: for what it shows besides its soft pieces.</summary>
        protected void Outlast(float seconds) => _seconds = Mathf.Max(_seconds, seconds);

        protected void Add(SoftParticle particle) => _particles.Add(particle);

        protected float Roll(float low, float high) => Mathf.Lerp(low, high, (float)_dice.NextDouble());

        /// <summary>A level direction, <paramref name="index"/> of <paramref name="count"/> round the circle, a little off its place.</summary>
        protected Vector3 Round(int index, int count, float turn)
        {
            float angle = turn + 2f * Mathf.PI * (index + Roll(-0.3f, 0.3f)) / Mathf.Max(1, count);
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        /// <summary>Lays a shape of two tones: <paramref name="body"/> as given, and over it the same piece wearing <paramref name="top"/> in <paramref name="tone"/>.</summary>
        protected void Pair(SoftParticle body, SoftSprite top, Color32 tone)
        {
            _particles.Add(body);
            body.Sprite = top;
            body.Color = tone;
            _particles.Add(body);
        }

        /// <summary>A cloud of dust and its lit side, drawn <see cref="Near"/> towards the eye.</summary>
        protected void Dust(SoftParticle cloud, int tone)
        {
            cloud.Sprite = SoftSprite.Cloud;
            cloud.Color = DustTones[tone % DustTones.Length];
            cloud.Alpha = 1f;
            cloud.FadeFrom = 0.9f;
            cloud.Turn = Roll(-18f, 18f);
            cloud.Fore = Near;
            Pair(cloud, SoftSprite.CloudLight, Lit(cloud.Color));
        }

        /// <summary>
        /// How far towards the eye the clouds of a building <paramref name="width"/> across and <paramref name="height"/>
        /// high are drawn, to come in front of it wherever round it they are: without it the walls and the roof cut
        /// them, and the building is seen falling in the open in the middle of its own dust.
        /// </summary>
        public static float Fore(float width, float height) => width * 0.8f + height * 0.6f;

        /// <summary>A dust tone's lit side.</summary>
        public static Color32 Lit(Color32 tone) => Color32.Lerp(tone, DustLight, DustLit);

        /// <summary>One mesh for all the soft pieces, four corners each; only its corners and their colours change as it plays.</summary>
        private void BuildMesh(string meshName)
        {
            int count = _particles.Count;
            _corners = new Vector3[count * 4];
            _colours = new Color32[count * 4];
            var uv = new Vector2[count * 4];
            var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                Rect cell = SoftSprites.Uv(_particles[i].Sprite);
                uv[i * 4] = new Vector2(cell.xMin, cell.yMin);
                uv[i * 4 + 1] = new Vector2(cell.xMax, cell.yMin);
                uv[i * 4 + 2] = new Vector2(cell.xMax, cell.yMax);
                uv[i * 4 + 3] = new Vector2(cell.xMin, cell.yMax);
                int t = i * 6, v = i * 4;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            _mesh = new Mesh { name = meshName, hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
            _mesh.vertices = _corners;
            _mesh.uv = uv;
            _mesh.colors32 = _colours;
            _mesh.triangles = triangles;

            var soft = new GameObject("Soft pieces");
            soft.transform.SetParent(transform, false);
            soft.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var drawn = soft.AddComponent<MeshRenderer>();
            drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            drawn.receiveShadows = false;
            drawn.sharedMaterial = SoftSprites.Material();
            // Without the print shader (not in this build) nothing soft is drawn rather than white squares.
            if (drawn.sharedMaterial == null) drawn.enabled = false;
        }

        protected void LateUpdate() => Step(Time.deltaTime);

        /// <summary>Moves the burst on by <paramref name="seconds"/> (public so a test can run it without the clock); it is gone once its time is up.</summary>
        public void Step(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            Move(seconds);
            _age += seconds;
            if (_age >= _seconds)
            {
                Clear();
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            Draw();
        }

        /// <summary>What a burst moves besides its soft pieces, over the <paramref name="seconds"/> that start at <see cref="Age"/>.</summary>
        protected virtual void Move(float seconds)
        {
        }

        /// <summary>How many soft pieces are in the air at <paramref name="time"/>.</summary>
        public int Showing(float time)
        {
            int shown = 0;
            foreach (SoftParticle particle in _particles)
            {
                if (particle.Alive(time)) shown++;
            }

            return shown;
        }

        /// <summary>Lays every soft piece's quad where it is now: facing the eye (or flat on the ground), turned, sized and coloured.</summary>
        private void Draw()
        {
            if (_mesh == null) return;
            Vector3 right = Vector3.right, up = Vector3.up;
            if (_view != null)
            {
                right = transform.InverseTransformDirection(_view.right);
                up = transform.InverseTransformDirection(_view.up);
            }

            for (int i = 0; i < _particles.Count; i++)
            {
                SoftParticle particle = _particles[i];
                float age = particle.Age(_age);
                float opacity = particle.Opacity(age);
                Color32 colour = particle.Color;
                colour.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(opacity) * 255f);
                int v = i * 4;
                _colours[v] = _colours[v + 1] = _colours[v + 2] = _colours[v + 3] = colour;
                if (opacity <= 0f)
                {
                    _corners[v] = _corners[v + 1] = _corners[v + 2] = _corners[v + 3] = particle.From;
                    continue;
                }

                Quad(particle, age, right, up, out _corners[v], out _corners[v + 1], out _corners[v + 2], out _corners[v + 3]);
            }

            _mesh.vertices = _corners;
            _mesh.colors32 = _colours;
        }

        /// <summary>
        /// The four corners of a soft piece at <paramref name="age"/> of its life, for an eye whose right and up are
        /// <paramref name="right"/> and <paramref name="up"/>: lower left, lower right, upper right, upper left. A piece
        /// with <see cref="SoftParticle.Fore"/> is drawn that much nearer the eye, so it stays where it is on the screen
        /// (the game's view has no perspective) but comes in front of the building it would otherwise be cut by.
        /// </summary>
        public static void Quad(SoftParticle particle, float age, Vector3 right, Vector3 up, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
        {
            // Towards the eye is out of the plane it faces; a piece on the ground stays where it lies.
            Vector3 near = particle.Flat ? Vector3.zero : Vector3.Cross(up, right) * particle.Fore;
            if (particle.Flat)
            {
                right = Vector3.right;
                up = Vector3.forward;
            }

            float angle = particle.Angle(age) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            float half = particle.Size(age) * 0.5f;
            Vector3 across = (right * cos + up * sin) * half;
            Vector3 along = (up * cos - right * sin) * half;
            Vector3 middle = particle.Position(age) + near;
            a = middle - across - along;
            b = middle + across - along;
            c = middle + across + along;
            d = middle - across + along;
        }

        protected void OnDestroy() => Clear();

        /// <summary>Throws away what it made: the mesh of the soft pieces (their material is shared and stays).</summary>
        protected virtual void Clear()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
            _mesh = null;
        }
    }
}
