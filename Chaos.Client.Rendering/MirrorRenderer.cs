#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Owns the mirror render targets and textures. Each frame, before the world pass, WorldScreen paints every character
///     a mirror needs into the character atlas (one cell per character and pose), then composes each visible face into
///     its own cell of the face atlas: glass first, then reflections clipped to that glass. A reflection therefore
///     cannot land on a neighbouring face. The world pass draws that cell and then the frame sprite.
/// </summary>
public sealed class MirrorRenderer : IDisposable
{
    public const int CELL_SIZE = 160;
    public const int CELL_ANCHOR_X = 80;
    public const int CELL_ANCHOR_Y = 136;
    public const int ATLAS_COLUMNS = 12;
    public const int ATLAS_ROWS = 8;
    public const int FACE_COLUMNS = 32;
    public const int FACE_ROWS = 16;

    /// <summary>Draws only where the target already has alpha (the glass) and keeps the target's alpha.</summary>
    public static readonly BlendState SourceAtop = new()
    {
        Name = "MirrorSourceAtop",
        ColorSourceBlend = Blend.DestinationAlpha,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One
    };

    private static readonly RasterizerState CellScissor = new()
    {
        CullMode = CullMode.None,
        ScissorTestEnable = true
    };

    private readonly Dictionary<ulong, int> CellByKey = [];
    private readonly Dictionary<(int X, int Y, MirrorSide Side), int> FaceByWall = [];
    private readonly GraphicsDevice Device;
    private RenderTarget2D? Atlas;
    private SpriteBatch? Batch;
    private Texture2D? DiamondTexture;
    private RenderTarget2D? FaceAtlas;
    private Texture2D? GlowTexture;
    private int NextCell;
    private int NextFace;
    private Texture2D? NorthGlass;
    private Texture2D? PixelTexture;
    private Texture2D? WestGlass;

    public MirrorRenderer(GraphicsDevice device) => Device = device;

    public Texture2D? AtlasTexture => Atlas;

    public Texture2D? FaceAtlasTexture => FaceAtlas;

    /// <summary>True once this frame's faces have been composed.</summary>
    public bool FacesReady { get; private set; }

    /// <summary>A white 56x27 tile diamond.</summary>
    public Texture2D Diamond => DiamondTexture ??= BuildDiamond();

    /// <summary>A soft white 64x32 ellipse, premultiplied, for additive light.</summary>
    public Texture2D Glow => GlowTexture ??= BuildGlow();

    public Texture2D Pixel => PixelTexture ??= BuildPixel();

    public void Dispose()
    {
        Atlas?.Dispose();
        FaceAtlas?.Dispose();
        Batch?.Dispose();
        DiamondTexture?.Dispose();
        GlowTexture?.Dispose();
        NorthGlass?.Dispose();
        WestGlass?.Dispose();
        PixelTexture?.Dispose();
    }

    /// <summary>A face's glass: white at the top fading to grey at the bottom, transparent outside the glass. Tint it.</summary>
    public Texture2D Glass(MirrorSide side)
        => side == MirrorSide.North ? NorthGlass ??= BuildGlass(MirrorSide.North) : WestGlass ??= BuildGlass(MirrorSide.West);

    /// <summary>Forgets last frame's cells and marks the faces stale. Call once a frame before reserving cells.</summary>
    public void BeginFrame()
    {
        CellByKey.Clear();
        FaceByWall.Clear();
        NextCell = 0;
        NextFace = 0;
        FacesReady = false;
    }

    public static Rectangle CellRect(int index)
        => new(
            index % ATLAS_COLUMNS * CELL_SIZE,
            index / ATLAS_COLUMNS * CELL_SIZE,
            CELL_SIZE,
            CELL_SIZE);

