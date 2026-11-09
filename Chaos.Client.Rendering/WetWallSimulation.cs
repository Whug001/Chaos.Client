using Microsoft.Xna.Framework;

namespace Chaos.Client.Rendering;

/// <summary>Water on walls and floors for rainy maps. Holds no graphics resources.</summary>
public sealed class WetWallSimulation
{
    public const int MAX_DROPS = 180;
    public const int MAX_STREAKS = 1200;
    public const int MAX_BEADS = 200;
    public const int MAX_RINGS = 150;
    public const int MAX_SPLASHES = 200;
    public const int MAX_PATCHES = 120;

    private const float RING_EVERY = 0.055f;
    private const float DROP_EVERY = 0.09f;
    private const float FACE_REFRESH = 0.25f;

    public sealed class Drop
    {
        public float X, Y, Base, Hold, V, Size, LastWet, NextMeander;
    }

    public record struct Fade(float X, float Y, float Size, float Age, float Life, float Alpha);

    public record struct Splash(float X, float Y, float VX, float VY, float Age, float Life);

    private readonly Random Random;
    private readonly List<WallFace> VisibleFaces = [];
    private float NextRing;
    private float NextDrop;
    private float NextFaceRefresh;

    public List<Drop> Drops { get; } = [];
    public List<Fade> Streaks { get; } = [];
    public List<Fade> Beads { get; } = [];
    public List<Fade> Rings { get; } = [];
    public List<Fade> Patches { get; } = [];
    public List<Splash> Splashes { get; } = [];

    public WetWallSimulation(Random random) => Random = random;

    private float Roll(float min, float max) => min + ((max - min) * Random.NextSingle());

    public void Clear()
    {
        Drops.Clear();
        Streaks.Clear();
        Beads.Clear();
        Rings.Clear();
        Patches.Clear();
        Splashes.Clear();
        VisibleFaces.Clear();
        NextFaceRefresh = 0f;
    }

    public void Step(float dt, Rectangle visibleWorld, MapSurface? surface, bool spawning)
    {
        if (spawning && surface is not null)
            Spawn(dt, visibleWorld, surface);

        for (var i = Drops.Count - 1; i >= 0; i--)
            if (StepDrop(Drops[i], dt))
                RemoveSwapBack(Drops, i);

        Age(Streaks, dt);
        Age(Beads, dt);
        Age(Rings, dt);
        Age(Patches, dt);

        for (var i = Splashes.Count - 1; i >= 0; i--)
        {
            var s = Splashes[i];
            s.Age += dt;

            if (s.Age >= s.Life)
            {
                RemoveSwapBack(Splashes, i);

                continue;
            }

            s.VY += 240f * dt;
            s.X += s.VX * dt;
            s.Y += s.VY * dt;
            Splashes[i] = s;
        }
    }

    private void Spawn(float dt, Rectangle visibleWorld, MapSurface surface)
    {
        NextFaceRefresh -= dt;

        if (NextFaceRefresh <= 0f)
        {
            NextFaceRefresh = FACE_REFRESH;
            RefreshFaces(visibleWorld, surface);
        }

        NextRing -= dt;

        while (NextRing <= 0f)
        {
            NextRing += RING_EVERY;
            var x = Roll(visibleWorld.Left, visibleWorld.Right);
            var y = Roll(visibleWorld.Top, visibleWorld.Bottom);

            if (surface.IsFloorAtWorld(x, y))
            {
                AddRing(x, y, Roll(8f, 15f));

                if (Random.NextSingle() < 0.35f)
                    AddSplashes(x, y, 2, 42f);
            }
        }

        NextDrop -= dt;

        while (NextDrop <= 0f)
        {
            NextDrop += DROP_EVERY;

            if ((VisibleFaces.Count > 0) && (Drops.Count < MAX_DROPS))
                AddDrop(VisibleFaces[Random.Next(VisibleFaces.Count)]);
        }
    }

