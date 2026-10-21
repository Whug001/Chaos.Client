#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Beauty;
using Chaos.Client.Controls.World.Popups.Beauty.Pages;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Mounts;

/// <summary>
///     The mount window: a paged tile grid of the player's mounts (left), the walking preview with its name and turn
///     button (right), the cloak swatches underneath, and Close and Choose. Opened by the server's
///     MountWindowDisplay Open; Choose sends the pick back, everything else sends nothing.
/// </summary>
public sealed class MountWindowControl : GuildCloakDialogBase
{
    private const int CELL = 48;
    private const int LEFT = 20;
    private const int GAP = 8;
    private const int TITLE_TOP = 8;
    private const int TOP = 28;
    private const int PREVIEW_WIDTH = 150;
    private const int PREVIEW_HEIGHT = 150;
    private const int BUTTON_WIDTH = 80;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private const int PAGE_TOP = 182;
    private const int COLOR_TOP = 198;
    private const int SWATCH_TOP = 214;
    private const int NAME_TOP = 185;
    private const int TURN_TOP = 180;
    private const int TURN_WIDTH = 60;
    private const int NAME_GAP = 4;

    //tall enough for the grid, the captions and a full 72-colour swatch list above the footer buttons
    private const int PANEL_HEIGHT = 432;

    private readonly AislingRenderer Renderer;
    private readonly CreatureRenderer Creatures;
    private readonly MountWindowState State = new();
    private readonly ThumbnailGrid<MountWindowMount> Grid;
    private readonly UILabel PageLabel;
    private readonly UILabel EmptyLabel;
    private readonly CustomButton ChooseButton;
    private readonly MountPreview Preview;
    private readonly UILabel NameLabel;
    private readonly UILabel ColorLabel;
    private readonly SwatchGrid Swatches;

    //tile textures this window rendered (owned, disposed on hide); Sleigh tiles use renderer frames and are not stored here
    private readonly Dictionary<byte, Texture2D?> Thumbs = [];

    private AislingAppearance BaseLook;

    /// <summary>Raised with the chosen mount and cloak color to send to the server.</summary>
    public event Action<byte, DisplayColor>? Chosen;

    public MountWindowControl(AislingRenderer renderer, CreatureRenderer creatures)
        : base("_nsett", false)
    {
        Renderer = renderer;
        Creatures = creatures;

        var gridWidth = ThumbnailGrid<MountWindowMount>.WidthFor(MountWindowState.COLUMNS, cellSize: CELL);
        Width = LEFT + gridWidth + GAP + PREVIEW_WIDTH + LEFT;
        Height = PANEL_HEIGHT;
        Name = "MountWindow";
        Visible = false;
        UsesControlStack = true;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        if (OkButton is not null)
            OkButton.X = LEFT;

        Caption("Mounts", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);

        Grid = new ThumbnailGrid<MountWindowMount>(MountWindowState.COLUMNS, MountWindowState.ROWS, cellSize: CELL)
        {
            X = LEFT,
            Y = TOP
        };
        Grid.Selected += m => { State.Select(m); Refresh(); };
        Grid.PageStepped += d => { State.StepPage(d); Refresh(); };
        AddChild(Grid);

        PageLabel = Caption(string.Empty, LEFT, PAGE_TOP, gridWidth, HorizontalAlignment.Center, LegendColors.Gray);

        EmptyLabel = Caption("You don't have a mount yet.", LEFT, TOP + 60, gridWidth, HorizontalAlignment.Center, LegendColors.Gray);

        var previewLeft = LEFT + gridWidth + GAP;
        Preview = new MountPreview(Renderer, Creatures, PREVIEW_WIDTH, PREVIEW_HEIGHT)
        {
            X = previewLeft,
            Y = TOP
        };
        AddChild(Preview);

        NameLabel = Caption(string.Empty, previewLeft, NAME_TOP, PREVIEW_WIDTH - TURN_WIDTH - NAME_GAP);

        AddButton(
            "Turn",
            TURN_WIDTH,
            previewLeft + PREVIEW_WIDTH - TURN_WIDTH,
            TURN_TOP,
            () => { State.Turn(); Refresh(); });

        ColorLabel = Caption(string.Empty, LEFT, COLOR_TOP, gridWidth, HorizontalAlignment.Left, LegendColors.Gray);

        Swatches = new SwatchGrid
        {
            X = LEFT + ((gridWidth - SwatchGrid.WidthFor(SwatchGrid.COLUMNS)) / 2),
            Y = SWATCH_TOP
        };
        Swatches.Selected += c => { State.SetColor(c); ReleaseThumbs(); Refresh(); };
        Swatches.Hovered += c => ColorLabel.Text = ColorText(c);
        Swatches.HoverCleared += () => ColorLabel.Text = ColorText(State.Color);
        AddChild(Swatches);

        ChooseButton = AddButton(
            "Choose",
            BUTTON_WIDTH,
            Width - LEFT - BUTTON_WIDTH,
            Height - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4,
            Choose);
    }

