#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

public enum KartLayer
{
    Base,
    Driver,
    Shadow,
    Effect
}

public static class KartLayerTint
{
    public static Color Dye(byte value)
        => (BodyColor)value switch
        {
            BodyColor.White => Color.White,
            BodyColor.Pale => new Color(232, 217, 197),
            BodyColor.Brown => new Color(143, 94, 60),
            BodyColor.Green => Color.Green,
            BodyColor.Yellow => Color.Yellow,
            BodyColor.Tan => new Color(191, 153, 112),
            BodyColor.Grey => Color.Gray,
            BodyColor.LightBlue => Color.LightBlue,
            BodyColor.Orange => Color.Orange,
            BodyColor.Purple => Color.Purple,
            _ => Color.White
        };

    public static Color For(KartLayer layer, Color dye) => layer == KartLayer.Driver ? dye : Color.White;
}
