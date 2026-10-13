#region
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Hud;

/// <summary>A docked bar as the layout sees it: its edge, which way its slots run, and its size.</summary>
public readonly record struct DockedHotbar(HotbarDock Edge, bool Vertical, int Width, int Height)
{
    /// <summary>
    ///     The bar's slots run across its edge rather than along it: a vertical bar on the top or bottom, a horizontal one
    ///     on the left or right.
    /// </summary>
    public bool RunsAcross => Vertical == Edge is HotbarDock.Bottom or HotbarDock.Top;
}

/// <summary>
///     Where the docked hotbars go, worked out together so bars on the same edge never overlap.
/// </summary>
/// <remarks>
///     Bars that run along their edge stack outward from it in bar order: rows up from the bottom and down from the top,
///     columns in from the sides. Bars that run across their edge then line up beside each other just past that stack:
///     side by side, centred, on the top and bottom; one under the next on the sides.
///     <para />
///     Only the rows on the bottom raise the status strips. A column docked to the bottom can be most of the viewport
///     tall, and the strips stacked above it would be pushed off the top of the screen.
/// </remarks>
public static class HotbarDockLayout
{
    /// <param name="bars">One per bar, in bar order; null for a bar that is hidden or not docked.</param>
    /// <param name="viewport">The world viewport.</param>
    /// <param name="bottomBaseline">The y the bottom stack builds up from: the top of the orange bar or an expanded panel.</param>
    /// <param name="sideMargin">How far in from the viewport's left and right the side stacks start.</param>
    /// <param name="sideTop">The y the side stacks start at.</param>
    /// <param name="gap">Pixels between two bars, and between a bar and its edge.</param>
    /// <param name="bottomStackTop">
    ///     How high the rows docked to the bottom reach, or null when there are none. Never above the viewport.
    /// </param>
    /// <param name="places">Filled with each bar's top-left corner, or null where <paramref name="bars" /> had null.</param>
    public static void Place(
        IReadOnlyList<DockedHotbar?> bars,
        Point?[] places,
        Rectangle viewport,
        int bottomBaseline,
        int sideMargin,
        int sideTop,
        int gap,
        out int? bottomStackTop)
    {
        Array.Clear(places);

        var bottom = bottomBaseline - gap;
        var top = viewport.Y + gap;
        var left = viewport.X + sideMargin;
        var right = viewport.Right - sideMargin;
        var anyBottomRow = false;

        for (var i = 0; i < bars.Count; i++)
        {
            if (bars[i] is not { RunsAcross: false } bar)
                continue;

            var centredX = viewport.X + ((viewport.Width - bar.Width) / 2);

            switch (bar.Edge)
            {
                case HotbarDock.Bottom:
                    bottom -= bar.Height;
                    places[i] = new Point(centredX, bottom);
                    bottom -= gap;
                    anyBottomRow = true;

                    break;

                case HotbarDock.Top:
                    places[i] = new Point(centredX, top);
                    top += bar.Height + gap;

                    break;

                case HotbarDock.Left:
                    places[i] = new Point(left, sideTop);
                    left += bar.Width + gap;

                    break;

                case HotbarDock.Right:
                    right -= bar.Width;
                    places[i] = new Point(right, sideTop);
                    right -= gap;

                    break;
            }
        }

        bottomStackTop = anyBottomRow ? Math.Max(viewport.Y, bottom + gap) : null;

        var bottomX = CentredRowStart(bars, HotbarDock.Bottom, viewport, gap);
        var topX = CentredRowStart(bars, HotbarDock.Top, viewport, gap);
        var leftY = sideTop;
        var rightY = sideTop;

        for (var i = 0; i < bars.Count; i++)
        {
            if (bars[i] is not { RunsAcross: true } bar)
                continue;

            switch (bar.Edge)
            {
                case HotbarDock.Bottom:
                    places[i] = new Point(bottomX, bottom - bar.Height);
                    bottomX += bar.Width + gap;

                    break;

                case HotbarDock.Top:
                    places[i] = new Point(topX, top);
                    topX += bar.Width + gap;

                    break;

                case HotbarDock.Left:
                    places[i] = new Point(left, leftY);
                    leftY += bar.Height + gap;

                    break;

                case HotbarDock.Right:
                    places[i] = new Point(right - bar.Width, rightY);
                    rightY += bar.Height + gap;

                    break;
            }
        }
    }

    /// <summary>Where the first of the bars running across <paramref name="edge" /> starts, so the row of them is centred.</summary>
    private static int CentredRowStart(IReadOnlyList<DockedHotbar?> bars, HotbarDock edge, Rectangle viewport, int gap)
    {
        var width = 0;
        var count = 0;

        foreach (var bar in bars)
            if (bar is { RunsAcross: true } across && (across.Edge == edge))
            {
                width += across.Width;
                count++;
            }

        if (count > 0)
            width += (count - 1) * gap;

        return viewport.X + ((viewport.Width - width) / 2);
    }
}
