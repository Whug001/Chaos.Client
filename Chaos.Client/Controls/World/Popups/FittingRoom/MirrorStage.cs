#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.FittingRoom;

/// <summary>
///     The fitting room's left column: an arched, gold-framed mirror on the wall showing the figure from the front, and
///     the figure itself standing before it, seen from behind, its head over the mirror's lower edge. Both are drawn at
///     2x. On <see cref="StartWalkIn" /> the figure walks up from below while its reflection walks toward the glass.
/// </summary>
/// <remarks>
///     <see cref="AislingRenderer.Render" /> allocates a fresh texture per call, so this stage owns every frame it
///     renders: they are cached per (facing, step) for the current look and disposed when the look changes and on
///     <see cref="Release" />.
/// </remarks>
public sealed class MirrorStage : UIElement
{
    public const int FACING_COUNT = 4;

    private const int SCALE = 2;
    private const int RIM = 2;

    //the world walks on frames 1-4 of each direction's five (frame 0 is standing); see AnimationSystem.GetAislingFrame
    private const int WALK_STEPS = 4;
    private const int FIRST_WALK_FRAME = 1;
    private const int STEP_MS = 114;
    private const int WALK_MS = 1000;
    private const int WALK_DISTANCE = 90;
    private const int IDLE_STEP = -1;

    private const int MIRROR_TOP = 4;
    private const int MIRROR_WIDTH = 196;
    private const int MIRROR_HEIGHT = 196;
    private const int FRAME_THICKNESS = 2;
    private const int ARCH_RADIUS = 40;
    private const int REFLECTION_CENTER_Y = MIRROR_TOP + 104;
    private const int FIGURE_CENTER_Y = 246;
    private const int FLOOR_TOP = MIRROR_TOP + MIRROR_HEIGHT + 6;

    private const int UP = 0;
    private const int RIGHT = 1;
    private const int DOWN = 2;
    private const int LEFT = 3;

    //clockwise: Up, Right, Down, Left as (frame, flip, isFront). The sheets hold up (0-4) and right (5-9); down is right
    //flipped and left is up flipped, as in MirrorPreview.
    private static readonly (int Frame, bool Flip, bool IsFront)[] Facings =
    [
        (0, false, false),
        (5, false, true),
        (5, true, true),
        (0, true, false)
    ];

    private static readonly SKColor WallFill = new(24, 22, 30);
    private static readonly SKColor GlassFill = new(46, 54, 68);
    private static readonly Color FloorColor = new(34, 29, 36);
    private static readonly Color FloorEdgeColor = new(52, 45, 50);

    private static readonly Color FrameDark = new(122, 88, 30);
    private static readonly Color SheenColor = new(14, 14, 18, 14);
    private static readonly Color ReflectionTint = new(214, 222, 236);

    //per row of the arch: how far in from each side the glass starts
    private static readonly int[] ArchInsets = BuildArchInsets();

    private readonly AislingRenderer Renderer;
    private readonly Texture2D Wall;
    private readonly Texture2D Glass;
    private readonly Texture2D Frame;
    private readonly Dictionary<(int Facing, int Step), Texture2D?> Frames = [];

    private AislingAppearance? Look;
    private int Facing = UP;
    private bool Walking;
    private double WalkElapsed;

    public MirrorStage(AislingRenderer renderer, int width, int height)
    {
        Renderer = renderer;
        Width = width;
        Height = height;
        Wall = DialogFrame.BuildRecessedTexture(WallFill, width, height);
        Glass = DialogFrame.BuildRecessedTexture(GlassFill, MIRROR_WIDTH, MIRROR_HEIGHT);
        Frame = BuildFrameTexture();
    }

    public override void Dispose()
    {
        Release();
        Wall.Dispose();
        Glass.Dispose();
        Frame.Dispose();
        base.Dispose();
    }

    /// <summary>Shows <paramref name="look" />; a different look drops the frames rendered for the old one.</summary>
    public void SetLook(AislingAppearance look)
    {
        if (Nullable.Equals(look, Look))
            return;

        ReleaseFrames();
        Look = look;
    }

    /// <summary>Turns the figure <paramref name="delta" /> quarter turns clockwise; the reflection follows. Ends a walk-in.</summary>
    public void Turn(int delta)
    {
        Walking = false;
        Facing = (Facing + delta + FACING_COUNT) % FACING_COUNT;
    }

    /// <summary>Faces the mirror and walks up to it from below.</summary>
    public void StartWalkIn()
    {
        Facing = UP;
        Walking = true;
        WalkElapsed = 0;
    }