    /// <summary>Finds or reserves the atlas cell for <paramref name="key" />. False when the atlas is full.</summary>
    public bool TryReserveCell(ulong key, out Rectangle cell, out bool isNew)
    {
        isNew = false;

        if (CellByKey.TryGetValue(key, out var index))
        {
            cell = CellRect(index);

            return true;
        }

        if (NextCell >= ATLAS_COLUMNS * ATLAS_ROWS)
        {
            cell = default;

            return false;
        }

        index = NextCell++;
        CellByKey[key] = index;
        cell = CellRect(index);
        isNew = true;

        return true;
    }

    public bool TryGetCell(ulong key, out Rectangle cell)
    {
        if (CellByKey.TryGetValue(key, out var index))
        {
            cell = CellRect(index);

            return true;
        }

        cell = default;

        return false;
    }

    /// <summary>Clears the atlas and runs <paramref name="paint" /> with it bound, then restores the previous target.</summary>
    public void RenderAtlas(Action paint)
    {
        Atlas = EnsureTarget(Atlas, ATLAS_COLUMNS * CELL_SIZE, ATLAS_ROWS * CELL_SIZE);
        var previous = CurrentTarget();
        Device.SetRenderTarget(Atlas);
        Device.ScissorRectangle = new Rectangle(0, 0, Atlas.Width, Atlas.Height);
        Device.Clear(Color.Transparent);

        try
        {
            paint();
        } finally
        {
            Device.SetRenderTarget(previous);
        }
    }

    /// <summary>
    ///     Inside <see cref="RenderAtlas" />: runs <paramref name="draw" /> with a batch whose transform puts the screen
    ///     point <paramref name="feetScreen" /> on the cell's anchor. Anything outside the cell is cut off.
    /// </summary>
    public void PaintCell(Rectangle cell, Vector2 feetScreen, Action<SpriteBatch> draw)
    {
        Batch ??= new SpriteBatch(Device);
        Device.ScissorRectangle = cell;

        var transform = Matrix.CreateTranslation(cell.X + CELL_ANCHOR_X - feetScreen.X, cell.Y + CELL_ANCHOR_Y - feetScreen.Y, 0);

        Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, CellScissor, null, transform);

