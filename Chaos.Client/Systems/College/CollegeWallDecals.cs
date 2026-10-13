#region
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Systems.College;

/// <summary>
///     The College room's wall art as strip textures, one per wall tile: the six picture frames and the glowing board.
///     Strips are built once per change and disposed when replaced.
/// </summary>
public sealed class CollegeWallDecals : IDisposable
{
    private static readonly IReadOnlyList<Decal> NoDecals = [];

    private readonly Dictionary<(int X, int Y), List<Decal>> BoardStrips = [];
    private readonly Dictionary<(int X, int Y), List<Decal>> FrameStrips = [];

    //frames, then board per tile, rebuilt on every change so At allocates nothing
    private readonly Dictionary<(int X, int Y), List<Decal>> Ordered = [];

    public bool HasWall { get; private set; }

    public void Dispose() => Clear();

    /// <summary>The strips on wall tile (x, y), in draw order: frames, then the board.</summary>
    public IReadOnlyList<Decal> At(int x, int y) => Ordered.TryGetValue((x, y), out var decals) ? decals : NoDecals;

    public void Clear()
    {
        Release(FrameStrips);
        Release(BoardStrips);
        Ordered.Clear();
        HasWall = false;
    }

    public void SetBoard(GraphicsDevice device, PixelDrawing? drawing)
    {
        Release(BoardStrips);

        Add(
            device,
            BoardStrips,
            WallDecalPainter.Board(drawing?.Palette, drawing?.Pixels),
            WallDecalGeometry.Board);

        Reorder();
    }

    public void SetWall(GraphicsDevice device, IReadOnlyList<CollegeWallSlotInfo> slots)
    {
        Release(FrameStrips);

        for (var i = 0; (i < slots.Count) && (i < WallDecalGeometry.Frames.Length); i++)
        {
            var slot = slots[i];

            if (!slot.Filled
                || (slot.Palette.Length != CollegeProtocol.DRAWING_PALETTE_BYTES)
                || (slot.Pixels.Length != CollegeProtocol.DRAWING_PIXELS))
                continue;

            var drawing = PixelDrawing.From(slot.Palette, slot.Pixels);

            Add(
                device,
                FrameStrips,
                WallDecalPainter.Framed(drawing.Palette, drawing.Pixels),
                WallDecalGeometry.Frames[i]);
        }

        HasWall = true;

        if (BoardStrips.Count == 0)
            SetBoard(device, null);
        else
            Reorder();
    }

    private static void Add(
        GraphicsDevice device,
        Dictionary<(int X, int Y), List<Decal>> target,
        Color[] image,
        WallSpot spot)
    {
        var strips = WallDecalPainter.Strips(image, WallDecalGeometry.PICTURE_HEIGHT, spot);
        var height = WallDecalGeometry.StripHeight(WallDecalGeometry.PICTURE_HEIGHT);

        for (var i = 0; i < strips.Length; i++)
        {
            var texture = new Texture2D(device, MirrorGeometry.FACE_WIDTH, height);
            texture.SetData(strips[i]);

            var tile = spot.TileOf(i);

            if (!target.TryGetValue(tile, out var list))
                target[tile] = list = [];

            list.Add(new Decal(texture, spot.Side));
        }
    }

    private static void Release(Dictionary<(int X, int Y), List<Decal>> strips)
    {
        foreach (var list in strips.Values)
            foreach (var decal in list)
                decal.Texture.Dispose();

        strips.Clear();
    }

    private void Reorder()
    {
        Ordered.Clear();

        foreach (var source in (ReadOnlySpan<Dictionary<(int X, int Y), List<Decal>>>)[FrameStrips, BoardStrips])
            foreach (var (tile, decals) in source)
            {
                if (!Ordered.TryGetValue(tile, out var list))
                    Ordered[tile] = list = [];

                list.AddRange(decals);
            }
    }

    public sealed record Decal(Texture2D Texture, MirrorSide Side);
}