    /// <summary>Drops every rendered frame and the look, so a closed window holds no GPU memory.</summary>
    public void Release()
    {
        ReleaseFrames();
        Look = null;
        Walking = false;
    }

    public override void OnClick(ClickEvent e)
    {
        Walking = false;
        e.Handled = true;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Walking)
            return;

        WalkElapsed += gameTime.ElapsedGameTime.TotalMilliseconds;

        if (WalkElapsed >= WALK_MS)
            Walking = false;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        DrawTexture(spriteBatch, Wall, new Vector2(ScreenX, ScreenY), Color.White);

        var floor = new Rectangle(ScreenX + RIM, ScreenY + FLOOR_TOP, Width - (2 * RIM), Height - FLOOR_TOP - RIM);
        DrawRectClipped(spriteBatch, floor, FloorColor);
        DrawRectClipped(spriteBatch, new Rectangle(floor.X, floor.Y, floor.Width, 1), FloorEdgeColor);

        var mirror = new Rectangle(ScreenX + ((Width - MIRROR_WIDTH) / 2), ScreenY + MIRROR_TOP, MIRROR_WIDTH, MIRROR_HEIGHT);
        DrawTexture(spriteBatch, Glass, new Vector2(mirror.X, mirror.Y), Color.White);

        var centerX = ScreenX + (Width / 2);
        var step = Walking ? (int)(WalkElapsed / STEP_MS) % WALK_STEPS : IDLE_STEP;
        var progress = Walking ? WalkElapsed / WALK_MS : 1.0;

        if (Look is not null)
        {
            var glass = new Rectangle(
                mirror.X + FRAME_THICKNESS,
                mirror.Y + FRAME_THICKNESS,
                MIRROR_WIDTH - (2 * FRAME_THICKNESS),
                MIRROR_HEIGHT - (2 * FRAME_THICKNESS));

            //the reflection firms up as the figure nears the glass
            var tint = ReflectionTint * (float)(0.55 + (0.45 * progress));
            DrawFigure(spriteBatch, GetFrame(Reflect(Facing), step), centerX, ScreenY + REFLECTION_CENTER_Y, glass, tint);
        }

        DrawTexture(spriteBatch, Frame, new Vector2(mirror.X, mirror.Y), Color.White);

        if (Look is null)
            return;

