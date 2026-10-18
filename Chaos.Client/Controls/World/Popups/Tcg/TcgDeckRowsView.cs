#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg;

/// <summary>The deck being built, one row per card: a cost badge, the name and "xN". Click a row to remove one copy; the wheel scrolls.</summary>
public sealed class TcgDeckRowsView : UIElement
{
    public const int ROW_HEIGHT = 14;
    private const int BADGE_SIZE = 12;
    private static readonly Color Back = new(0, 0, 0, 120);

    private IReadOnlyList<TcgDeckRow> Rows = [];
    private int ScrollRows;

    private int MaxScroll => Math.Max(0, Rows.Count - VisibleRows);
    private int VisibleRows => Math.Max(1, Height / ROW_HEIGHT);

    public event Action<TcgDeckRow>? RowClicked;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), Back);

        if (Rows.Count == 0)
        {
            DrawTextClipped(spriteBatch, new Vector2(ScreenX + 4, ScreenY + 4), "Click a card to add it.", LegendColors.Silver, false);

            return;
        }

        for (var i = 0; (i < VisibleRows) && ((ScrollRows + i) < Rows.Count); i++)
        {
            var row = Rows[ScrollRows + i];
            var y = ScreenY + (i * ROW_HEIGHT) + 1;
            var badge = new Rectangle(ScreenX + 2, y, BADGE_SIZE, BADGE_SIZE);
            var cost = row.Cost >= 99 ? "?" : row.Cost.ToString();
            var count = $"x{row.Count}";

            DrawRectClipped(spriteBatch, badge, TcgCardFace.ElementColor(row.Element));
            DrawTextClipped(spriteBatch, new Vector2(badge.X + ((BADGE_SIZE - TextRenderer.MeasureWidth(cost)) / 2), y), cost, Color.White, false);
            DrawTextClipped(spriteBatch, new Vector2(ScreenX + BADGE_SIZE + 6, y), row.Name, LegendColors.White, false);
            DrawTextClipped(spriteBatch, new Vector2(ScreenX + Width - 4 - TextRenderer.MeasureWidth(count), y), count, LegendColors.Gold, false);
        }
    }

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        var index = ScrollRows + ((e.ScreenY - ScreenY) / ROW_HEIGHT);

        if ((index >= 0) && (index < Rows.Count))
            RowClicked?.Invoke(Rows[index]);

        e.Handled = true;
    }

    public override void OnMouseScroll(MouseScrollEvent e)
    {
        ScrollRows = Math.Clamp(ScrollRows - Math.Sign(e.Delta), 0, MaxScroll);
        e.Handled = true;
    }

    /// <summary>The row under a screen point, using the same row maths as <see cref="OnClick"/>.</summary>
    public TcgDeckRow? RowAt(int screenX, int screenY)
    {
        if ((screenX < ScreenX) || (screenX >= ScreenX + Width) || (screenY < ScreenY) || (screenY >= ScreenY + Height))
            return null;

        var index = ScrollRows + ((screenY - ScreenY) / ROW_HEIGHT);

        return (index >= 0) && (index < Rows.Count) ? Rows[index] : null;
    }

    public void SetRows(IReadOnlyList<TcgDeckRow> rows)
    {
        Rows = rows;
        ScrollRows = Math.Clamp(ScrollRows, 0, MaxScroll);
    }
}
