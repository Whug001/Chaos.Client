#region
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Market;

/// <summary>
///     The right-hand detail pane of the Market Results tab. Populated on row <b>hover</b> via <see cref="Show" />: it
///     shows the item icon + name (a fixed header), a compact "label: value" base-field block (level / class / weight /
///     durability), then a mouse-wheel-scrollable region where the listing's stat modifiers are
///     laid out as inline blocks ("+10 HP", "-50 MP") that wrap like text, each block kept whole. The price is shown in
///     the results footer (next to the OK button), not here.
/// </summary>
/// <remarks>
///     Data-driven: it renders whatever stat blocks <see cref="MarketItemStats.ToBlocks" /> produces, so the stat
///     taxonomy lives entirely in the (server-supplied) data — adding or removing a stat needs no client change. The
///     pane <b>keeps the last-hovered item</b> until a different row is hovered (it does not clear when the cursor leaves
///     a row); that is what lets the user move onto the pane and wheel-scroll a tall stat block. All children are
///     non-hit-test-visible, so the pane itself is the single hit-test target and owns the wheel-scroll
///     (<see cref="OnMouseScroll" />), which it always consumes so scrolling over the detail never pages the list.
/// </remarks>
public sealed class MarketItemDetailControl : UIPanel
{
    private const int PAD = 6;
    private const int ICON = 32;
    private const int LINE = TextRenderer.CHAR_HEIGHT; //12
    private const int NAME_GAP = 6; //between icon and name
    private const int STAT_SCROLL_STEP = LINE * 2; //pixels per wheel notch

    //pooled stat LINES — created once, bound on demand. Stats flow as inline blocks ("+10 HP") wrapped across these.
    private const int MAX_STAT_LINES = 48;

    private const int BASE_TOP = PAD + ICON + 4; //first base-field line, just under the icon/name header
    private const int DIVIDER_TOP = BASE_TOP + (LINE + 1) * 3 + 2; //separator sits just below the 3 base lines

    private static readonly Color NameColor = LegendColors.White;
    private static readonly Color InfoColor = LegendColors.PaleSilver;
    private static readonly Color StatLabelColor = LegendColors.Gray;
    private static readonly Color StatValueColor = LegendColors.SpringGreen;
    private static readonly Color DividerColor = LegendColors.DarkGray;

    private readonly UILabel HintLabel;
    private readonly UIImage IconImage;
    private readonly UILabel NameLabel;
    private readonly UILabel BaseLine1; //level + class
    private readonly UILabel BaseLine2; //weight
    private readonly UILabel BaseLine3; //durability
    private readonly UIPanel HeaderDivider;

    private readonly UIPanel StatViewport;
    private readonly UIPanel StatContent;
    private readonly UILabel[] StatLineLabels = new UILabel[MAX_STAT_LINES];

    private bool HasItem;
    private int ScrollOffsetPx;
    private int ContentHeightPx;
    private int ViewportHeightPx;

    public MarketItemDetailControl(int width, int height)
    {
        Width = width;
        Height = height;

        var innerWidth = width - PAD * 2;

        //── hint shown while nothing is hovered ──
        HintLabel = new UILabel
        {
            X = PAD,
            Y = height / 2 - LINE,
            Width = innerWidth,
            Height = LINE * 2,
            WordWrap = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = StatLabelColor,
            IsHitTestVisible = false,
            Text = "Hover a listing to view its details."
        };
        AddChild(HintLabel);

        //── fixed header: icon + name ──
        IconImage = new UIImage
        {
            X = PAD,
            Y = PAD,
            Width = ICON,
            Height = ICON,
            IsHitTestVisible = false,
            Visible = false
        };
        AddChild(IconImage);

        var nameX = PAD + ICON + NAME_GAP;

        //a two-line-tall, vertically-centered box: short names sit on the icon's midpoint, long names wrap and straddle
        //it. UILabel now honors VerticalAlignment for wrapped text, so this needs no per-item repositioning. Paddings
        //are zeroed so the box is exactly two text lines and the wrap width is the full label width.
        NameLabel = new UILabel
        {
            X = nameX,
            Y = PAD + (ICON - LINE * 2) / 2,
            Width = width - nameX - PAD,
            Height = LINE * 2,
            WordWrap = true,
            VerticalAlignment = VerticalAlignment.Center,
            PaddingLeft = 0,
            PaddingRight = 0,
            PaddingTop = 0,
            PaddingBottom = 0,
            ForegroundColor = NameColor,
            IsHitTestVisible = false,
            Visible = false
        };
        AddChild(NameLabel);

        //── fixed base-field block (label: value, one per line) ──
        BaseLine1 = MakeInfoLabel(PAD, BASE_TOP, innerWidth, InfoColor);
        BaseLine2 = MakeInfoLabel(PAD, BASE_TOP + LINE + 1, innerWidth, InfoColor);
        BaseLine3 = MakeInfoLabel(PAD, BASE_TOP + (LINE + 1) * 2, innerWidth, InfoColor);
        AddChild(BaseLine1);
        AddChild(BaseLine2);
        AddChild(BaseLine3);

        //── separator between item info and stats, fixed just below the base-field block ──
        HeaderDivider = new UIPanel
        {
            X = PAD,
            Y = DIVIDER_TOP,
            Width = innerWidth,
            Height = 1,
            BackgroundColor = DividerColor,
            IsHitTestVisible = false,
            Visible = false
        };
        AddChild(HeaderDivider);

        //── scrollable stat region (Y/Height set per item below the separator) ──
        StatViewport = new UIPanel
        {
            X = PAD,
            Y = DIVIDER_TOP,
            Width = innerWidth,
            Height = 0,
            IsHitTestVisible = false
        };
        AddChild(StatViewport);

        StatContent = new UIPanel
        {
            X = 0,
            Y = 0,
            Width = innerWidth,
            Height = 0,
            IsHitTestVisible = false
        };
        StatViewport.AddChild(StatContent);

        for (var i = 0; i < MAX_STAT_LINES; i++)
        {
            var line = new UILabel
            {
                X = 0,
                Y = i * LINE,
                Width = innerWidth,
                Height = LINE,
                ForegroundColor = StatValueColor,
                ColorCodesEnabled = true, //each stat block carries an inline {=x color code (positive/negative tint)
                IsHitTestVisible = false,
                Visible = false
            };

            StatLineLabels[i] = line;
            StatContent.AddChild(line);
        }

        LayoutDividerAndStats(); //separator + stat viewport geometry is fixed (no variable-height description)
    }

