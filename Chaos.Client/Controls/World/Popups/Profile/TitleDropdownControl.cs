#region
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Profile;

/// <summary>
///     Title-selection dropdown for the self-profile Equipment tab. Lists every title the player owns
///     (alphabetical), highlights the active one, and raises <see cref="TitleSelected" /> when a row is
///     clicked. Styling: tiled dark-wood background + a small wooden bevel matching the title field box,
///     gold (active) / silver / white (hover) text. Scrolls with the mouse wheel when titles exceed the
///     visible rows. Opened/closed by its owning tab via <see cref="UIElement.Visible" />.
/// </summary>
public sealed class TitleDropdownControl : UIPanel
{
    private const int PAD = 2;
    private const int TOP_PAD = 4;
    private const int ROW_H = 15;
    private const int BOT_PAD = 4;
    private const int MAX_VISIBLE_ROWS = 5;

    private static readonly Color Gold = new(252, 215, 80);
    private static readonly Color White = new(255, 255, 255);
    private static readonly Color Silver = new(201, 198, 182);
    private static readonly Color Shadow = Color.Black;
    private static readonly Color HoverFill = new(44, 46, 74, 210);
    //tiled DlgBack2 wood, darkened by this overlay to match the legend panel's recessed dark-wood areas
    private static readonly Color WoodOverlay = new(0, 0, 0, 120);
    private static readonly Color DarkWood = new(49, 32, 16);

    //small wooden bevel matching the title field box (sampled from _nui_eq art): dark-brown outer edge,
    //light-tan inner highlight, around the dark recessed interior.
    private static readonly Color WoodDark = new(33, 20, 8);
    private static readonly Color WoodLight = new(165, 142, 123);

    private readonly List<string> Titles = [];
    private string ActiveTitle = string.Empty;
    private int HoverRow = -1;
    private int ScrollOffset;
    private Texture2D? WoodTexture;
    private bool WoodLoaded;

    public event Action<string>? TitleSelected;

    public TitleDropdownControl()
    {
        Name = "TitleDropdown";
        Visible = false;
        IsHitTestVisible = true;
        ZIndex = 100;
    }

    private int VisibleRows => Math.Min(Titles.Count, MAX_VISIBLE_ROWS);
    private int MaxScroll => Math.Max(0, Titles.Count - VisibleRows);
    private bool CanScroll => Titles.Count > VisibleRows;

    /// <summary>
    ///     Replaces the dropdown contents with the player's titles (sorted alphabetically) and the active
    ///     title, and recomputes the panel size. Does not change visibility.
    /// </summary>
    public void SetTitles(string activeTitle, IEnumerable<string> titles)
    {
        ActiveTitle = activeTitle ?? string.Empty;
        Titles.Clear();
        Titles.AddRange(titles.OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
        ScrollOffset = 0;
        HoverRow = -1;
        Recalculate();
    }

    //Width is set by the owning tab to match the title bar; here we only size the height to the rows.
    private void Recalculate() => Height = TOP_PAD + (VisibleRows * ROW_H) + BOT_PAD;

    private void EnsureWood()
    {
        if (WoodLoaded)
            return;

        WoodLoaded = true;
        WoodTexture = UiRenderer.Instance?.GetSpfTexture("DlgBack2.spf");
    }

    public override void OnMouseScroll(MouseScrollEvent e)
    {
        if (!Visible || !CanScroll)
            return;

        //wheel up (positive delta) scrolls toward the top
        ScrollOffset = Math.Clamp(ScrollOffset - Math.Sign(e.Delta), 0, MaxScroll);
        e.Handled = true;
    }

    public override void OnMouseMove(MouseMoveEvent e) => HoverRow = RowAt(e.ScreenX, e.ScreenY);

    public override void OnMouseLeave() => HoverRow = -1;

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        //consume any click inside the panel so it never falls through to the tab behind it
        e.Handled = true;

        var row = RowAt(e.ScreenX, e.ScreenY);

        if (row < 0)
            return;

        var index = ScrollOffset + row;

        if ((index < 0) || (index >= Titles.Count))
            return;

        TitleSelected?.Invoke(Titles[index]);
    }

    //maps screen coords to a visible row index [0, VisibleRows), or -1 if outside the row band.
    private int RowAt(int screenX, int screenY)
    {
        var listTop = ScreenY + TOP_PAD;
        var listBottom = listTop + (VisibleRows * ROW_H);

        if ((screenY < listTop) || (screenY >= listBottom))
            return -1;

        if (screenX < ScreenX + 1)
            return -1;

        return (screenY - listTop) / ROW_H;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        if (Titles.Count == 0)
            return;

        //base refreshes ClipRect (no background fill — we paint the wood ourselves)
        base.Draw(spriteBatch);

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        var sx = ScreenX;
        var sy = ScreenY;
        var w = Width;
        var h = Height;

        //── dark wood background (tiled DlgBack2 darkened to the recessed-panel tone) ──
        EnsureWood();

        if (WoodTexture is not null)
        {
            var tw = WoodTexture.Width;
            var th = WoodTexture.Height;

            for (var ty = 0; ty < h; ty += th)
            for (var tx = 0; tx < w; tx += tw)
                DrawTexture(spriteBatch, WoodTexture, new Vector2(sx + tx, sy + ty), Color.White);

            DrawRectClipped(spriteBatch, new Rectangle(sx, sy, w, h), WoodOverlay);
        }
        else
            DrawRectClipped(spriteBatch, new Rectangle(sx, sy, w, h), DarkWood);

        //── small wooden bevel outline (matches the title field box) ──
        //outer dark-brown edge
        DrawRectClipped(spriteBatch, new Rectangle(sx, sy, w, 1), WoodDark);
        DrawRectClipped(spriteBatch, new Rectangle(sx, sy + h - 1, w, 1), WoodDark);
        DrawRectClipped(spriteBatch, new Rectangle(sx, sy, 1, h), WoodDark);
        DrawRectClipped(spriteBatch, new Rectangle(sx + w - 1, sy, 1, h), WoodDark);
        //inner light-tan highlight
        DrawRectClipped(spriteBatch, new Rectangle(sx + 1, sy + 1, w - 2, 1), WoodLight);
        DrawRectClipped(spriteBatch, new Rectangle(sx + 1, sy + h - 2, w - 2, 1), WoodLight);
        DrawRectClipped(spriteBatch, new Rectangle(sx + 1, sy + 1, 1, h - 2), WoodLight);
        DrawRectClipped(spriteBatch, new Rectangle(sx + w - 2, sy + 1, 1, h - 2), WoodLight);

        //── rows ──
        var listTop = sy + TOP_PAD;

        for (var i = 0; i < VisibleRows; i++)
        {
            var index = ScrollOffset + i;

            if (index >= Titles.Count)
                break;

            var title = Titles[index];
            var rowY = listTop + (i * ROW_H);
            var isActive = string.Equals(title, ActiveTitle, StringComparison.OrdinalIgnoreCase);
            var isHover = i == HoverRow;

            if (isHover)
                DrawRectClipped(
                    spriteBatch,
                    new Rectangle(
                        sx + 2,
                        rowY,
                        w - 4,
                        ROW_H),
                    HoverFill);

            var color = isActive ? Gold : isHover ? White : Silver;

            TextRenderer.DrawShadowedText(
                spriteBatch,
                new Vector2(sx + PAD, rowY + 1),
                title,
                color,
                Shadow);
        }
    }
}
