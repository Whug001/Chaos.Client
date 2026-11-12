#region
using System.Globalization;
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Bank;
using Chaos.Client.Controls.World.Popups.Beauty;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Controls.World.Popups.FittingRoom;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Dye;

/// <summary>
///     The Dye Vat: the player's dyeable items (right, top), the colours on offer (right, bottom) and a mirror showing
///     the selected item in the hovered or chosen colour (left). Everything is read from <see cref="WorldState.DyeShop" />;
///     gold only moves when the server answers an Apply, which the player confirms first.
/// </summary>
/// <remarks>
///     The view model's item page is rebuilt on every read, so the panel repaints only when something changes (open,
///     reject, page, selection, hover), never per frame. Hover repaints only the figure and the item caption.
/// </remarks>
public sealed class DyeShopControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 600;
    private const int PANEL_HEIGHT = 470;
    private const int TOP_MARGIN = 5;
    private const int HEADER_TOP = 10;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    //footer -- kept clear of the frame's ornate bottom border
    private const int FOOTER_TOP = PANEL_HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;
    private const int FOOTER_TEXT_TOP = FOOTER_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);
    private const int GOLD_WIDTH = 120;
    private const int DYE_BUTTON_WIDTH = 80;

    //left column: the mirror stage over a row of turn buttons
    private const int STAGE_LEFT = 20;
    private const int STAGE_WIDTH = 230;
    private const int STAGE_TOP = HEADER_TOP + TextRenderer.CHAR_HEIGHT + 4;
    private const int TURN_TOP = FOOTER_TOP - CustomButton.HEIGHT - 6;
    private const int STAGE_HEIGHT = TURN_TOP - 6 - STAGE_TOP;
    private const int TURN_BUTTON_WIDTH = 28;
    private const int ICON_SCALE = 3;

    //right column: the item grid and its page, the item caption, then the two swatch blocks
    private const int RACK_LEFT = STAGE_LEFT + STAGE_WIDTH + 12;
    private const int RACK_WIDTH = PANEL_WIDTH - RACK_LEFT - 20;
    private const int GRID_TOP = HEADER_TOP + TextRenderer.CHAR_HEIGHT + 6;
    private const int GRID_COLUMNS = 6;
    private const int GRID_ROWS = ViewModel.DyeShop.PAGE_SIZE / GRID_COLUMNS;
    private const int GRID_COLUMN_GAP = 6;
    private const int GRID_HEIGHT = (GRID_ROWS * (ThumbnailGrid<DyeShopItem>.CELL + ThumbnailGrid<DyeShopItem>.GAP)) - ThumbnailGrid<DyeShopItem>.GAP;
    private const int ITEM_CAPTION_TOP = GRID_TOP + GRID_HEIGHT + 6;
    private const int COLOURS_CAPTION_TOP = ITEM_CAPTION_TOP + TextRenderer.CHAR_HEIGHT + 8;
    private const int SWATCH_CAPTION_GAP = 4;
    private const int SWATCH_BLOCK_GAP = 8;

    //SetColors needs a selected colour; with no item selected nothing is, and no colour on offer is 255
    private const DisplayColor NO_SELECTION = (DisplayColor)byte.MaxValue;

    private readonly MirrorStage Stage;
    private readonly ScaledIcon Icon;
    private readonly UILabel PageLabel;
    private readonly ThumbnailGrid<DyeShopItem> Grid;
    private readonly UILabel EmptyLabel;
    private readonly UILabel ItemCaption;
    private readonly UILabel ColoursCaption;
    private readonly SwatchGrid ColourSwatches;
    private readonly UILabel OmbreCaption;
    private readonly SwatchGrid OmbreSwatches;
    private readonly UILabel GoldLabel;
    private readonly UILabel StatusLabel;
    private readonly CustomButton DyeButton;
    private readonly DyeConfirmPanel Confirm;

    /// <summary>The player confirmed a dye: inventory slot, item unique id, colour.</summary>
    public event Action<byte, ulong, DisplayColor>? ApplyRequested;

    /// <summary>The player closed the panel (Close or Escape). Not raised when the server closes it.</summary>
    public event Action? Closed;

    public DyeShopControl(AislingRenderer renderer)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        Name = "DyeShop";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();
        Y = TOP_MARGIN;

        OkButton = CreateCloseButton(RequestDismissal, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        //── left column ──
        var title = NewLabel(STAGE_LEFT, HEADER_TOP, STAGE_WIDTH, LegendColors.Gold, HorizontalAlignment.Center);
        title.Text = "Dye Vat";
        AddChild(title);

        Stage = new MirrorStage(renderer, STAGE_WIDTH, STAGE_HEIGHT) { X = STAGE_LEFT, Y = STAGE_TOP };
        AddChild(Stage);

        //an item the figure can't wear is shown as its own icon over the same spot
        Icon = new ScaledIcon
        {
            X = STAGE_LEFT,
            Y = STAGE_TOP,
            Width = STAGE_WIDTH,
            Height = STAGE_HEIGHT,
            Visible = false,
            IsHitTestVisible = false
        };
        AddChild(Icon);

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

        //── right column ──
        var itemsCaption = NewLabel(RACK_LEFT, HEADER_TOP, RACK_WIDTH, LegendColors.Gray);
        itemsCaption.Text = "Dyeable items";
        AddChild(itemsCaption);

        PageLabel = NewLabel(RACK_LEFT, HEADER_TOP, RACK_WIDTH, LegendColors.Gray, HorizontalAlignment.Right);
        AddChild(PageLabel);

        Grid = new ThumbnailGrid<DyeShopItem>(GRID_COLUMNS, GRID_ROWS, GRID_COLUMN_GAP);
        Grid.X = RACK_LEFT + ((RACK_WIDTH - Grid.Width) / 2);
        Grid.Y = GRID_TOP;
        Grid.Selected += SelectItem;
        Grid.PageStepped += StepPage;
        AddChild(Grid);

        EmptyLabel = NewLabel(RACK_LEFT, GRID_TOP + ((GRID_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2), RACK_WIDTH, LegendColors.Gray, HorizontalAlignment.Center);
        EmptyLabel.Text = "You carry nothing that can be dyed.";
        AddChild(EmptyLabel);

        ItemCaption = NewLabel(RACK_LEFT, ITEM_CAPTION_TOP, RACK_WIDTH, LegendColors.Gold, HorizontalAlignment.Center);
        AddChild(ItemCaption);

        var swatchLeft = RACK_LEFT + ((RACK_WIDTH - SwatchGrid.WidthFor(SwatchGrid.COLUMNS)) / 2);

        ColoursCaption = NewLabel(RACK_LEFT, COLOURS_CAPTION_TOP, RACK_WIDTH, LegendColors.Gray, HorizontalAlignment.Center);
        AddChild(ColoursCaption);
        ColourSwatches = NewSwatchGrid(swatchLeft, COLOURS_CAPTION_TOP + TextRenderer.CHAR_HEIGHT + SWATCH_CAPTION_GAP);
        AddChild(ColourSwatches);

        //placed under the colours in RefreshSwatches, once their height is known
        OmbreCaption = NewLabel(RACK_LEFT, COLOURS_CAPTION_TOP, RACK_WIDTH, LegendColors.Gray, HorizontalAlignment.Center);
        AddChild(OmbreCaption);
        OmbreSwatches = NewSwatchGrid(swatchLeft, COLOURS_CAPTION_TOP);
        AddChild(OmbreSwatches);

        //── footer ──
        GoldLabel = NewLabel(STAGE_LEFT, FOOTER_TEXT_TOP, GOLD_WIDTH, LegendColors.Gold);
        AddChild(GoldLabel);

        DyeButton = new CustomButton("Dye", DYE_BUTTON_WIDTH) { X = PANEL_WIDTH - 20 - DYE_BUTTON_WIDTH, Y = FOOTER_TOP };
        DyeButton.Clicked += AskToDye;
        AddChild(DyeButton);

        StatusLabel = NewLabel(
            STAGE_LEFT + GOLD_WIDTH,
            FOOTER_TEXT_TOP,
            DyeButton.X - 6 - (STAGE_LEFT + GOLD_WIDTH),
            LegendColors.Gray,
            HorizontalAlignment.Center);
        AddChild(StatusLabel);

        //drawn above everything else in the panel, and covering it, so nothing beneath can be used while it stands
        Confirm = new DyeConfirmPanel(PANEL_WIDTH, PANEL_HEIGHT) { X = 0, Y = 0, ZIndex = 100 };
        Confirm.Confirmed += Dye;
        AddChild(Confirm);
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

    private SwatchGrid NewSwatchGrid(int x, int y)
    {
        var grid = new SwatchGrid { X = x, Y = y, Visible = false };
        grid.Hovered += color => HoverColor(color);
        grid.HoverCleared += () => HoverColor(null);
        grid.Selected += ChooseColor;

        return grid;
    }

    /// <summary>What the footer says when the server refused the last dye.</summary>
    public static string RejectText(DyeShopRejectReason reason)
        => reason switch
        {
            DyeShopRejectReason.NotNearShop   => "Return to the tailor.",
            DyeShopRejectReason.ItemGone      => "That item is no longer in your pack.",
            DyeShopRejectReason.InvalidColor  => "The tailor can't do that colour.",
            DyeShopRejectReason.NotEnoughGold => "You can't afford that.",
            _                                 => "The tailor shakes his head."
        };

    /// <summary>The question the confirm prompt asks before a dye is sent.</summary>
    public static string ConfirmText(string itemName, DisplayColor color, int price)
        => $"Dye {itemName} {OmbreDyes.NameOf(color)} for {price.ToString("N0", CultureInfo.InvariantCulture)} gold?";

    public override void Show()
    {
        base.Show();
        Stage.StartWalkIn();
    }

    /// <summary>Hides the window, the prompt and the figure's textures. Sends nothing: see <see cref="RequestDismissal" />.</summary>
    public override void Hide()
    {
        if (!Visible)
            return;

        Confirm.Hide();
        base.Hide();
        Stage.Release();
        WorldState.DyeShop.HoveredColor = null;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            RequestDismissal();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Dispose()
    {
        //the icon is UiRenderer's shared cached texture: unbind it, never dispose it
        Icon.Texture = null;
        base.Dispose();
    }

    /// <summary>Repaints everything from <see cref="WorldState.DyeShop" />.</summary>
    public void Refresh()
    {
        var vm = WorldState.DyeShop;

        RefreshItems(vm);
        RefreshSwatches(vm);
        RefreshPreview();
        RefreshFooter(vm);
    }

    /// <summary>
    ///     The player's own close (Close button or Escape): hide, then tell the server. The server's Close goes through
    ///     <see cref="Hide" /> alone, so it is never echoed back -- the same split as <c>BeautyShopControl</c>.
    /// </summary>
    private void RequestDismissal()
    {
        if (!Visible)
            return;

        Hide();
        Closed?.Invoke();
    }

    private void RefreshItems(ViewModel.DyeShop vm)
    {
        var items = vm.VisibleItems;

        //the thumbnails are UiRenderer's shared cached textures: bound here, never disposed
        Grid.SetItems(items, vm.SelectedItem, vm.PageCount, item => UiRenderer.Instance!.GetItemIcon(item.PanelSprite, item.Color));

        PageLabel.Text = $"PAGE {vm.Page + 1}/{vm.PageCount}";
        EmptyLabel.Visible = vm.Items.Count == 0;
    }

    private void RefreshSwatches(ViewModel.DyeShop vm)
    {
        var selected = vm.SelectedItem is { } item ? vm.SelectedColor ?? item.Color : NO_SELECTION;
        var ombres = DyeSwatches.VisibleOmbres(vm.OmbreColors);

        ColoursCaption.Visible = ColourSwatches.Visible = vm.Colors.Count > 0;
        OmbreCaption.Visible = OmbreSwatches.Visible = ombres.Count > 0;

        var nextTop = COLOURS_CAPTION_TOP;

        if (ColourSwatches.Visible)
        {
            ColoursCaption.Text = $"Colours - {FormatGold(vm.DyePrice)} gold";
            ColourSwatches.SetColors(vm.Colors, selected, DyeSwatches.SwatchColorFor);
            nextTop = ColourSwatches.Y + ColourSwatches.Height + SWATCH_BLOCK_GAP;
        }

        if (!OmbreSwatches.Visible)
            return;

        OmbreCaption.Text = $"Ombre dyes - {FormatGold(vm.OmbreDyePrice)} gold";
        OmbreCaption.Y = nextTop;
        OmbreSwatches.Y = nextTop + TextRenderer.CHAR_HEIGHT + SWATCH_CAPTION_GAP;
        OmbreSwatches.SetColors(ombres, selected, DyeSwatches.SwatchColorFor, DyeSwatches.TipSwatchColorFor);
    }

    /// <summary>The figure (or the item's icon) in the preview colour, and the caption naming them.</summary>
    private void RefreshPreview()
    {
        var vm = WorldState.DyeShop;

        if ((vm.SelectedItem is not { } item) || (vm.PreviewColor is not { } color))
        {
            ItemCaption.ForegroundColor = LegendColors.Gray;
            ItemCaption.Text = "Pick an item to dye.";
            ShowFigure(TownImportPreviewLook.Plain(vm.Gender) with { HeadSprite = vm.HairStyle, HeadColor = vm.HairColor });

            return;
        }

        ItemCaption.ForegroundColor = LegendColors.Gold;
        ItemCaption.Text = BankItemRow.FitToLabel(ItemCaption, $"{item.Name} -> {OmbreDyes.NameOf(color)}");

        if (DyeShopFigure.CanWear(item, vm.Gender))
        {
            ShowFigure(DyeShopFigure.Compose(vm.Gender, vm.HairStyle, vm.HairColor, item, color));

            return;
        }

        Stage.Visible = false;
        Icon.Visible = true;
        Icon.Texture = UiRenderer.Instance!.GetItemIcon(item.PanelSprite, color);
    }

    private void ShowFigure(AislingAppearance look)
    {
        Icon.Visible = false;
        Icon.Texture = null;
        Stage.Visible = true;
        Stage.SetLook(look);
    }

    private void RefreshFooter(ViewModel.DyeShop vm)
    {
        GoldLabel.Text = $"Gold: {FormatGold(vm.Gold)}";
        DyeButton.Enabled = vm.CanDye;

        if (vm.LastRejectReason is { } reason)
        {
            StatusLabel.ForegroundColor = LegendColors.Red;
            StatusLabel.Text = RejectText(reason);
        } else
        {
            StatusLabel.ForegroundColor = LegendColors.Gray;
            StatusLabel.Text = vm.DisabledReason ?? string.Empty;
        }
    }

    private static string FormatGold(int amount) => amount.ToString("N0", CultureInfo.InvariantCulture);

    private void SelectItem(DyeShopItem item)
    {
        if (!Visible)
            return;

        WorldState.DyeShop.SelectItem(item);
        Refresh();
    }

    private void StepPage(int delta)
    {
        if (!Visible)
            return;

        WorldState.DyeShop.StepPage(delta);
        Refresh();
    }

    /// <summary>Pointing at a swatch dyes the preview; leaving the swatches returns it to the chosen or current colour.</summary>
    private void HoverColor(DisplayColor? color)
    {
        //InputDispatcher doesn't forget a hovered element across Hide(); ignore any late callback from it
        if (!Visible || (WorldState.DyeShop.SelectedItem is null))
            return;

        WorldState.DyeShop.HoveredColor = color;
        RefreshPreview();
    }

    private void ChooseColor(DisplayColor color)
    {
        if (!Visible || (WorldState.DyeShop.SelectedItem is null))
            return;

        WorldState.DyeShop.SelectColor(color);
        Refresh();
    }

    private void AskToDye()
    {
        var vm = WorldState.DyeShop;

        if (!Visible || !vm.CanDye || (vm.SelectedItem is not { } item) || (vm.SelectedColor is not { } color))
            return;

        Confirm.Ask(ConfirmText(item.Name, color, vm.CostOf(color)));
    }

    /// <summary>The player said Yes. Re-checked, because a server Open while the prompt stood resets the choice.</summary>
    private void Dye()
    {
        var vm = WorldState.DyeShop;

        if (!Visible || !vm.CanDye || (vm.SelectedItem is not { } item) || (vm.SelectedColor is not { } color))
            return;

        vm.MarkSent();
        ApplyRequested?.Invoke(item.Slot, item.UniqueId, color);
        Refresh();
    }

    /// <summary>An item icon drawn at <see cref="ICON_SCALE" />x, centred in this element's bounds.</summary>
    private sealed class ScaledIcon : UIImage
    {
        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            //primes ClipRect for DrawTextureFitted's clip check
            UpdateClipRect();

            if (Texture is not { IsDisposed: false } texture)
                return;

            var width = texture.Width * ICON_SCALE;
            var height = texture.Height * ICON_SCALE;

            DrawTextureFitted(
                spriteBatch,
                texture,
                new Rectangle(ScreenX + ((Width - width) / 2), ScreenY + ((Height - height) / 2), width, height),
                Tint);
        }
    }
}
