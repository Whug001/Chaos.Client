#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildHall;

/// <summary>
///     The guild hall editor's own button: a soft vertical shade with 1px corner trims that read as rounded, and a
///     gold glow when selected. Draws its 12px icon above the caption when both are set, so tab and tool buttons read
///     as icon-over-label. StageButton keeps its look for the theatre; this class carries the editor's look only.
/// </summary>
public sealed class GuildHallEditorButton : UIElement
{
    private static readonly Color TopFace = new(64, 50, 36);
    private static readonly Color BottomFace = new(38, 29, 21);
    private static readonly Color TopFaceSelected = new(178, 146, 88);
    private static readonly Color BottomFaceSelected = new(138, 109, 59);
    private static readonly Color TopFaceRecessed = new(28, 23, 18);
    private static readonly Color BottomFaceRecessed = new(19, 16, 12);
    private static readonly Color TopFaceGreen = new(92, 182, 106);
    private static readonly Color BottomFaceGreen = new(52, 136, 66);
    private static readonly Color Edge = new(120, 95, 52);
    private static readonly Color EdgeRecessed = new(72, 60, 42);
    private static readonly Color EdgeDisabled = new(54, 50, 44);
    private static readonly Color GlowOuter = new(255, 222, 140);
    private static readonly Color DarkInk = new(28, 24, 20);

    private bool Hovered;
    private bool Pressed;

    public GuildHallEditorButton(string caption, int width, int height)
    {
        Caption = caption;
        Width = width;
        Height = height;
    }

    public string Caption { get; set; }
    public Texture2D? Icon { get; set; }

    /// <summary>A picture drawn fitted at the left edge, with the caption left-aligned after it (list rows).</summary>
    public Texture2D? LeadingImage { get; set; }
    public bool Selected { get; set; }

    /// <summary>An inactive tab: darker than a plain button, so the open tab reads as raised.</summary>
    public bool Recessed { get; set; }

    /// <summary>The header done button: green instead of gold when selected.</summary>
    public bool Green { get; set; }

    /// <summary>An open tab: no bottom edge, and the face runs past it, so the tab joins the area below.</summary>
    public bool JoinedBottom { get; set; }

    public event Action? Clicked;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        var bounds = ScreenBounds;
        var (top, bottom) = Face();
        DrawShadedFace(spriteBatch, bounds, top, bottom);

        if (Pressed && Enabled)
            DrawRectClipped(spriteBatch, bounds, Color.Black * 0.25f);

        DrawTrimmedEdge(spriteBatch, bounds, EdgeColor(), JoinedBottom);

        if (Selected && !Green && Enabled)
            DrawTrimmedEdge(
                spriteBatch,
                new Rectangle(bounds.X + 1, bounds.Y + 1, Width - 2, Height - 2),
                LegendColors.Gold,
                false);