        try
        {
            draw(Batch);
        } finally
        {
            Batch.End();
        }
    }

    public static Rectangle FaceRect(int index)
        => new(
            index % FACE_COLUMNS * MirrorGeometry.FACE_WIDTH,
            index / FACE_COLUMNS * MirrorGeometry.CANVAS_HEIGHT,
            MirrorGeometry.FACE_WIDTH,
            MirrorGeometry.CANVAS_HEIGHT);

    /// <summary>Finds or reserves the face-atlas cell for the wall tile's face. False when the atlas is full.</summary>
    public bool TryReserveFace(int x, int y, MirrorSide side, out Rectangle cell, out bool isNew)
    {
        isNew = false;

        if (FaceByWall.TryGetValue((x, y, side), out var index))
        {
            cell = FaceRect(index);

            return true;
        }

        if (NextFace >= FACE_COLUMNS * FACE_ROWS)
        {
            cell = default;

            return false;
        }

        index = NextFace++;
        FaceByWall[(x, y, side)] = index;
        cell = FaceRect(index);
        isNew = true;

        return true;
    }

    public bool TryGetFace(int x, int y, MirrorSide side, out Rectangle cell)
    {
        if (FaceByWall.TryGetValue((x, y, side), out var index))
        {
            cell = FaceRect(index);

            return true;
        }

        cell = default;

        return false;
    }

    /// <summary>
    ///     Clears the face atlas and runs <paramref name="compose" />. Each face is drawn into its own cell, so a
    ///     reflection cannot land on a neighbouring face.
    /// </summary>
    public void RenderFaces(Action compose)
    {
        FaceAtlas = EnsureTarget(
            FaceAtlas,
            FACE_COLUMNS * MirrorGeometry.FACE_WIDTH,
            FACE_ROWS * MirrorGeometry.CANVAS_HEIGHT);

        var previous = CurrentTarget();
        Device.SetRenderTarget(FaceAtlas);
        Device.ScissorRectangle = new Rectangle(0, 0, FaceAtlas.Width, FaceAtlas.Height);
        Device.Clear(Color.Transparent);

        try
        {
            compose();
        } finally
        {
            Device.SetRenderTarget(previous);
        }

        FacesReady = true;
    }

    /// <summary>
    ///     Draws one face into <paramref name="cell" />: its glass, then <paramref name="drawScreenSpace" /> (reflections
    ///     and the glint, in screen pixels) clipped to that glass.
    /// </summary>
    public void ComposeFace(
        Rectangle cell,
        MirrorSide side,
        Vector2 screenOrigin,
        Color glassColour,
        Action<SpriteBatch> drawScreenSpace)
    {
        Batch ??= new SpriteBatch(Device);
        Device.ScissorRectangle = cell;

        Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, CellScissor);

        try
        {
            Batch.Draw(Glass(side), new Vector2(cell.X, cell.Y), glassColour);
        } finally
        {
            Batch.End();
        }

        Device.ScissorRectangle = cell;

        var transform = Matrix.CreateTranslation(cell.X - screenOrigin.X, cell.Y - screenOrigin.Y, 0);

        Batch.Begin(SpriteSortMode.Deferred, SourceAtop, SamplerState.PointClamp, null, CellScissor, null, transform);

        try
        {
            drawScreenSpace(Batch);
        } finally
        {
            Batch.End();
        }
    }

    private RenderTarget2D? CurrentTarget()
    {
        var bindings = Device.GetRenderTargets();

        return bindings.Length > 0 ? bindings[0].RenderTarget as RenderTarget2D : null;
    }

    private RenderTarget2D EnsureTarget(RenderTarget2D? target, int width, int height)
    {
        if (target is not null && (target.Width == width) && (target.Height == height))
            return target;

        target?.Dispose();

        return new RenderTarget2D(Device, width, height);
    }

    private Texture2D BuildDiamond()
    {
        const int W = 56;
        const int H = 27;
        var data = new Color[W * H];

        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
                if ((Math.Abs(x + 0.5f - W / 2f) / (W / 2f)) + (Math.Abs(y + 0.5f - H / 2f) / (H / 2f)) <= 1f)
                    data[y * W + x] = Color.White;

        var texture = new Texture2D(Device, W, H);
        texture.SetData(data);

        return texture;
    }

    private Texture2D BuildGlass(MirrorSide side)
    {
        const int W = MirrorGeometry.FACE_WIDTH;
        const int H = MirrorGeometry.CANVAS_HEIGHT;
        var data = new Color[W * H];

        for (var row = 0; row < H; row++)
        {
            var localRow = row + MirrorGeometry.CANVAS_TOP;

            for (var column = 0; column < W; column++)
                if (MirrorGeometry.IsGlassPixel(side, column, localRow))
                {
                    var v = Math.Min(1f, 0.55f + 0.6f * MirrorGeometry.GlassHeightFraction(side, column, localRow));
                    data[row * W + column] = new Color(v, v, v, 1f);
                }
        }

        var texture = new Texture2D(Device, W, H);
        texture.SetData(data);

        return texture;
    }

    private Texture2D BuildGlow()
    {
        const int W = 64;
        const int H = 32;
        var data = new Color[W * H];

        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
            {
                var dx = (x + 0.5f - W / 2f) / (W / 2f);
                var dy = (y + 0.5f - H / 2f) / (H / 2f);
                var a = Math.Clamp(1f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                a *= a;
                data[y * W + x] = new Color(a, a, a, a);
            }

        var texture = new Texture2D(Device, W, H);
        texture.SetData(data);

        return texture;
    }

    private Texture2D BuildPixel()
    {
        var texture = new Texture2D(Device, 1, 1);
        texture.SetData(new[] { Color.White });

        return texture;
    }
}
