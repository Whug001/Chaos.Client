#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Data;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty.Pages;

/// <summary>Page 2: a hairstyle grid (16 heads a page) above every hair color.</summary>
public sealed class HairPage : MirrorPageView
{
    private const int STYLE_COLUMNS = 8;
    private const int STYLE_ROWS = (ViewModel.BeautyShop.HAIRSTYLE_PAGE_SIZE + STYLE_COLUMNS - 1) / STYLE_COLUMNS;
    private const int GRID_TOP = TextRenderer.CHAR_HEIGHT + 2;
    private const int SECTION_GAP = 8;

    private readonly UILabel StyleCaption;
    private readonly UILabel StylePageLabel;
    private readonly ThumbnailGrid<BeautyShopHairstyleEntry> Styles;
    private readonly UILabel ColorCaption;
    private readonly SwatchGrid Colors;

    public HairPage(MirrorActions actions, int width, int height)
        : base(actions, width, height)
    {
        StyleCaption = AddLabel(0, 0, width);
        StylePageLabel = AddLabel(0, 0, width, LegendColors.Gray, HorizontalAlignment.Right);

        Styles = new ThumbnailGrid<BeautyShopHairstyleEntry>(STYLE_COLUMNS, STYLE_ROWS) { X = 0, Y = GRID_TOP };
        Styles.Hovered += h => Actions.Hover(v => v.SetHoverHairStyle(h.Sprite));
        Styles.HoverCleared += () => Actions.Hover(v => v.ClearHover());
        Styles.Selected += h => Actions.Select(v => v.SelectHairStyle(h.Sprite));
        Styles.PageStepped += d => Actions.Select(v => v.StepHairstylePage(d));
        AddChild(Styles);

        var colorTop = Styles.Y + Styles.Height + SECTION_GAP;
        ColorCaption = AddLabel(0, colorTop, width);

        Colors = new SwatchGrid { X = Styles.CellLeft, Y = colorTop + TextRenderer.CHAR_HEIGHT + 2 };
        Colors.Hovered += c => Actions.Hover(v => v.SetHoverHairColor(c));
        Colors.HoverCleared += () => Actions.Hover(v => v.ClearHover());
        Colors.Selected += c => Actions.Select(v => v.SelectHairColor(c));
        AddChild(Colors);
    }

    public override void Refresh()
    {
        var vm = WorldState.BeautyShop;
        var baseLook = BaseLook(vm);

        var style = vm.HoveredHairStyle ?? vm.HairStyle;
        var stylePosition = Position(vm.Hairstyles, h => h.Sprite == style);
        StyleCaption.Text = $"Hairstyle   {stylePosition} / {vm.Hairstyles.Count} - {CostText(style == vm.CurrentHairStyle, vm.CostOfHairStyle(style))}";
        StylePageLabel.Text = $"PAGE {vm.HairstylePage + 1}/{vm.HairstylePageCount}";
        Styles.SetItems(
            vm.VisibleHairstyles,
            vm.Hairstyles.FirstOrDefault(h => h.Sprite == vm.HairStyle),
            vm.HairstylePageCount,
            h => Actions.Thumbnails.Get(baseLook with { HeadSprite = h.Sprite }));

        var color = vm.HoveredHairColor ?? vm.HairColor;
        ColorCaption.Text = $"Hair color   {color} - {CostText(color == vm.CurrentHairColor, vm.CostOfHairColor(color))}";
        Colors.SetColors(vm.HairColors, vm.HairColor, SwatchColorFor);
    }

    private static Color SwatchColorFor(DisplayColor color)
    {
        if (color == DisplayColor.Default)
            return LegendColors.DeepLavender;

        var table = DataContext.AislingDrawData.DyeColorTable;

        if (!table.Contains((int)color))
            return LegendColors.Gray;

        var colors = table[(int)color].Colors;
        var mid = colors[Math.Min(2, colors.Length - 1)];

        return new Color(mid.Red, mid.Green, mid.Blue);
    }
}