        if (LeadingImage is not null)
        {
            var side = Height - 2;
            DrawTextureFitted(spriteBatch, LeadingImage, new Rectangle(bounds.X + 2, bounds.Y + 1, side, side), Color.White);
            DrawTextClipped(
                spriteBatch,
                new Vector2(bounds.X + side + 5, bounds.Y + ((Height - TextRenderer.CHAR_HEIGHT) / 2)),
                Caption,
                TextColor(),
                false);
        } else if (Icon is not null && !string.IsNullOrEmpty(Caption))
        {
            var iconX = bounds.X + ((Width - Icon.Width) / 2);
            DrawTexture(spriteBatch, Icon, new Vector2(iconX, bounds.Y + 2), IconColor());
            DrawCaption(spriteBatch, bounds.Y + Height - TextRenderer.CHAR_HEIGHT - 2);
        } else if (Icon is not null)
        {
            var iconX = bounds.X + ((Width - Icon.Width) / 2);
            var iconY = bounds.Y + ((Height - Icon.Height) / 2);
            DrawTexture(spriteBatch, Icon, new Vector2(iconX, iconY), IconColor());
        } else if (!string.IsNullOrEmpty(Caption))
            DrawCaption(spriteBatch, bounds.Y + ((Height - TextRenderer.CHAR_HEIGHT) / 2));
    }

    public override void OnMouseEnter() => Hovered = true;

    public override void OnMouseLeave()
    {
        Hovered = false;
        Pressed = false;
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if ((e.Button != MouseButton.Left) || !Enabled)
            return;

        Pressed = true;
        e.Handled = true;
    }

    public override void OnMouseUp(MouseUpEvent e) => Pressed = false;

    public override void OnClick(ClickEvent e)
    {
        if (!Enabled)
            return;

        Clicked?.Invoke();
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        Hovered = false;
        Pressed = false;
    }

    private void DrawCaption(SpriteBatch spriteBatch, int y)
    {
        var textWidth = TextRenderer.MeasureWidth(Caption);
        var x = ScreenX + ((Width - textWidth) / 2);
        DrawTextClipped(spriteBatch, new Vector2(x, y), Caption, TextColor(), false);
    }

    private (Color Top, Color Bottom) Face()
    {
        if (!Enabled)
            return (new Color(26, 22, 17), new Color(17, 14, 11));

        if (Green)
            return (TopFaceGreen, BottomFaceGreen);

        if (Selected)
            return (TopFaceSelected, BottomFaceSelected);

        return Recessed ? (TopFaceRecessed, BottomFaceRecessed) : (TopFace, BottomFace);
    }

    private Color EdgeColor()
    {
        if (!Enabled)
            return EdgeDisabled;

        if (Selected && !Green)
            return GlowOuter;

        if (Hovered)
            return LegendColors.Silver;

        return Recessed ? EdgeRecessed : Edge;
    }

    private Color TextColor()
    {
        if (!Enabled)
            return LegendColors.Gray;

        if (Selected && !Green)
            return DarkInk;

        return Hovered ? Color.White : LegendColors.Silver;
    }

    private Color IconColor()
    {
        if (!Enabled)
            return LegendColors.Gray;

        if (Selected && !Green)
            return DarkInk;

        return LegendColors.Silver;
    }

    /// <summary>Two stacked fills with the corner pixels left out, so the button reads as rounded.</summary>
    private void DrawShadedFace(SpriteBatch spriteBatch, Rectangle bounds, Color top, Color bottom)
    {
        var height = bounds.Height + (JoinedBottom && Enabled ? 2 : 0);
        var mid = height / 2;

        DrawRectClipped(spriteBatch, new Rectangle(bounds.X + 1, bounds.Y, bounds.Width - 2, mid), top);

        if (mid > 1)
            DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Y + 1, bounds.Width, mid - 1), top);

        var bottomY = bounds.Y + mid;
        var bottomHeight = height - mid;
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X + 1, bottomY, bounds.Width - 2, bottomHeight), bottom);

        if (bottomHeight > 1)
            DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bottomY, bounds.Width, bottomHeight - 1), bottom);
    }

    /// <summary>Four edge lines shortened a pixel at each end, matching the trimmed face corners.</summary>
    private void DrawTrimmedEdge(SpriteBatch spriteBatch, Rectangle bounds, Color color, bool skipBottom)
    {
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X + 1, bounds.Y, bounds.Width - 2, 1), color);
        DrawRectClipped(spriteBatch, new Rectangle(bounds.X, bounds.Y + 1, 1, bounds.Height - 2), color);
        DrawRectClipped(
            spriteBatch,
            new Rectangle(bounds.X + bounds.Width - 1, bounds.Y + 1, 1, bounds.Height - 2),
            color);

        if (!skipBottom)
            DrawRectClipped(
                spriteBatch,
                new Rectangle(bounds.X + 1, bounds.Y + bounds.Height - 1, bounds.Width - 2, 1),
                color);
    }
}
