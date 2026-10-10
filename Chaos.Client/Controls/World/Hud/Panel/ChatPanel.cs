#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Definitions;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Hud.Panel;

/// <summary>
///     Chat display panel (F key). Shows chat message history with word-wrap. Background loaded from _nchatbk.spf (shown
///     in tab area). Text is rendered by a <see cref="VirtualizedRowList{T}" /> (one row per wrapped line) hosted in a
///     <see cref="ScrollViewerControl" /> placed over the chat display area. The list pins to the bottom: new lines
///     appear at the bottom and follow while the view is sitting there; scrolling up into history holds position.
/// </summary>
public sealed class ChatPanel : ExpandablePanel
{
    private const int MAX_CHAT_LINES = 200;
    private const int GLYPH_HEIGHT = 12;
    private readonly List<ChatLine> ChatLog = [];
    private readonly Rectangle NormalDisplayBounds;
    private readonly int PanelOriginX;
    private readonly int PanelOriginY;
    private readonly VirtualizedRowList<ChatLine> RowList;
    private readonly ScrollViewerControl Viewer;
    private readonly UILabel HoverTooltip;

    private Rectangle DisplayBounds;
    private Rectangle ExpandedDisplayBounds;

    //the panel width the hover tooltip was last sized for; -1 until the first hover
    private int TooltipSizedForWidth = -1;

    public ChatPanel(Rectangle displayBounds, Rectangle panelBounds)
    {
        Name = "Chat";
        NormalDisplayBounds = displayBounds;
        DisplayBounds = displayBounds;
        PanelOriginX = panelBounds.X;
        PanelOriginY = panelBounds.Y;

        Background = UiRenderer.Instance!.GetSpfTexture("_nchatbk.spf");

        RowList = new VirtualizedRowList<ChatLine>(
            displayBounds.Width,
            displayBounds.Height,
            GLYPH_HEIGHT,
            static () => new UILabel
            {
                PaddingLeft = 0,
                PaddingTop = 0
            },
            static (row, line, _) =>
            {
                var label = (UILabel)row;
                label.Text = line.Text;
                label.ForegroundColor = line.Color;
            },
            pinToBottom: true);

        RowList.SetItems(ChatLog);

        //position relative to panel origin (panel is placed at panelbounds by registertab); LayoutViewer sets Y/Height
        Viewer = new ScrollViewerControl(RowList)
        {
            X = displayBounds.X - panelBounds.X,
            Width = displayBounds.Width
        };

        LayoutViewer(NormalDisplayBounds);

        AddChild(Viewer);

        //shows the untranslated original over a translated row; drawn above the list and never hit-tested, so it
        //can't steal the hover or the wheel
        HoverTooltip = new UILabel
        {
            Name = "ChatHoverTooltip",
            Visible = false,
            IsHitTestVisible = false,
            PaddingLeft = 1,
            PaddingTop = 1,
            BackgroundColor = new Color(0, 0, 0, 128),
            BorderColor = LegendColors.White,
            ForegroundColor = LegendColors.White,
            WordWrap = true,
            ZIndex = 100
        };

        AddChild(HoverTooltip);
        WorldState.Chat.MessageAdded += OnMessageAdded;
        WorldState.Chat.MessageReplaced += OnMessageReplaced;
    }

    //word-wraps one message into rows that all carry its line id and hover text
    private List<ChatLine> Wrap(string text, Color color, uint lineId, string? hoverText)
    {
        var maxWidth = DisplayBounds.Width - ScrollBarControl.DEFAULT_WIDTH;

        return maxWidth <= 0 ? [] : WrapRows(text, color, lineId, hoverText, maxWidth);
    }

    internal static List<ChatLine> WrapRows(string text, Color color, uint lineId, string? hoverText, int maxWidth)
    {
        var rows = new List<ChatLine>();
        var remaining = text;
        string? activeColorCode = null;

        while (remaining.Length > 0)
        {
            //ponytail: FindLineBreak already returns >= 1 for non-empty input; clamp anyway so a future measure change
            //(e.g. a glyph wider than the box yielding 0) can never leave `remaining` un-shrunk and spin this forever.
            var lineEnd = Math.Max(1, TextRenderer.FindLineBreak(remaining, maxWidth));

            var line = remaining[..lineEnd]
                .TrimEnd();

            remaining = remaining[lineEnd..]
                .TrimStart();

            rows.Add(new ChatLine(line, color, lineId, hoverText));
            activeColorCode = TextRenderer.FindLastColorCode(line) ?? activeColorCode;

            //only re-prepend when the code is shorter than what this row consumed, else the next row would shrink nothing
            if (activeColorCode is not null && (remaining.Length > 0) && (activeColorCode.Length < lineEnd))
                remaining = activeColorCode + remaining;
        }

        return rows;
    }

    private void AddMessage(string text, Color color, uint lineId = 0, string? hoverText = null)
    {
        ChatLog.AddRange(Wrap(text, color, lineId, hoverText));

        if (ChatLog.Count > MAX_CHAT_LINES)
        {
            var trimmed = ChatLog.Count - MAX_CHAT_LINES;
            ChatLog.RemoveRange(0, trimmed);

            //front-trim shifts every surviving line's index down; keep a scrolled-up reader on the same lines
            RowList.NotifyRemovedFromFront(trimmed);
        }

        //pin-aware: follows the new lines to the bottom if the view was sitting there, else holds position
        RowList.Invalidate();
    }

