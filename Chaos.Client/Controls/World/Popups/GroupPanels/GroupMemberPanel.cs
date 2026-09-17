#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GroupPanels;

/// <summary>
///     One group member's row: portrait, name, and health and mana bars. The group leader's name is followed by
///     a star.
/// </summary>
/// <remarks>
///     Drawn rather than composed out of child controls, the same way <c>PollPanel</c> is. Everything on it is a
///     bar, a label or an icon whose position is fixed, none of it is interactive on its own, and drawing it
///     directly keeps the whole layout readable in one method instead of spread across a constructor's worth of
///     child positioning.
///     <para />
///     Nothing is drawn behind any of it -- no frame, no fill, not even behind the portrait. The row is read
///     against the world, which is what the shadow under every piece of text and the dark track under every bar
///     are for.
///     <para />
///     One band: the portrait on the left, the name and the two bars in the column beside it.
///     <para />
///     Nothing on it is written except the name. The bars carry no numbers -- their length is the readout, which
///     is what lets them be five pixels tall -- the class is not named, and the member's effects are not shown.
///     All three were dropped to get the panel down to a size that a column of them can sit beside the viewport
///     without taking it over. The class was the most expensive by width: spelled out, "Elementalist" alone cost
///     most of the panel. The effects were the most expensive by height, at a third of it.
///     <para />
///     The snapshot still carries the effects (<see cref="GroupMemberSnapshot.Effects" />) and the server still
///     sends them, so putting the row back is a drawing change and nothing more.
/// </remarks>
public sealed class GroupMemberPanel : UIElement
{
    /// <summary>
    ///     Exactly the portrait plus <see cref="NAME_CHARS" /> characters, with no margin either side. Derived
    ///     rather than written down, so changing the name budget moves the panel with it.
    /// </summary>
    /// <remarks>
    ///     No horizontal padding on purpose. The column's grab bar spans this same width, so the panel's edges are
    ///     the bar's edges: the portrait starts level with the collapse arrow on the left, and the bars end level
    ///     with the right end of the bar. Any margin here would show up as the grab bar overhanging the content.
    /// </remarks>
    public const int PANEL_WIDTH = TEXT_X + NAME_WIDTH;

    public const int PANEL_HEIGHT = 28;

    /// <summary>
    ///     Where the name and the two bars start. Public because the grab bar above the column lines its caption
    ///     up with this, rather than with the collapse arrow it sits beside.
    /// </summary>
    public const int TEXT_X = PORTRAIT_X + PORTRAIT_SIZE + 3;

    /// <summary>Top and bottom only. See <see cref="PANEL_WIDTH" /> for why there is none at the sides.</summary>
    private const int PAD_Y = 2;

    /// <summary>Matches the block beside it -- the name row and the two bars -- so the two line up top and bottom.</summary>
    private const int PORTRAIT_SIZE = 24;

    /// <summary>The panel's left edge, which is the column's left edge and so the collapse arrow's.</summary>
    private const int PORTRAIT_X = 0;

    private const int PORTRAIT_Y = PAD_Y;

    /// <summary>
    ///     How many characters the name row holds: twelve for the name, then the space and the star that mark the
    ///     group leader. Twelve is the longest name the game allows, so a leader's full name and their star both
    ///     fit and nothing truncates.
    /// </summary>
    private const int NAME_CHARS = 14;

    /// <summary>
    ///     The room the name row has, in pixels.
    /// </summary>
    /// <remarks>
    ///     An exact character count rather than an estimate: the 12px font is fixed-advance, every English glyph
    ///     costing <see cref="TextRenderer.CHAR_WIDTH" />, so a width in characters converts to pixels with a
    ///     multiply and no measuring.
    /// </remarks>
    private const int NAME_WIDTH = NAME_CHARS * TextRenderer.CHAR_WIDTH;

    /// <summary>
    ///     Marks the group leader, after their name.
    /// </summary>
    /// <remarks>
    ///     An asterisk rather than a drawn star or a sprite. The name is the panel's only text and it is drawn in
    ///     the 12px bitmap font, which has no star glyph; anything nicer would be an icon to load, position and
    ///     scale for the sake of one character. It costs the leader's name about five pixels of the nine or so
    ///     characters that fit.
    /// </remarks>
    private const string LEADER_MARK = " *";

    /// <summary>
    ///     Nothing is written inside the bars, so their length is the whole readout and they take whatever the
    ///     name row leaves.
    /// </summary>
    private const int BAR_WIDTH = NAME_WIDTH;

    private const int NAME_Y = PAD_Y;

