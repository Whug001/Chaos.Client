#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Definitions;
using Chaos.Client.Models;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     The item detail pane shared by the market's Results tab and the bank window. Populated on row <b>hover</b> via
///     <see cref="Populate" />: the item icon + name (a fixed header), a fixed block of "label: value" base-field lines
///     (level / class / weight / durability, plus whatever else the owner adds), then a mouse-wheel-scrollable region
///     where the item's non-zero stat modifiers are laid out as inline blocks ("+10 HP", "-50 MP") that wrap like text,
///     each block kept whole.
/// </summary>
/// <remarks>
///     A dumb view: it renders only what it is handed, and neither reads world state nor sends packets. The base-field
///     block is a fixed number of slots (given at construction) — a null line hides its slot but keeps the layout below
///     it put, so a non-durable item does not shift the separator. The pane <b>keeps the last-hovered item</b> until a
///     different row is hovered, which is what lets the cursor move onto the pane and wheel-scroll a tall stat block.
///     All children are non-hit-test-visible, so the pane itself is the single hit-test target and owns the wheel
///     (<see cref="OnMouseScroll" />), which it always consumes so scrolling over the detail never reaches the owning
///     window's own list/page scrolling.
/// </remarks>
public sealed class ItemDetailControl : UIPanel
{
    private const int PAD = 6;
    private const int ICON = 32;
    private const int NAME_GAP = 6; //between icon and name
    private const int STAT_SCROLL_STEP = TextRenderer.CHAR_HEIGHT * 2; //pixels per wheel notch

    //pooled stat lines — created once, bound on demand. Stats flow as inline blocks ("+10 HP") wrapped across these.
    //A fully-enchanted item is 19 blocks; in a narrow pane those wrap to well under this cap.
    private const int MAX_STAT_LINES = 48;

    private const int BASE_TOP = PAD + ICON + 4; //first base-field line, just under the icon/name header

    private static readonly Color NameColor = LegendColors.White;
    private static readonly Color InfoColor = LegendColors.PaleSilver;
    private static readonly Color StatLabelColor = LegendColors.Gray;
    private static readonly Color StatValueColor = LegendColors.SpringGreen;
    private static readonly Color DividerColor = LegendColors.DarkGray;

    private readonly UILabel[] BaseLines;
    private readonly UIPanel HeaderDivider;
    private readonly UILabel HintLabel;
    private readonly UIImage IconImage;
    private readonly UILabel NameLabel;

    private readonly UILabel[] StatLineLabels = new UILabel[MAX_STAT_LINES];
    private readonly UIPanel StatContent;
    private readonly StatScrollHost StatHost;
    private readonly ScrollViewerControl StatViewer;

    private readonly int ViewportHeightPx;