    public void Open(MountWindowDisplayArgs args, AislingAppearance? playerLook)
    {
        //a morphed player has no aisling look; fall back to a plain figure
        BaseLook = playerLook ?? new AislingAppearance { Gender = Gender.Male, BodySpriteId = AislingRenderer.BODY_ID, HeadSprite = 1 };
        ReleaseThumbs();
        State.Open(args);
        Show();
        Refresh();
    }

    /// <summary>The server closed the window; nothing is sent back.</summary>
    public void CloseFromServer() => Hide();

    /// <summary>Hides the window and drops its tile and preview textures. Sends nothing.</summary>
    public override void Hide()
    {
        ReleaseThumbs();
        Preview.ReleaseFrames();
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

    private void Choose()
    {
        if (!State.CanChoose || State.Selected is not { } mount)
            return;

        Chosen?.Invoke(mount.Mount, State.Color);
        Hide();
    }

    private void Refresh()
    {
        var empty = !State.CanChoose;
        EmptyLabel.Visible = empty;
        Grid.Visible = !empty;
        PageLabel.Visible = !empty;
        ChooseButton.Enabled = State.CanChoose;
        Grid.SetItems(State.PageItems, State.Selected, State.PageCount, ThumbFor);
        PageLabel.Text = $"{State.Page + 1}/{State.PageCount}";
        Swatches.SetColors(State.Colors, State.Color, HairPage.SwatchColorFor);
        ColorLabel.Text = ColorText(State.Color);
        NameLabel.Text = State.Selected?.Name ?? string.Empty;
        Preview.SetFigure(State.Selected is { } m ? MountedLook.Build(BaseLook, m, State.Color) : null);
        Preview.SetFacing(State.Facing);
    }

    private static string ColorText(DisplayColor color)
        => color == DisplayColor.Default ? "Cloak: none" : $"Cloak: {DisplayColorNames.Spaced(color)}";

    private Texture2D? ThumbFor(MountWindowMount mount)
    {
        var figure = MountedLook.Build(BaseLook, mount, State.Color);

        if (figure.Look is not { } look)
            return SleighThumb(figure.CreatureSprite);

        if (!Thumbs.TryGetValue(mount.Mount, out var texture))
        {
            //standing, facing down: frame 5 flipped, front layers
            texture = Renderer.Render(in look, 5, AislingRenderer.WALK_ANIM, true, true);
            Thumbs[mount.Mount] = texture;
        }

        return texture;
    }

    //renderer-owned frame: never stored in Thumbs, never disposed
    private Texture2D? SleighThumb(ushort spriteId)
    {
        if (Creatures.GetAnimInfo(spriteId) is not { } info)
            return null;

        (var start, _, _) = AnimationSystem.GetCreatureWalkCycle(in info, Direction.Down);

        return Creatures.GetFrame(spriteId, start)?.Texture;
    }

    private void ReleaseThumbs()
    {
        foreach (var texture in Thumbs.Values)
            texture?.Dispose();

        Thumbs.Clear();
    }
}
