#region
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>The places the mirror can stand the player in. Plain is the bare pedestal.</summary>
public enum MirrorBackdrop
{
    Plain,
    Mileth,
    Woodlands,
    Beach,
    FrozenCave,
    Crypt
}

/// <summary>
///     The backdrop list, each place's name and picture, and how a picture lines up with the figure. The pictures are
///     <see cref="SOURCE_WIDTH" /> x <see cref="SOURCE_HEIGHT" /> with the standing tile's centre at
///     (<see cref="SOURCE_ANCHOR_X" />, <see cref="SOURCE_ANCHOR_Y" />); Tools/MirrorBackdrops cuts them to match.
/// </summary>
public static class MirrorBackdrops
{
    public const int SOURCE_WIDTH = 260;
    public const int SOURCE_HEIGHT = 250;
    public const int SOURCE_ANCHOR_X = 130;
    public const int SOURCE_ANCHOR_Y = 153;
    public const int THUMBNAIL_SIZE = 24;

    /// <summary>How far below a box's centre <see cref="MirrorPreview" /> puts the figure's feet, in figure pixels.</summary>
    public const int FEET_BELOW_CENTER = AislingRenderer.CANVAS_CENTER_Y - AislingRenderer.BODY_CENTER_Y;

    public static IReadOnlyList<MirrorBackdrop> All { get; } = Enum.GetValues<MirrorBackdrop>();

    public static string Name(MirrorBackdrop place)
        => place switch
        {
            MirrorBackdrop.Plain      => "Plain",
            MirrorBackdrop.Mileth     => "Mileth",
            MirrorBackdrop.Woodlands  => "Woodlands",
            MirrorBackdrop.Beach      => "Beach",
            MirrorBackdrop.FrozenCave => "Frozen Cave",
            MirrorBackdrop.Crypt      => "Crypt",
            _                         => throw new ArgumentOutOfRangeException(nameof(place), place, null)
        };

    /// <summary>The embedded picture's key (see <see cref="MirrorBackdropAssets" />), or null for Plain, which has none.</summary>
    public static string? Key(MirrorBackdrop place)
        => place switch
        {
            MirrorBackdrop.Plain      => null,
            MirrorBackdrop.Mileth     => "mileth",
            MirrorBackdrop.Woodlands  => "woodlands",
            MirrorBackdrop.Beach      => "beach",
            MirrorBackdrop.FrozenCave => "frozencave",
            MirrorBackdrop.Crypt      => "crypt",
            _                         => throw new ArgumentOutOfRangeException(nameof(place), place, null)
        };

    /// <summary>
    ///     The picture pixel shown at (<paramref name="x" />, <paramref name="y" />) of a box drawn at
    ///     <paramref name="scale" />. The box pixel under the figure's feet shows the standing tile's centre and the rest
    ///     step away from it, so the tile stays under the feet at 2x too, where a source rectangle would land half a
    ///     picture pixel off.
    /// </summary>
    public static Point SourcePixel(int x, int y, int boxWidth, int boxHeight, int scale)
        => new(
            SOURCE_ANCHOR_X + FloorDiv(x - (boxWidth / 2), scale),
            SOURCE_ANCHOR_Y + FloorDiv(y - (boxHeight / 2) - (FEET_BELOW_CENTER * scale), scale));

    private static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);
}
