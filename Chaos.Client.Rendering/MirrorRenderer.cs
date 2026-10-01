#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Owns the mirror render targets and textures. Each frame, before the world pass, WorldScreen paints every character
///     a mirror needs into the atlas (one cell per character and pose), then composes all visible glass and reflections
///     into the layer, which is the size of the back buffer, like the silhouette target. The world pass pastes each wall
///     tile's slice of the layer and then its frame sprite.
/// </summary>
public sealed class MirrorRenderer : IDisposable
{
    public const int CELL_SIZE = 160;
    public const int CELL_ANCHOR_X = 80;
    public const int CELL_ANCHOR_Y = 136;
    public const int ATLAS_COLUMNS = 12;
    public const int ATLAS_ROWS = 8;

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
    private readonly GraphicsDevice Device;
    private RenderTarget2D? Atlas;
    private SpriteBatch? Batch;
    private Texture2D? DiamondTexture;
    private Texture2D? GlowTexture;
    private RenderTarget2D? Layer;
    private int NextCell;
    private Texture2D? NorthGlass;
    private Texture2D? PixelTexture;
    private Texture2D? WestGlass;

    public MirrorRenderer(GraphicsDevice device) => Device = device;

    public Texture2D? AtlasTexture => Atlas;

    /// <summary>A white 56x27 tile diamond.</summary>
    public Texture2D Diamond => DiamondTexture ??= BuildDiamond();

    /// <summary>A soft white 64x32 ellipse, premultiplied, for additive light.</summary>
    public Texture2D Glow => GlowTexture ??= BuildGlow();

    public Texture2D? LayerTexture => Layer;

    /// <summary>True once this frame's layer has been composed.</summary>
    public bool LayerReady { get; private set; }

    public Texture2D Pixel => PixelTexture ??= BuildPixel();

    public void Dispose()
    {
        Atlas?.Dispose();
        Layer?.Dispose();
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

    /// <summary>Forgets last frame's cells and marks the layer stale. Call once a frame before reserving cells.</summary>
    public void BeginFrame()
    {
        CellByKey.Clear();
        NextCell = 0;
        LayerReady = false;
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

    /// <summary>
    ///     Clears the layer, runs <paramref name="drawGlass" /> with normal blending, then
    ///     <paramref name="drawReflections" /> with <see cref="SourceAtop" />, so reflections and glints only land on glass.
    /// </summary>
    public void RenderLayer(Action<SpriteBatch> drawGlass, Action<SpriteBatch> drawReflections)
    {
        Batch ??= new SpriteBatch(Device);

        Layer = EnsureTarget(Layer, Device.PresentationParameters.BackBufferWidth, Device.PresentationParameters.BackBufferHeight);
        var previous = CurrentTarget();
        Device.SetRenderTarget(Layer);
        Device.ScissorRectangle = new Rectangle(0, 0, Layer.Width, Layer.Height);
        Device.Clear(Color.Transparent);

        try
        {
            Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            try
            {
                drawGlass(Batch);
            } finally
            {
                Batch.End();
            }

            Batch.Begin(SpriteSortMode.Deferred, SourceAtop, SamplerState.PointClamp);

            try
            {
                drawReflections(Batch);
            } finally
            {
                Batch.End();
            }
        } finally
        {
            Device.SetRenderTarget(previous);
        }

        LayerReady = true;
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
