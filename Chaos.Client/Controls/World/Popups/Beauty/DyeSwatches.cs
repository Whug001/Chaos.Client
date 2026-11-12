#region
using Chaos.Client.Data;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>Swatch colors and ombre visibility shared by every window that offers dyes.</summary>
public static class DyeSwatches
{
    /// <summary>
    ///     Ombre dyes the client can actually paint: catalog ombres gated on Legend.dat's dye table holding the
    ///     root ramp (72) and the Lod tip ramps (200+). Old tables render ombres as plain hair, so the block stays
    ///     hidden and they cannot be picked.
    /// </summary>
    public static List<DisplayColor> VisibleOmbres(IReadOnlyList<DisplayColor> colors)
        => VisibleOmbres(colors, static i => DataContext.AislingDrawData.DyeColorTable.Contains(i));

    public static List<DisplayColor> VisibleOmbres(IReadOnlyList<DisplayColor> colors, Func<int, bool> dyeContains)
        => dyeContains(72) && dyeContains(200) ? colors.Where(OmbreDyes.IsOmbre).ToList() : [];

    public static Color SwatchColorFor(DisplayColor color)
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
    public static Color? TipSwatchColorFor(DisplayColor color)
        => OmbreDyes.TryGet(color, out var ombre) ? SwatchColorFor((DisplayColor)ombre.Tip) : null;
}
