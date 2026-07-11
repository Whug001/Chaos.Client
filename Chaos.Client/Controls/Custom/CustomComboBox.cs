#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.Custom;

/// <summary>
///     A dropdown selector built from the dlgframe.epf border (same source as the item tooltip and
///     UICheckBox). A collapsed header shows the current selection + a ▼ arrow; clicking opens a framed
///     list of options. The open list + a transparent click-catcher are mounted into the nearest root
///     ancestor so they draw on top and are never clipped by a scrolled/clipped container.
/// </summary>
/// <remarks>
///     The width is fixed at construction — the framed header texture is pre-built for that width. To use
///     a different width, create a new instance (there is intentionally no Resize method). The option list
///     is rebuilt automatically whenever <see cref="SetItems" /> is called.
/// </remarks>
public sealed class CustomComboBox : UIPanel
{
    private const int INNER_PAD = 5;                              // text inset from the frame
    private const int ARROW_BOX = 12;                             // right-side arrow column width
    private const int LIST_Z = 100_000;                           // draw list/catcher above HUD when mounted in root
    private const int RowHeight = TextRenderer.CHAR_HEIGHT + 2;   // per-option row height
    private const int MAX_VISIBLE_ROWS = 8;                       // open list caps at this many rows; longer lists scroll
    private const int ARROW_ZONE = 9;                            // reserved px at the list's top & bottom for scroll arrows
    private const int ARROW_TEX_W = 9;                           // scroll-affordance arrow indicator texture size
    private const int ARROW_TEX_H = 6;

    private static readonly SKColor FillColor = new(10, 8, 5, 255);
    private static Color TextColor => TextColors.Default;          // standard UI text (LegendColors.Silver)
    private static Color TextHover => Color.Yellow;                // hover highlight (matches board "Hilight" posts)

    private readonly UILabel HeaderLabel;
    private readonly List<string> ItemList = [];

    private Texture2D? HeaderClosedTex;
    private Texture2D? HeaderOpenTex;
    private Texture2D? HeaderClosedDisabledTex;
    private UIPanel? ListPanel;
    private UIElement? Catcher;
    private UIPanel? Host;
    private int Selected = -1;
    private bool Open;

    public CustomComboBox(int width)
    {
        Width = width;
        Height = TextRenderer.CHAR_HEIGHT + INNER_PAD * 2;

        HeaderLabel = new UILabel
        {
            X = INNER_PAD,
            Y = (Height - TextRenderer.CHAR_HEIGHT) / 2,
            Width = width - INNER_PAD - ARROW_BOX,
            Height = TextRenderer.CHAR_HEIGHT,
            PaddingLeft = 0,
            PaddingTop = 0,
            ForegroundColor = TextColor,
            IsHitTestVisible = false
        };

        AddChild(HeaderLabel);

        RebuildHeaderTextures();
        Background = HeaderClosedTex;
    }

    public IReadOnlyList<string> Items => ItemList;
    public bool IsOpen => Open;

    public string? SelectedItem
        => (Selected >= 0) && (Selected < ItemList.Count) ? ItemList[Selected] : null;

    public int SelectedIndex
    {
        get => Selected;
        set
        {
            var clamped = ItemList.Count == 0 ? -1 : Math.Clamp(value, 0, ItemList.Count - 1);

            if (clamped == Selected)
                return;

            Selected = clamped;
            HeaderLabel.Text = SelectedItem ?? string.Empty;
        }
    }

    /// <summary>Overrides the auto-discovered root ancestor used to host the open list. Optional.</summary>
    public UIPanel? OverlayHost { get; set; }

    public event Action<int>? SelectionChanged;

    public void SetItems(IReadOnlyList<string> items, int selectedIndex = 0)
    {
        ItemList.Clear();
        ItemList.AddRange(items);
        Selected = ItemList.Count == 0 ? -1 : Math.Clamp(selectedIndex, 0, ItemList.Count - 1);
        HeaderLabel.Text = SelectedItem ?? string.Empty;
        DestroyListPanel();
    }