    /// <summary>Thin, because there is no value written inside it to clear.</summary>
    private const int BAR_HEIGHT = 5;

    private const int HP_BAR_Y = 15;
    private const int MP_BAR_Y = 21;

    /// <summary>The front-facing idle pose, the same one the launcher card and the poker portraits use.</summary>
    private const int FRONT_IDLE_FRAME = 5;

    private const string IDLE_ANIM = "04";

    /// <summary>
    ///     The window kept out of the composite for the portrait, in the composite's own pixels. Close to the
    ///     head's own size: a wider window is not a bigger portrait, it is the same head with more empty canvas
    ///     around it scaled down to fit the same box.
    /// </summary>
    private const int CROP_WIDTH = 30;

    private const int CROP_HEIGHT = 30;

    private static readonly Color NameColor = new(255, 245, 210);

    /// <summary>Warmer than the name it sits beside, so the star reads as a mark rather than part of the name.</summary>
    private static readonly Color LeaderColor = new(255, 214, 102);
    private static readonly Color Shadow = Color.Black;
    private static readonly Color BarTrack = new(18, 11, 5);
    private static readonly Color BarBorder = new(54, 34, 16);
    private static readonly Color HealthFill = new(196, 46, 46);
    private static readonly Color HealthFillLow = new(240, 96, 72);
    private static readonly Color ManaFill = new(52, 104, 212);
    private static readonly Color DeadName = new(150, 150, 158);

    /// <summary>Below this fraction the health bar brightens, so a member in trouble reads at a glance.</summary>
    private const double LOW_HEALTH = 0.25;

    private readonly AislingRenderer Renderer;

    private GroupMemberSnapshot? Member;

    //the composited figure the portrait is cropped out of. Render() allocates a fresh texture per call -- the
    //renderer's own cache is keyed by world entity and serves world drawing, not this -- so it is held until the
    //member's appearance actually changes, and disposed when it does.
    private Texture2D? Figure;
    private AislingAppearance? RenderedAppearance;
    private int FaceTop;

    public GroupMemberPanel(AislingRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        Renderer = renderer;
        Name = "GroupMemberPanel";
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;

        //the stack owns dragging for all of its panels, so a panel is drawn but never hit-tested itself
        IsHitTestVisible = false;
    }

    /// <summary>
    ///     Points this panel at <paramref name="member" />, re-rendering the portrait only when how they look
    ///     actually changed. Null empties the panel.
    /// </summary>
    public void Show(GroupMemberSnapshot? member)
    {
        Member = member;
        Visible = member is not null;

        if (member is null)
        {
            ReleaseFigure();

            return;
        }

        if (Nullable.Equals(member.Appearance, RenderedAppearance))
            return;

        RenderedAppearance = member.Appearance;
        MeasureFace();
        RenderFigure();
    }

    /// <summary>Drops the portrait texture, so panels folded away or left over hold no GPU memory.</summary>
    public void ReleaseFigure()
    {
        Figure?.Dispose();
        Figure = null;
        RenderedAppearance = null;
        FaceTop = 0;
    }

    /// <summary>
    ///     Finds the top of the head in the composite.
    /// </summary>
    /// <remarks>
    ///     Neither of the renderer's own offsets marks it: <c>topPadding</c> is extra canvas added above the
    ///     standard body for tall headwear, and cropping from it starts below the hairline, while cropping from
    ///     zero leaves the head in the bottom of the frame because a composite is mostly empty above the figure.
    ///     So the first row holding any pixel is measured directly -- exact for every body, hat and hairstyle, and
    ///     it runs only when a member's appearance changes.
    /// </remarks>
    private void MeasureFace()
    {
        FaceTop = 0;

        if (RenderedAppearance is not { } appearance)
            return;

        using var composite = Renderer.Render(
            in appearance,
            FRONT_IDLE_FRAME,
            IDLE_ANIM,
            false,
            true);

        if (composite is not null)
            FaceTop = FindContentTop(composite);
    }

    private void RenderFigure()
    {
        Figure?.Dispose();
        Figure = null;

        if (RenderedAppearance is not { } appearance)
            return;

        Figure = Renderer.Render(
            in appearance,
            FRONT_IDLE_FRAME,
            IDLE_ANIM,
            false,
            true);
    }

