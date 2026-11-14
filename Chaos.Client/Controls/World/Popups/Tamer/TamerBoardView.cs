#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     The battle board: a 7 by 6 flat board, halves tinted differently with a line between rows 2 and 3. Draws only
///     what <see cref="TamerBattlePlayback" /> answers: each unit's sprite at one size for its cell, standing on the
///     cell's bottom-centre and facing its target, a health bar with a white shield edge over each, ranged dots,
///     ability names, floating numbers, gold flashes and leaps along an arc. A missing sprite draws a plain
///     element-coloured marker with the unit's initial; the frame is fetched from <see cref="CreatureRenderer" />
///     every draw and never kept or disposed.
/// </summary>
public sealed class TamerBoardView : UIElement
{
    public const int CELL = 40;
    public const int BOARD_HEIGHT = 240;
    public const int BOARD_WIDTH = 280;

    private const int BAR_HEIGHT = 5;
    private const int BAR_WIDTH = 36;
    private const int LEAP_LIFT = 18;
    private const int MARKER_SIZE = 24;
    private const int PROJECTILE_SIZE = 4;

    private static readonly Color BottomHalf = new(42, 30, 26);
    private static readonly Color GridLine = new(60, 60, 70);
    private static readonly Color TopHalf = new(26, 30, 42);

    private readonly CreatureRenderer Creatures;

    public TamerBoardView(CreatureRenderer creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        Creatures = creatures;
        Width = BOARD_WIDTH;
        Height = BOARD_HEIGHT;
        IsHitTestVisible = false;
    }