    /// <summary>
    ///     The width a combobox needs to show the widest of <paramref name="items" /> without truncation,
    ///     including the text inset and the arrow column. Use it to size a combobox to its content (the
    ///     width is fixed at construction).
    /// </summary>
    public static int MeasureRequiredWidth(IReadOnlyList<string> items)
    {
        var maxText = 0;

        foreach (var item in items)
            maxText = Math.Max(maxText, TextRenderer.MeasureWidth(item));

        return maxText + INNER_PAD + ARROW_BOX + 2;
    }

    public void Show()
    {
        if (!Enabled)
            return;

        if (Open || (ItemList.Count == 0))
            return;

        EnsureListBuilt();

        if (Host is null || ListPanel is null)
            return;

        ListPanel.X = ScreenX - Host.ScreenX;

        var fitsBelow = (ScreenY + Height + ListPanel.Height) <= ChaosGame.VIRTUAL_HEIGHT;
        ListPanel.Y = (fitsBelow ? ScreenY + Height : ScreenY - ListPanel.Height) - Host.ScreenY;

        if (Catcher is not null)
            Catcher.Visible = true;

        ListPanel.Visible = true;
        (ListPanel as ListPopup)?.ScrollToIndex(Selected); //open scrolled so the current selection is in the visible window
        Open = true;
        Background = HeaderOpenTex;
        InputDispatcher.Instance?.PushControl(ListPanel);
    }

    public void Close()
    {
        if (!Open)
            return;

        Open = false;

        if (ListPanel is not null)
        {
            ListPanel.Visible = false;
            InputDispatcher.Instance?.RemoveControl(ListPanel);
        }

        if (Catcher is not null)
            Catcher.Visible = false;

        Background = HeaderClosedTex;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        if (!Enabled)
        {
            if (Open)
                Close();

            if (Background != HeaderClosedDisabledTex)
                Background = HeaderClosedDisabledTex;

            var dimColor = Dim(TextColor);

            if (HeaderLabel.ForegroundColor != dimColor)
                HeaderLabel.ForegroundColor = dimColor;
        }
        else
        {
            var normal = Open ? HeaderOpenTex : HeaderClosedTex;

            if (Background != normal)
                Background = normal;

            if (HeaderLabel.ForegroundColor != TextColor)
                HeaderLabel.ForegroundColor = TextColor;
        }

        base.Draw(spriteBatch);
    }

    private static Color Dim(Color c)
        => new(
            (byte)(c.R / 2),
            (byte)(c.G / 2),
            (byte)(c.B / 2),
            c.A);

    public override void OnClick(ClickEvent e)
    {
        if (e.Button == MouseButton.Left)
            Toggle();

        e.Handled = true;
    }

    public override void Dispose()
    {
        if (Open && ListPanel is not null)
            InputDispatcher.Instance?.RemoveControl(ListPanel);

        if (Host is not null)
        {
            if (ListPanel is not null)
                Host.Children.Remove(ListPanel);

            if (Catcher is not null)
                Host.Children.Remove(Catcher);
        }

        ListPanel?.Dispose();
        Catcher?.Dispose();
        HeaderClosedTex?.Dispose();
        HeaderOpenTex?.Dispose();
        HeaderClosedDisabledTex?.Dispose();
        Background = null; // detach so base doesn't double-dispose a header texture
        base.Dispose();
    }

    private void Toggle()
    {
        //while the list is open, the full-screen ClickCatcher sits just below the list in Z and
        //intercepts any click outside the list — including one on the header — and closes the
        //dropdown itself. So the header rarely receives a click while open; this close branch is
        //essentially only reached when the list is somehow not covering the header.
        if (Open)
            Close();
        else
            Show();
    }

    private void OnRowSelected(int index)
    {
        Selected = index;
        HeaderLabel.Text = SelectedItem ?? string.Empty;
        Close();
        SelectionChanged?.Invoke(index);
    }

