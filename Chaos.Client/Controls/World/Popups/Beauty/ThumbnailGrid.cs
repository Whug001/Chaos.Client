#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     One page of picker cells laid out row by row, with page arrows on both sides that hide when everything fits on
///     one page, or no arrows at all for a short list that always fits. Holds no state of its own beyond the items it
///     was last given: the page calls <see cref="SetItems" /> on every refresh and reacts to the events.
/// </summary>
public sealed class ThumbnailGrid<T> : UIPanel where T : notnull
{
    public const int CELL = 32;
    public const int GAP = 3;
    public const int ARROW_WIDTH = 16;

    private readonly Cell[] Cells;
    private readonly CustomButton? Left;
    private readonly CustomButton? Right;
    private readonly int Columns;
    private readonly int ColumnPitch;
    private readonly int RowPitch;

    public event Action<T>? Hovered;
    public event Action? HoverCleared;
    public event Action<T>? Selected;
    public event Action<int>? PageStepped;

    /// <summary>X of the first cell column inside the grid, after the left arrow when there is one.</summary>
    public int CellLeft { get; }

    public static int WidthFor(int columns, int columnGap = GAP, int cellSize = CELL, bool arrows = true)
        => (arrows ? 2 * (ARROW_WIDTH + GAP) : 0) + ((columns * (cellSize + columnGap)) - columnGap);

    public static int HeightFor(int rows, int rowGap = GAP, int cellSize = CELL) => (rows * (cellSize + rowGap)) - rowGap;

    /// <param name="arrows">False leaves out the page arrows and their space, for a list that always fits on one page.</param>
    public ThumbnailGrid(int columns, int rows, int columnGap = GAP, int rowGap = GAP, int cellSize = CELL, bool arrows = true)
    {
        Background = null;
        Columns = columns;
        ColumnPitch = cellSize + columnGap;
        RowPitch = cellSize + rowGap;
        CellLeft = arrows ? ARROW_WIDTH + GAP : 0;
        Width = WidthFor(columns, columnGap, cellSize, arrows);
        Height = HeightFor(rows, rowGap, cellSize);

        var arrowY = (Height - CustomButton.HEIGHT) / 2;

        if (arrows)
        {
            Left = new CustomButton("<", ARROW_WIDTH) { X = 0, Y = arrowY };
            Left.Clicked += () => PageStepped?.Invoke(-1);
            AddChild(Left);
        }

        Cells = new Cell[columns * rows];

        for (var i = 0; i < Cells.Length; i++)
        {
            var origin = CellOrigin(i);
            var cell = new Cell { X = origin.X, Y = origin.Y, Width = cellSize, Height = cellSize, Visible = false };
            cell.Hovered += item => Hovered?.Invoke((T)item);
            cell.HoverCleared += () => HoverCleared?.Invoke();
            cell.Clicked += item => Selected?.Invoke((T)item);
            Cells[i] = cell;
            AddChild(cell);
        }

        if (arrows)
        {
            Right = new CustomButton(">", ARROW_WIDTH) { X = Width - ARROW_WIDTH, Y = arrowY };
            Right.Clicked += () => PageStepped?.Invoke(+1);
            AddChild(Right);
        }
    }

    /// <summary>Top-left of cell <paramref name="index" />, relative to the grid.</summary>
    public Point CellOrigin(int index) => new(CellLeft + ((index % Columns) * ColumnPitch), (index / Columns) * RowPitch);

