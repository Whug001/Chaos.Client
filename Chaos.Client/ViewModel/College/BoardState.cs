using Chaos.DarkAges.Definitions;
using Chaos.Networking.Converters;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel.College;

/// <summary>The room's live board as last received. <see cref="Drawing" /> is null for a blank pane.</summary>
public sealed class BoardState
{
    /// <summary>Swatch 0 in the editor and view: the glowing pane the wall shows through.</summary>
    public static readonly Color Pane = new(36, 92, 140);

    public CollegeBoardInfo Info { get; private set; } = new();
    public PixelDrawing? Drawing { get; private set; }

    /// <summary>Goes up on every <see cref="Apply" />, so UI that shows <see cref="Info" /> refreshes.</summary>
    public int Version { get; private set; }

    /// <summary>Goes up only when the board's data changes, so textures rebuild only then.</summary>
    public int DrawingVersion { get; private set; }

    public void Apply(CollegeBoardInfo info)
    {
        var changed = !info.Data.AsSpan().SequenceEqual(Info.Data);
        Info = info;
        Version++;

        if (!changed)
            return;

        Drawing = CollegeBoardCodec.TryUnpack(info.Data, out var palette, out var pixels)
            ? PixelDrawing.From(palette, pixels, CollegeProtocol.BOARD_WIDTH, CollegeProtocol.BOARD_HEIGHT)
            : null;

        DrawingVersion++;
    }

    public void Clear()
    {
        Info = new CollegeBoardInfo();
        Drawing = null;
        Version++;
        DrawingVersion++;
    }

    public static PixelDrawing NewBoard() => PixelDrawing.Blank(CollegeProtocol.BOARD_WIDTH, CollegeProtocol.BOARD_HEIGHT, Pane);
}