    /// <summary>The first row of <paramref name="texture" /> holding any pixel worth seeing.</summary>
    private static int FindContentTop(Texture2D texture)
    {
        using var scope = new PixelBufferScope(texture);

        var pixels = scope.AsSpan();

        for (var y = 0; y < scope.Height; y++)
        {
            var row = y * scope.Width;

            for (var x = 0; x < scope.Width; x++)
                //a threshold rather than zero: the composite's edges are antialiased, and a stray one-alpha pixel
                //above the hairline would anchor the crop to nothing
                if (pixels[row + x].A > 16)
                    return y;
        }

        return 0;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible || (Member is null))
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        var sx = ScreenX;
        var sy = ScreenY;

        DrawPortrait(spriteBatch, sx, sy);
        DrawNameRow(spriteBatch, Member, sx, sy);

        DrawVitalBar(
            spriteBatch,
            sx + TEXT_X,
            sy + HP_BAR_Y,
            Member.HealthPercent,
            Member.HealthPercent <= LOW_HEALTH ? HealthFillLow : HealthFill,
            Member.CurrentHp);

        DrawVitalBar(
            spriteBatch,
            sx + TEXT_X,
            sy + MP_BAR_Y,
            Member.ManaPercent,
            ManaFill,
            Member.CurrentMp);
    }

    /// <summary>
    ///     The member's head, drawn straight onto whatever is behind the panel. No plate and no border: the crop
    ///     already carries the composite's own transparency, so the head reads as a head rather than as a picture
    ///     in a box.
    /// </summary>
    private void DrawPortrait(SpriteBatch spriteBatch, int sx, int sy)
    {
        if (Figure is null)
            return;

        var top = Math.Clamp(FaceTop, 0, Math.Max(0, Figure.Height - 1));

        var source = new Rectangle(
            Math.Max(0, AislingRenderer.CANVAS_CENTER_X - (CROP_WIDTH / 2)),
            top,
            Math.Min(CROP_WIDTH, Figure.Width),
            Math.Min(CROP_HEIGHT, Figure.Height - top));

        if (source is { Width: <= 0 } or { Height: <= 0 })
            return;

        var dest = new Rectangle(
            sx + PORTRAIT_X,
            sy + PORTRAIT_Y,
            PORTRAIT_SIZE,
            PORTRAIT_SIZE);

        var visible = Rectangle.Intersect(dest, ClipRect);

        if (visible is not { Width: > 0, Height: > 0 })
            return;

        spriteBatch.Draw(Figure, visible, source, Color.White);
    }

    private static void DrawNameRow(
        SpriteBatch spriteBatch,
        GroupMemberSnapshot member,
        int sx,
        int sy)
    {
        var x = sx + TEXT_X;

        //the mark is reserved out of the row before the name is measured, so a long name gives way to it rather
        //than pushing it off the end
        var markWidth = member.IsLeader ? TextRenderer.MeasureWidth(LEADER_MARK) : 0;
        var name = Truncate(member.Name, NAME_WIDTH - markWidth);

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(x, sy + NAME_Y),
            name,
            member.CurrentHp == 0 ? DeadName : NameColor,
            Shadow);

        if (!member.IsLeader)
            return;

        //drawn separately from the name so it keeps its own colour
        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(x + TextRenderer.MeasureWidth(name), sy + NAME_Y),
            LEADER_MARK,
            LeaderColor,
            Shadow);
    }

    private static void DrawVitalBar(
        SpriteBatch spriteBatch,
        int x,
        int y,
        double percent,
        Color fill,
        uint current)
    {
        var track = new Rectangle(
            x,
            y,
            BAR_WIDTH,
            BAR_HEIGHT);

        DrawRect(spriteBatch, track, BarTrack);

        var fillWidth = (int)Math.Round((BAR_WIDTH - 2) * percent);

        //a member on one hit point must not read as a member on none, so any health at all keeps a sliver lit
        if ((fillWidth == 0) && (current > 0))
            fillWidth = 1;

        if (fillWidth > 0)
            DrawRect(
                spriteBatch,
                new Rectangle(
                    x + 1,
                    y + 1,
                    fillWidth,
                    BAR_HEIGHT - 2),
                fill);

        DrawBorder(spriteBatch, track, BarBorder);
    }

    /// <summary>The longest prefix of <paramref name="text" /> that fits in <paramref name="width" /> pixels.</summary>
    private static string Truncate(string text, int width)
    {
        if ((width <= 0) || (text.Length == 0))
            return string.Empty;

        if (TextRenderer.MeasureWidth(text) <= width)
            return text;

        for (var length = text.Length - 1; length > 0; length--)
            if (TextRenderer.MeasureWidth(text.AsSpan(0, length)) <= width)
                return text[..length];

        return string.Empty;
    }

    public override void Dispose()
    {
        ReleaseFigure();
        base.Dispose();
    }
}
