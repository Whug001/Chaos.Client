#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Bank;
using Chaos.Client.Controls.World.Popups.Beauty;
using Chaos.Client.Controls.World.Popups.Beauty.Pages;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.FittingRoom;

/// <summary>
///     Illustria's Closet: the player stands at a mirror (left) and tries on overcoats, hats and accessories from the
///     racks (right). Everything is read from <see cref="WorldState.FittingRoom" />. Nothing is sent to the server:
///     closing the window only hides it, and the server closes it when the player steps off the mirror tile.
/// </summary>
/// <remarks>
///     The view model's lists are rebuilt on every read, so the grid, the rows, the swatches and the figure are only
///     repainted when something changes (tab, search, page, hover, toggle, color, strip, open), never per frame.
/// </remarks>
public sealed class FittingRoomControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 600;
    private const int PANEL_HEIGHT = 470;
    private const int TOP_MARGIN = 5;
    private const int HEADER_TOP = 10;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    //footer -- kept clear of the frame's ornate bottom border
    private const int FOOTER_TOP = PANEL_HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;
    private const int STRIP_BUTTON_WIDTH = 150;

    //left column: the mirror stage over a row of turn buttons
    private const int STAGE_LEFT = 20;
    private const int STAGE_WIDTH = 230;
    private const int STAGE_TOP = HEADER_TOP + TextRenderer.CHAR_HEIGHT + 4;
    private const int TURN_TOP = FOOTER_TOP - CustomButton.HEIGHT - 6;
    private const int STAGE_HEIGHT = TURN_TOP - 6 - STAGE_TOP;
    private const int TURN_BUTTON_WIDTH = 28;

    //right column: tabs, search and page, the grid (or the Wearing rows), the caption and the swatches
    private const int RACK_LEFT = STAGE_LEFT + STAGE_WIDTH + 12;
    private const int RACK_WIDTH = PANEL_WIDTH - RACK_LEFT - 20;
    private const int TAB_TOP = 8;
    private const int TAB_GAP = 4;
    private const int TAB_WIDTH = (RACK_WIDTH - (3 * TAB_GAP)) / 4;
    private const int SEARCH_TOP = TAB_TOP + CustomButton.HEIGHT + 6;
    private const int SEARCH_WIDTH = 190;
    private const int SEARCH_MAX_CHARS = 24;
    private const int GRID_TOP = SEARCH_TOP + CustomButton.HEIGHT + 6;
    private const int GRID_COLUMNS = 6;
    private const int GRID_ROWS = ViewModel.FittingRoom.PAGE_SIZE / GRID_COLUMNS;
    private const int GRID_COLUMN_GAP = 6;
    private const int GRID_HEIGHT = (GRID_ROWS * (ThumbnailGrid<FittingRoomLook>.CELL + ThumbnailGrid<FittingRoomLook>.GAP)) - ThumbnailGrid<FittingRoomLook>.GAP;
    private const int CAPTION_TOP = GRID_TOP + GRID_HEIGHT + 4;
    private const int SWATCH_TOP = CAPTION_TOP + TextRenderer.CHAR_HEIGHT + 4;

    //overcoat, head and three accessories
    private const int WORN_ROW_COUNT = 5;

    private static readonly (FittingRoomTab Tab, string Caption)[] TabCaptions =
    [
        (FittingRoomTab.Overcoats, "Overcoats"),
        (FittingRoomTab.Hats, "Hats"),
        (FittingRoomTab.Accessories, "Accessories"),
        (FittingRoomTab.Wearing, "Wearing")
    ];

    private readonly MirrorStage Stage;
    private readonly CustomButton[] TabButtons;
    private readonly CustomTextBox SearchBox;
    private readonly UILabel PageLabel;
    private readonly ThumbnailGrid<FittingRoomLook> Grid;
    private readonly WornRow[] WornRows;
    private readonly UILabel EmptyLabel;
    private readonly UILabel CaptionLabel;
    private readonly SwatchGrid Swatches;
    private readonly CustomButton StripButton;

    //the worn look whose colors the swatches show: the last one put on or clicked in the Wearing tab
    private FittingRoomLook? Picked;
    private DisplayColor? HoveredColor;
    private string LastSearch = string.Empty;

    public FittingRoomControl(AislingRenderer renderer)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        Name = "FittingRoom";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();
        Y = TOP_MARGIN;

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        //── left column ──
        var title = NewLabel(STAGE_LEFT, HEADER_TOP, STAGE_WIDTH, LegendColors.Gold, HorizontalAlignment.Center);
        title.Text = "Illustria's Closet";
        AddChild(title);

        Stage = new MirrorStage(renderer, STAGE_WIDTH, STAGE_HEIGHT) { X = STAGE_LEFT, Y = STAGE_TOP };
        AddChild(Stage);

        var turnLeft = new CustomButton("<", TURN_BUTTON_WIDTH) { X = STAGE_LEFT, Y = TURN_TOP };
        turnLeft.Clicked += () => Stage.Turn(-1);
        AddChild(turnLeft);

        var turnCaption = NewLabel(
            STAGE_LEFT + TURN_BUTTON_WIDTH,
            TURN_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            STAGE_WIDTH - (2 * TURN_BUTTON_WIDTH),
            LegendColors.Gray,
            HorizontalAlignment.Center);
        turnCaption.Text = "Turn";
        AddChild(turnCaption);

        var turnRight = new CustomButton(">", TURN_BUTTON_WIDTH) { X = STAGE_LEFT + STAGE_WIDTH - TURN_BUTTON_WIDTH, Y = TURN_TOP };
        turnRight.Clicked += () => Stage.Turn(+1);
        AddChild(turnRight);

        StripButton = new CustomButton(string.Empty, STRIP_BUTTON_WIDTH) { X = STAGE_LEFT, Y = FOOTER_TOP };
        StripButton.Clicked += ToggleStripped;
        AddChild(StripButton);

        //── right column ──
        TabButtons = new CustomButton[TabCaptions.Length];

        for (var i = 0; i < TabCaptions.Length; i++)
        {
            var tab = TabCaptions[i].Tab;
            var button = new CustomButton(TabCaptions[i].Caption, TAB_WIDTH) { X = RACK_LEFT + (i * (TAB_WIDTH + TAB_GAP)), Y = TAB_TOP };
            button.Clicked += () => ChooseTab(tab);
            TabButtons[i] = button;
            AddChild(button);
        }

        SearchBox = new CustomTextBox
        {
            X = RACK_LEFT,
            Y = SEARCH_TOP,
            Width = SEARCH_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = SEARCH_MAX_CHARS,
            HintText = "Search"
        };
        AddChild(SearchBox);

        PageLabel = NewLabel(
            RACK_LEFT + SEARCH_WIDTH,
            SEARCH_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            RACK_WIDTH - SEARCH_WIDTH,
            LegendColors.Gray,
            HorizontalAlignment.Right);
        AddChild(PageLabel);

        Grid = new ThumbnailGrid<FittingRoomLook>(GRID_COLUMNS, GRID_ROWS, GRID_COLUMN_GAP);
        Grid.X = RACK_LEFT + ((RACK_WIDTH - Grid.Width) / 2);
        Grid.Y = GRID_TOP;
        Grid.Hovered += look => Hover(look);
        Grid.HoverCleared += () => Hover(null);
        Grid.Selected += ToggleLook;
        Grid.PageStepped += StepPage;
        AddChild(Grid);

        WornRows = new WornRow[WORN_ROW_COUNT];

        for (var i = 0; i < WornRows.Length; i++)
        {
            var row = new WornRow(RACK_WIDTH) { X = RACK_LEFT, Y = GRID_TOP + (i * WornRow.HEIGHT), Visible = false };
            row.Clicked += PickWorn;
            row.RemoveClicked += RemoveLook;
            WornRows[i] = row;
            AddChild(row);
        }

        EmptyLabel = NewLabel(RACK_LEFT, GRID_TOP + 60, RACK_WIDTH, LegendColors.Gray, HorizontalAlignment.Center);
        AddChild(EmptyLabel);

        CaptionLabel = NewLabel(RACK_LEFT, CAPTION_TOP, RACK_WIDTH, LegendColors.Gold, HorizontalAlignment.Center);
        AddChild(CaptionLabel);

        Swatches = new SwatchGrid
        {
            X = RACK_LEFT + ((RACK_WIDTH - SwatchGrid.WidthFor(SwatchGrid.COLUMNS)) / 2),
            Y = SWATCH_TOP,
            Visible = false
        };
        Swatches.Hovered += color => HoverColor(color);
        Swatches.HoverCleared += () => HoverColor(null);
        Swatches.Selected += ChooseColor;
        AddChild(Swatches);
    }

    private static UILabel NewLabel(int x, int y, int width, Color color, HorizontalAlignment alignment = HorizontalAlignment.Left)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color,
            IsHitTestVisible = false
        };

    public override void Show()
    {
        Picked = null;
        HoveredColor = null;
        LastSearch = string.Empty;
        SearchBox.Text = string.Empty;
        WorldState.FittingRoom.Hovered = null;
        base.Show();
        Stage.StartWalkIn();
        Refresh();
    }

    /// <summary>Hides the window and drops the figure's textures. Sends nothing.</summary>
    public override void Hide()
    {
        if (!Visible)
            return;

        SearchBox.IsFocused = false;
        base.Hide();
        Stage.Release();
        WorldState.FittingRoom.Hovered = null;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        switch (e.Keycode)
        {
            case Keycode.Escape:
                Hide();
                e.Handled = true;

                break;

            //single-line boxes let Enter bubble up to here; the search is already live, so Enter just leaves the box
            case Keycode.Enter when SearchBox.IsFocused:
                SearchBox.IsFocused = false;
                e.Handled = true;

                break;

            default:
                base.OnKeyDown(e);

                break;
        }
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible || (SearchBox.Text == LastSearch))
            return;

        LastSearch = SearchBox.Text;
        WorldState.FittingRoom.Search = LastSearch.Trim();
        Refresh();
    }

    /// <summary>Repaints everything from the view model.</summary>
    private void Refresh()
    {
        var vm = WorldState.FittingRoom;

        //a look that was taken off (or a catalog that changed) no longer drives the swatches
        if (Picked is not null && !vm.IsWorn(Picked))
            Picked = null;

        for (var i = 0; i < TabButtons.Length; i++)
            TabButtons[i].Selected = TabCaptions[i].Tab == vm.Tab;

        StripButton.Caption = vm.Stripped ? "Put my clothes back" : "Take everything off";

        RefreshRack();
        RefreshDetail();
        RefreshStage();
    }

    private void RefreshRack()
    {
        var vm = WorldState.FittingRoom;
        var wearing = vm.Tab == FittingRoomTab.Wearing;
        var looks = vm.VisibleLooks;

        Grid.Visible = !wearing;
        PageLabel.Visible = !wearing;

        if (wearing)
        {
            for (var i = 0; i < WornRows.Length; i++)
            {
                var look = i < looks.Count ? looks[i] : null;
                WornRows[i].SetLook(look, look is null ? DisplayColor.Default : vm.ColorOf(look));
                WornRows[i].Selected = look is not null && (look == Picked);
            }

            EmptyLabel.Visible = looks.Count == 0;
            EmptyLabel.Text = vm.Worn.Count == 0 ? "You are wearing nothing new." : "Nothing matches.";

            return;
        }

        foreach (var row in WornRows)
            row.SetLook(null, DisplayColor.Default);

        //the icons are UiRenderer's shared cached textures: bound here, never disposed
        Grid.SetItems(
            looks,
            Picked,
            vm.PageCount,
            look => UiRenderer.Instance!.GetItemIcon(look.PanelSprite, vm.ColorOf(look)),
            vm.IsWorn);

        PageLabel.Text = $"Page {vm.Page + 1} / {vm.PageCount}";
        EmptyLabel.Visible = looks.Count == 0;
        EmptyLabel.Text = "Nothing matches.";
    }

    /// <summary>The caption (the hovered look, else the picked one) and the picked look's swatches.</summary>
    private void RefreshDetail()
    {
        var vm = WorldState.FittingRoom;
        var shown = vm.Hovered ?? Picked;

        if (shown is null)
        {
            CaptionLabel.ForegroundColor = LegendColors.Gray;
            CaptionLabel.Text = "Pick something to try on.";
        } else
        {
            var text = (vm.Hovered is null) && HoveredColor is { } color ? $"{shown.Name} - {color}" : shown.Name;
            CaptionLabel.ForegroundColor = LegendColors.Gold;
            CaptionLabel.Text = BankItemRow.FitToLabel(CaptionLabel, text);
        }

        if (Picked is null)
        {
            Swatches.Visible = false;

            return;
        }

        var colors = vm.SwatchColors(Picked);
        Swatches.Visible = colors.Count > 1;

        if (Swatches.Visible)
            Swatches.SetColors(colors, vm.ColorOf(Picked), HairPage.SwatchColorFor);
    }

    private void RefreshStage() => Stage.SetLook(WorldState.FittingRoom.Effective);

    private void ChooseTab(FittingRoomTab tab)
    {
        if (!Visible)
            return;

        WorldState.FittingRoom.Tab = tab;
        WorldState.FittingRoom.Hovered = null;
        Refresh();
    }

    private void StepPage(int delta)
    {
        if (!Visible)
            return;

        WorldState.FittingRoom.StepPage(delta);
        Refresh();
    }

    /// <summary>Pointing at a tile tries it on over the outfit; leaving it takes the preview off.</summary>
    private void Hover(FittingRoomLook? look)
    {
        //InputDispatcher doesn't forget a hovered element across Hide(); ignore any late callback from it
        if (!Visible)
            return;

        WorldState.FittingRoom.Hovered = look;
        RefreshDetail();
        RefreshStage();
    }

    private void ToggleLook(FittingRoomLook look)
    {
        if (!Visible)
            return;

        var vm = WorldState.FittingRoom;
        vm.Toggle(look);

        //the hover preview would keep a look that was just taken off on the figure until the mouse moved off its tile
        vm.Hovered = null;

        if (vm.IsWorn(look))
            Picked = look;

        Refresh();
    }

    private void PickWorn(FittingRoomLook look)
    {
        if (!Visible)
            return;

        Picked = look;
        Refresh();
    }

    private void RemoveLook(FittingRoomLook look)
    {
        if (!Visible)
            return;

        WorldState.FittingRoom.Remove(look);
        Refresh();
    }

    private void HoverColor(DisplayColor? color)
    {
        if (!Visible)
            return;

        HoveredColor = color;
        RefreshDetail();
    }

    private void ChooseColor(DisplayColor color)
    {
        if (!Visible || Picked is null)
            return;

        WorldState.FittingRoom.SetColor(Picked, color);
        Refresh();
    }

    private void ToggleStripped()
    {
        if (!Visible)
            return;

        WorldState.FittingRoom.Stripped = !WorldState.FittingRoom.Stripped;
        Refresh();
    }
}