    private void RefreshFaces(Rectangle visibleWorld, MapSurface surface)
    {
        VisibleFaces.Clear();

        foreach (var face in surface.WallFaces)
        {
            var foot = MathF.Max(face.BaseWorldY, face.BaseWorldY + (face.BaseSlope * MapSurface.FACE_WIDTH));

            if ((face.WorldX + MapSurface.FACE_WIDTH >= visibleWorld.Left)
                && (face.WorldX <= visibleWorld.Right)
                && (foot >= visibleWorld.Top)
                && (foot - face.Height <= visibleWorld.Bottom))
                VisibleFaces.Add(face);
        }
    }

    private void AddDrop(WallFace face)
    {
        var column = Random.Next(MapSurface.FACE_WIDTH);
        var foot = face.BaseWorldY + (face.BaseSlope * column);

        var wall = face.Height - 14;

        Drops.Add(new Drop
        {
            X = face.WorldX + column,
            Y = foot - wall + Roll(0f, wall * 0.3f),
            Base = foot,
            Hold = Roll(0.2f, 1.1f),
            Size = Roll(1f, 1.3f),
            LastWet = float.MinValue
        });
    }

    private bool StepDrop(Drop drop, float dt)
    {
        if (drop.Hold > 0f)
        {
            drop.Hold -= dt;
            drop.Size = MathF.Min(1.9f, drop.Size + (dt * 0.7f));

            if (drop.Hold <= 0f)
                drop.V = Roll(35f, 65f);

            return false;
        }

        var before = drop.Y;
        drop.V = MathF.Min(180f, drop.V + (280f * drop.Size * dt));
        drop.Y += drop.V * dt;
        drop.Size = MathF.Max(0.9f, drop.Size - (dt * 0.35f));

        if (drop.Y >= drop.NextMeander)
        {
            drop.NextMeander = drop.Y + Roll(7f, 16f);
            drop.X += Random.Next(4) switch { 0 => -1, 1 => 1, _ => 0 };
        }

        if (drop.Y - drop.LastWet >= 6f)
        {
            drop.LastWet = drop.Y;

            if (Streaks.Count < MAX_STREAKS)
                Streaks.Add(new Fade(drop.X, MathF.Floor(before), 7f, 0f, Roll(3.5f, 6.5f), Roll(0.14f, 0.26f)));

            if ((Random.NextSingle() < 0.28f) && (Beads.Count < MAX_BEADS))
                Beads.Add(new Fade(drop.X, MathF.Floor(drop.Y - 4f), 2f, 0f, Roll(1.1f, 2.8f), Roll(0.55f, 0.9f)));
        }

        if (drop.Y >= drop.Base)
        {
            AddRing(drop.X, drop.Base + 1f, Roll(7f, 11f));
            AddSplashes(drop.X, drop.Base, 3, 58f);

            if (Patches.Count < MAX_PATCHES)
                Patches.Add(new Fade(drop.X, drop.Base + 1f, Roll(5f, 10f), 0f, Roll(4f, 7f), 0.2f));

            return true;
        }

        if (Random.NextSingle() < (dt * 1.5f))
        {
            drop.Hold = Roll(0.12f, 0.6f);
            drop.V = 0f;
        }

        return false;
    }

    private void AddRing(float x, float y, float radius)
    {
        if (Rings.Count < MAX_RINGS)
            Rings.Add(new Fade(x, y, radius, 0f, Roll(0.5f, 0.85f), 0.55f));
    }

    private void AddSplashes(float x, float y, int count, float up)
    {
        for (var i = 0; (i < count) && (Splashes.Count < MAX_SPLASHES); i++)
            Splashes.Add(new Splash(x, y, Roll(-24f, 24f), Roll(-up, -up * 0.45f), 0f, Roll(0.22f, 0.4f)));
    }

    private static void Age(List<Fade> list, float dt)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var item = list[i];
            item.Age += dt;

            if (item.Age >= item.Life)
                RemoveSwapBack(list, i);
            else
                list[i] = item;
        }
    }

    private static void RemoveSwapBack<T>(List<T> list, int index)
    {
        list[index] = list[^1];
        list.RemoveAt(list.Count - 1);
    }
}
