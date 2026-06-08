#region
using System.Globalization;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Definitions;
using Chaos.Client.Models;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Market;

/// <summary>
///     The Logs tab page: a scrollable, newest-first list of the player's own completed sales rendered as wrapped prose
///     lines (e.g. "[Jun 1, 8:42p] You sold 5 Mana Potion to Dakota for 12,500 gold"). Each sale's sentence is
///     word-wrapped to the panel width via <see cref="TextRenderer.WrapText" /> and the flattened line list scrolls
///     line-by-line through a <see cref="VirtualizedRowList{T}" /> hosted in a <see cref="ScrollViewerControl" />, which
///     owns the always-visible scrollbar and the mouse wheel. Shows a centered "Loading..." until the snapshot lands and
///     "No sales yet." when the player has no sales. As a PAGE it owns no Show/Hide/Escape — <see cref="MarketControl" />
///     manages visibility.
/// </summary>
public sealed class MarketLogsControl : UIPanel
{
    private const int SCROLLBAR_W = ScrollBarControl.DEFAULT_WIDTH;
    private const int RIGHT_PAD = 2; //gap between the text column and the scrollbar

    //one display line per row of pre-wrapped text; a little leading so descenders don't touch the next line.
    private const int LINE_LEADING = 2;

    private readonly VirtualizedRowList<string> LineList;
    private readonly int RowAreaWidth;
    private readonly UILabel StatusLabel;

    public MarketLogsControl(Rectangle contentRect)
    {
        X = contentRect.X;
        Y = contentRect.Y;
        Width = contentRect.Width;
        Height = contentRect.Height;

        //the row column stops short of the scrollbar gutter; ContentRightPadding reproduces the original RIGHT_PAD gap.
        RowAreaWidth = Width - SCROLLBAR_W - RIGHT_PAD;

        var lineHeight = TextRenderer.CHAR_HEIGHT + LINE_LEADING;

        LineList = new VirtualizedRowList<string>(
            RowAreaWidth,
            Height,
            lineHeight,
            () => new UILabel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ForegroundColor = LegendColors.White,
                PaddingLeft = 0,
                PaddingTop = 0,
                IsHitTestVisible = false
            },
            static (row, line, _) => ((UILabel)row).Text = line);

        var viewer = new ScrollViewerControl(LineList)
        {
            X = 0,
            Y = 0,
            Width = Width,
            Height = Height,
            ContentRightPadding = RIGHT_PAD
        };
        AddChild(viewer);

        StatusLabel = new UILabel
        {
            X = 0,
            Y = (Height - TextRenderer.CHAR_HEIGHT) / 2,
            Width = RowAreaWidth,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.LightGray,
            IsHitTestVisible = false,
            Visible = false
        };
        AddChild(StatusLabel); //added after the viewer so the status text draws on top of the empty list
    }

    /// <summary>Clears the list and shows "Loading..." until <see cref="SetEntries" /> replaces it.</summary>
    public void ShowLoading()
    {
        LineList.SetItems([]);
        StatusLabel.Text = "Loading...";
        StatusLabel.Visible = true;
    }

    /// <summary>Replaces the displayed sales with the server snapshot (already newest-first) and resets scroll.</summary>
    public void SetEntries(IReadOnlyList<MarketSaleLog> entries)
    {
        var lines = new List<string>();

        foreach (var entry in entries)
            lines.AddRange(TextRenderer.WrapText(FormatEntry(entry), RowAreaWidth));

        LineList.SetItems(lines);
        StatusLabel.Text = "No sales yet.";
        StatusLabel.Visible = entries.Count == 0;
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
}