    /// <summary>
    ///     Shows one page. <paramref name="selected" /> is compared with <see cref="object.Equals(object)" />;
    ///     <paramref name="thumbFor" /> may return null (cell draws its border only). The arrows show only when
    ///     <paramref name="pageCount" /> is more than one.
    /// </summary>
    /// <remarks>
    ///     There is no "no selection" sentinel for value-type <typeparamref name="T" />: pass a real, currently-selected
    ///     value -- <c>default</c> would just compare equal to whatever item equals <c>default(T)</c>.
    /// </remarks>
    public void SetItems(IReadOnlyList<T> items, T? selected, int pageCount, Func<T, Texture2D?> thumbFor)
    {
        if (Left is not null)
            Left.Visible = pageCount > 1;

        if (Right is not null)
            Right.Visible = pageCount > 1;

        for (var i = 0; i < Cells.Length; i++)
        {
            var cell = Cells[i];
            var previousItem = cell.Item;

            if (i < items.Count)
            {
                cell.Visible = true;
                cell.Item = items[i];
                cell.Thumbnail = thumbFor(items[i]);
                cell.IsSelected = (selected is not null) && items[i].Equals(selected);
            } else
            {
                cell.Visible = false;
                cell.Item = null;
                cell.Thumbnail = null;
                cell.IsSelected = false;
            }

            //InputDispatcher only re-runs hit-testing on cursor movement and diffs the hovered element by
            //identity. Paging reuses these same Cell instances for different items (or hides them outright),
            //so a cell the dispatcher still considers "hovered" gets no Enter/Leave call when its item changes
            //out from under it -- the hover has to be re-announced here by hand, or a stale item (or a hidden
            //cell) stays "hovered" until the mouse physically moves again.
            if (cell.IsHovered && !Equals(previousItem, cell.Item))
            {
                if (cell.Item is not null)
                    Hovered?.Invoke((T)cell.Item);
                else
                {
                    HoverCleared?.Invoke();
                    cell.IsHovered = false;
                }
            }
        }
    }

    /// <summary>
    ///     One thumbnail slot. Draws its texture fitted, then the state border and a checkmark badge. There is no
    ///     static text-draw helper suitable for a single glyph this small, so the badge is a gold square with a
    ///     dark square inset -- it reads as "selected" without needing a text renderer.
    /// </summary>
    private sealed class Cell : UIElement
    {
        private static readonly Color HoverBorder = LegendColors.Silver;
        private static readonly Color SelectedBorder = LegendColors.Gold;
        private static readonly Color Slot = new(18, 16, 22);

        public object? Item;
        public Texture2D? Thumbnail;
        public bool IsSelected;
        public bool IsHovered;

        public event Action<object>? Hovered;
        public event Action? HoverCleared;
        public event Action<object>? Clicked;

        public override void OnMouseEnter()
        {
            IsHovered = true;

            if (Item is not null)
                Hovered?.Invoke(Item);
        }

        public override void OnMouseLeave()
        {
            IsHovered = false;
            HoverCleared?.Invoke();
        }

        public override void OnClick(ClickEvent e)
        {
            if (Item is not null)
                Clicked?.Invoke(Item);

            e.Handled = true;
        }

        /// <summary>Clears transient hover state when the grid (or an ancestor) is hidden.</summary>
        public override void ResetInteractionState() => IsHovered = false;

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            //primes ClipRect -- used for hit-testing (ContainsPoint) and by DrawTextureFitted's clip-intersect
            //check below. this cell draws itself directly (it is not a UIPanel), so nothing else sets it.
            base.Draw(spriteBatch);

            var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);
            DrawRect(spriteBatch, bounds, Slot);

            //HeadThumbnailRenderer.Clear() can dispose the texture this cell still references between one
            //SetItems call and the next (e.g. the base look changed) -- guard against drawing a stale handle.
            if (Thumbnail is { IsDisposed: false })
                DrawTextureFitted(spriteBatch, Thumbnail, new Rectangle(bounds.X + 2, bounds.Y + 2, Width - 4, Height - 4), Color.White);

            if (IsSelected)
            {
                DrawBorder(spriteBatch, bounds, SelectedBorder);
                DrawBorder(spriteBatch, new Rectangle(bounds.X + 1, bounds.Y + 1, Width - 2, Height - 2), SelectedBorder);
                DrawRect(spriteBatch, new Rectangle(bounds.Right - 11, bounds.Y + 1, 10, 10), SelectedBorder);
                DrawRect(spriteBatch, new Rectangle(bounds.Right - 8, bounds.Y + 4, 4, 4), LegendColors.AlmostBlack);
            } else if (IsHovered)
                DrawBorder(spriteBatch, bounds, HoverBorder);
        }
    }
}