    private void EnsureListBuilt()
    {
        if (ListPanel is not null)
            return;

        Host = OverlayHost ?? FindRootAncestor();

        if (Host is null)
            return;

        //cap the visible window at MAX_VISIBLE_ROWS; a longer list scrolls via the wheel and reserves a thin
        //zone above the first row and below the last for the up/down scroll-affordance arrows.
        var scrolling = ItemList.Count > MAX_VISIBLE_ROWS;
        var visibleRows = scrolling ? MAX_VISIBLE_ROWS : ItemList.Count;
        var interiorTop = INNER_PAD + (scrolling ? ARROW_ZONE : 0);
        var listH = interiorTop + visibleRows * RowHeight + (scrolling ? ARROW_ZONE : 0) + INNER_PAD;

        var listPanel = new ListPopup(Close)
        {
            Width = Width,
            Height = listH,
            Visible = false,
            ZIndex = LIST_Z,
            Background = BuildListTexture(Width, listH),
            UsesControlStack = true
        };

        //add every row; ListPopup.LayoutRows positions/hides them based on the scroll offset.
        for (var i = 0; i < ItemList.Count; i++)
            listPanel.AddRow(
                new ComboBoxRow(i, ItemList[i], Width - INNER_PAD * 2, RowHeight, OnRowSelected, TextColor, TextHover)
                {
                    X = INNER_PAD
                });

        listPanel.Configure(
            RowHeight,
            interiorTop,
            visibleRows,
            ARROW_ZONE,
            scrolling,
            scrolling ? BuildScrollArrow(true) : null,
            scrolling ? BuildScrollArrow(false) : null);

        ListPanel = listPanel;

        Catcher = new ClickCatcher(Close)
        {
            //host-local offsets that cancel the host's screen position, placing the catcher at
            //screen origin (0,0) so it fills the entire screen regardless of where the host sits.
            X = -Host.ScreenX,
            Y = -Host.ScreenY,
            Width = ChaosGame.VIRTUAL_WIDTH,
            Height = ChaosGame.VIRTUAL_HEIGHT,
            Visible = false,
            ZIndex = LIST_Z - 1
        };

        Host.AddChild(Catcher);
        Host.AddChild(ListPanel);
    }

    private void DestroyListPanel()
    {
        if (ListPanel is null)
            return;

        if (Open)
            Close();

        if (Host is not null)
        {
            Host.Children.Remove(ListPanel);

            if (Catcher is not null)
                Host.Children.Remove(Catcher);
        }

        ListPanel.Dispose();
        Catcher?.Dispose();
        ListPanel = null;
        Catcher = null;
    }

    private UIPanel? FindRootAncestor()
    {
        UIPanel? root = null;
        var cur = Parent;

        while (cur is not null)
        {
            root = cur;
            cur = cur.Parent;
        }

        return root;
    }

    private void RebuildHeaderTextures()
    {
        HeaderClosedTex?.Dispose();
        HeaderOpenTex?.Dispose();
        HeaderClosedDisabledTex?.Dispose();
        HeaderClosedTex = BuildHeader(false, false);
        HeaderOpenTex = BuildHeader(true, false);
        HeaderClosedDisabledTex = BuildHeader(false, true);
    }

    private Texture2D BuildHeader(bool open, bool dim)
        => BuildFramedTexture(
            Width,
            Height,
            canvas => DrawArrow(canvas, Width - ARROW_BOX - 2, 0, ARROW_BOX, Height, open),
            dim);

    private static Texture2D BuildListTexture(int w, int h) => BuildFramedTexture(w, h);

