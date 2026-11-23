#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Crane;

/// <summary>
///     The crane steering controls, shown while the player steers the claw: four arrow keycaps in an inverted T labelled
///     "Steer", a wide SPACE keycap labelled "Drop", and the seconds of steering left. A pressed key's keycap lights
///     briefly. Drawn in code (no art). It only draws: it is never on the control stack, never takes focus, and is not
///     hit-tested, so clicks pass through to the world. The state is <see cref="CraneControlsState" />, which the world
///     screen owns and feeds from the server's CraneControls packet.
/// </summary>
public sealed class CraneControlsHud : UIPanel
{
    private const int KEY = 22;
    private const int GAP = 3;
    private const int GROUP_GAP = 16;
    private const int PADDING = 6;
    private const int SPACE_WIDTH = 96;

    private const int ARROWS_WIDTH = (3 * KEY) + (2 * GAP);
    private const int KEYS_HEIGHT = (2 * KEY) + GAP;

    public const int PANEL_WIDTH = PADDING + ARROWS_WIDTH + GROUP_GAP + SPACE_WIDTH + PADDING;
    public const int PANEL_HEIGHT = PADDING + KEYS_HEIGHT + 2 + TextRenderer.CHAR_HEIGHT + PADDING;

    private const string DROP_LABEL = "Drop";
    private const string SPACE_LABEL = "SPACE";
    private const string STEER_LABEL = "Steer";

    private static readonly Color Backing = new(0, 0, 0, 150);
    private static readonly Color Edge = new(150, 150, 160);
    private static readonly Color KeyFill = new(46, 44, 54);
    private static readonly Color KeyEdge = new(200, 196, 210);
    private static readonly Color LitFill = new(255, 214, 90);
    private static readonly Color LitEdge = new(255, 244, 200);
    private static readonly Color Glyph = new(226, 222, 214);
    private static readonly Color LitGlyph = new(40, 30, 10);
    private static readonly Color LabelColor = new(220, 220, 220);
    private static readonly Color SecondsColor = new(255, 230, 90);

    private readonly CraneControlsState State;

    public CraneControlsHud(CraneControlsState state)
    {
        State = state;
        Name = "CraneControlsHud";
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        Visible = false;
        IsHitTestVisible = false;
        UsesControlStack = false;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        State.Advance(gameTime.ElapsedGameTime.TotalMilliseconds);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible || !State.Active || (ClipRect.Width <= 0))
            return;

        var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);
        DrawRectClipped(spriteBatch, bounds, Backing);
        DrawBorder(spriteBatch, bounds, Edge);

        //the arrows: an inverted T, up above left/down/right
        var left = ScreenX + PADDING;
        var top = ScreenY + PADDING;
        var bottomRow = top + KEY + GAP;

        DrawKey(spriteBatch, new Rectangle(left + KEY + GAP, top, KEY, KEY), CraneKey.Up);
        DrawKey(spriteBatch, new Rectangle(left, bottomRow, KEY, KEY), CraneKey.Left);
        DrawKey(spriteBatch, new Rectangle(left + KEY + GAP, bottomRow, KEY, KEY), CraneKey.Down);
        DrawKey(spriteBatch, new Rectangle(left + (2 * (KEY + GAP)), bottomRow, KEY, KEY), CraneKey.Right);

        //the space bar, on the arrows' bottom row, with the seconds left above it
        var spaceLeft = left + ARROWS_WIDTH + GROUP_GAP;
        var space = new Rectangle(spaceLeft, bottomRow, SPACE_WIDTH, KEY);
        DrawKey(spriteBatch, space, CraneKey.Space);

        var seconds = State.SecondsText;
        DrawText(spriteBatch, space.Center.X - (TextRenderer.MeasureWidth(seconds) / 2), top + ((KEY - TextRenderer.CHAR_HEIGHT) / 2), seconds, SecondsColor);

        //the labels under each group
        var labelY = bottomRow + KEY + 2;
        DrawText(spriteBatch, left + (ARROWS_WIDTH / 2) - (TextRenderer.MeasureWidth(STEER_LABEL) / 2), labelY, STEER_LABEL, LabelColor);
        DrawText(spriteBatch, space.Center.X - (TextRenderer.MeasureWidth(DROP_LABEL) / 2), labelY, DROP_LABEL, LabelColor);
    }

    private void DrawKey(SpriteBatch spriteBatch, Rectangle key, CraneKey which)
    {
        var lit = State.IsLit(which);

        //the keycap: a raised face with a darker lip at the bottom, pressed flat while lit
        DrawRectClipped(spriteBatch, key, lit ? LitFill : KeyFill);
        DrawBorder(spriteBatch, key, lit ? LitEdge : KeyEdge);

        if (!lit)
            DrawRectClipped(spriteBatch, new Rectangle(key.X + 1, key.Bottom - 3, key.Width - 2, 2), Color.Black * 0.35f);

        var glyph = lit ? LitGlyph : Glyph;

        if (which == CraneKey.Space)
        {
            DrawText(spriteBatch, key.Center.X - (TextRenderer.MeasureWidth(SPACE_LABEL) / 2), key.Center.Y - (TextRenderer.CHAR_HEIGHT / 2), SPACE_LABEL, glyph);

            return;
        }

        DrawArrow(spriteBatch, key, which, glyph);
    }

    /// <summary>A small solid triangle pointing <paramref name="which" /> way, built from 1px rows or columns.</summary>
    private void DrawArrow(SpriteBatch spriteBatch, Rectangle key, CraneKey which, Color color)
    {
        const int SIZE = 6;

        var cx = key.Center.X;
        var cy = key.Center.Y - 1;

        for (var i = 0; i < SIZE; i++)
        {
            //row i of the triangle, from the tip (1px) to the base (2 * SIZE - 1 px)
            var span = (2 * i) + 1;

            var line = which switch
            {
                CraneKey.Up    => new Rectangle(cx - i, cy - (SIZE / 2) + i, span, 1),
                CraneKey.Down  => new Rectangle(cx - i, cy + (SIZE / 2) - i, span, 1),
                CraneKey.Left  => new Rectangle(cx - (SIZE / 2) + i, cy - i, 1, span),
                _              => new Rectangle(cx + (SIZE / 2) - i, cy - i, 1, span)
            };

            DrawRectClipped(spriteBatch, line, color);
        }
    }

    private void DrawText(SpriteBatch spriteBatch, int x, int y, string text, Color color)
        => DrawTextClipped(spriteBatch, new Vector2(x, y), text, color, false);

    private void DrawBorder(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
        DrawRectClipped(spriteBatch, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
    }
}
