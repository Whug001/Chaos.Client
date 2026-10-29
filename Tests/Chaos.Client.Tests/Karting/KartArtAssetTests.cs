#region
using Chaos.Client.Rendering;
using FluentAssertions;
using SkiaSharp;
#endregion

namespace Chaos.Client.Tests.Karting;

public sealed class KartArtAssetTests
{
    //formatter:off
    [Test]
    [Arguments("kart_base")]
    [Arguments("kart_driver")]
    [Arguments("kart_driver_body")]
    [Arguments("kart_base_bare")]
    [Arguments("kart_treads")]
    [Arguments("icon_ember_bolt")]
    [Arguments("icon_hunting_raven")]
    [Arguments("icon_thorn_snare")]
    [Arguments("icon_gale_draught")]
    [Arguments("icon_wyrmfire_mantle")]
    [Arguments("icon_gloaming")]
    [Arguments("icon_rune_ward")]
    [Arguments("cache")]
    [Arguments("wisp")]
    [Arguments("bolt")]
    [Arguments("snare")]
    [Arguments("ward")]
    [Arguments("raven")]
    [Arguments("shadow")]
    [Arguments("spark_blue")]
    [Arguments("spark_gold")]
    [Arguments("spark_violet")]
    [Arguments("boost_flame")]
    [Arguments("mantle")]
    [Arguments("fire_warning")]
    [Arguments("fire_active")]
    [Arguments("hud_slot")]
    [Arguments("medal_gold")]
    [Arguments("medal_silver")]
    [Arguments("medal_bronze")]
    //formatter:on
    public void Required_embedded_sheets_have_valid_frames_and_transparent_pixels(string key)
    {
        var manifest = KartArtAssets.ReadManifest();
        manifest.Should().ContainKey(key);
        var sheet = manifest[key];
        using var stream = KartArtAssets.OpenAsset(key);
        using var bitmap = SKBitmap.Decode(stream);
        bitmap.Should().NotBeNull();
        bitmap.Width.Should().Be(sheet.FrameWidth * sheet.Columns);
        bitmap.Height.Should().Be(sheet.FrameHeight * ((sheet.Frames + sheet.Columns - 1) / sheet.Columns));

        for (var frame = 0; frame < sheet.Frames; frame++)
        {
            var rect = sheet.Frame(frame);
            rect.Left.Should().BeGreaterThanOrEqualTo(0);
            rect.Top.Should().BeGreaterThanOrEqualTo(0);
            rect.Right.Should().BeLessThanOrEqualTo(bitmap.Width);
            rect.Bottom.Should().BeLessThanOrEqualTo(bitmap.Height);
            var visible = 0;
            var clear = 0;

            for (var y = rect.Top; y < rect.Bottom; y++)
                for (var x = rect.Left; x < rect.Right; x++)
                {
                    if (bitmap.GetPixel(x, y).Alpha > 0)
                        visible++;
                    else
                        clear++;
                }

            visible.Should().BeGreaterThan(0);
            clear.Should().BeGreaterThan(0);
        }
    }

    [Test]
    public void Kart_layers_have_sixteen_aligned_frames_and_a_neutral_driver()
    {
        foreach (var key in new[] { "kart_base", "kart_base_bare", "kart_driver", "kart_driver_body" })
        {
            var sheet = KartArtAssets.ReadManifest()[key];
            sheet.FrameWidth.Should().Be(80);
            sheet.FrameHeight.Should().Be(60);
            sheet.Frames.Should().Be(16);
            sheet.Columns.Should().Be(4);
            sheet.AnchorX.Should().Be(40);
            sheet.AnchorY.Should().Be(45);
        }

        using var stream = KartArtAssets.OpenAsset("kart_driver");
        using var driver = SKBitmap.Decode(stream);

        foreach (var pixel in driver.Pixels.Where(p => p.Alpha > 0))
        {
            pixel.Red.Should().Be(pixel.Green);
            pixel.Blue.Should().Be(pixel.Green);
        }
    }

    [Test]
    public void Treads_have_three_turns_of_every_kart_frame()
    {
        var treads = KartArtAssets.ReadManifest()["kart_treads"];
        var kart = KartArtAssets.ReadManifest()["kart_base"];

        treads.Frames.Should().Be(3 * kart.Frames);
        (treads.FrameWidth, treads.FrameHeight, treads.AnchorX, treads.AnchorY).Should().Be((kart.FrameWidth, kart.FrameHeight, kart.AnchorX, kart.AnchorY));
    }

    [Test]
    public void Item_icons_are_twenty_four_pixel_cells()
    {
        foreach (var (key, sheet) in KartArtAssets.ReadManifest().Where(entry => entry.Key.StartsWith("icon_", StringComparison.Ordinal)))
        {
            sheet.FrameWidth.Should().Be(24, key);
            sheet.FrameHeight.Should().Be(24, key);
            sheet.Frames.Should().Be(1);
        }
    }

    [Test]
    public void Missing_assets_wrong_dimensions_and_invalid_frames_are_refused()
    {
        FluentActions.Invoking(() => KartArtAssets.OpenAsset("missing")).Should().Throw<KeyNotFoundException>();
        var sheet = new KartArtSheet("test.png", 64, 48, 16, 4, 32, 36);
        FluentActions.Invoking(() => KartArtAssets.Validate(sheet, 255, 192)).Should().Throw<InvalidDataException>();
        FluentActions.Invoking(() => sheet.Frame(-1)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => sheet.Frame(16)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => KartArtAssets.Validate(sheet with { Frames = 0 }, 256, 192)).Should().Throw<InvalidDataException>();
    }
}