    private static Texture2D BuildFramedTexture(int w, int h, Action<SKCanvas>? afterFrame = null, bool dim = false)
    {
        using var frame = DialogFrame.Composite(FillColor, w, h);

        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        if (frame is not null)
            canvas.DrawImage(frame, 0, 0);
        else
            canvas.Clear(FillColor);

        afterFrame?.Invoke(canvas);

        using var snapshot = surface.Snapshot();

        if (!dim)
            return TextureConverter.ToTexture2D(snapshot);

        //50%-brightness variant: redraw the snapshot through an RGB-scaling color matrix (alpha row left at 1).
        using var dimSurface = SKSurface.Create(info);
        using var dimPaint = new SKPaint();

        //@formatter:off
        dimPaint.ColorFilter = SKColorFilter.CreateColorMatrix([
                                                                   0.5f, 0f, 0f, 0f, 0f,
                                                                   0f, 0.5f, 0f, 0f, 0f,
                                                                   0f, 0f, 0.5f, 0f, 0f,
                                                                   0f, 0f, 0f, 1f, 0f
                                                               ]);
        //@formatter:on

        dimSurface.Canvas.DrawImage(
            snapshot,
            0,
            0,
            dimPaint);

        using var dimSnapshot = dimSurface.Snapshot();

        return TextureConverter.ToTexture2D(dimSnapshot);
    }

    private static void DrawArrow(SKCanvas canvas, int x, int y, int w, int h, bool up)
    {
        var arrow = TextColors.Default;

        using var paint = new SKPaint
        {
            Color = new SKColor(arrow.R, arrow.G, arrow.B),
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };

        var cx = x + w / 2f;
        var cy = y + h / 2f;
        const float R = 3f;

        using var path = new SKPath();

        if (up)
        {
            path.MoveTo(cx - R, cy + R);
            path.LineTo(cx + R, cy + R);
            path.LineTo(cx, cy - R);
        } else
        {
            path.MoveTo(cx - R, cy - R);
            path.LineTo(cx + R, cy - R);
            path.LineTo(cx, cy + R);
        }

        path.Close();
        canvas.DrawPath(path, paint);
    }

    /// <summary>Bakes a small up/down triangle indicator on a transparent background for the scroll affordance.</summary>
    private static Texture2D BuildScrollArrow(bool up)
    {
        var info = new SKImageInfo(ARROW_TEX_W, ARROW_TEX_H, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);
        DrawArrow(surface.Canvas, 0, 0, ARROW_TEX_W, ARROW_TEX_H, up);

        using var snapshot = surface.Snapshot();

        return TextureConverter.ToTexture2D(snapshot);
    }
}

/// <summary>One option row. Reuses UILabel text rendering; adds hover-brighten + click-select.</summary>
file sealed class ComboBoxRow : UILabel
{
    private readonly int Index;
    private readonly Action<int> Select;
    private readonly Color Normal;
    private readonly Color Hover;

    public ComboBoxRow(int index, string text, int width, int height, Action<int> select, Color normal, Color hover)
    {
        Index = index;
        Select = select;
        Normal = normal;
        Hover = hover;
        Width = width;
        Height = height;
        PaddingLeft = 0;
        PaddingTop = 0;
        PaddingRight = 0;
        PaddingBottom = 0;
        ForegroundColor = normal;
        Text = text;
    }

    public override void OnMouseEnter() => ForegroundColor = Hover;
    public override void OnMouseLeave() => ForegroundColor = Normal;

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Select(Index);
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        base.ResetInteractionState();
        ForegroundColor = Normal;
    }
}

