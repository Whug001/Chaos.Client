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
    private readonly UILabel OmbreCaption;
    private readonly SwatchGrid Ombres;

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

        OmbreCaption = AddLabel(0, 0, width);
        OmbreCaption.Text = "Ombre dyes";

        Ombres = new SwatchGrid { X = Styles.CellLeft };
        Ombres.Hovered += c => Actions.Hover(v => v.SetHoverHairColor(c));
        Ombres.HoverCleared += () => Actions.Hover(v => v.ClearHover());
        Ombres.Selected += c => Actions.Select(v => v.SelectHairColor(c));
        AddChild(Ombres);
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
        ColorCaption.Text = $"Hair color   {OmbreDyes.NameOf(color)} - {CostText(color == vm.CurrentHairColor, vm.CostOfHairColor(color))}";
        Colors.SetColors(vm.HairColors.Where(c => !OmbreDyes.IsOmbre(c)).ToList(), vm.HairColor, SwatchColorFor);

        var ombres = VisibleOmbres(vm.HairColors);
        OmbreCaption.Visible = Ombres.Visible = ombres.Count > 0;
        OmbreCaption.Y = Colors.Y + Colors.Height + SECTION_GAP;
        Ombres.Y = OmbreCaption.Y + TextRenderer.CHAR_HEIGHT + 2;
        Ombres.SetColors(ombres, vm.HairColor, SwatchColorFor, TipSwatchColorFor);
    }

    /// <summary>
    ///     Ombre dyes the client can actually paint: catalog ombres gated on Legend.dat's dye table holding the
    ///     root ramp (72) and the Lod tip ramps (200+). Old tables render ombres as plain hair, so the block stays
    ///     hidden and they cannot be picked.
    /// </summary>
    internal static List<DisplayColor> VisibleOmbres(IReadOnlyList<DisplayColor> hairColors)
        => VisibleOmbres(hairColors, static i => DataContext.AislingDrawData.DyeColorTable.Contains(i));

    internal static List<DisplayColor> VisibleOmbres(IReadOnlyList<DisplayColor> hairColors, Func<int, bool> dyeContains)
        => dyeContains(72) && dyeContains(200) ? hairColors.Where(OmbreDyes.IsOmbre).ToList() : [];

    internal static Color SwatchColorFor(DisplayColor color)
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

    /// <summary>The bottom half of an ombre swatch: the tip ramp's middle color. Null for plain colors.</summary>
    internal static Color? TipSwatchColorFor(DisplayColor color)
        => OmbreDyes.TryGet(color, out var ombre) ? SwatchColorFor((DisplayColor)ombre.Tip) : null;
}