        var offset = (int)(WALK_DISTANCE * (1 - progress));
        var inside = new Rectangle(ScreenX + RIM, ScreenY + RIM, Width - (2 * RIM), Height - (2 * RIM));
        DrawFigure(spriteBatch, GetFrame(Facing, step), centerX, ScreenY + FIGURE_CENTER_Y + offset, inside, Color.White);
    }

    //every facing is diagonal and the glass is straight ahead, so the mirror flips only the toward/away half:
    //up-right becomes down-right and up-left becomes down-left
    private static int Reflect(int facing)
        => facing switch
        {
            UP    => RIGHT,
            RIGHT => UP,
            DOWN  => LEFT,
            _     => DOWN
        };

    private static int[] BuildArchInsets()
    {
        var insets = new int[ARCH_RADIUS];

        for (var y = 0; y < ARCH_RADIUS; y++)
        {
            var dy = ARCH_RADIUS - y - 0.5;
            insets[y] = (int)Math.Round(ARCH_RADIUS - Math.Sqrt((ARCH_RADIUS * ARCH_RADIUS) - (dy * dy)));
        }

        return insets;
    }

    /// <summary>
    ///     Bakes the gold frame drawn over the glass and its reflection into <see cref="Frame" />: the arch's top corners
    ///     are filled row by row with the wall's color, so nothing in the glass shows past the curve, then each row gets its gold edge.
    /// </summary>
    private static Texture2D BuildFrameTexture()
    {
        var info = new SKImageInfo(MIRROR_WIDTH, MIRROR_HEIGHT, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        using var wallPaint = new SKPaint { Color = WallFill, Style = SKPaintStyle.Fill };
        using var goldPaint = new SKPaint { Color = new SKColor(LegendColors.Gold.R, LegendColors.Gold.G, LegendColors.Gold.B), Style = SKPaintStyle.Fill };
        using var darkPaint = new SKPaint { Color = new SKColor(FrameDark.R, FrameDark.G, FrameDark.B), Style = SKPaintStyle.Fill };
        using var sheenPaint = new SKPaint { Color = new SKColor(SheenColor.R, SheenColor.G, SheenColor.B, SheenColor.A), Style = SKPaintStyle.Fill };
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        //baked once: the loop always runs to the last row, where it snapshots and returns
        for (var y = 0; ; y++)
        {
            var inset = y < ARCH_RADIUS ? ArchInsets[y] : 0;

            var left = inset;
            var right = MIRROR_WIDTH - inset;

            if (inset > 0)
            {
                canvas.DrawRect(0, y, inset, 1, wallPaint);
                canvas.DrawRect(right, y, inset, 1, wallPaint);
            }

            //the top and bottom edges, and the arch's own top rows, are solid gold across
            if ((y < FRAME_THICKNESS) || (y >= (MIRROR_HEIGHT - FRAME_THICKNESS)))
            {
                canvas.DrawRect(left, y, right - left, 1, y == 0 ? goldPaint : darkPaint);

                // the bottom row is solid, so the last row always returns here; the loop-end check below is only a backstop
                if (y == MIRROR_HEIGHT - 1)
                {
                    using var snapshot = surface.Snapshot();

                    return TextureConverter.ToTexture2D(snapshot);
                }

                continue;
            }

            //where the curve steps in by more than a pixel, the gold edge widens to close the gap with the row above
            var previousInset = (y > 0) && (y <= ARCH_RADIUS) ? ArchInsets[y - 1] : inset;
            var edge = Math.Max(FRAME_THICKNESS, (previousInset - inset) + 1);

            canvas.DrawRect(left, y, edge, 1, goldPaint);
            canvas.DrawRect(right - edge, y, edge, 1, goldPaint);

            //a faint diagonal sheen across the glass
            var sheenX = 30 + ((MIRROR_HEIGHT - y) / 2);
            var sheenLeft = Math.Max(sheenX, left + edge);
            var sheenRight = Math.Min(sheenX + 10, right - edge);

            if (sheenRight > sheenLeft)
                canvas.DrawRect(sheenLeft, y, sheenRight - sheenLeft, 1, sheenPaint);

            if (y == MIRROR_HEIGHT - 1)
            {
                using var snapshot = surface.Snapshot();

                return TextureConverter.ToTexture2D(snapshot);
            }
        }
    }

    /// <summary>
    ///     Draws <paramref name="figure" /> at 2x with its body centre on (<paramref name="centerX" />,
    ///     <paramref name="centerY" />), cropped in whole source pixels to <paramref name="clip" /> and this element's clip,
    ///     like <c>GuildCloakPreview</c>'s crop around the body centre.
    /// </summary>
    private void DrawFigure(SpriteBatch spriteBatch, Texture2D? figure, int centerX, int centerY, Rectangle clip, Color tint)
    {
        if (figure is null)
            return;

        var originX = centerX - (AislingRenderer.CANVAS_CENTER_X * SCALE);
        var originY = centerY - (AislingRenderer.BODY_CENTER_Y * SCALE);
        var full = new Rectangle(originX, originY, figure.Width * SCALE, figure.Height * SCALE);
        var visible = Rectangle.Intersect(Rectangle.Intersect(full, clip), ClipRect);

        if ((visible.Width <= 0) || (visible.Height <= 0))
            return;

        var left = ((visible.X - originX) + SCALE - 1) / SCALE;
        var top = ((visible.Y - originY) + SCALE - 1) / SCALE;
        var right = (visible.Right - originX) / SCALE;
        var bottom = (visible.Bottom - originY) / SCALE;

        if ((right <= left) || (bottom <= top))
            return;

        var source = new Rectangle(left, top, right - left, bottom - top);
        var destination = new Rectangle(originX + (left * SCALE), originY + (top * SCALE), source.Width * SCALE, source.Height * SCALE);

        //Render returns a plain texture (never an atlas region), so the source rectangle is the texture's own
        spriteBatch.Draw(figure, destination, source, tint);
    }

    private Texture2D? GetFrame(int facing, int step)
    {
        if (Frames.TryGetValue((facing, step), out var cached))
            return cached;

        var (frame, flip, isFront) = Facings[facing];
        var look = Look!.Value;

        //a frame the renderer cannot draw is cached as null so Draw does not retry it every frame
        var texture = step == IDLE_STEP
            ? Renderer.Render(in look, frame, AislingRenderer.IDLE_ANIM, flip, isFront)
            : Renderer.Render(in look, frame + FIRST_WALK_FRAME + step, AislingRenderer.WALK_ANIM, flip, isFront);

        Frames[(facing, step)] = texture;

        return texture;
    }

    private void ReleaseFrames()
    {
        foreach (var texture in Frames.Values)
            texture?.Dispose();

        Frames.Clear();
    }
}
