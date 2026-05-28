#region
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.Components;

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
public sealed class UIComboBox : UIPanel
{
    private const int INNER_PAD = 5;                              // text inset from the frame
    private const int ARROW_BOX = 12;                             // right-side arrow column width
    private const int LIST_Z = 100_000;                           // draw list/catcher above HUD when mounted in root
    private const int RowHeight = TextRenderer.CHAR_HEIGHT + 2;   // per-option row height

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

    public UIComboBox(int width)
    {
        Width = width;
        Height = TextRenderer.CHAR_HEIGHT + (INNER_PAD * 2);

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

        if ((Host is null) || (ListPanel is null))
            return;

        ListPanel.X = ScreenX - Host.ScreenX;

        var fitsBelow = (ScreenY + Height + ListPanel.Height) <= ChaosGame.VIRTUAL_HEIGHT;
        ListPanel.Y = (fitsBelow ? ScreenY + Height : ScreenY - ListPanel.Height) - Host.ScreenY;

        if (Catcher is not null)
            Catcher.Visible = true;

        ListPanel.Visible = true;
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
        if (Open && (ListPanel is not null))
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

        var listH = (INNER_PAD * 2) + (ItemList.Count * RowHeight);

        ListPanel = new ListPopup(Close)
        {
            Width = Width,
            Height = listH,
            Visible = false,
            ZIndex = LIST_Z,
            Background = BuildListTexture(Width, listH),
            UsesControlStack = true
        };

        for (var i = 0; i < ItemList.Count; i++)
            ListPanel.AddChild(
                new ComboBoxRow(i, ItemList[i], Width - (INNER_PAD * 2), RowHeight, OnRowSelected, TextColor, TextHover)
                {
                    X = INNER_PAD,
                    Y = INNER_PAD + (i * RowHeight)
                });

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

        var cx = x + (w / 2f);
        var cy = y + (h / 2f);
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

/// <summary>The framed dropdown list panel. Closes on Escape while it is the control-stack top.</summary>
file sealed class ListPopup(Action onClose) : UIPanel
{
    //Modal: consume EVERY key while the list is open so nothing leaks to the world hotkeys (several
    //of which — q/w/e/r — run above the dispatcher's control-stack guard and would otherwise fire on
    //the bubble up from this popup). Escape also closes the list.
    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Key == Keys.Escape)
            onClose();

        e.Handled = true;
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
