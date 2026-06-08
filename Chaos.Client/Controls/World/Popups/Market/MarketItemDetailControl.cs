#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Market;

/// <summary>
///     The right-hand detail pane of the Market Results tab. Populated on row <b>hover</b> via <see cref="Show" />: it
///     shows the item icon + name (a fixed header), a compact "label: value" base-field block (level / class / weight /
///     durability / seller), then a mouse-wheel-scrollable region where the listing's stat modifiers are
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
    private const int DIVIDER_TOP = BASE_TOP + (LINE + 1) * 4 + 2; //separator sits just below the 4 base lines (seller is last)

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
    private readonly UILabel SellerLine; //seller name — last info line, just above the separator
    private readonly UIPanel HeaderDivider;

    private readonly UILabel[] StatLineLabels = new UILabel[MAX_STAT_LINES];
    private readonly UIPanel StatContent;
    private readonly StatScrollHost StatHost;
    private readonly ScrollViewerControl StatViewer;

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
        SellerLine = MakeInfoLabel(PAD, BASE_TOP + (LINE + 1) * 3, innerWidth, InfoColor);
        AddChild(BaseLine1);
        AddChild(BaseLine2);
        AddChild(BaseLine3);
        AddChild(SellerLine);

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

        //── scrollable stat region (viewer Y/Height set per item below the separator) ──
        //StatContent (the scrolled surface) lives in StatHost (an IVerticalScrollable clip host), wrapped in a
        //bar-less (Hidden) ScrollViewerControl that owns the wheel. Bar-less keeps the narrow pane's full width; the
        //host translates the viewer's unit offset into StatContent.Y (one unit = one wheel notch). The pane's own
        //OnMouseScroll still consumes the wheel unconditionally so it never falls through to the Results list's paging.
        StatContent = new UIPanel
        {
            X = 0,
            Y = 0,
            Width = innerWidth,
            Height = 0,
            IsHitTestVisible = false
        };

        StatHost = new StatScrollHost(StatContent, STAT_SCROLL_STEP) { IsPassThrough = true };
        StatHost.AddChild(StatContent);

        StatViewer = new ScrollViewerControl(StatHost)
        {
            X = PAD,
            Y = DIVIDER_TOP,
            Width = innerWidth,
            Height = 0,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
        };
        AddChild(StatViewer);

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

        LayoutDividerAndStats(); //separator + stat viewer geometry is fixed (no variable-height description)
    }

    /// <summary>Positions the separator just below the base-field block, then the stat viewport below it.</summary>
    private void LayoutDividerAndStats()
    {
        HeaderDivider.Y = DIVIDER_TOP;

        var viewportTop = DIVIDER_TOP + 4;
        ViewportHeightPx = Math.Max(0, Height - viewportTop - PAD);

        //the viewer is the clip window; with a Hidden bar it reserves no gutter, so StatContent keeps the full innerWidth.
        StatViewer.Y = viewportTop;
        StatViewer.Height = ViewportHeightPx;

        //seed the host's Height now so it reports a correct viewport on the first frame (the wheel handler runs before
        //the viewer's first Update sizes it); the viewer overwrites this each frame thereafter.
        StatHost.Height = ViewportHeightPx;
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

        SellerLine.Text = $"Seller: {listing.SellerName}";
        SellerLine.Visible = true;

        HeaderDivider.Visible = true;

        BindStats(listing.Stats.ToBlocks());

        StatHost.ResetScroll(); //each hovered item opens at the top of its stat block
    }

    /// <summary>Resets the pane to its idle "hover a listing" hint (e.g. when the result set is replaced).</summary>
    public void Clear()
    {
        HintLabel.Visible = true;

        IconImage.Texture = null;
        IconImage.Visible = false;
        NameLabel.Visible = false;
        BaseLine1.Visible = false;
        BaseLine2.Visible = false;
        BaseLine3.Visible = false;
        SellerLine.Visible = false;
        HeaderDivider.Visible = false;

        for (var i = 0; i < MAX_STAT_LINES; i++)
            StatLineLabels[i].Visible = false;

        StatContent.Height = 0; //no scrollable content while idle
        StatHost.ResetScroll();
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

    //the StatViewer (a bar-less ScrollViewerControl) owns the actual wheel scrolling of the stat block; this override
    //only guarantees the wheel is always consumed over the detail pane — including the fixed header region above the
    //viewer — so it never falls through to the Results list's paging.
    public override void OnMouseScroll(MouseScrollEvent e) => e.Handled = true;

    //── scroll host ─────────────────────────────────────────────────────────────────────────────────────────────
    //Clip + scroll surface the (bar-less) ScrollViewerControl hosts. The viewer forces this element's X/Y to 0 and
    //sizes it to the viewport each frame, then drives scrolling through IVerticalScrollable; we translate the unit
    //offset into the inner StatContent surface's pixel Y (one unit = STAT_SCROLL_STEP px, i.e. one wheel notch).
    private sealed class StatScrollHost(UIPanel content, int step) : UIPanel, IVerticalScrollable
    {
        private int OffsetUnits;

        private int MaxScrollPx => Math.Max(0, content.Height - Height);

        public void ResetScroll()
        {
            OffsetUnits = 0;
            content.Y = 0;
        }

        int IVerticalScrollable.VerticalViewport => step > 0 ? Height / step : 0;

        int IVerticalScrollable.VerticalExtent
            => ((IVerticalScrollable)this).VerticalViewport + (step > 0 ? (MaxScrollPx + step - 1) / step : 0);

        int IVerticalScrollable.VerticalOffset
        {
            get => OffsetUnits;
            set
            {
                OffsetUnits = value;
                content.Y = -Math.Min(value * step, MaxScrollPx);
            }
        }
    }
}
