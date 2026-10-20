#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Beauty;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Rendering.Models;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Effects;

/// <summary>
///     The Effects window: a paged tile grid of the player with each effect's still frame (left), the looping preview
///     with its name and turn button (right), and Close and Choose. Opened by the server's EffectWindowDisplay Open;
///     Choose sends the pick back, everything else sends nothing.
/// </summary>
public sealed class EffectWindowControl : GuildCloakDialogBase
{
    private const int CELL = 48;
    private const int LEFT = 20;
    private const int GAP = 10;
    private const int TITLE_TOP = 8;
    private const int TAB_TOP = 28;
    private const int TAB_WIDTH = 110;
    private const int TAB_GAP = 6;
    private const int TOP = 56;
    private const int PREVIEW_SIZE = 170;
    private const int TURN_WIDTH = 60;
    private const int BUTTON_WIDTH = 80;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int PANEL_HEIGHT = 330;

    private readonly AislingRenderer Renderer;
    private readonly EffectRenderer Effects;
    private readonly EffectWindowState State = new();
    private readonly CustomButton LoginTab;
    private readonly CustomButton DeathTab;
    private readonly ThumbnailGrid<EffectWindowEntry> Grid;
    private readonly UILabel PageLabel;
    private readonly EffectPreview Preview;
    private readonly UILabel NameLabel;
    private readonly UILabel StatusLabel;
    private readonly CustomButton ChooseButton;

    //tile textures this window composed (owned, disposed on hide), by tab and effect
    private readonly Dictionary<(EffectWindowKind Kind, byte Effect), Texture2D?> Thumbs = [];

    private AislingAppearance BaseLook;

    /// <summary>Raised with the tab and effect to send to the server.</summary>
    public event Action<EffectWindowKind, byte>? Chosen;

    public EffectWindowControl(AislingRenderer renderer, EffectRenderer effects)
        : base("_nsett", false)
    {
        Renderer = renderer;
        Effects = effects;

        var gridWidth = ThumbnailGrid<EffectWindowEntry>.WidthFor(EffectWindowState.COLUMNS, cellSize: CELL);
        var gridHeight = ThumbnailGrid<EffectWindowEntry>.HeightFor(EffectWindowState.ROWS, cellSize: CELL);
        Width = LEFT + gridWidth + GAP + PREVIEW_SIZE + LEFT;
        Height = PANEL_HEIGHT;
        Name = "EffectWindow";
        Visible = false;
        UsesControlStack = true;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        if (OkButton is not null)
            OkButton.X = LEFT;

        Caption("Effects", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);

        LoginTab = AddButton("Login & Logout", TAB_WIDTH, LEFT, TAB_TOP, () => ChooseTab(EffectWindowKind.LoginLogout));
        DeathTab = AddButton("Death", TAB_WIDTH, LEFT + TAB_WIDTH + TAB_GAP, TAB_TOP, () => ChooseTab(EffectWindowKind.Death));

        Grid = new ThumbnailGrid<EffectWindowEntry>(EffectWindowState.COLUMNS, EffectWindowState.ROWS, cellSize: CELL)
        {
            X = LEFT,
            Y = TOP
        };
        Grid.Selected += e => { State.Select(e); Refresh(); };
        Grid.PageStepped += d => { State.StepPage(d); Refresh(); };
        AddChild(Grid);

        PageLabel = Caption(string.Empty, LEFT, TOP + gridHeight + 4, gridWidth, HorizontalAlignment.Center, LegendColors.Gray);

        var previewLeft = LEFT + gridWidth + GAP;
        Preview = new EffectPreview(Renderer, Effects, PREVIEW_SIZE, PREVIEW_SIZE)
        {
            X = previewLeft,
            Y = TOP
        };
        AddChild(Preview);

        NameLabel = Caption(string.Empty, previewLeft, TOP + PREVIEW_SIZE + 6, PREVIEW_SIZE - TURN_WIDTH - 4);

        AddButton(
            "Turn",
            TURN_WIDTH,
            previewLeft + PREVIEW_SIZE - TURN_WIDTH,
            TOP + PREVIEW_SIZE + 2,
            () => { State.Turn(); Refresh(); });

        //ends before the Choose button so a long name never slides under it
        StatusLabel = Caption(
            string.Empty,
            LEFT,
            TOP + PREVIEW_SIZE + 28,
            Width - LEFT - BUTTON_WIDTH - GAP - LEFT,
            HorizontalAlignment.Left,
            LegendColors.Gray);

        ChooseButton = AddButton(
            "Choose",
            BUTTON_WIDTH,
            Width - LEFT - BUTTON_WIDTH,
            Height - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4,
            Choose);
    }

