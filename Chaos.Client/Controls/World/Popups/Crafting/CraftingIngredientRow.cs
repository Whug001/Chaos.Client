using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.Crafting;

/// <summary>One ingredient in the confirm window: a half-size icon, the name and "have / need" on the right, red when short.</summary>
public sealed class CraftingIngredientRow : UIPanel
{
    public const int HEIGHT = 20;
    private const int ICON_X = 1;
    private const int NAME_GAP = 2;
    private const int NAME_X = 20;

    private readonly UILabel CountLabel;
    private readonly UILabel NameLabel;
    private Texture2D? Icon;

    public CraftingIngredientRow(int width)
    {
        Width = width;
        Height = HEIGHT;

        NameLabel = new UILabel
        {
            X = NAME_X,
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = Math.Max(0, width - NAME_X),
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        CountLabel = new UILabel
        {
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ColorCodesEnabled = true,
            IsHitTestVisible = false
        };

        AddChild(NameLabel);
        AddChild(CountLabel);
    }

    public string CountText { get; private set; } = string.Empty;

    /// <summary>True for a left click, false for a right click, on a choosable row.</summary>
    public event Action<bool>? SwapRequested;

    /// <summary>Whether this row offers a choice of ingredients (cooking mode).</summary>
    public bool Choosable { get; private set; }

    /// <summary>This row's position among the confirm window's ingredient rows (the catalyst line has none).</summary>
    public int Index { get; private set; }

    /// <summary>Binds an ingredient whose Need is already the total for the batch.</summary>
    public void Bind(CraftingIngredientInfo ingredient, int index = 0)
    {
        Choosable = ingredient.Choosable;
        Index = index;

        //a choosable row takes clicks; otherwise the row stays click-through as now
        IsHitTestVisible = Choosable;

        CountText = (ingredient.Have >= ingredient.Need ? "{=u" : "{=b") + $"{ingredient.Have} / {ingredient.Need}";

        //the count takes only the room its text needs (plus the label's padding and the last glyph's overhang),
        //so the name gets the rest of the row
        var countWidth = Math.Min(TextRenderer.MeasureWidth(CountText) + 4, Math.Max(0, Width - NAME_X));
        CountLabel.X = Width - countWidth;
        CountLabel.Width = countWidth;
        NameLabel.Width = Math.Max(0, CountLabel.X - NAME_GAP - NAME_X);

        if (Choosable)
        {
            //the arrow is appended after the name is fit, so it stays visible when the name is cut with "...".
            //the name fits in what remains after reserving the arrow, and never reaches under the count.
            //the label's own truncation stays off here, so it cannot eat the arrow a second time
            NameLabel.TruncateWithEllipsis = false;
            NameLabel.Text = WithArrow(ingredient.Name, NameLabel.Width - NameLabel.PaddingLeft - NameLabel.PaddingRight);
        } else
        {
            NameLabel.TruncateWithEllipsis = true;
            NameLabel.Text = ingredient.Name;
        }

        //color-code measuring needs the legend palette, which only exists once the client has loaded its data
        if (UiRenderer.Instance is not { } ui)
            return;

        Icon = ingredient.Sprite == 0 ? null : ui.GetItemIcon(ingredient.Sprite, ingredient.Color);
        CountLabel.Text = CountText;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (Visible)
            DrawIcon(spriteBatch);
    }

    public override void OnClick(ClickEvent e)
    {
        if (Choosable)
        {
            if (e.Button == MouseButton.Left)
                SwapRequested?.Invoke(true);
            else if (e.Button == MouseButton.Right)
                SwapRequested?.Invoke(false);
            //any other button does nothing
        }

        //panels absorb their own clicks either way, so a swap click never reaches the world behind
        base.OnClick(e);
    }

    public override void Dispose()
    {
        //shared cached texture
        Icon = null;
        base.Dispose();
    }

    /// <summary>
    ///     The name with the choosable arrow directly after it, fitting <paramref name="innerWidth" /> pixels. The
    ///     arrow's width is reserved first; the name is cut with "..." to fit what remains, so the arrow is always
    ///     visible. Mirrors <see cref="UILabel" /> ellipsis truncation, then appends the arrow.
    /// </summary>
    private static string WithArrow(string name, int innerWidth)
    {
        var arrowWidth = TextRenderer.MeasureWidth(">");
        var maxNameWidth = innerWidth - arrowWidth;

        if (TextRenderer.MeasureWidth(name) <= maxNameWidth)
            return name + ">";

        var maxTextWidth = maxNameWidth - TextRenderer.MeasureWidth("...");
        var truncLen = name.Length;

        while ((truncLen > 0) && (TextRenderer.MeasureWidth(name[..truncLen]) > maxTextWidth))
            truncLen--;

        return (truncLen > 0 ? name[..truncLen] + "..." : "...") + ">";
    }

    private void DrawIcon(SpriteBatch spriteBatch)
    {
        if (Icon is null)
            return;

        var texture = Icon;
        var source = new Rectangle(0, 0, Icon.Width, Icon.Height);

        if (Icon is CachedTexture2D { AtlasRegion: { } region })
        {
            texture = region.Atlas;
            source = region.SourceRect;
        }

        //exactly half size, so the point sampler keeps every other pixel and the icon stays crisp
        var destination = new Rectangle(
            ScreenX + ICON_X,
            ScreenY + (HEIGHT - source.Height / 2) / 2,
            source.Width / 2,
            source.Height / 2);
        var visible = Rectangle.Intersect(destination, ClipRect);

        if ((visible.Width <= 0) || (visible.Height <= 0))
            return;

        var visibleSource = new Rectangle(
            source.X + (visible.X - destination.X) * 2,
            source.Y + (visible.Y - destination.Y) * 2,
            visible.Width * 2,
            visible.Height * 2);

        spriteBatch.Draw(texture, visible, visibleSource, Color.White);
    }
}