    /// <summary>The playback the board draws. Null before the first fight arrives.</summary>
    public TamerBattlePlayback? Playback { get; set; }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        var playback = Playback;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);

        DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Y, bounds.Width, 3 * CELL), TopHalf);
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Y + (3 * CELL), bounds.Width, 3 * CELL), BottomHalf);
        DrawGrid(spriteBatch, bounds);
        DrawBorderClipped(spriteBatch, bounds, LegendColors.DimGray);

        if (playback is null)
            return;

        foreach (var projectile in playback.Projectiles.OrderBy(p => p.Progress))
            DrawProjectile(spriteBatch, bounds, projectile);

        foreach (var unit in playback.Units.OrderBy(u => u.DrawY).ThenBy(u => u.DrawX))
        {
            if (unit.Alive)
                DrawUnit(spriteBatch, bounds, unit);
            else if ((unit.Action == TamerBattleAction.Fading) && (unit.ActionProgress < 1.0))
                DrawUnit(spriteBatch, bounds, unit);
        }

        foreach (var floater in playback.Floaters)
            DrawFloater(spriteBatch, bounds, floater);

        foreach (var unit in playback.Units)
            if (unit.AbilityName is not null)
                DrawAbility(spriteBatch, bounds, unit);
    }

    private void DrawGrid(SpriteBatch spriteBatch, Rectangle bounds)
    {
        for (var column = 1; column < TamerBattlePlayback.BOARD_WIDTH; column++)
            DrawRectClipped(
                spriteBatch,
                new Rectangle(bounds.X + (column * CELL), bounds.Y, 1, bounds.Height),
                GridLine);

        for (var row = 1; row < TamerBattlePlayback.BOARD_DEPTH; row++)
        {
            var color = row == 3 ? LegendColors.Gold : GridLine;
            DrawRectClipped(
                spriteBatch,
                new Rectangle(bounds.X, bounds.Y + (row * CELL), bounds.Width, 1),
                color);
        }
    }

    private void DrawUnit(SpriteBatch spriteBatch, Rectangle bounds, TamerBattleUnitState unit)
    {
        var fading = (unit.Action == TamerBattleAction.Fading) && !unit.Alive;
        var opacity = fading ? (float)Math.Clamp(1 - unit.ActionProgress, 0, 1) : 1f;
        var lift = unit.Action == TamerBattleAction.Leap
            ? (int)(MathF.Sin((float)Math.Clamp(unit.ActionProgress, 0, 1) * MathF.PI) * LEAP_LIFT)
            : 0;
        var cellX = (int)(bounds.X + (unit.DrawX * CELL));
        var cellY = (int)(bounds.Y + (unit.DrawY * CELL)) - lift;
        var centreX = cellX + (CELL / 2);
        var centreY = cellY + (CELL / 2);

        if (unit.Flash == TamerBattleFlash.Gold)
            DrawBorderClipped(
                spriteBatch,
                new Rectangle(cellX, cellY, CELL, CELL),
                Fade(LegendColors.Gold, opacity));

        var (frame, flip) = TamerCreaturePortrait.GetBattleFrame(
            Creatures,
            unit.Sprite,
            unit.Facing,
            unit.Action,
            unit.ActionProgress);

        //one scale per sprite for the cell, from the bounds over its idle, walk and attack frames, and every frame
        //drawn by its anchor with the feet on the cell's bottom-centre: the unit keeps its size and does not shift
        var layout = frame is null ? default : TamerCreaturePortrait.GetBattleLayout(Creatures, unit.Sprite, CELL, CELL);

        if (frame is { } spriteFrame && spriteFrame.Texture is { Width: > 0, Height: > 0 } texture && !layout.IsEmpty)
        {
            var placed = layout.Place(TamerCreaturePortrait.BoxOf(in spriteFrame, flip));
            var dest = new Rectangle(cellX + placed.X, cellY + placed.Y, placed.Width, placed.Height);

            DrawTextureFitted(
                spriteBatch,
                texture,
                dest,
                Color.White * opacity,
                flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
        } else
        {
            var marker = new Rectangle(centreX - (MARKER_SIZE / 2), centreY - (MARKER_SIZE / 2), MARKER_SIZE, MARKER_SIZE);
            var colour = Fade(TamerMonsterRow.ElementColor(unit.Element), opacity);
            DrawRectClipped(spriteBatch, marker, colour);
            DrawBorderClipped(spriteBatch, marker, Fade(LegendColors.White, opacity));

            var initial = string.IsNullOrEmpty(unit.Name) ? "?" : unit.Name[0].ToString();
            var textWidth = TextRenderer.MeasureWidth(initial);
            DrawTextClipped(
                spriteBatch,
                new Vector2(marker.X + ((MARKER_SIZE - textWidth) / 2), marker.Y + ((MARKER_SIZE - TextRenderer.CHAR_HEIGHT) / 2)),
                initial,
                Fade(LegendColors.White, opacity),
                false);
        }

        DrawBar(spriteBatch, cellX, cellY, unit, opacity);
    }

    private void DrawBar(SpriteBatch spriteBatch, int cellX, int cellY, TamerBattleUnitState unit, float opacity)
    {
        var barX = cellX + ((CELL - BAR_WIDTH) / 2);
        var barY = cellY + 2;
        var outer = new Rectangle(barX, barY, BAR_WIDTH, BAR_HEIGHT);
        var border = unit.Shield > 0 ? Fade(LegendColors.White, opacity) : Fade(LegendColors.DimGray, opacity);

        DrawRectClipped(spriteBatch, outer, Fade(new Color(10, 8, 5), opacity));
        DrawBorderClipped(spriteBatch, outer, border);

        if ((unit.MaxHealth > 0) && (unit.Health > 0))
        {
            var fraction = (float)unit.Health / unit.MaxHealth;
            var fillWidth = Math.Clamp((int)((BAR_WIDTH - 2) * fraction), 0, BAR_WIDTH - 2);

            if (fillWidth > 0)
                DrawRectClipped(
                    spriteBatch,
                    new Rectangle(barX + 1, barY + 1, fillWidth, BAR_HEIGHT - 2),
                    Fade(HealthColor(fraction), opacity));
        }
    }

    private void DrawProjectile(SpriteBatch spriteBatch, Rectangle bounds, TamerBattleProjectile projectile)
    {
        var fromX = bounds.X + ((projectile.FromX + 0.5) * CELL);
        var fromY = bounds.Y + ((projectile.FromY + 0.5) * CELL);
        var toX = bounds.X + ((projectile.ToX + 0.5) * CELL);
        var toY = bounds.Y + ((projectile.ToY + 0.5) * CELL);
        var x = fromX + ((toX - fromX) * projectile.Progress);
        var y = fromY + ((toY - fromY) * projectile.Progress);

        DrawRectClipped(
            spriteBatch,
            new Rectangle((int)(x - (PROJECTILE_SIZE / 2)), (int)(y - (PROJECTILE_SIZE / 2)), PROJECTILE_SIZE, PROJECTILE_SIZE),
            LegendColors.Gold);
    }

    private void DrawFloater(SpriteBatch spriteBatch, Rectangle bounds, TamerBattleFloater floater)
    {
        var colour = floater.Kind switch
        {
            TamerBattleFloaterKind.Damage => LegendColors.Red,
            TamerBattleFloaterKind.Heal => LegendColors.SpringGreen,
            _ => LegendColors.White
        };
        var opacity = (float)Math.Clamp(1 - (floater.Age * 0.5), 0, 1);
        var text = floater.Amount.ToString();
        var textWidth = TextRenderer.MeasureWidth(text);
        var centreX = bounds.X + ((floater.ViewColumn + 0.5) * CELL);
        var centreY = bounds.Y + ((floater.ViewRow + 0.5) * CELL) - (floater.Age * 20);

        DrawTextClipped(
            spriteBatch,
            new Vector2((float)(centreX - (textWidth / 2)), (float)(centreY - (TextRenderer.CHAR_HEIGHT / 2))),
            text,
            colour,
            false,
            opacity);
    }

    private void DrawAbility(SpriteBatch spriteBatch, Rectangle bounds, TamerBattleUnitState unit)
    {
        var name = unit.AbilityName;

        if (string.IsNullOrEmpty(name))
            return;

        var textWidth = TextRenderer.MeasureWidth(name);
        var centreX = bounds.X + ((unit.DrawX + 0.5) * CELL);
        var top = (int)(bounds.Y + (unit.DrawY * CELL)) + 10;
        var boxWidth = textWidth + 4;

        //kept inside the board: a name over a unit in the first or last column would be cut at the board's edge
        var left = Math.Clamp((int)(centreX - (boxWidth / 2)), bounds.X, Math.Max(bounds.X, bounds.Right - boxWidth));
        var box = new Rectangle(left, top, boxWidth, TextRenderer.CHAR_HEIGHT);

        DrawRectClipped(spriteBatch, box, new Color((byte)0, (byte)0, (byte)0, (byte)180));
        DrawTextClipped(spriteBatch, new Vector2(box.X + 2, top), name, LegendColors.White, false);
    }

    private static Color HealthColor(float fraction)
        => fraction switch
        {
            > 0.5f => LegendColors.SpringGreen,
            > 0.25f => LegendColors.Orange,
            _ => LegendColors.Red
        };

    private static Color Fade(Color colour, float opacity) => colour * opacity;

    private void DrawBorderClipped(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Y + bounds.Height - 1, bounds.Width, 1), color);
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X + bounds.Width - 1, bounds.Y, 1, bounds.Height), color);
    }
}
