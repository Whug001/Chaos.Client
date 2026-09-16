#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GroupPanels;

/// <summary>
///     One group member's floating window: portrait, name, class, health and mana bars with their values written
///     inside them, and a row of the effects closest to expiring.
/// </summary>
/// <remarks>
///     Drawn rather than composed out of child controls, the same way <c>PollPanel</c> is. Everything on it is a
///     bar, a label or an icon whose position is fixed, none of it is interactive on its own, and drawing it
///     directly keeps the whole layout readable in one method instead of spread across a constructor's worth of
///     child positioning.
///     <para />
///     The frame is the settings window's (<see cref="OrnateFrame" />), in its compact form -- see
///     <see cref="OrnateFrame.DrawCompact" /> for why the full one does not fit a stack of these.
/// </remarks>
public sealed class GroupMemberPanel : UIElement
{
    /// <summary>Matches <c>PollPanel</c>, the other floating wooden panel, so the two look related.</summary>
    public const int PANEL_WIDTH = 178;

    public const int PANEL_HEIGHT = 76;

    private const int PAD_X = 12;
    private const int PORTRAIT_SIZE = 44;
    private const int PORTRAIT_X = PAD_X;
    private const int PORTRAIT_Y = 8;

    private const int TEXT_X = PORTRAIT_X + PORTRAIT_SIZE + 6;
    private const int TEXT_RIGHT = PANEL_WIDTH - PAD_X;
    private const int TEXT_WIDTH = TEXT_RIGHT - TEXT_X;

    private const int NAME_Y = 8;
    private const int HP_BAR_Y = 22;
    private const int MP_BAR_Y = 36;
    private const int BAR_HEIGHT = 12;
    private const int EFFECTS_Y = 51;

    private const int EFFECT_ICON_SIZE = 15;
    private const int EFFECT_GAP = 2;

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
    private static readonly Color ClassColor = new(201, 198, 182);
    private static readonly Color Shadow = Color.Black;
    private static readonly Color BarTrack = new(18, 11, 5);
    private static readonly Color BarBorder = new(54, 34, 16);
    private static readonly Color HealthFill = new(196, 46, 46);
    private static readonly Color HealthFillLow = new(240, 96, 72);
    private static readonly Color ManaFill = new(52, 104, 212);
    private static readonly Color BarText = new(255, 255, 255);
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

        OrnateFrame.DrawCompact(
            spriteBatch,
            sx,
            sy,
            PANEL_WIDTH,
            PANEL_HEIGHT);

        DrawPortrait(spriteBatch, sx, sy);
        DrawNameRow(spriteBatch, Member, sx, sy);

        DrawVitalBar(
            spriteBatch,
            sx + TEXT_X,
            sy + HP_BAR_Y,
            Member.HealthPercent,
            Member.HealthPercent <= LOW_HEALTH ? HealthFillLow : HealthFill,
            Member.CurrentHp,
            Member.MaximumHp);

        DrawVitalBar(
            spriteBatch,
            sx + TEXT_X,
            sy + MP_BAR_Y,
            Member.ManaPercent,
            ManaFill,
            Member.CurrentMp,
            Member.MaximumMp);

        DrawEffects(spriteBatch, Member, sx + TEXT_X, sy + EFFECTS_Y);
    }

    private void DrawPortrait(SpriteBatch spriteBatch, int sx, int sy)
    {
        var dest = new Rectangle(
            sx + PORTRAIT_X,
            sy + PORTRAIT_Y,
            PORTRAIT_SIZE,
            PORTRAIT_SIZE);

        DrawRect(spriteBatch, dest, BarTrack);
        DrawBorder(spriteBatch, dest, BarBorder);

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

        //inset by the border so the face sits inside the recess rather than on top of its edge
        var inner = new Rectangle(
            dest.X + 1,
            dest.Y + 1,
            dest.Width - 2,
            dest.Height - 2);

        var visible = Rectangle.Intersect(inner, ClipRect);

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
        var className = ClassName(member.BaseClass);
        var classWidth = TextRenderer.MeasureWidth(className);

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(sx + TEXT_RIGHT - classWidth, sy + NAME_Y),
            className,
            ClassColor,
            Shadow);

        //the class label is the fixed part of the row; the name gives way to it rather than running underneath
        var nameRoom = TEXT_WIDTH - classWidth - 4;
        var name = Truncate(member.Name, nameRoom);

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(sx + TEXT_X, sy + NAME_Y),
            name,
            member.CurrentHp == 0 ? DeadName : NameColor,
            Shadow);
    }

    private static void DrawVitalBar(
        SpriteBatch spriteBatch,
        int x,
        int y,
        double percent,
        Color fill,
        uint current,
        uint maximum)
    {
        var track = new Rectangle(
            x,
            y,
            TEXT_WIDTH,
            BAR_HEIGHT);

        DrawRect(spriteBatch, track, BarTrack);

        var fillWidth = (int)Math.Round((TEXT_WIDTH - 2) * percent);

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

        var text = $"{current}/{maximum}";
        var textWidth = TextRenderer.MeasureWidth(text);

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(x + ((TEXT_WIDTH - textWidth) / 2), y + ((BAR_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2)),
            text,
            BarText,
            Shadow);
    }

    private static void DrawEffects(
        SpriteBatch spriteBatch,
        GroupMemberSnapshot member,
        int x,
        int y)
    {
        var renderer = UiRenderer.Instance;

        if (renderer is null)
            return;

        for (var i = 0; i < member.EffectIcons.Count; i++)
        {
            var icon = renderer.GetHalfSizeSpellIcon(member.EffectIcons[i]);

            AtlasHelper.Draw(
                spriteBatch,
                icon,
                new Vector2(x + (i * (EFFECT_ICON_SIZE + EFFECT_GAP)), y),
                Color.White);
        }
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

    /// <summary>
    ///     The base class as a player reads it. Deliberately the base class and not the advanced one: the packet
    ///     carries what the group tab and the world list carry, and a master's title is not a class.
    /// </summary>
    private static string ClassName(BaseClass baseClass)
        => baseClass switch
        {
            BaseClass.Peasant => "Peasant",
            BaseClass.Warrior => "Warrior",
            BaseClass.Rogue   => "Rogue",
            BaseClass.Wizard  => "Wizard",
            BaseClass.Priest  => "Priest",
            BaseClass.Monk    => "Monk",
            _                 => string.Empty
        };

    public override void Dispose()
    {
        ReleaseFigure();
        base.Dispose();
    }
}
