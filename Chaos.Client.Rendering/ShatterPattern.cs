#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>One piece of the shattered screen. <see cref="Direction" /> is in radians, <see cref="Speed" /> in pixels per second.</summary>
public sealed record ShatterShard(
    int Index,
    int Ring,
    Rectangle Bounds,
    int PixelCount,
    Vector2 Centroid,
    float Direction,
    float Speed,
    float Spin,
    double Delay);

/// <summary>
///     Cuts a frame into glass shards around a point near its centre. <see cref="RAYS" /> jittered rays split it into
///     sectors, and straight chords between points on neighbouring rays split each sector into <see cref="RINGS" />
///     rings, so every pixel belongs to exactly one shard.
/// </summary>
public sealed class ShatterPattern
{
    public const int RAYS = 11;
    public const int RINGS = 3;
    private const float GRAVITY = 1500f;

    public static readonly Color CrackColour = new(225, 235, 255);

    //inner and outer chord radii, before each ray's own jitter. the outer one stays under the shortest centre-to-edge
    //distance so no outer shard is empty
    private static readonly (float Base, float Spread, float Jitter)[] Chords = [(70, 20, 12), (150, 30, 15)];

    private readonly int[] Owners;

    public int Width { get; }
    public int Height { get; }
    public Vector2 Centre { get; }
    public IReadOnlyList<ShatterShard> Shards { get; }

    private ShatterPattern(int width, int height, Vector2 centre, int[] owners, IReadOnlyList<ShatterShard> shards)
    {
        Width = width;
        Height = height;
        Centre = centre;
        Owners = owners;
        Shards = shards;
    }

    public static ShatterPattern Build(int seed, int width, int height)
    {
        var random = new Random(seed);
        var centre = new Vector2(width / 2f + Jitter(random, 20), height / 2f + Jitter(random, 15));
        var first = random.NextSingle() * MathF.Tau;
        var angles = new float[RAYS];

        for (var i = 0; i < RAYS; i++)
            angles[i] = first + i * MathF.Tau / RAYS + Jitter(random, 0.2f);

        var chordEnds = new Vector2[Chords.Length, RAYS];

        for (var k = 0; k < Chords.Length; k++)
        {
            var radius = Chords[k].Base + random.NextSingle() * Chords[k].Spread;

            for (var i = 0; i < RAYS; i++)
            {
                var r = radius + Jitter(random, Chords[k].Jitter);
                chordEnds[k, i] = centre + new Vector2(MathF.Cos(angles[i]), MathF.Sin(angles[i])) * r;
            }
        }

        var owners = new int[width * height];
        var count = new int[RAYS * RINGS];
        var sum = new Vector2[RAYS * RINGS];
        var min = Enumerable.Repeat(new Point(int.MaxValue, int.MaxValue), RAYS * RINGS).ToArray();
        var max = Enumerable.Repeat(new Point(int.MinValue, int.MinValue), RAYS * RINGS).ToArray();

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var sector = SectorOf(angles, p - centre);
                var ring = 0;

                for (var k = 0; k < Chords.Length; k++)
                    if (IsBeyond(chordEnds[k, sector], chordEnds[k, (sector + 1) % RAYS], centre, p))
                        ring++;

                var owner = sector * RINGS + ring;
                owners[y * width + x] = owner;
                count[owner]++;
                sum[owner] += p;
                min[owner] = new Point(Math.Min(min[owner].X, x), Math.Min(min[owner].Y, y));
                max[owner] = new Point(Math.Max(max[owner].X, x), Math.Max(max[owner].Y, y));
            }

        var shards = new ShatterShard[RAYS * RINGS];

        for (var i = 0; i < shards.Length; i++)
        {
            var sector = i / RINGS;
            var ring = i % RINGS;
            var next = sector + 1 < RAYS ? angles[sector + 1] : angles[0] + MathF.Tau;
            var bounds = count[i] == 0 ? Rectangle.Empty : new Rectangle(min[i].X, min[i].Y, max[i].X - min[i].X + 1, max[i].Y - min[i].Y + 1);
            var centroid = count[i] == 0 ? centre : sum[i] / count[i];

            shards[i] = new ShatterShard(
                i,
                ring,
                bounds,
                count[i],
                centroid,
                (angles[sector] + next) / 2,
                500 + random.NextSingle() * 800,
                Jitter(random, 5),
                ring * 0.025 + random.NextDouble() * 0.04);
        }

        return new ShatterPattern(width, height, centre, owners, shards);
    }

    public int ShardAt(int x, int y) => Owners[y * Width + x];

    /// <summary>
    ///     Each shard's pixels from <paramref name="frame" />, one array per shard sized to its bounds. Pixels outside the
    ///     shard are transparent; pixels next to another shard are <see cref="CrackColour" />.
    /// </summary>
    public Color[][] Cut(Color[] frame)
    {
        var pieces = Shards.Select(shard => new Color[shard.Bounds.Width * shard.Bounds.Height]).ToArray();

        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var owner = Owners[y * Width + x];
                var bounds = Shards[owner].Bounds;
                var crack = ((x > 0) && (Owners[y * Width + x - 1] != owner))
                            || ((x < Width - 1) && (Owners[y * Width + x + 1] != owner))
                            || ((y > 0) && (Owners[(y - 1) * Width + x] != owner))
                            || ((y < Height - 1) && (Owners[(y + 1) * Width + x] != owner));

                pieces[owner][(y - bounds.Y) * bounds.Width + (x - bounds.X)] = crack ? CrackColour : frame[y * Width + x] with { A = 255 };
            }

        return pieces;
    }

    /// <summary>Where a shard has moved, and how far it has turned, <paramref name="secondsSinceBreak" /> after the glass breaks.</summary>
    public static (Vector2 Offset, float Rotation) Motion(ShatterShard shard, double secondsSinceBreak)
    {
        var t = (float)Math.Max(0, secondsSinceBreak - shard.Delay);
        var flight = new Vector2(MathF.Cos(shard.Direction), MathF.Sin(shard.Direction)) * shard.Speed * t;

        return (flight + new Vector2(0, GRAVITY * t * t), shard.Spin * t);
    }

    private static int SectorOf(float[] angles, Vector2 offset)
    {
        var relative = MathF.Atan2(offset.Y, offset.X) - angles[0];
        relative -= MathF.Floor(relative / MathF.Tau) * MathF.Tau;

        var sector = 0;

        for (var i = 1; i < RAYS; i++)
            if (relative >= angles[i] - angles[0])
                sector = i;

        return sector;
    }

    //true when p is on the far side of the chord a-b from the centre
    private static bool IsBeyond(Vector2 a, Vector2 b, Vector2 centre, Vector2 p)
    {
        var edge = b - a;
        var sideOfCentre = Cross(edge, centre - a);
        var sideOfPoint = Cross(edge, p - a);

        return sideOfCentre * sideOfPoint < 0;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static float Jitter(Random random, float amount) => (random.NextSingle() * 2 - 1) * amount;
}
