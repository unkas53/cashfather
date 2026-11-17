using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// What is in the air as a building leaves (the owner's wish, 2026-11-17: professional). Sold: a ring of gold light
    /// spreads from its foot, motes drift up out of it, and where it vanishes a soft flash opens and glints fly out,
    /// twinkling as they droop. Torn down: wisps at the jolt, then a rolling cloud of dust from its foot and up its
    /// height, a ring over the ground, grit, and rubble in the colour of its walls thrown out to bounce and lie.
    /// The soft pieces are quads of one mesh that face the eye (<see cref="SoftParticle"/>, <see cref="SoftSprites"/>);
    /// the rubble is solid and casts shadows. It clears itself away.
    /// </summary>
    public sealed class LeavingBurst : MonoBehaviour
    {
        public const int Motes = 14;
        public const int Glints = 12;
        public const int Wisps = 5;
        public const int Clouds = 16;
        public const int Plumes = 8;
        public const int Grit = 12;
        public const int Haze = 3;
        public const int Rubble = 16;

        /// <summary>The soft pieces of each leaving: sold, two rings, the motes, the flash and its big glint, the glints; torn down, the wisps, a ring, the clouds, the plumes, the grit, the haze.</summary>
        public const int SoldParticles = 2 + Motes + 2 + Glints;
        public const int DemolishedParticles = Wisps + 1 + Clouds + Plumes + Grit + Haze;

        /// <summary>How the rubble falls, bounces and lies, in widths of the square (per second where it is a speed).</summary>
        public const float Gravity = 7.5f;
        public const float Bounce = 0.32f;
        public const float Grip = 0.5f;
        public const float RubbleSeconds = 1.5f;
        public const float RubbleGone = 0.3f;

        /// <summary>The longest step the rubble is moved in one go, so a slow frame cannot throw it through the ground.</summary>
        public const float Tick = 1f / 90f;

        public static readonly Color32 Gold = new Color32(255, 196, 64, 255);
        public static readonly Color32 PaleGold = new Color32(255, 236, 170, 255);
        public static readonly Color32 Light = new Color32(255, 247, 222, 255);
        public static readonly Color32[] DustTones =
        {
            new Color32(172, 161, 147, 255), new Color32(146, 135, 122, 255), new Color32(194, 185, 172, 255),
        };

        public static readonly Color32 GritTone = new Color32(96, 84, 72, 255);

        private sealed class Chunk
        {
            public Transform Piece;
            public Vector3 Place;
            public Vector3 Speed;
            public Vector3 Axis;
            public float Whirl;
            public float Turned;
            public float Born;
            public float Size;
            public Vector3 Shape;
            public float Floor;
        }

        private readonly List<SoftParticle> _particles = new List<SoftParticle>();
        private readonly List<Chunk> _chunks = new List<Chunk>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly Material[] _tones = new Material[3];
        private Leaving _how;
        private System.Random _dice;
        private Transform _view;
        private Mesh _mesh;
        private Vector3[] _corners;
        private Color32[] _colours;
        private float _tile;
        private float _bound;
        private float _age;
        private float _seconds;

        /// <summary>The kind of burst, and how long it lasts from its building's first move to its last piece gone.</summary>
        public Leaving How => _how;
        public float Seconds => _seconds;

        /// <summary>Its soft pieces, as they were laid down, and its rubble.</summary>
        public IReadOnlyList<SoftParticle> Particles => _particles;
        public int RubbleCount => _chunks.Count;

        /// <summary>Where piece <paramref name="index"/> of the rubble is now, and whether it is shown yet.</summary>
        public Vector3 RubblePlace(int index) => _chunks[index].Place;
        public bool RubbleShown(int index) => _chunks[index].Piece.gameObject.activeSelf;

        /// <summary>The time it has run.</summary>
        public float Age => _age;

        /// <summary>
        /// Starts the burst of a building leaving, under <paramref name="parent"/> and in its frame: the building stands
        /// with the middle of its foot at <paramref name="foot"/>, <paramref name="width"/> across and
        /// <paramref name="height"/> high, on a square <paramref name="tile"/> wide; a sold one rises by
        /// <paramref name="lift"/> as it goes. The rubble takes <paramref name="wall"/>'s colour; <paramref name="seed"/>
        /// settles the scatter, so the same building always leaves the same way. No rubble goes further from the parent's
        /// middle than <paramref name="bound"/> along either side (the table's edge), where one is given.
        /// </summary>
        public static LeavingBurst Play(Transform parent, Leaving how, Vector3 foot, float width, float height, float tile, float lift, Color wall, int seed, float bound = 0f)
        {
            var root = new GameObject(how == Leaving.Demolished ? "Demolition dust" : "Sale light");
            root.transform.SetParent(parent, false);
            var burst = root.AddComponent<LeavingBurst>();
            burst._how = how;
            burst._tile = tile;
            burst._bound = bound;
            burst._dice = new System.Random(seed);
            Camera eye = Camera.main;
            burst._view = eye != null ? eye.transform : null;
            if (how == Leaving.Demolished) burst.LayDemolition(foot, width, height, tile, wall);
            else burst.LaySale(foot, width, height, tile, lift);
            foreach (SoftParticle particle in burst._particles) burst._seconds = Mathf.Max(burst._seconds, particle.Born + particle.Life);
            foreach (Chunk chunk in burst._chunks) burst._seconds = Mathf.Max(burst._seconds, chunk.Born + RubbleSeconds + RubbleGone);
            burst.BuildMesh();
            burst.Draw();
            return burst;
        }

        private float Roll(float low, float high) => Mathf.Lerp(low, high, (float)_dice.NextDouble());

        /// <summary>A level direction, <paramref name="index"/> of <paramref name="count"/> round the circle, a little off its place.</summary>
        private Vector3 Round(int index, int count, float turn)
        {
            float angle = turn + 2f * Mathf.PI * (index + Roll(-0.3f, 0.3f)) / Mathf.Max(1, count);
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        private void LaySale(Vector3 foot, float width, float height, float tile, float lift)
        {
            float flash = LeavingMotion.SoldSeconds * LeavingMotion.FlashAt;
            Vector3 gone = foot + Vector3.up * (lift + height * 0.12f);
            float ring = Mathf.Min(tile * 1.1f, Mathf.Max(width * 2.4f, tile * 0.8f));

            // Two rings of light over the ground, the second fainter and a breath later.
            for (int i = 0; i < 2; i++)
            {
                _particles.Add(new SoftParticle
                {
                    Sprite = SoftSprite.Ring, Flat = true, Born = 0.12f * i, Life = 0.6f,
                    From = foot + Vector3.up * (tile * 0.012f), SizeFrom = width * 0.8f, SizeTo = ring,
                    Color = i == 0 ? Gold : PaleGold, Alpha = i == 0 ? 1f : 0.65f, FadeIn = 0.12f, FadeFrom = 0.35f,
                });
            }

            // Motes of light drift up out of the building while it is drawn away.
            for (int i = 0; i < Motes; i++)
            {
                Vector3 inside = new Vector3(Roll(-0.45f, 0.45f) * width, Roll(0.1f, 0.9f) * height, Roll(-0.45f, 0.45f) * width);
                _particles.Add(new SoftParticle
                {
                    Sprite = SoftSprite.Dot, Born = Roll(0.05f, flash), Life = Roll(0.7f, 1f),
                    From = foot + inside, Drift = new Vector3(Roll(-0.08f, 0.08f), 0f, Roll(-0.08f, 0.08f)) * tile,
                    Rise = Roll(0.35f, 0.6f) * tile, SizeFrom = Roll(0.16f, 0.24f) * tile, SizeTo = 0.07f * tile,
                    Color = i % 3 == 0 ? Gold : PaleGold, Alpha = 0.9f, FadeIn = 0.15f, FadeFrom = 0.45f,
                });
            }

            // Where the last of it goes: a soft flash opening, and one big glint turning in it.
            _particles.Add(new SoftParticle
            {
                Sprite = SoftSprite.Dot, Born = flash - 0.08f, Life = 0.6f, From = gone,
                SizeFrom = width * 0.7f, SizeTo = Mathf.Min(tile * 1.5f, width * 2.6f),
                Color = Light, Alpha = 0.95f, FadeIn = 0.12f, FadeFrom = 0.35f,
            });
            _particles.Add(new SoftParticle
            {
                Sprite = SoftSprite.Star, Born = flash - 0.05f, Life = 0.4f, From = gone,
                SizeFrom = tile * 0.5f, SizeTo = tile * 0.95f, Color = Light, Alpha = 1f, FadeIn = 0.15f, FadeFrom = 0.35f,
                Turn = 10f, Spin = 35f,
            });

            // The glints: out and up from the flash, drooping, twinkling, the last ones lingering.
            for (int i = 0; i < Glints; i++)
            {
                Vector3 way = Round(i, Glints, 0.3f);
                float up = Roll(0.15f, 0.9f);
                Vector3 out3 = (way * Mathf.Sqrt(1f - up * up * 0.6f) + Vector3.up * up).normalized;
                float size = Roll(0.34f, 0.56f) * tile;
                _particles.Add(new SoftParticle
                {
                    Sprite = SoftSprite.Star, Born = flash + Roll(-0.03f, 0.08f), Life = Roll(0.55f, 0.95f),
                    From = gone, Drift = out3 * (Roll(0.35f, 0.7f) * tile), Fall = Roll(0.1f, 0.22f) * tile,
                    SizeFrom = size, SizeTo = size * 0.55f,
                    Color = i % 3 == 0 ? Light : i % 3 == 1 ? Gold : PaleGold, Alpha = 1f, FadeIn = 0.08f, FadeFrom = 0.55f,
                    Turn = Roll(-20f, 20f), Spin = Roll(-45f, 45f), Twinkle = 0.45f, Beats = Roll(3f, 5f),
                });
            }
        }

        private void LayDemolition(Vector3 foot, float width, float height, float tile, Color wall)
        {
            float drop = LeavingMotion.DemolishedSeconds * LeavingMotion.Jolt;

            // The jolt shakes wisps out from under the walls.
            for (int i = 0; i < Wisps; i++)
            {
                Vector3 way = Round(i, Wisps, 0.9f);
                _particles.Add(new SoftParticle
                {
                    Sprite = SoftSprite.Puff, Born = Roll(0f, 0.06f), Life = 0.55f,
                    From = foot + way * (width * 0.45f), Drift = way * (0.14f * tile), Rise = 0.05f * tile,
                    SizeFrom = 0.1f * tile, SizeTo = 0.26f * tile, Sit = 0.26f,
                    Color = DustTones[i % DustTones.Length], Alpha = 0.65f, FadeIn = 0.2f, FadeFrom = 0.4f,
                    Turn = Roll(0f, 360f), Spin = Roll(-40f, 40f),
                });
            }

            // As it gives way a ring of dust runs out over the ground.
            _particles.Add(new SoftParticle
            {
                Sprite = SoftSprite.Ring, Flat = true, Born = drop + 0.04f, Life = 0.6f,
                From = foot + Vector3.up * (tile * 0.012f), SizeFrom = width, SizeTo = tile * 1.15f,
                Color = DustTones[1], Alpha = 0.55f, FadeIn = 0.1f, FadeFrom = 0.3f,
            });

            // The cloud rolls out from the foot all round and climbs the walls, swelling as it slows: thick enough to
            // take the building in.
            for (int i = 0; i < Clouds; i++)
            {
                Vector3 way = Round(i, Clouds, 0.4f);
                _particles.Add(new SoftParticle
                {
                    Sprite = SoftSprite.Puff, Born = drop + 0.3f * i / Clouds + Roll(0f, 0.04f), Life = Roll(0.95f, 1.35f),
                    From = foot + way * (width * 0.4f), Drift = way * (Roll(0.12f, 0.28f) * tile), Rise = Roll(0.14f, 0.34f) * tile,
                    SizeFrom = 0.28f * tile, SizeTo = Roll(0.44f, 0.56f) * tile, Sit = 0.26f,
                    Color = DustTones[i % DustTones.Length], Alpha = Roll(0.85f, 0.95f), FadeIn = 0.08f, FadeFrom = 0.4f,
                    Turn = Roll(0f, 360f), Spin = Roll(-50f, 50f),
                });
            }

            // And up through the falling walls, to where the roof was.
            for (int i = 0; i < Plumes; i++)
            {
                Vector3 inside = new Vector3(Roll(-0.3f, 0.3f) * width, Roll(0.2f, 0.8f) * height, Roll(-0.3f, 0.3f) * width);
                _particles.Add(new SoftParticle
                {
                    Sprite = SoftSprite.Puff, Born = drop + Roll(0.05f, 0.4f), Life = Roll(0.9f, 1.25f),
                    From = foot + inside, Drift = new Vector3(Roll(-0.1f, 0.1f), 0f, Roll(-0.1f, 0.1f)) * tile,
                    Rise = Roll(0.2f, 0.38f) * tile,
                    SizeFrom = 0.3f * tile, SizeTo = Roll(0.44f, 0.54f) * tile,
                    Color = DustTones[(i + 1) % DustTones.Length], Alpha = Roll(0.8f, 0.9f), FadeIn = 0.12f, FadeFrom = 0.4f,
                    Turn = Roll(0f, 360f), Spin = Roll(-40f, 40f),
                });
            }

            // Grit flung out of it, landing as it fades.
            for (int i = 0; i < Grit; i++)
            {
                Vector3 way = Round(i, Grit, 1.7f);
                float start = Roll(0.2f, 0.7f) * height;
                float thrown = Roll(0.2f, 0.5f) * tile;
                _particles.Add(new SoftParticle
                {
                    Sprite = SoftSprite.Dot, Born = drop + Roll(0f, 0.3f), Life = Roll(0.5f, 0.8f),
                    From = foot + Vector3.up * start, Drift = way * (Roll(0.4f, 0.75f) * tile) + Vector3.up * thrown,
                    Fall = start + thrown - tile * 0.02f, SizeFrom = 0.09f * tile, SizeTo = 0.05f * tile,
                    Color = GritTone, Alpha = 0.9f, FadeIn = 0.05f, FadeFrom = 0.8f,
                });
            }

            // What hangs in the air after: a thin haze that lifts and clears.
            for (int i = 0; i < Haze; i++)
            {
                _particles.Add(new SoftParticle
                {
                    Sprite = SoftSprite.Puff, Born = drop + 0.45f + 0.15f * i, Life = 1.3f,
                    From = foot + new Vector3(Roll(-0.1f, 0.1f), 0.1f, Roll(-0.1f, 0.1f)) * tile, Rise = 0.18f * tile,
                    SizeFrom = 0.4f * tile, SizeTo = 0.62f * tile, Sit = 0.2f,
                    Color = DustTones[2], Alpha = 0.22f, FadeIn = 0.3f, FadeFrom = 0.45f, Turn = Roll(0f, 360f), Spin = 25f,
                });
            }

            // The rubble: broken slabs of wall, of its darker courses and of mortar, thrown up and out of the lower storeys.
            Color[] tones = { Color.Lerp(wall, Color.black, 0.18f), Color.Lerp(wall, Color.black, 0.5f), Color.Lerp(wall, new Color(0.6f, 0.57f, 0.52f), 0.75f) };
            Mesh block = MeshFactory.RoundedBox(Vector3.one, 0.07f, 1);
            for (int i = 0; i < Rubble; i++)
            {
                Vector3 way = Round(i, Rubble, 1.1f);
                float size = Roll(0.045f, 0.09f) * tile;
                var chunk = new Chunk
                {
                    Piece = Block(block, tones, i % tones.Length),
                    Place = foot + new Vector3(Roll(-0.35f, 0.35f) * width, Roll(0.15f, 0.75f) * height, Roll(-0.35f, 0.35f) * width),
                    Speed = (way * Roll(0.25f, 0.7f) + Vector3.up * Roll(1.2f, 2.5f)) * tile,
                    Axis = new Vector3(Roll(-1f, 1f), Roll(-0.4f, 0.4f), Roll(-1f, 1f)).normalized,
                    Whirl = Roll(360f, 900f) * (i % 2 == 0 ? 1f : -1f),
                    Born = drop + Roll(0f, 0.3f),
                    Size = size,
                    Shape = new Vector3(1f, Roll(0.4f, 0.8f), Roll(0.55f, 1f)),
                    Floor = foot.y + size * 0.3f,
                };
                chunk.Piece.gameObject.SetActive(false);
                _chunks.Add(chunk);
            }
        }

        /// <summary>One piece of rubble; pieces of a tone share its material.</summary>
        private Transform Block(Mesh mesh, Color[] tones, int tone)
        {
            if (_tones[tone] == null)
            {
                GameObject first = ModelParts.Create("Rubble", transform, mesh, tones[tone], 0.08f);
                _tones[tone] = first.GetComponent<Renderer>().sharedMaterial;
                if (_tones[tone] != null) _materials.Add(_tones[tone]);
                return first.transform;
            }

            var part = new GameObject("Rubble");
            part.transform.SetParent(transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = _tones[tone];
            return part.transform;
        }

        /// <summary>One mesh for all the soft pieces, four corners each; only its corners and their colours change as it plays.</summary>
        private void BuildMesh()
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

            _mesh = new Mesh { name = "Leaving burst", hideFlags = HideFlags.DontSave };
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

        private void LateUpdate() => Step(Time.deltaTime);

        /// <summary>Moves the burst on by <paramref name="seconds"/> (public so a test can run it without the clock); it is gone once its time is up.</summary>
        public void Step(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            for (float left = seconds; left > 0f; left -= Tick) Tumble(Mathf.Min(left, Tick), _age + (seconds - left));
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

        /// <summary>The rubble's flight for one short step that starts at <paramref name="time"/>: thrown, pulled down, bouncing with less each time, gripping the ground, and dwindling at the end.</summary>
        private void Tumble(float step, float time)
        {
            foreach (Chunk chunk in _chunks)
            {
                float lived = time + step - chunk.Born;
                if (lived < 0f) continue;
                if (!chunk.Piece.gameObject.activeSelf) chunk.Piece.gameObject.SetActive(true);
                chunk.Speed += Vector3.down * (Gravity * _tile * step);
                chunk.Place += chunk.Speed * step;
                chunk.Turned += chunk.Whirl * step;
                if (chunk.Place.y < chunk.Floor)
                {
                    chunk.Place.y = chunk.Floor;
                    bool rests = Mathf.Abs(chunk.Speed.y) < 0.35f * _tile;
                    chunk.Speed = new Vector3(chunk.Speed.x * Grip, rests ? 0f : -chunk.Speed.y * Bounce, chunk.Speed.z * Grip);
                    chunk.Whirl *= rests ? 0f : Grip;
                }

                // Nothing is thrown off the table: at its edge a piece drops straight down.
                float edge = _bound - chunk.Size;
                if (_bound > 0f && Mathf.Abs(chunk.Place.x) > edge)
                {
                    chunk.Place.x = Mathf.Sign(chunk.Place.x) * edge;
                    chunk.Speed.x = 0f;
                }

                if (_bound > 0f && Mathf.Abs(chunk.Place.z) > edge)
                {
                    chunk.Place.z = Mathf.Sign(chunk.Place.z) * edge;
                    chunk.Speed.z = 0f;
                }

                float size = chunk.Size * RubbleSize(lived);
                chunk.Piece.localPosition = chunk.Place;
                chunk.Piece.localRotation = Quaternion.AngleAxis(chunk.Turned, chunk.Axis);
                chunk.Piece.localScale = chunk.Shape * size;
            }
        }

        /// <summary>A piece of rubble's size over its time, as a share of its own: whole while it flies and lies, then dwindling to nothing.</summary>
        public static float RubbleSize(float lived)
        {
            if (lived < 0f) return 0f;
            if (lived <= RubbleSeconds) return 1f;
            return Mathf.Max(0f, 1f - Ease.InCubic((lived - RubbleSeconds) / RubbleGone));
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
        /// <paramref name="right"/> and <paramref name="up"/>: lower left, lower right, upper right, upper left.
        /// </summary>
        public static void Quad(SoftParticle particle, float age, Vector3 right, Vector3 up, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
        {
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
            Vector3 middle = particle.Position(age);
            a = middle - across - along;
            b = middle + across - along;
            c = middle + across + along;
            d = middle - across + along;
        }

        private void OnDestroy() => Clear();

        /// <summary>Throws away what it made: the rubble's materials and the mesh of the soft pieces (their material is shared and stays).</summary>
        private void Clear()
        {
            foreach (Material material in _materials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }

            _materials.Clear();
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
            _mesh = null;
        }
    }
}