/// <summary>
///     The framed dropdown list panel. Shows at most <c>MAX_VISIBLE_ROWS</c> rows at once; a longer list scrolls with
///     the mouse wheel, and small up/down triangles in the reserved top/bottom zones signal more content above/below.
///     Closes on Escape while it is the control-stack top.
/// </summary>
file sealed class ListPopup(Action onClose) : UIPanel
{
    private readonly List<ComboBoxRow> Rows = [];

    private Texture2D? UpArrow;
    private Texture2D? DownArrow;
    private int RowH;
    private int InteriorTop;  //local Y of the first row (below the top arrow zone)
    private int VisibleCount; //rows shown at once
    private int ArrowZone;    //reserved px above the first row / below the last
    private int ScrollOffset; //index of the first visible row
    private bool Scrolling;   //true only when the item count exceeds the window

    private int MaxOffset => Math.Max(0, Rows.Count - VisibleCount);

    public void AddRow(ComboBoxRow row)
    {
        Rows.Add(row);
        AddChild(row);
    }

    /// <summary>Stores the scroll metrics + arrow textures and lays the rows out for the first time.</summary>
    public void Configure(
        int rowHeight,
        int interiorTop,
        int visibleCount,
        int arrowZone,
        bool scrolling,
        Texture2D? upArrow,
        Texture2D? downArrow)
    {
        RowH = rowHeight;
        InteriorTop = interiorTop;
        VisibleCount = visibleCount;
        ArrowZone = arrowZone;
        Scrolling = scrolling;
        UpArrow = upArrow;
        DownArrow = downArrow;
        ScrollOffset = 0;
        LayoutRows();
    }

    /// <summary>Scrolls so <paramref name="index" /> falls within the visible window (no-op when not scrolling).</summary>
    public void ScrollToIndex(int index)
    {
        if (!Scrolling || (index < 0))
            return;

        var off = ScrollOffset;

        if (index < off)
            off = index;
        else if (index >= off + VisibleCount)
            off = index - VisibleCount + 1;

        off = Math.Clamp(off, 0, MaxOffset);

        if (off != ScrollOffset)
        {
            ScrollOffset = off;
            LayoutRows();
        }
    }

    //position the rows in the window [ScrollOffset, ScrollOffset+VisibleCount); hide the rest. Hidden rows are
    //skipped by both Draw and hit-testing, so whole-row scrolling needs no partial-row clipping.
    private void LayoutRows()
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            var slot = i - ScrollOffset;
            var visible = (slot >= 0) && (slot < VisibleCount);

            Rows[i].Visible = visible;

            if (visible)
                Rows[i].Y = InteriorTop + slot * RowH;
        }
    }

    public override void OnMouseScroll(MouseScrollEvent e)
    {
        if (Scrolling)
        {
            //wheel-up (positive delta) reveals earlier rows — matches ScrollBarControl's convention.
            var next = Math.Clamp(ScrollOffset - e.Delta, 0, MaxOffset);

            if (next != ScrollOffset)
            {
                ScrollOffset = next;
                LayoutRows();
            }
        }

        e.Handled = true;
    }

    //Modal: consume EVERY key while the list is open so nothing leaks to the world hotkeys (several
    //of which — q/w/e/r — run above the dispatcher's control-stack guard and would otherwise fire on
    //the bubble up from this popup). Escape also closes the list.
    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
            onClose();

        e.Handled = true;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch); //frame + the visible rows

        if (!Visible || !Scrolling)
            return;

        //up arrow when rows are scrolled off the top
        if ((ScrollOffset > 0) && (UpArrow is not null))
        {
            var x = ScreenX + (Width - UpArrow.Width) / 2;
            var y = ScreenY + (InteriorTop - ArrowZone) + (ArrowZone - UpArrow.Height) / 2;
            DrawTexture(spriteBatch, UpArrow, new Vector2(x, y), Color.White);
        }

        //down arrow when rows remain below the window
        if ((ScrollOffset < MaxOffset) && (DownArrow is not null))
        {
            var x = ScreenX + (Width - DownArrow.Width) / 2;
            var downZoneTop = InteriorTop + VisibleCount * RowH;
            var y = ScreenY + downZoneTop + (ArrowZone - DownArrow.Height) / 2;
            DrawTexture(spriteBatch, DownArrow, new Vector2(x, y), Color.White);
        }
    }

    public override void Dispose()
    {
        UpArrow?.Dispose();
        DownArrow?.Dispose();
        UpArrow = null;
        DownArrow = null;
        base.Dispose();
    }
}

/// <summary>Transparent, screen-filling catcher. A mousedown that isn't on the list closes the dropdown.</summary>
file sealed class ClickCatcher(Action onClose) : UIElement
{
    public override void OnMouseDown(MouseDownEvent e)
    {
        onClose();
        e.Handled = true;
    }
}