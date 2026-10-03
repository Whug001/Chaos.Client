#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering.Fishing;

/// <summary>The pole's three shades: the shadow row under it, its body, and the lit tip.</summary>
public readonly record struct FishingPoleColors(Color Shadow, Color Body, Color Tip);

/// <summary>
///     Draws a fishing pose's pole, line, bobber and splash pixel by pixel on top of a finished aisling composite. The
///     pole replaces the equipped pole's own art while the pose plays.
/// </summary>
public static class FishingPoleRenderer
{
    public const int POLE_LENGTH = 30;

    private const int REEL_AT = 5;
    private const float PULL = 10f;

    private static readonly Color Reel = new(51, 51, 51);
    private static readonly Color ReelLight = new(67, 67, 67);
    private static readonly Color Line = new(235, 235, 235);
    private static readonly Color BobberRed = new(220, 40, 40);
    private static readonly Color BobberWhite = new(250, 250, 250);
    private static readonly Color Splash = new(200, 230, 255);

    /// <summary>
    ///     The shades of each fishing pole's display sprite, taken from its own palette. Unknown sprites get the basic
    ///     wooden pole.
    /// </summary>
    public static FishingPoleColors ColorsFor(int weaponSprite)
        => weaponSprite switch
        {
            207 => new FishingPoleColors(new Color(0, 99, 0), new Color(0, 119, 0), new Color(0, 219, 0)),
            206 => new FishingPoleColors(new Color(31, 31, 103), new Color(43, 43, 119), new Color(83, 87, 203)),
            210 => new FishingPoleColors(new Color(87, 87, 111), new Color(103, 103, 127), new Color(227, 167, 227)),
            211 => new FishingPoleColors(new Color(95, 7, 15), new Color(115, 11, 23), new Color(203, 0, 23)),
            _   => new FishingPoleColors(new Color(99, 59, 11), new Color(123, 79, 23), new Color(211, 167, 103))
        };

    /// <summary>
    ///     The pole's pixels from the hand to the tip, in composite coordinates. A bent pole curves toward
    ///     <paramref name="pullToward" />, more strongly near the tip.
    /// </summary>
    public static Point[] PolePoints(Point hand, float angle, float bend, Point? pullToward)
    {
        var radians = MathHelper.ToRadians(angle);
        var tipX = hand.X + MathF.Cos(radians) * POLE_LENGTH;
        var tipY = hand.Y - MathF.Sin(radians) * POLE_LENGTH;
        var pull = Vector2.Zero;

        if ((bend > 0f) && pullToward is { } toward)
        {
            var delta = new Vector2(toward.X - tipX, toward.Y - tipY);

            if (delta.LengthSquared() > 0.01f)
                pull = Vector2.Normalize(delta) * (bend * PULL);
        }

        var points = new Point[POLE_LENGTH + 1];

        for (var s = 0; s <= POLE_LENGTH; s++)
        {
            var t = s / (float)POLE_LENGTH;
            var x = hand.X + (tipX - hand.X) * t + pull.X * t * t;
            var y = hand.Y + (tipY - hand.Y) * t + pull.Y * t * t;
            points[s] = new Point((int)MathF.Round(x), (int)MathF.Round(y));
        }

        return points;
    }

    /// <summary>The pixels of a one-pixel line from <paramref name="from" /> to <paramref name="to" />, both ends included.</summary>
    public static IEnumerable<Point> LinePoints(Point from, Point to)
    {
        int x = from.X, y = from.Y;
        var dx = Math.Abs(to.X - x);
        var dy = -Math.Abs(to.Y - y);
        var sx = x < to.X ? 1 : -1;
        var sy = y < to.Y ? 1 : -1;
        var err = dx + dy;

        while (true)
        {
            yield return new Point(x, y);

            if ((x == to.X) && (y == to.Y))
                yield break;

            var e2 = 2 * err;

            if (e2 >= dy)
            {
                err += dy;
                x += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    /// <summary>
    ///     Draws one shot over the aisling just drawn. The origin maths mirrors <see cref="AislingRenderer.Draw" />, so
    ///     composite pixels land on the sprite's pixels, flipped with it for the Down and Left facings.
    /// </summary>
    public static void Draw(
        SpriteBatch batch,
        Camera camera,
        in FishingPoseShot shot,
        FishingPoleColors colors,
        float tileCenterX,
        float tileCenterY,
        Vector2 visualOffset,
        int topPadding,
        bool flip,
        float alpha)
    {
        var baseX = tileCenterX + visualOffset.X - AislingRenderer.CANVAS_CENTER_X;
        var baseY = tileCenterY + visualOffset.Y - AislingRenderer.CANVAS_CENTER_Y - topPadding;
        var origin = camera.WorldToScreen(new Vector2(baseX, baseY));
        var pen = new Pen(batch, origin, flip, alpha);

        var pole = PolePoints(shot.Hand, shot.Angle, shot.Bend, shot.LineEnd);
        var tip = pole[^1];

        if (shot.LineEnd is { } end)
        {
            foreach (var p in LinePoints(tip, end))
                pen.Dot(p, Line);

            if (shot.Splash >= 0)
                DrawSplash(pen, end, shot.Splash);

            if (shot.ShowBobber)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    pen.Dot(new Point(end.X + dx, end.Y - 1), BobberRed);
                    pen.Dot(new Point(end.X + dx, end.Y), BobberRed);
                }

                pen.Dot(new Point(end.X, end.Y + 1), BobberWhite);
            }
        }

        foreach (var p in pole)
            pen.Dot(new Point(p.X, p.Y + 1), colors.Shadow);

        for (var i = 0; i < pole.Length; i++)
            pen.Dot(pole[i], i >= pole.Length - 3 ? colors.Tip : colors.Body);

        var reel = pole[REEL_AT];
        pen.Dot(new Point(reel.X, reel.Y + 1), Reel);
        pen.Dot(new Point(reel.X + 1, reel.Y + 1), Reel);
        pen.Dot(new Point(reel.X, reel.Y + 2), ReelLight);
        pen.Dot(new Point(reel.X + 1, reel.Y + 2), Reel);
    }

    private static void DrawSplash(Pen pen, Point at, int step)
    {
        var r = 2 + step % 3;

        for (var i = 0; i < 16; i++)
        {
            var a = i * MathF.Tau / 16f;
            pen.Dot(new Point(at.X + (int)MathF.Round(MathF.Cos(a) * r * 2), at.Y + (int)MathF.Round(MathF.Sin(a) * r)), Splash);
        }
    }

    private readonly record struct Pen(SpriteBatch Batch, Vector2 Origin, bool Flip, float Alpha)
    {
        public void Dot(Point p, Color color)
        {
            var x = Flip ? AislingRenderer.MirrorX(p.X) : p.X;
            RenderHelper.DrawRect(Batch, new Rectangle((int)Origin.X + x, (int)Origin.Y + p.Y, 1, 1), color * Alpha);
        }
    }
}