    /// <summary>Positions the separator just below the base-field block, then the stat viewport below it.</summary>
    private void LayoutDividerAndStats()
    {
        HeaderDivider.Y = DIVIDER_TOP;

        var viewportTop = DIVIDER_TOP + 4;
        StatViewport.Y = viewportTop;
        ViewportHeightPx = Math.Max(0, Height - viewportTop - PAD);
        StatViewport.Height = ViewportHeightPx;
    }

    private static UILabel MakeInfoLabel(int x, int y, int width, Color color)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = LINE,
            ForegroundColor = color,
            IsHitTestVisible = false,
            Visible = false
        };

    /// <summary>Binds the pane to a listing (called on row hover) and resets the stat scroll to the top.</summary>
    public void Show(MarketListing listing)
    {
        HasItem = true;
        HintLabel.Visible = false;

        IconImage.Texture = UiRenderer.Instance!.GetItemIcon(listing.Sprite);
        IconImage.Visible = true;

        NameLabel.Text = listing.Name;
        NameLabel.Visible = true;

        BaseLine1.Text = listing.LevelReq > 0
            ? $"Level: {listing.LevelReq}   Class: {listing.ClassReq}"
            : $"Class: {listing.ClassReq}";
        BaseLine1.Visible = true;

        BaseLine2.Text = $"Weight: {listing.Weight}";
        BaseLine2.Visible = true;

        if (listing.MaxDurability > 0)
        {
            BaseLine3.Text = $"Durability: {listing.CurrentDurability}/{listing.MaxDurability}";
            BaseLine3.Visible = true;
        } else
            BaseLine3.Visible = false;

        HeaderDivider.Visible = true;

        BindStats(listing.Stats.ToBlocks());

        ScrollOffsetPx = 0;
        StatContent.Y = 0;
    }

    /// <summary>Resets the pane to its idle "hover a listing" hint (e.g. when the result set is replaced).</summary>
    public void Clear()
    {
        HasItem = false;
        HintLabel.Visible = true;

        IconImage.Texture = null;
        IconImage.Visible = false;
        NameLabel.Visible = false;
        BaseLine1.Visible = false;
        BaseLine2.Visible = false;
        BaseLine3.Visible = false;
        HeaderDivider.Visible = false;

        for (var i = 0; i < MAX_STAT_LINES; i++)
            StatLineLabels[i].Visible = false;

        ScrollOffsetPx = 0;
        ContentHeightPx = 0;
        StatContent.Y = 0;
    }

    private void BindStats(IReadOnlyList<string> blocks)
    {
        //wrap the inline blocks into lines (subtract the label's 1px side padding from the usable width).
        var lines = WrapBlocks(blocks, StatContent.Width - 2);
        var count = Math.Min(lines.Count, MAX_STAT_LINES);

        for (var i = 0; i < MAX_STAT_LINES; i++)
        {
            if (i < count)
            {
                StatLineLabels[i].Text = lines[i];
                StatLineLabels[i].Visible = true;
            } else
                StatLineLabels[i].Visible = false;
        }

        var contentHeight = count * LINE;
        ContentHeightPx = contentHeight;
        StatContent.Height = Math.Max(contentHeight, ViewportHeightPx);
    }

    /// <summary>
    ///     Greedily packs the stat blocks into single-space-separated lines that each fit within
    ///     <paramref name="maxWidthPx" />, never splitting a block across lines — so "+10 HP" always stays whole
    ///     (word-wrap at block boundaries, as if the whole thing were one wrapped multi-line label).
    /// </summary>
    private static List<string> WrapBlocks(IReadOnlyList<string> blocks, int maxWidthPx)
    {
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var block in blocks)
        {
            if (current.Length == 0)
            {
                current = block;

                continue;
            }

            var candidate = current + " " + block;

            if (TextRenderer.MeasureWidth(candidate) <= maxWidthPx)
                current = candidate;
            else
            {
                lines.Add(current);
                current = block;
            }
        }

        if (current.Length > 0)
            lines.Add(current);

        return lines;
    }

    public override void OnMouseScroll(MouseScrollEvent e)
    {
        //always consume the wheel over the detail pane so it never falls through to the list's paging.
        e.Handled = true;

        if (!HasItem)
            return;

        var maxScroll = Math.Max(0, ContentHeightPx - ViewportHeightPx);

        if (maxScroll <= 0)
            return;

        //wheel up (positive delta) scrolls toward the top of the stat block.
        ScrollOffsetPx = Math.Clamp(ScrollOffsetPx - Math.Sign(e.Delta) * STAT_SCROLL_STEP, 0, maxScroll);
        StatContent.Y = -ScrollOffsetPx;
    }
}
