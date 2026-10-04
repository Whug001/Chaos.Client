#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty.Pages;

/// <summary>Page 3: every skin color at once, each head with its name under it.</summary>
public sealed class SkinPage : MirrorPageView
{
    private const int COLUMNS = 5;
    private const int ROWS = (ViewModel.BeautyShop.BODY_COLOR_PAGE_SIZE + COLUMNS - 1) / COLUMNS;

    //a 60 px column pitch leaves room for the longest name ("LightBlue") under a 32 px head
    private const int COLUMN_GAP = 28;
    private const int ROW_GAP = TextRenderer.CHAR_HEIGHT + 4;
    private const int GRID_TOP = TextRenderer.CHAR_HEIGHT + 4;
    private const int NAME_WIDTH = ThumbnailGrid<BodyColor>.CELL + COLUMN_GAP;

    private readonly UILabel Caption;
    private readonly ThumbnailGrid<BodyColor> Skins;
    private readonly UILabel[] Names = new UILabel[COLUMNS * ROWS];

    public SkinPage(MirrorActions actions, int width, int height)
        : base(actions, width, height)
    {
        Caption = AddLabel(0, 0, width);

        Skins = new ThumbnailGrid<BodyColor>(COLUMNS, ROWS, COLUMN_GAP, ROW_GAP)
        {
            X = (width - ThumbnailGrid<BodyColor>.WidthFor(COLUMNS, COLUMN_GAP)) / 2,
            Y = GRID_TOP
        };
        Skins.Hovered += c => Actions.Hover(v => v.SetHoverBodyColor(c));
        Skins.HoverCleared += () => Actions.Hover(v => v.ClearHover());
        Skins.Selected += c => Actions.Select(v => v.SelectBodyColor(c));
        Skins.PageStepped += d => Actions.Select(v => v.StepBodyColorPage(d));
        AddChild(Skins);

        for (var i = 0; i < Names.Length; i++)
        {
            var origin = Skins.CellOrigin(i);

            Names[i] = AddLabel(
                Skins.X + origin.X - (COLUMN_GAP / 2),
                Skins.Y + origin.Y + ThumbnailGrid<BodyColor>.CELL + 2,
                NAME_WIDTH,
                LegendColors.LightGray,
                HorizontalAlignment.Center);
        }
    }

    public override void Refresh()
    {
        var vm = WorldState.BeautyShop;
        var baseLook = BaseLook(vm);

        var skin = vm.HoveredBodyColor ?? vm.BodyColor;
        Caption.Text = $"Skin   {skin} - {CostText(skin == vm.CurrentBodyColor, vm.CostOfBodyColor(skin))}";

        var visible = vm.VisibleBodyColors;
        Skins.SetItems(visible, vm.BodyColor, vm.BodyColorPageCount, c => Actions.Thumbnails.Get(baseLook with { BodyColor = (int)c }));

        for (var i = 0; i < Names.Length; i++)
        {
            Names[i].Visible = i < visible.Count;
            Names[i].Text = i < visible.Count ? visible[i].ToString() : string.Empty;
        }
    }
}