    /// <param name="baseLineCount">
    ///     How many base-field slots to reserve. The separator (and everything under it) hangs off this count, so the
    ///     layout is stable no matter how many of the slots a given item actually fills.
    /// </param>
    public ItemDetailControl(int width, int height, int baseLineCount, string hint)
    {
        Width = width;
        Height = height;

        var innerWidth = width - PAD * 2;

        //separator sits just below the base-field block
        var dividerTop = BASE_TOP + (TextRenderer.CHAR_HEIGHT + 1) * baseLineCount + 2;

        //── hint shown while nothing is hovered ──
        HintLabel = new UILabel
        {
            X = PAD,
            Y = height / 2 - TextRenderer.CHAR_HEIGHT,
            Width = innerWidth,
            Height = TextRenderer.CHAR_HEIGHT * 2,
            WordWrap = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = StatLabelColor,
            IsHitTestVisible = false,
            Text = hint
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
        //it. Paddings are zeroed so the box is exactly two text lines and the wrap width is the full label width.
        NameLabel = new UILabel
        {
            X = nameX,
            Y = PAD + (ICON - TextRenderer.CHAR_HEIGHT * 2) / 2,
            Width = width - nameX - PAD,
            Height = TextRenderer.CHAR_HEIGHT * 2,
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
        BaseLines = new UILabel[baseLineCount];

        for (var i = 0; i < baseLineCount; i++)
        {
            var line = new UILabel
            {
                X = PAD,
                Y = BASE_TOP + (TextRenderer.CHAR_HEIGHT + 1) * i,
                Width = innerWidth,
                Height = TextRenderer.CHAR_HEIGHT,
                ForegroundColor = InfoColor,
                IsHitTestVisible = false,
                Visible = false
            };

            BaseLines[i] = line;
            AddChild(line);
        }

        //── separator between item info and stats, fixed just below the base-field block ──
        HeaderDivider = new UIPanel
        {
            X = PAD,
            Y = dividerTop,
            Width = innerWidth,
            Height = 1,
            BackgroundColor = DividerColor,
            IsHitTestVisible = false,
            Visible = false
        };
        AddChild(HeaderDivider);

        //── scrollable stat region ──
        //StatContent (the scrolled surface) lives in StatHost (an IVerticalScrollable clip host), wrapped in a bar-less
        //(Hidden) ScrollViewerControl that owns the wheel. Bar-less keeps the narrow pane's full width; the host
        //translates the viewer's unit offset into StatContent.Y (one unit = one wheel notch). The pane's own
        //OnMouseScroll still consumes the wheel unconditionally so it never falls through to the owning window.
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

        var viewportTop = dividerTop + 4;
        ViewportHeightPx = Math.Max(0, Height - viewportTop - PAD);

        //the viewer is the clip window; with a Hidden bar it reserves no gutter, so StatContent keeps the full innerWidth.
        StatViewer = new ScrollViewerControl(StatHost)
        {
            X = PAD,
            Y = viewportTop,
            Width = innerWidth,
            Height = ViewportHeightPx,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
        };
        AddChild(StatViewer);

        //seed the host's Height now so it reports a correct viewport on the first frame (the wheel handler runs before
        //the viewer's first Update sizes it); the viewer overwrites this each frame thereafter.
        StatHost.Height = ViewportHeightPx;

        for (var i = 0; i < MAX_STAT_LINES; i++)
        {
            var line = new UILabel
            {
                X = 0,
                Y = i * TextRenderer.CHAR_HEIGHT,
                Width = innerWidth,
                Height = TextRenderer.CHAR_HEIGHT,
                ForegroundColor = StatValueColor,
                ColorCodesEnabled = true, //each stat block carries an inline {=x color code (positive/negative tint)
                IsHitTestVisible = false,
                Visible = false
            };

            StatLineLabels[i] = line;
            StatContent.AddChild(line);
        }
    }

    /// <summary>
    ///     Binds the pane to an item (called on row hover) and resets the scroll to the top. A null entry in
    ///     <paramref name="baseLines" /> hides that slot without moving the ones below it.
    /// </summary>
    public void Populate(
        ushort sprite,
        DisplayColor color,
        string name,
        IReadOnlyList<string?> baseLines,
        ItemStats stats)
    {
        HintLabel.Visible = false;

        IconImage.Texture = UiRenderer.Instance!.GetItemIcon(sprite, color); //shared cache — never disposed
        IconImage.Visible = true;

        NameLabel.Text = name;
        NameLabel.Visible = true;

        for (var i = 0; i < BaseLines.Length; i++)
        {
            var text = i < baseLines.Count ? baseLines[i] : null;

            if (text is null)
            {
                BaseLines[i].Visible = false;

                continue;
            }

            BaseLines[i].Text = text;
            BaseLines[i].Visible = true;
        }

        HeaderDivider.Visible = true;

        BindContent(stats);

        StatHost.ResetScroll(); //each hovered item opens at the top
    }

    /// <summary>Resets the pane to its idle hint (e.g. when the result set is replaced or the category switched).</summary>
    public void Clear()
    {
        HintLabel.Visible = true;

        IconImage.Texture = null;
        IconImage.Visible = false;
        NameLabel.Visible = false;
        HeaderDivider.Visible = false;

        foreach (var line in BaseLines)
            line.Visible = false;

        for (var i = 0; i < MAX_STAT_LINES; i++)
            StatLineLabels[i].Visible = false;

        StatContent.Height = 0; //no scrollable content while idle
        StatHost.ResetScroll();
    }

    private void BindContent(ItemStats stats)
    {
        //wrap the inline blocks into lines (subtract the label's 1px side padding from the usable width).
        var lines = WrapBlocks(stats.ToBlocks(), StatContent.Width - 2);
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

        StatContent.Height = Math.Max(count * TextRenderer.CHAR_HEIGHT, ViewportHeightPx);
    }

    /// <summary>
    ///     Greedily packs the stat blocks into single-space-separated lines that each fit within
    ///     <paramref name="maxWidthPx" />, never splitting a block across lines — so "+10 HP" always stays whole.
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

    //the StatViewer (a bar-less ScrollViewerControl) owns the actual wheel scrolling; this override only guarantees the
    //wheel is always consumed over the detail pane — including the fixed header region above the viewer — so it never
    //falls through to the owning window's list/page/rail scrolling.
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
