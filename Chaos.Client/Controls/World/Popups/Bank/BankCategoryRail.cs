#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Bank;

/// <summary>
///     The bank window's left rail: one row per category the player holds items in. Clicking a
///     row raises <see cref="CategorySelected" />; the owner then asks the server for that category's items. Scrolling is
///     wheel-only (no scrollbar) with the same mechanics as <c>CustomComboBox</c>'s dropdown list — whole-row offsets and
///     small up/down triangles in a reserved 9px zone at the top and bottom, shown only while the list overflows.
/// </summary>
/// <remarks>
///     A dumb view: it never reads <c>WorldState</c> and never sends packets — the owner feeds it via
///     <see cref="SetCategories" />. Rows are pooled (one per visible slot, added once) and re-bound on scroll/refresh,
///     matching <c>MarketResultsControl</c>.
/// </remarks>
public sealed class BankCategoryRail : UIPanel
{
    private const int ROW_HEIGHT = TextRenderer.CHAR_HEIGHT + 2; //same row pitch as the combobox dropdown
    private const int ARROW_ZONE = 9;                            //reserved px at the top & bottom for the scroll arrows
    private const int SIDE_PAD = 4;

    private readonly BankCategoryRow[] Rows;
    private readonly Texture2D UpArrow;
    private readonly Texture2D DownArrow;
    private readonly int VisibleRows;

    private IReadOnlyList<string> Categories = [];
    private string SelectedName = string.Empty;
    private int ScrollOffset;

    private int MaxOffset => Math.Max(0, Categories.Count - VisibleRows);

    public BankCategoryRail(int width, int height)
    {
        Width = width;
        Height = height;
        BackgroundColor = Color.Black * 0.5f;

        //the arrow zones are always reserved so the visible row count doesn't change when the list starts/stops
        //overflowing; only the triangles themselves come and go.
        VisibleRows = Math.Max(1, (height - ARROW_ZONE * 2) / ROW_HEIGHT);

        Rows = new BankCategoryRow[VisibleRows];

        for (var i = 0; i < VisibleRows; i++)
        {
            var row = new BankCategoryRow(width - SIDE_PAD * 2)
            {
                X = SIDE_PAD,
                Y = ARROW_ZONE + i * ROW_HEIGHT,
                Visible = false
            };

            var slot = i;
            row.Clicked += () => SelectSlot(slot);
            Rows[i] = row;
            AddChild(row);
        }

        UpArrow = ImageUtil.BuildScrollArrow(true, TextColors.Default);
        DownArrow = ImageUtil.BuildScrollArrow(false, TextColors.Default);
    }

    /// <summary>Raised with the category name when a row is clicked.</summary>
    public event Action<string>? CategorySelected;

    /// <summary>
    ///     Replaces the rail's contents. <paramref name="selected" /> is the category to highlight (pass
    ///     <see cref="string.Empty" /> for none); it need not be present in <paramref name="categories" />.
    /// </summary>
    public void SetCategories(IReadOnlyList<string> categories, string selected)
    {
        Categories = categories;
        SelectedName = selected;
        ScrollOffset = Math.Clamp(ScrollOffset, 0, MaxOffset);
        RefreshRows();
    }

    private void SelectSlot(int slot)
    {
        var index = ScrollOffset + slot;

        if ((index < 0) || (index >= Categories.Count))
            return;

        SelectedName = Categories[index];
        RefreshRows();
        CategorySelected?.Invoke(SelectedName);
    }

    private void RefreshRows()
    {
        for (var i = 0; i < Rows.Length; i++)
        {
            var index = ScrollOffset + i;
            var row = Rows[i];

            if (index >= Categories.Count)
            {
                row.Visible = false;

                continue;
            }

            var name = Categories[index];
            row.SetEntry(name, name == SelectedName);
            row.Visible = true;
        }
    }

    //the wheel is consumed unconditionally — the bank window has three independently scrollable regions side by side,
    //and a wheel event over the rail must never fall through to the item list.
    public override void OnMouseScroll(MouseScrollEvent e)
    {
        //wheel-up (positive delta) reveals earlier rows — matches ScrollBarControl's convention.
        var next = Math.Clamp(ScrollOffset - e.Delta, 0, MaxOffset);

        if (next != ScrollOffset)
        {
            ScrollOffset = next;
            RefreshRows();
        }

        e.Handled = true;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible || (Categories.Count <= VisibleRows))
            return;

        if (ScrollOffset > 0)
            DrawTexture(
                spriteBatch,
                UpArrow,
                new Vector2(ScreenX + (Width - UpArrow.Width) / 2, ScreenY + (ARROW_ZONE - UpArrow.Height) / 2),
                Color.White);

        if (ScrollOffset < MaxOffset)
            DrawTexture(
                spriteBatch,
                DownArrow,
                new Vector2(
                    ScreenX + (Width - DownArrow.Width) / 2,
                    ScreenY + Height - ARROW_ZONE + (ARROW_ZONE - DownArrow.Height) / 2),
                Color.White);
    }

    public override void Dispose()
    {
        UpArrow.Dispose();
        DownArrow.Dispose();
        base.Dispose();
    }

    /// <summary>One rail row: the category name, red while selected (same as the market list row).</summary>
    private sealed class BankCategoryRow : UIPanel
    {
        private readonly UILabel NameLabel;

        public BankCategoryRow(int width)
        {
            Width = width;
            Height = TextRenderer.CHAR_HEIGHT + 2;

            //display-only child so the row panel stays the deepest hit-test target (clicks land on the row).
            NameLabel = new UILabel
            {
                Width = width,
                Height = TextRenderer.CHAR_HEIGHT,
                Y = 1,
                ForegroundColor = LegendColors.White,
                IsHitTestVisible = false
            };

            AddChild(NameLabel);
        }

        public event ClickedHandler? Clicked;

        public void SetEntry(string name, bool selected)
        {
            //ellipsize once here rather than let UILabel re-do it every frame for as long as the name overflows.
            NameLabel.Text = BankItemRow.FitToLabel(NameLabel, name);
            NameLabel.ForegroundColor = selected ? LegendColors.Scarlet : LegendColors.White;
        }

        public override void OnClick(ClickEvent e)
        {
            Clicked?.Invoke();
            e.Handled = true;
        }
    }
}