    /// <summary>
    ///     Configures expand support for the large HUD chat panel (larger text area).
    /// </summary>
    public void ConfigureExpand(Texture2D? expandedBackground, Rectangle expandedBounds, Rectangle panelBounds)
    {
        ExpandedDisplayBounds = expandedBounds;

        //clear the normal background so expandyoffset is computed from panel height, not the
        //texture height (which is the same as the expanded texture, yielding expandyoffset=0).
        Background = null;
        Height = panelBounds.Height;

        ConfigureExpand(expandedBackground);

        //grow the recycled row pool to cover the expanded line count (kept across expand/collapse)
        RowList.EnsureViewportCapacity(expandedBounds.Height / GLYPH_HEIGHT);

        //in the large hud the compact chat area hides the bar but keeps the right-edge gap; SetExpanded shows it
        Viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        Viewer.ContentRightPadding = ScrollBarControl.DEFAULT_WIDTH;
    }

    public override void Dispose()
    {
        WorldState.Chat.MessageAdded -= OnMessageAdded;
        WorldState.Chat.MessageReplaced -= OnMessageReplaced;

        base.Dispose();
    }

    private void OnMessageAdded(ViewModel.Chat.ChatMessage msg) => AddMessage(msg.Text, msg.Color, msg.LineId, msg.HoverText);

    //swaps the line's rows in place; a reader scrolled up keeps the same first visible line, a pinned one stays pinned
    private void OnMessageReplaced(ViewModel.Chat.ChatMessage previous, ViewModel.Chat.ChatMessage current)
    {
        var newRows = Wrap(current.Text, current.Color, current.LineId, current.HoverText);
        var (index, removed) = ChatRows.Replace(ChatLog, current.LineId, newRows);

        //the line already scrolled out of the panel's row cap
        if (index < 0)
            return;

        RowList.NotifyReplaced(index, removed, newRows.Count);
        RowList.Invalidate();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        UpdateHoverTooltip();
    }

    //read every frame, not on hover events: rows are recycled and a scroll under a still mouse changes the hovered row
    private void UpdateHoverTooltip()
    {
        if (Visible
            && RowList.TryGetItemAt(
                InputBuffer.MouseX,
                InputBuffer.MouseY,
                out var itemIndex,
                out var rowScreenY)
            && ChatLog[itemIndex].HoverText is { Length: > 0 } text)
        {
            //measured and sized only when the hovered text or the panel width changes, not on every frame of a hover
            if ((HoverTooltip.Text != text) || (TooltipSizedForWidth != Width))
            {
                //the label wraps itself (WordWrap) inside its padding; the same wrap is computed here to size the height
                var tooltipWidth = Math.Clamp(TextRenderer.MeasureWidth(text) + 4, 1, Math.Max(1, Width));
                var lines = ChatRows.WrapTooltip(text, tooltipWidth - HoverTooltip.PaddingLeft - HoverTooltip.PaddingRight, static (t, w) => TextRenderer.FindLineBreak(t, w));

                HoverTooltip.Width = tooltipWidth;
                HoverTooltip.Height = (Math.Max(1, lines.Count) * TextRenderer.CHAR_HEIGHT) + 4;
                HoverTooltip.Text = text;
                TooltipSizedForWidth = Width;
            }

            //above the hovered row, else below it, else pinned to the panel top so it stays in the visible area
            var rowY = rowScreenY - ScreenY;

            HoverTooltip.X = Math.Clamp(InputBuffer.MouseX - ScreenX, 0, Math.Max(0, Width - HoverTooltip.Width));
            HoverTooltip.Y = ChatRows.TooltipY(rowY, GLYPH_HEIGHT, HoverTooltip.Height, Height);
            HoverTooltip.Visible = true;

            return;
        }

        HoverTooltip.Visible = false;
    }

    public override void SetExpanded(bool expanded)
    {
        base.SetExpanded(expanded);

        if (!CanExpand)
            return;

        DisplayBounds = expanded ? ExpandedDisplayBounds : NormalDisplayBounds;

        //re-anchor the viewer to the current mode's box (also makes VisibleRows current for the re-pin below). X/Width
        //stay normal and the panel's Y-shift grows the area upward, so the newest line stays pinned at the box bottom.
        LayoutViewer(DisplayBounds);
        Viewer.VerticalScrollBarVisibility = expanded ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
        Viewer.ContentRightPadding = expanded ? 0 : ScrollBarControl.DEFAULT_WIDTH;

        //collapse shrinks the viewport (MaxOffset grows): re-pin to the new bottom if we were sitting there
        RowList.Invalidate();
    }

    //bottom-anchors the viewer: floors the height to whole GLYPH_HEIGHT rows and pushes it down by the remainder so the
    //newest line sits flush with the box bottom (matching the bottom-anchored labels). The shipped chat rects (36 normal,
    //96 expanded) are whole multiples of GLYPH_HEIGHT, so the remainder is 0 in every mode and this is a no-op; the floor
    //keeps the bottom flush should a rect ever not be. X/Width are fixed at the normal position; the panel Y-shift expands.
    private void LayoutViewer(Rectangle bounds)
    {
        var usedHeight = bounds.Height / GLYPH_HEIGHT * GLYPH_HEIGHT;
        Viewer.Y = NormalDisplayBounds.Y - PanelOriginY + (bounds.Height - usedHeight);
        Viewer.Height = usedHeight;
        RowList.Height = usedHeight;
    }

    public void ScrollToBottom() => RowList.ScrollToBottom();

    public override void OnMouseScroll(MouseScrollEvent e)
    {
        if (Scroll(e.Delta))
            e.Handled = true;
    }

    //positive delta scrolls toward older messages (matching the wheel + Shift+Up); top-anchored offset moves the
    //opposite way, hence the sign flip into the list.
    public bool Scroll(int delta) => RowList.ScrollByRows(-delta);
}
