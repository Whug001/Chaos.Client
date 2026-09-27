using Chaos.Client.Controls.World.Emblems;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class EmblemTexturesTests
{
    [Test]
    public async Task File_names_are_three_digit()
    {
        EmblemTextures.FileName(6).Should().Be("embl006.spf");
        EmblemTextures.FileName(183).Should().Be("embl183.spf");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Frames_advance_every_120_ms_and_wrap()
    {
        EmblemTextures.FrameAt(1, 5_000).Should().Be(0);
        EmblemTextures.FrameAt(7, 0).Should().Be(0);
        EmblemTextures.FrameAt(7, 119).Should().Be(0);
        EmblemTextures.FrameAt(7, 120).Should().Be(1);
        EmblemTextures.FrameAt(7, 7 * 120).Should().Be(0);
        await Task.CompletedTask;
    }
}
