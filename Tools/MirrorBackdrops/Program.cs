// Cuts the Josephine's Mirror backdrops out of real maps and writes them where the client embeds them.
// Run from the Chaos.Client repo root:
//   dotnet run --project Tools/MirrorBackdrops/MirrorBackdrops.csproj -c Release -- [--sheet <contact-sheet.png>]
// Reads seo.dat / ia.dat from DA_PATH (default C:\Users\Michael\Documents\Unora\Unora Files) and the maps from
// MAPS_PATH (default the Unora repo's Data\Configuration\MapData, the copies the server sends players).
// The picture size and standing point must match MirrorBackdrops in the client (Controls/World/Popups/Beauty).
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using SkiaSharp;

(string Key, int MapId, int Width, int Height, int TileX, int TileY)[] places =
[
    ("mileth", 397, 60, 60, 23, 38),
    ("woodlands", 97, 50, 50, 15, 34),
    ("beach", 336, 20, 20, 4, 4),
    ("frozencave", 443, 20, 30, 5, 15),
    ("crypt", 473, 30, 30, 7, 13)
];

const int PAD = 256;
const int PICTURE_WIDTH = 260;
const int PICTURE_HEIGHT = 250;
const int ANCHOR_X = 130;
const int ANCHOR_Y = 153;
const int THUMB_CROP = 64;
const int THUMB_ANCHOR_X = 32;
const int THUMB_ANCHOR_Y = 48;
const int THUMB_SIZE = 24;
var fill = new SKColor(24, 22, 30);

var sheetPath = args is ["--sheet", var path] ? path : null;

if ((args.Length != 0) && sheetPath is null)
{
    Console.Error.WriteLine("usage: MirrorBackdrops [--sheet <contact-sheet.png>]");
    return 1;
}

var rendering = Path.Combine(Directory.GetCurrentDirectory(), "Chaos.Client.Rendering");

if (!Directory.Exists(rendering))
{
    Console.Error.WriteLine("run this from the Chaos.Client repo root");
    return 1;
}

var outDir = Path.Combine(rendering, "Assets", "MirrorBackdrops");
Directory.CreateDirectory(outDir);

var da = Environment.GetEnvironmentVariable("DA_PATH") ?? @"C:\Users\Michael\Documents\Unora\Unora Files";
var maps = Environment.GetEnvironmentVariable("MAPS_PATH") ?? @"C:\Users\Michael\Documents\GitHub\Unora\Data\Configuration\MapData";

using var seo = DataArchive.FromFile(Path.Combine(da, "seo.dat"));
using var ia = DataArchive.FromFile(Path.Combine(da, "ia.dat"));
var made = new List<(string Key, SKBitmap Picture, SKBitmap Thumb)>();

foreach (var place in places)
{
    var map = MapFile.FromFile(Path.Combine(maps, $"lod{place.MapId}.map"), place.Width, place.Height);
    using var image = Graphics.RenderMap(map, seo, ia, PAD);

    //centre of the standing tile in the rendered map -- the same tile maths Graphics.RenderMap draws with
    var centerX = ((place.Height - 1 + place.TileX - place.TileY) * CONSTANTS.HALF_TILE_WIDTH) + CONSTANTS.HALF_TILE_WIDTH;
    var centerY = PAD + ((place.TileX + place.TileY) * CONSTANTS.HALF_TILE_HEIGHT) + CONSTANTS.HALF_TILE_HEIGHT;

    var picture = Cut(image, centerX - ANCHOR_X, centerY - ANCHOR_Y, PICTURE_WIDTH, PICTURE_HEIGHT);
    using var thumbCrop = Cut(image, centerX - THUMB_ANCHOR_X, centerY - THUMB_ANCHOR_Y, THUMB_CROP, THUMB_CROP);
    var thumb = thumbCrop.Resize(new SKImageInfo(THUMB_SIZE, THUMB_SIZE), new SKSamplingOptions(SKCubicResampler.Mitchell));

    Save(picture, Path.Combine(outDir, $"{place.Key}.png"));
    Save(thumb, Path.Combine(outDir, $"{place.Key}-thumb.png"));
    made.Add((place.Key, picture, thumb));
}

