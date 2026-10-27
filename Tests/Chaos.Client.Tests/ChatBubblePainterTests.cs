using Chaos.Client.Rendering.Utility;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Tests;

public sealed class ChatBubblePainterTests
{
    private const int WIDTH = 130;

    private static Color[] PaintNew(ChatBubbleLook look, int bodyHeight, Color[]? frame = null)
    {
        var (w, h) = ChatBubblePainter.BufferSize(WIDTH, bodyHeight, look.Effect);
        var pixels = new Color[w * h];
        ChatBubblePainter.Paint(pixels, WIDTH, bodyHeight, look, frame);

        return pixels;
    }

    private static Color At(Color[] pixels, ChatBubbleLook look, int x, int y)
    {
        var (w, _) = ChatBubblePainter.BufferSize(WIDTH, 0, look.Effect);

        return pixels[(y * w) + x];
    }

    //formatter:off
    [Test]
    [Arguments(17)]
    [Arguments(30)]
    [Arguments(78)]
    //formatter:on
    public void Default_matches_the_legacy_bubble(int bodyHeight)
    {
        var legacy = new Color[WIDTH * (bodyHeight + ChatBubblePainter.TAIL_HEIGHT)];
        ImageUtil.DrawBubbleBody(legacy, WIDTH, 0, 0, WIDTH, bodyHeight, Color.White, new Color(0, 0, 0, 85));
        ImageUtil.DrawBubbleTail(legacy, WIDTH, WIDTH / 2, bodyHeight - 1, Color.White, new Color(0, 0, 0, 85));

        PaintNew(ChatBubbleLook.Default, bodyHeight).Should().Equal(legacy);
    }

    [Test]
    public void Gradient_border_runs_border_to_accent()
    {
        var look = new ChatBubbleLook { Effect = ChatBubbleEffect.GradientBorder, Border = 0xFF0000FF, Fill = 0x000000B4, Text = 0xFFFFFFFF, Accent = 0x0000FFFF };
        var pixels = PaintNew(look, 30);

        At(pixels, look, WIDTH / 2, 0).Should().Be(ChatBubblePainter.ToColor(look.Border));
        At(pixels, look, WIDTH / 2, 29).Should().Be(ChatBubblePainter.ToColor(look.Accent));
    }

    [Test]
    public void Two_tone_border_draws_accent_inside()
    {
        var look = new ChatBubbleLook { Effect = ChatBubbleEffect.TwoToneBorder, Border = 0xFFFFFFFF, Fill = 0x000000B4, Text = 0xFFFFFFFF, Accent = 0x00FF00FF };

        At(PaintNew(look, 30), look, WIDTH / 2, 1).Should().Be(ChatBubblePainter.ToColor(look.Accent));
    }

    [Test]
    public void Glow_adds_a_margin_and_a_half_strength_ring()
    {
        var look = new ChatBubbleLook { Effect = ChatBubbleEffect.Glow, Border = 0xFFFFFFFF, Fill = 0x000000B4, Text = 0xFFFFFFFF, Accent = 0x00FF00FF };
        var (w, h) = ChatBubblePainter.BufferSize(WIDTH, 30, look.Effect);

        (w, h).Should().Be((WIDTH + 4, 30 + ChatBubblePainter.TAIL_HEIGHT + 4));
        At(PaintNew(look, 30), look, ChatBubblePainter.GLOW_MARGIN - 1, ChatBubblePainter.GLOW_MARGIN + 15)
            .Should()
            .Be(ChatBubblePainter.ToColor(look.Accent) * 0.5f);
    }

    private static readonly Color[] CellColors =
    [
        new(10, 0, 0, 255), new(20, 0, 0, 255), new(30, 0, 0, 255),
        new(40, 0, 0, 255), Color.Transparent, new(60, 0, 0, 255),
        new(70, 0, 0, 255), new(80, 0, 0, 255), new(90, 0, 0, 255)
    ];

    private static Color[] TestFrame()
    {
        var size = ChatBubblePainter.FRAME_SIZE;
        var cell = ChatBubblePainter.FRAME_CELL;
        var frame = new Color[size * size];

        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                frame[(y * size) + x] = CellColors[((y / cell) * 3) + (x / cell)];

        return frame;
    }

    //formatter:off
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    //formatter:on
    public void Frame_places_corners_and_tiles_edges(int lines)
    {
        var look = ChatBubbleLook.Default with { Frame = 1, Fill = 0x000000B4 };
        var bodyHeight = (2 * ChatBubblePainter.FRAME_CELL) + (lines * 12);
        var pixels = PaintNew(look, bodyHeight, TestFrame());

        At(pixels, look, 0, 0).Should().Be(CellColors[0]);
        At(pixels, look, WIDTH - 1, 0).Should().Be(CellColors[2]);
        At(pixels, look, 0, bodyHeight - 1).Should().Be(CellColors[6]);
        At(pixels, look, WIDTH - 1, bodyHeight - 1).Should().Be(CellColors[8]);
        At(pixels, look, 0, bodyHeight / 2).Should().Be(CellColors[3]);
        At(pixels, look, WIDTH - 1, bodyHeight / 2).Should().Be(CellColors[5]);
        At(pixels, look, WIDTH / 2, 0).Should().Be(CellColors[1]);
        At(pixels, look, WIDTH / 2, bodyHeight - 1).Should().Be(CellColors[7]);
    }

    //formatter:off
    [Test]
    [Arguments((byte)1)]
    [Arguments((byte)2)]
    [Arguments((byte)3)]
    [Arguments((byte)4)]
    //formatter:on
    public void Embedded_frames_load_with_a_clear_centre(byte frame)
    {
        var pixels = ChatBubbleFrames.Get(frame);

        pixels.Should().HaveCount(21 * 21);

        for (var y = 7; y < 14; y++)
            for (var x = 7; x < 14; x++)
                pixels![(y * 21) + x].A.Should().Be(0);
    }

    [Test]
    public void Decode_reads_a_21px_png_and_refuses_other_sizes()
    {
        ChatBubbleFrames.Decode(Png(21)).Should().HaveCount(21 * 21);
        ChatBubbleFrames.Decode(Png(20)).Should().BeNull();
        ChatBubbleFrames.Get(0).Should().BeNull();
        ChatBubbleFrames.Get(99).Should().BeNull();
    }

    private static MemoryStream Png(int size)
    {
        using var bitmap = new SKBitmap(size, size);
        bitmap.Erase(new SKColor(200, 100, 50, 255));
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        return new MemoryStream(data.ToArray());
    }
}
