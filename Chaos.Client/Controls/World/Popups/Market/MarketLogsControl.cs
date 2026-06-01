#region
using System.Globalization;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Definitions;
using Chaos.Client.Models;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Market;

/// <summary>
///     The Logs tab page: a scrollable, newest-first list of the player's own completed sales rendered as wrapped prose
///     lines (e.g. "[Jun 1, 8:42p] You sold 5 Mana Potion to Dakota for 12,500 gold"). Each sale's sentence is
///     word-wrapped to the panel width via <see cref="TextRenderer.WrapText" /> and the flattened line list scrolls
///     line-by-line through a vertical <see cref="ScrollBarControl" /> (and the mouse wheel). Shows a centered
///     "Loading..." until the snapshot lands and "No sales yet." when the player has no sales. As a PAGE it owns no
///     Show/Hide/Escape — <see cref="MarketControl" /> manages visibility.
/// </summary>
public sealed class MarketLogsControl : UIPanel
{
    private const int SCROLLBAR_W = ScrollBarControl.DEFAULT_WIDTH;
    private const int RIGHT_PAD = 2; //gap between the text column and the scrollbar

    private readonly UILabel[] LineLabels;
    private readonly int RowAreaWidth;
    private readonly ScrollBarControl ScrollBar;
    private readonly UILabel StatusLabel;

    private readonly List<string> Lines = [];
    private int ScrollOffset;

    public MarketLogsControl(Rectangle contentRect)
    {
        X = contentRect.X;
        Y = contentRect.Y;
        Width = contentRect.Width;
        Height = contentRect.Height;

        RowAreaWidth = Width - SCROLLBAR_W - RIGHT_PAD;

        //one display line per row of pre-wrapped text; a little leading so descenders don't touch the next line.
        var lineHeight = TextRenderer.CHAR_HEIGHT + 2;
        var visibleLines = Math.Max(1, Height / lineHeight);
        LineLabels = new UILabel[visibleLines];

        for (var i = 0; i < visibleLines; i++)
        {
            var label = new UILabel
            {
                X = 0,
                Y = i * lineHeight,
                Width = RowAreaWidth,
                Height = TextRenderer.CHAR_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Left,
                ForegroundColor = LegendColors.White,
                IsHitTestVisible = false,
                Visible = false
            };
            LineLabels[i] = label;
            AddChild(label);
        }

        ScrollBar = new ScrollBarControl
        {
            X = Width - SCROLLBAR_W,
            Y = 0,
            Height = Height,
            Orientation = ScrollOrientation.Vertical,
            VisibleItems = visibleLines
        };
        ScrollBar.OnValueChanged += value =>
        {
            ScrollOffset = value;
            RefreshLines();
        };
        AddChild(ScrollBar);

        StatusLabel = new UILabel
        {
            X = 0,
            Y = (Height - TextRenderer.CHAR_HEIGHT) / 2,
            Width = RowAreaWidth,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.LightGray,
            Visible = false
        };
        AddChild(StatusLabel);
    }

    /// <summary>Clears the list and shows "Loading..." until <see cref="SetEntries" /> replaces it.</summary>
    public void ShowLoading()
    {
        Lines.Clear();
        ScrollOffset = 0;
        ScrollBar.TotalItems = 0;
        ScrollBar.MaxValue = 0;
        ScrollBar.Value = 0;
        StatusLabel.Text = "Loading...";
        StatusLabel.Visible = true;
        RefreshLines();
    }

    /// <summary>Replaces the displayed sales with the server snapshot (already newest-first) and resets scroll.</summary>
    public void SetEntries(IReadOnlyList<MarketSaleLog> entries)
    {
        Lines.Clear();

        foreach (var entry in entries)
            Lines.AddRange(TextRenderer.WrapText(FormatEntry(entry), RowAreaWidth));

        ScrollOffset = 0;
        ScrollBar.TotalItems = Lines.Count;
        ScrollBar.MaxValue = Math.Max(0, Lines.Count - LineLabels.Length);
        ScrollBar.Value = 0;
        StatusLabel.Text = "No sales yet.";
        StatusLabel.Visible = entries.Count == 0;
        RefreshLines();
    }

    /// <summary>
    ///     Builds the one-sentence log line for a sale, e.g.
    ///     "[Jun 1, 8:42p] You sold 5 Mana Potion to Bob for 12,500 gold". Absolute local date (invariant month
    ///     abbreviation) with a lowercase am/pm letter; grouped gold; literal amount + item name.
    /// </summary>
    private static string FormatEntry(MarketSaleLog e)
    {
        var time = e.SoldAtLocal.ToString("MMM d, h:mm", CultureInfo.InvariantCulture); //"Jun 1, 8:42"
        var meridiem = e.SoldAtLocal.Hour < 12 ? 'a' : 'p';

        return $"[{time}{meridiem}] You sold {e.Quantity} {e.ItemName} to {e.BuyerName} for {e.TotalPrice:N0} gold";
    }

    private void RefreshLines()
    {
        for (var i = 0; i < LineLabels.Length; i++)
        {
            var index = ScrollOffset + i;

            if (index < Lines.Count)
            {
                LineLabels[i].Text = Lines[index];
                LineLabels[i].Visible = true;
            } else
            {
                LineLabels[i].Text = string.Empty;
                LineLabels[i].Visible = false;
            }
        }
    }

    public override void OnMouseScroll(MouseScrollEvent e) => ScrollBar.OnMouseScroll(e);
}
