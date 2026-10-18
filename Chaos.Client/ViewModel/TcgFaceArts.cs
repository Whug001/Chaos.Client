#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>What a card face draws for its picture.</summary>
public enum TcgFaceArt : byte
{
    /// <summary>The card's sprite in the normal element frame: no painting, or its file is missing from setoa.dat.</summary>
    Sprite,
    Normal,
    Extended,
    Full
}

/// <summary>
///     Picks how a card face is drawn from its painting, the style asked for and the setoa.dat files this client has, so a
///     client with old game files still draws every card. Pure: the caller passes setoa.dat's frame counts.
/// </summary>
public static class TcgFaceArts
{
    public const string FRAME_FILE = "tcgframe.spf";
    public const string FRAME_F_FILE = "tcgframef.spf";
    public const string FRAME_X_FILE = "tcgframex.spf";

    /// <summary>Frames 3-5 of a painting are frames 0-2 at half size, for 1x faces.</summary>
    public const int HALF_OFFSET = 3;

    public const int MYTHIC_FRAME = 6;
    public const int PAINTING_EXTENDED = 1;
    public const int PAINTING_FRAMES = 6;
    public const int PAINTING_FULL = 2;
    public const int PAINTING_NORMAL = 0;

    public static int ElementFrame(TcgElement element)
        => element switch
        {
            TcgElement.Fire  => 0,
            TcgElement.Water => 1,
            TcgElement.Wind  => 2,
            TcgElement.Earth => 3,
            _                => 4
        };

    /// <summary>The frame for a card: the Mythic frame when the file has it, otherwise its element's.</summary>
    public static int FrameIndex(TcgCatalogCardView card, int frameFileCount)
        => (card.Rarity == TcgRarity.Mythic) && (frameFileCount > MYTHIC_FRAME) ? MYTHIC_FRAME : ElementFrame(card.Element);

    /// <summary>The painting's file in setoa.dat, for example tcgart007.spf.</summary>
    public static string PaintingFile(ushort painting) => $"tcgart{painting:D3}.spf";

    public static TcgFaceArt Resolve(TcgCatalogCardView card, TcgCardStyle style, Func<string, int> frameCount)
    {
        if ((card.Painting == 0) || (frameCount(PaintingFile(card.Painting)) < PAINTING_FRAMES))
            return TcgFaceArt.Sprite;

        return style switch
        {
            TcgCardStyle.Full when (card.Rarity >= TcgRarity.Rare) && (frameCount(FRAME_F_FILE) > MYTHIC_FRAME) => TcgFaceArt.Full,
            TcgCardStyle.Full or TcgCardStyle.Extended when frameCount(FRAME_X_FILE) > MYTHIC_FRAME           => TcgFaceArt.Extended,
            _                                                                                                 => TcgFaceArt.Normal
        };
    }
}
