using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// What is in the air as a building leaves (the owner's wishes, 2026-11-17 and 18: professional, in keeping with the
    /// game, and dollar signs for the gold glints). Sold: a ring of gold spreads over the ground, the building is gone
    /// in a flash and a puff of white cloud, and dollar signs spring up out of it, gold and money green, hang, and
    /// fade. Torn down: puffs at the jolt, then cartoon dust bursting out from the foot and up its height, a ring over
    /// the ground, grit, and rubble in the colour of its walls thrown out to bounce and lie. The clouds are the game's
    /// kind: clean shapes of two tones that pop out and dwindle away, not smoke. Everything see-through is a quad of one
    /// mesh that faces the eye (<see cref="SoftParticle"/>, <see cref="SoftSprites"/>); the rubble is solid and casts
    /// shadows. It clears itself away.
    /// </summary>
    public sealed class LeavingBurst : MonoBehaviour
    {
        public const int Poofs = 9;
        public const int Dollars = 11;
        public const int Kicks = 4;
        public const int Clouds = 12;
        public const int Plumes = 8;
        public const int Caps = 4;
        public const int Grit = 10;
        public const int Wisps = 3;
        public const int Rubble = 16;

        /// <summary>
        /// The quads of each leaving (a cloud and a dollar sign are two each: its body and its lit side or rim). Sold: two
        /// rings, the flash, the puff, the dollar signs. Torn down: the jolt's puffs, a ring, the clouds, the plumes, the
        /// caps, the grit, the last wisps.
        /// </summary>
        public const int SoldParticles = 2 + 1 + 2 * Poofs + 2 * Dollars;
        public const int DemolishedParticles = 2 * Kicks + 1 + 2 * Clouds + 2 * Plumes + 2 * Caps + Grit + 2 * Wisps;

        /// <summary>How the rubble falls, bounces and lies, in widths of the square (per second where it is a speed).</summary>
        public const float Gravity = 7.5f;
        public const float Bounce = 0.32f;
        public const float Grip = 0.5f;
        public const float RubbleSeconds = 1.3f;
        public const float RubbleGone = 0.3f;

        /// <summary>How far from the middle of the building the dust up its walls starts, in its widths: at its walls.</summary>
        public const float PlumeReach = 0.6f;

        /// <summary>How much of its height the dust at the walls comes down in its life, and the dust before the building: they fall in with it.</summary>
        public const float PlumeSinks = 0.6f;
        public const float CapSinks = 0.75f;

        /// <summary>The longest step the rubble is moved in one go, so a slow frame cannot throw it through the ground.</summary>
        public const float Tick = 1f / 90f;

        /// <summary>Gold and money green, as the game's gold bars and banknotes, each with the dark rim that keeps a sign readable over any square.</summary>
        public static readonly Color32 Gold = new Color32(255, 204, 64, 255);
        public static readonly Color32 GoldRim = new Color32(124, 74, 10, 255);
        public static readonly Color32 Money = new Color32(126, 200, 110, 255);
        public static readonly Color32 MoneyRim = new Color32(28, 88, 44, 255);
        public static readonly Color32 PaleGold = new Color32(255, 232, 160, 255);
        public static readonly Color32 Light = new Color32(255, 247, 222, 255);

        /// <summary>The sale's puff: white cloud, a touch warm, and its lit side.</summary>
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
        private float _fore;
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
            var root = new GameObject(how == Leaving.Demolished ? "Demolition dust" : "Sale puff");
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

        /// <summary>Lays a shape of two tones: <paramref name="body"/> as given, and over it the same piece wearing <paramref name="top"/> in <paramref name="tone"/>.</summary>
        private void Pair(SoftParticle body, SoftSprite top, Color32 tone)
        {
            _particles.Add(body);
            body.Sprite = top;
            body.Color = tone;
            _particles.Add(body);
        }

        /// <summary>
        /// How far towards the eye the clouds of a building <paramref name="width"/> across and <paramref name="height"/>
        /// high are drawn, to come in front of it wherever round it they are: without it the walls and the roof cut
        /// them, and the building is seen falling in the open in the middle of its own dust.
        /// </summary>
        public static float Fore(float width, float height) => width * 0.8f + height * 0.6f;

        /// <summary>A dust tone's lit side.</summary>
        public static Color32 Lit(Color32 tone) => Color32.Lerp(tone, DustLight, DustLit);

        private void LaySale(Vector3 foot, float width, float height, float tile, float lift)
        {
            float pop = LeavingMotion.SoldSeconds * LeavingMotion.PopAt;
            // The middle of the building as it pops, sprung up tall off its tile: where it dwindles to.
            Vector3 heart = foot + Vector3.up * (lift * LeavingMotion.SpringLift + height * (1f + LeavingMotion.Spring) * 0.5f);

            // Two rings of gold over the ground as it pops, the second fainter and a breath later.
            for (int i = 0; i < 2; i++)
            {
                _particles.Add(new SoftParticle
                {
                    Sprite = SoftSprite.Ring, Flat = true, Born = pop - 0.04f + 0.11f * i, Life = 0.55f,
                    From = foot + Vector3.up * (tile * 0.012f), SizeFrom = width * 0.8f, SizeTo = Mathf.Min(tile * 1.15f, Mathf.Max(width * 2.4f, tile * 0.8f)),
                    Color = i == 0 ? Gold : PaleGold, Alpha = i == 0 ? 1f : 0.65f, FadeIn = 0.12f, FadeFrom = 0.35f,
                });
            }

            // The flash it goes in.
            _particles.Add(new SoftParticle
            {
                Sprite = SoftSprite.Dot, Born = pop - 0.05f, Life = 0.4f, From = heart,
                SizeFrom = width * 0.8f, SizeTo = Mathf.Min(tile * 1.4f, width * 2.6f),
                Color = Light, Alpha = 0.95f, FadeIn = 0.12f, FadeFrom = 0.3f,
            });

            // The puff that takes it: one cloud where it stood and a wreath of them bursting out, popping and dwindling.
            for (int i = 0; i < Poofs; i++)
            {
                bool middle = i == 0;
                Vector3 way = Round(i, Poofs - 1, 0.3f);
                float up = Roll(-0.15f, 0.75f);
                Vector3 off = middle ? Vector3.zero : (way * Mathf.Sqrt(1f - up * up * 0.6f) + Vector3.up * up).normalized;
                float size = (middle ? Roll(0.5f, 0.56f) : Roll(0.3f, 0.42f)) * tile;
                Pair(new SoftParticle
                {
                    Sprite = SoftSprite.Cloud, Born = pop - 0.03f + (middle ? 0f : Roll(0f, 0.07f)), Life = middle ? 0.5f : Roll(0.5f, 0.7f),
                    From = heart + off * (width * 0.2f), Drift = off * (Roll(0.2f, 0.36f) * tile), Rise = 0.06f * tile,
                    SizeFrom = size * 0.6f, SizeTo = size, Pop = 0.22f, ShrinkFrom = 0.4f,
                    Color = Puff, Alpha = 1f, FadeFrom = 0.9f, Turn = Roll(-18f, 18f), Fore = Fore(width, height),
                }, SoftSprite.CloudLight, PuffLight);
            }

            // The dollar signs: up out of the puff one after another, popping in, hanging, drooping a little as they fade.
            for (int i = 0; i < Dollars; i++)
            {
                Vector3 way = Round(i, Dollars, 1.1f);
                bool green = i % 3 == 2;
                float size = Roll(0.26f, 0.38f) * tile;
                Pair(new SoftParticle
                {
                    Sprite = SoftSprite.DollarEdge, Born = pop + 0.02f + 0.3f * i / Dollars + Roll(0f, 0.03f), Life = Roll(1f, 1.4f),
                    From = heart + way * (width * 0.15f),
                    Drift = way * (Roll(0.12f, 0.46f) * tile) + Vector3.up * (Roll(0.3f, 0.7f) * tile), Fall = Roll(0.06f, 0.16f) * tile,
                    SizeFrom = size, SizeTo = size, Pop = 0.16f, ShrinkFrom = 0.8f,
                    Color = green ? MoneyRim : GoldRim, Alpha = 1f, FadeFrom = 0.72f, Turn = Roll(-14f, 14f), Spin = Roll(-20f, 20f),
                }, SoftSprite.Dollar, green ? Money : Gold);
            }
        }

        /// <summary>A cloud of dust and its lit side.</summary>
        private void Dust(SoftParticle cloud, int tone)
        {
            cloud.Sprite = SoftSprite.Cloud;
            cloud.Color = DustTones[tone % DustTones.Length];
            cloud.Alpha = 1f;
            cloud.FadeFrom = 0.9f;
            cloud.Turn = Roll(-18f, 18f);
            cloud.Fore = _fore;
            Pair(cloud, SoftSprite.CloudLight, Lit(cloud.Color));
        }

        private void LayDemolition(Vector3 foot, float width, float height, float tile, Color wall)
        {
            float drop = LeavingMotion.DemolishedSeconds * LeavingMotion.Jolt;

            // The jolt kicks small puffs out from under the walls.
            for (int i = 0; i < Kicks; i++)
            {
                Vector3 way = Round(i, Kicks, 0.9f);
                Dust(new SoftParticle
                {
                    Born = Roll(0f, 0.05f), Life = 0.45f,
                    From = foot + way * (width * 0.45f), Drift = way * (0.14f * tile), Rise = 0.04f * tile,
                    SizeFrom = 0.1f * tile, SizeTo = 0.22f * tile, Sit = 0.3f, Pop = 0.3f, ShrinkFrom = 0.4f,
                }, i);
            }

            // From here on the dust is drawn in front of the building: it is what the building goes down into.
            _fore = Fore(width, height);

            // As it gives way a ring of dust runs out over the ground.
            _particles.Add(new SoftParticle
            {
                Sprite = SoftSprite.Ring, Flat = true, Born = drop + 0.03f, Life = 0.5f,
                From = foot + Vector3.up * (tile * 0.012f), SizeFrom = width, SizeTo = tile * 1.15f,
                Color = DustTones[1], Alpha = 0.7f, FadeIn = 0.1f, FadeFrom = 0.3f,
            });

            // The dust bursts out from the foot all round, each cloud a moment after the one before: out fast, swelling,
            // then dwindling where it has stopped.
            for (int i = 0; i < Clouds; i++)
            {
                Vector3 way = Round(i, Clouds, 0.4f);
                Dust(new SoftParticle
                {
                    Born = drop + 0.25f * i / Clouds + Roll(0f, 0.03f), Life = Roll(0.8f, 1.15f),
                    From = foot + way * (width * 0.35f), Drift = way * (Roll(0.12f, 0.24f) * tile), Rise = Roll(0.05f, 0.18f) * tile,
                    SizeFrom = 0.16f * tile, SizeTo = Roll(0.3f, 0.4f) * tile, Sit = 0.3f, Pop = 0.2f, ShrinkFrom = 0.5f,
                }, i);
            }

            // And up its walls all round, coming down with them, so it drops out of sight into dust, not into the open.
            for (int i = 0; i < Plumes; i++)
            {
                Vector3 way = Round(i, Plumes, 0.2f);
                float start = Roll(0.3f, 0.8f) * height;
                Dust(new SoftParticle
                {
                    Born = drop + 0.05f + 0.25f * i / Plumes + Roll(0f, 0.04f), Life = Roll(0.85f, 1.1f),
                    From = foot + way * (width * PlumeReach) + Vector3.up * start,
                    Drift = way * (Roll(0.02f, 0.08f) * tile), Rise = -start * PlumeSinks,
                    SizeFrom = 0.2f * tile, SizeTo = Roll(0.36f, 0.46f) * tile, Pop = 0.2f, ShrinkFrom = 0.55f,
                }, i + 1);
            }

            // And a column of it before the building itself, from near its foot to its roof, falling in as it does: the
            // middle of the cloud, which a ring of dust alone leaves open.
            for (int i = 0; i < Caps; i++)
            {
                float start = Mathf.Lerp(0.2f, 0.9f, i / (Caps - 1f)) * height;
                Dust(new SoftParticle
                {
                    Born = drop + 0.1f + 0.05f * i, Life = Roll(0.85f, 0.95f),
                    From = foot + new Vector3(Roll(-0.15f, 0.15f) * width, start, Roll(-0.15f, 0.15f) * width), Rise = -start * CapSinks,
                    SizeFrom = 0.22f * tile, SizeTo = Roll(0.4f, 0.46f) * tile, Pop = 0.2f, ShrinkFrom = 0.55f,
                }, i);
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
                    From = foot + Vector3.up * start, Drift = way * (Roll(0.4f, 0.7f) * tile) + Vector3.up * thrown,
                    Fall = start + thrown - tile * 0.02f, SizeFrom = 0.09f * tile, SizeTo = 0.05f * tile,
                    Color = GritTone, Alpha = 0.9f, FadeIn = 0.05f, FadeFrom = 0.8f,
                });
            }

            // The last of it: a few small clouds lifting off where it stood.
            for (int i = 0; i < Wisps; i++)
            {
                Dust(new SoftParticle
                {
                    Born = LeavingMotion.DemolishedSeconds - 0.1f + 0.12f * i, Life = 0.7f,
                    From = foot + new Vector3(Roll(-0.2f, 0.2f) * width, 0f, Roll(-0.2f, 0.2f) * width), Rise = 0.2f * tile,
                    SizeFrom = 0.12f * tile, SizeTo = 0.24f * tile, Sit = 0.3f, Pop = 0.25f, ShrinkFrom = 0.45f,
                }, i + 2);
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