    public void Open(EffectWindowDisplayArgs args, AislingAppearance? playerLook)
    {
        //a morphed player has no aisling look; fall back to a plain figure
        BaseLook = playerLook ?? new AislingAppearance { Gender = Gender.Male, BodySpriteId = AislingRenderer.BODY_ID, HeadSprite = 1 };
        ReleaseThumbs();
        Preview.SetLook(BaseLook);
        State.Open(args);
        Show();
        Refresh();
    }

    /// <summary>The server saved a pick: move the check mark and the status line.</summary>
    public void ApplyUpdate(EffectWindowDisplayArgs args)
    {
        State.Update(args);

        if (Visible)
            Refresh();
    }

    /// <summary>Hides the window and drops its tile and preview textures. Sends nothing.</summary>
    public override void Hide()
    {
        ReleaseThumbs();
        Preview.ReleaseFigure();
        base.Hide();
    }

    public override void Dispose()
    {
        ReleaseThumbs();
        base.Dispose();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void ChooseTab(EffectWindowKind tab)
    {
        State.SetTab(tab);
        Refresh();
    }

    private void Choose()
    {
        if (!State.CanChoose || State.Selected is not { } entry)
            return;

        Chosen?.Invoke(State.Tab, entry.Effect);
    }

    private void Refresh()
    {
        LoginTab.Selected = State.Tab == EffectWindowKind.LoginLogout;
        DeathTab.Selected = State.Tab == EffectWindowKind.Death;
        Grid.SetItems(State.PageItems, State.Selected, State.PageCount, ThumbFor, isDimmed: e => !e.Owned, isMarked: State.IsCurrent);
        PageLabel.Text = $"{State.Page + 1}/{State.PageCount}";
        NameLabel.Text = State.Selected?.Name ?? string.Empty;
        StatusLabel.Text = State.StatusText;
        ChooseButton.Enabled = State.CanChoose;
        Preview.SetFacing(State.Facing);
        Preview.SetEffect(State.Tab, State.Selected);
    }

    private Texture2D? ThumbFor(EffectWindowEntry entry)
    {
        var key = (State.Tab, entry.Effect);

        if (Thumbs.TryGetValue(key, out var cached))
            return cached;

        //standing, facing down: frame 5 flipped, front layers (as the mount window's tiles)
        using var figure = Renderer.Render(in BaseLook, 5, out _, out var topPadding, AislingRenderer.WALK_ANIM, true, true);

        SpriteFrame? effect = null;

        if ((entry.Effect > 0) && (Effects.GetEffectInfo(entry.Animation) is { } info))
        {
            var frameIndex = EffectPreviewTimeline.StillFrame(new EffectClip(entry.Animation, info.FrameCount, info.FrameIntervalMs), entry.KeyTimeMs);

            if (frameIndex >= 0)
                effect = Effects.GetFrame(entry.Animation, frameIndex);
        }

        var texture = EffectTileRenderer.Compose(figure, topPadding, effect);
        Thumbs[key] = texture;

        return texture;
    }

    private void ReleaseThumbs()
    {
        foreach (var texture in Thumbs.Values)
            texture?.Dispose();

        Thumbs.Clear();
    }
}
