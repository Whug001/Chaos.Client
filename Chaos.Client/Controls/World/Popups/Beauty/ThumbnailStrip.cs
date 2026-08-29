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
///     A single page of picker cells with page arrows. Holds no state of its own beyond the items it was last
///     given: the panel calls <see cref="SetItems" /> on every refresh and reacts to the events. The page/index
///     text (e.g. "Hairstyle   12 / 101" and "page 2/17") is the panel's caption label, not this strip -- there
///     is no room for a label beside the arrows once the cells are wide enough to read as thumbnails.
/// </summary>
public sealed class ThumbnailStrip<T> : UIPanel where T : notnull
{
    public const int CELL = 32;
    public const int GAP = 3;
    private const int ARROW_WIDTH = 20;

    private readonly Cell[] Cells;
    private readonly CustomButton Left;
    private readonly CustomButton Right;

    public event Action<T>? Hovered;
    public event Action? HoverCleared;
    public event Action<T>? Selected;
    public event Action<int>? PageStepped;

    public static int WidthFor(int cellCount) => ARROW_WIDTH + GAP + (cellCount * (CELL + GAP)) + ARROW_WIDTH;

    public ThumbnailStrip(int cellCount)
    {
        Background = null;
        Width = WidthFor(cellCount);
        Height = CELL;

        Left = new CustomButton("<", ARROW_WIDTH) { X = 0, Y = (CELL - CustomButton.HEIGHT) / 2 };
        Left.Clicked += () => PageStepped?.Invoke(-1);
        AddChild(Left);

        Cells = new Cell[cellCount];

        for (var i = 0; i < cellCount; i++)
        {
            var cell = new Cell { X = ARROW_WIDTH + GAP + (i * (CELL + GAP)), Y = 0, Width = CELL, Height = CELL, Visible = false };
            cell.Hovered += item => Hovered?.Invoke((T)item);
            cell.HoverCleared += () => HoverCleared?.Invoke();
            cell.Clicked += item => Selected?.Invoke((T)item);
            Cells[i] = cell;
            AddChild(cell);
        }

        Right = new CustomButton(">", ARROW_WIDTH) { X = ARROW_WIDTH + GAP + (cellCount * (CELL + GAP)), Y = Left.Y };
        Right.Clicked += () => PageStepped?.Invoke(+1);
        AddChild(Right);
    }

    /// <summary>
    ///     Shows one page. <paramref name="selected" /> is compared with <see cref="object.Equals(object)" />;
    ///     <paramref name="thumbFor" /> may return null (cell draws its border only).
    /// </summary>
    public void SetItems(IReadOnlyList<T> items, T? selected, Func<T, Texture2D?> thumbFor)
    {
        for (var i = 0; i < Cells.Length; i++)
        {
            var cell = Cells[i];

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
        private bool IsHovered;

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

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            //required so DrawTextureFitted's clip-intersect check has a real rect to test against -- this cell
            //draws itself directly (it is not a UIPanel and never calls base.Draw), so nothing else sets it.
            UpdateClipRect();

            var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);
            DrawRect(spriteBatch, bounds, Slot);

            if (Thumbnail is not null)
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