if (sheetPath is not null)
    WriteSheet(sheetPath);

foreach (var (_, picture, thumb) in made)
{
    picture.Dispose();
    thumb.Dispose();
}

return 0;

//a window onto the rendered map; anything outside the map is the pedestal colour
SKBitmap Cut(SKImage image, int left, int top, int width, int height)
{
    var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(fill);
    canvas.DrawImage(image, -left, -top);

    return bitmap;
}

void Save(SKBitmap bitmap, string file)
{
    using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(file, data.ToArray());
    Console.WriteLine($"wrote {file} {bitmap.Width}x{bitmap.Height}");
}

//every backdrop with the part each mirror box shows outlined, a red cross on the standing point, and the
//thumbnail at 4x -- for choosing standing tiles, never shipped
void WriteSheet(string file)
{
    const int MARGIN = 10;
    const int THUMB_ZOOM = 4;
    const int CELL_WIDTH = PICTURE_WIDTH + MARGIN;
    const int THUMB_TOP = MARGIN + PICTURE_HEIGHT + MARGIN;

    //picture pixels each box shows (MirrorBackdrops.SourcePixel at the box corners), right/bottom exclusive; copied by hand,
    //so if the mirror's box sizes change, update these with MirrorBackdropTests.Box_corners_stay_inside_the_picture
    (SKRect Area, SKColor Color)[] boxes =
    [
        (new SKRect(17, 2, 243, 248), SKColors.White),      //preview at 1x, 226 x 246
        (new SKRect(73, 63, 187, 187), SKColors.Gold),       //preview at 2x
        (new SKRect(2, 47, 258, 203), SKColors.DeepSkyBlue)  //Review figures, 256 x 156 at 1x
    ];

    using var sheet = new SKBitmap(MARGIN + (made.Count * CELL_WIDTH), THUMB_TOP + (THUMB_SIZE * THUMB_ZOOM) + MARGIN + 14);
    using var canvas = new SKCanvas(sheet);
    using var outline = new SKPaint { IsStroke = true, StrokeWidth = 1 };
    using var cross = new SKPaint { Color = SKColors.Red, StrokeWidth = 1 };
    using var font = new SKFont(SKTypeface.Default, 12);
    using var label = new SKPaint { Color = SKColors.White, IsAntialias = true };
    canvas.Clear(new SKColor(40, 40, 40));

    for (var i = 0; i < made.Count; i++)
    {
        var left = MARGIN + (i * CELL_WIDTH);
        canvas.DrawBitmap(made[i].Picture, left, MARGIN);

        foreach (var (area, color) in boxes)
        {
            outline.Color = color;
            canvas.DrawRect(SKRect.Create(left + area.Left, MARGIN + area.Top, area.Width, area.Height), outline);
        }

        var crossX = left + ANCHOR_X + 0.5f;
        var crossY = MARGIN + ANCHOR_Y + 0.5f;
        canvas.DrawLine(crossX - 4, crossY, crossX + 4, crossY, cross);
        canvas.DrawLine(crossX, crossY - 4, crossX, crossY + 4, cross);

        canvas.DrawBitmap(made[i].Thumb, SKRect.Create(left, THUMB_TOP, THUMB_SIZE * THUMB_ZOOM, THUMB_SIZE * THUMB_ZOOM));
        canvas.DrawText(made[i].Key, left, THUMB_TOP + (THUMB_SIZE * THUMB_ZOOM) + MARGIN + 10, font, label);
    }

    using var data = sheet.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(file, data.ToArray());
    Console.WriteLine($"wrote {file} {sheet.Width}x{sheet.Height}");
}
