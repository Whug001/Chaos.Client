using Chaos.CannonFair;
using Chaos.Client.Data;
using DALib.Drawing;
using SkiaSharp;

namespace Chaos.Client.Controls.World.Popups.CannonFair;

public static class CannonFairArtBaker
{
    public const float TEXELS_PER_METRE = 16f;
    public const int GROUND_HEIGHT = 200;
    public const int WORLD_WIDTH = 640;
    public const int WORLD_HEIGHT = 480;

    private const int TILE_STEP_X = 56;
    private const int TILE_STEP_Y = 14;
    private const int FIELD_COLUMNS = 8;
    private const int FIELD_ROWS = 16;
    private const string WOOD_FILE = "woodbk.epf";
    private const float WOOD_BRIGHTNESS = 2.1f;

    private static readonly int[] GrassTiles = [61, 33, 90, 95, 87, 89];
    private static readonly int[] StoneTiles = [2318, 2341, 2319];
    private static readonly SKColor BevelLight = new(255, 240, 210, 80);
    private static readonly SKColor BevelDark = new(0, 0, 0, 120);

    public static SKImage? WoodSource()
    {
        using var wood = DataContext.UserControls.GetLegendEpfImage(WOOD_FILE);

        if (wood is null)
            return null;

        using var surface = Surface(wood.Width, wood.Height);
        using var paint = new SKPaint
        {
            ColorFilter = SKColorFilter.CreateColorMatrix(
                [WOOD_BRIGHTNESS, 0, 0, 0, 0, 0, WOOD_BRIGHTNESS, 0, 0, 0, 0, 0, WOOD_BRIGHTNESS, 0, 0, 0, 0, 0, 1, 0])
        };
        surface.Canvas.DrawImage(wood, 0, 0, paint);

        return surface.Snapshot();
    }

    public static SKImage StoneSource() => BakeField(StoneTiles, FIELD_COLUMNS * TILE_STEP_X, FIELD_ROWS * TILE_STEP_Y);

    public static SKImage Ground()
    {
        using var field = BakeField(GrassTiles, WORLD_WIDTH, GROUND_HEIGHT);
        using var surface = Surface(WORLD_WIDTH, GROUND_HEIGHT);
        var canvas = surface.Canvas;
        canvas.DrawImage(field, 0, 0);
        using var edge = new SKPaint { Color = new SKColor(26, 34, 12, 150) };
        canvas.DrawRect(0, 0, WORLD_WIDTH, 2, edge);

        return surface.Snapshot();
    }

    public static SKImage Sky()
    {
        using var surface = Surface(WORLD_WIDTH, WORLD_HEIGHT);
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, 400),
                [new SKColor(70, 96, 140), new SKColor(132, 150, 168), new SKColor(214, 196, 158)],
                [0f, 0.65f, 1f],
                SKShaderTileMode.Clamp)
        };
        surface.Canvas.DrawRect(0, 0, WORLD_WIDTH, WORLD_HEIGHT, paint);

        return surface.Snapshot();
    }

    public static SKImage Block(SKImage source, float widthMetres, float heightMetres, BlockMaterial material)
    {
        var width = Texels(widthMetres);
        var height = Texels(heightMetres);
        using var surface = Surface(width, height);
        var canvas = surface.Canvas;
        var turned = (material == BlockMaterial.Wood) && (height > width);

        if (turned)
        {
            canvas.Translate(width, 0);
            canvas.RotateDegrees(90);
        }

        using (var shader = source.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat))
        using (var paint = new SKPaint { Shader = shader })
            canvas.DrawRect(0, 0, turned ? height : width, turned ? width : height, paint);

        canvas.ResetMatrix();
        using var light = new SKPaint { Color = BevelLight };
        using var dark = new SKPaint { Color = BevelDark };
        canvas.DrawRect(0, 0, width, 1, light);
        canvas.DrawRect(0, 0, 1, height, light);
        canvas.DrawRect(0, height - 2, width, 2, dark);
        canvas.DrawRect(width - 1, 0, 1, height, dark);

        return surface.Snapshot();
    }

    public static SKImage Barrel(bool brass)
    {
        const int width = 22;
        const int height = 8;
        using var surface = Surface(width, height);
        var canvas = surface.Canvas;

        SKColor[] ramp = brass
            ? [new SKColor(236, 204, 120), new SKColor(176, 132, 58), new SKColor(84, 56, 22)]
            : [new SKColor(128, 128, 140), new SKColor(70, 70, 82), new SKColor(26, 26, 34)];

        using var body = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, height), ramp, [0f, 0.45f, 1f], SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 1, width, height - 2, body);
        canvas.DrawRect(width - 3, 0, 3, height, body);
        canvas.DrawRect(0, 0, 3, height, body);

        return surface.Snapshot();
    }

    public static SKImage Carriage(SKImage wood)
    {
        const int width = 18;
        const int height = 11;
        using var surface = Surface(width, height);
        var canvas = surface.Canvas;

        using (var shader = wood.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat))
        using (var plank = new SKPaint { Shader = shader })
            canvas.DrawRect(1, 1, width - 2, 5, plank);

        using var shade = new SKPaint { Color = BevelDark };
        canvas.DrawRect(1, 5, width - 2, 1, shade);
        DrawWheel(canvas, 5, height - 4);
        DrawWheel(canvas, width - 5, height - 4);

        return surface.Snapshot();
    }

    public static SKImage Blast()
    {
        const int size = 64;
        using var surface = Surface(size, size);
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(size / 2f, size / 2f),
                size / 2f,
                [new SKColor(255, 236, 170, 230), new SKColor(232, 120, 40, 170), new SKColor(120, 40, 16, 0)],
                [0f, 0.55f, 1f],
                SKShaderTileMode.Clamp)
        };
        surface.Canvas.DrawCircle(size / 2f, size / 2f, size / 2f, paint);

        return surface.Snapshot();
    }

    private static void DrawWheel(SKCanvas canvas, float x, float y)
    {
        using var rim = new SKPaint { Color = new SKColor(40, 26, 16), IsAntialias = false };
        using var hub = new SKPaint { Color = new SKColor(140, 100, 60), IsAntialias = false };
        canvas.DrawCircle(x, y, 4, rim);
        canvas.DrawCircle(x, y, 1.5f, hub);
    }

    private static SKImage BakeField(int[] tileIds, int width, int height)
    {
        using var surface = Surface(width, height);
        var canvas = surface.Canvas;
        var images = tileIds.Select(RenderTile).ToArray();

        for (var row = -2; row * TILE_STEP_Y < height; row++)
            for (var column = -1; ((column * TILE_STEP_X) - TILE_STEP_X) < width; column++)
            {
                var image = images[Math.Abs((row * 31) + (column * 17)) % images.Length];

                if (image is not null)
                    canvas.DrawImage(image, (column * TILE_STEP_X) + ((row & 1) * (TILE_STEP_X / 2)), row * TILE_STEP_Y);
            }

        foreach (var image in images)
            image?.Dispose();

        return surface.Snapshot();
    }

    private static SKImage? RenderTile(int tileId)
    {
        var tile = DataContext.Tiles.GetBackgroundTile(tileId);

        return tile is null ? null : Graphics.RenderTile(tile.Entity, tile.Palette);
    }

    private static SKSurface Surface(int width, int height)
        => SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));

    private static int Texels(float metres) => Math.Max(1, (int)MathF.Round(metres * TEXELS_PER_METRE));
}
