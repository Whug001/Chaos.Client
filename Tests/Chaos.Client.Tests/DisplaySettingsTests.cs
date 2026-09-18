using Chaos.Client.Systems;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class DisplaySettingsTests
{
    private const int VIRTUAL_WIDTH = 640;
    private const int VIRTUAL_HEIGHT = 480;

    [Test]
    public void FitToAspect_LeavesAnAlreadyCorrectBoxAlone()
        => Fit(1280, 960)
            .Should()
            .Be((1280, 960));

    /// <summary>A window dragged wider than 4:3 keeps its height and gives up the extra width.</summary>
    [Test]
    public void FitToAspect_TrimsWidthWhenHeightConstrains()
        => Fit(1600, 900)
            .Should()
            .Be((1200, 900));

    /// <summary>And a window dragged taller keeps its width instead.</summary>
    [Test]
    public void FitToAspect_TrimsHeightWhenWidthConstrains()
        => Fit(800, 900)
            .Should()
            .Be((800, 600));

    /// <summary>
    ///     Correcting an already-corrected box has to be a no-op. A float ratio made it drift a pixel each time --
    ///     800 wide came back 599 tall, which then came back 798 wide -- so a window nudged repeatedly shrank.
    /// </summary>
    [Test]
    [Arguments(1600, 900)]
    [Arguments(800, 900)]
    [Arguments(1366, 768)]
    [Arguments(1024, 768)]
    public void FitToAspect_IsIdempotent(int width, int height)
    {
        var once = Fit(width, height);
        var twice = Fit(once.Width, once.Height);

        twice.Should().Be(once);
    }

    [Test]
    [Arguments(0, 480)]
    [Arguments(640, 0)]
    [Arguments(-1, -1)]
    public void FitToAspect_RefusesANonPositiveBox(int width, int height)
        => Fit(width, height)
            .Should()
            .Be((0, 0));

    [Test]
    public void StoredSize_IsUsedWhenItFits()
    {
        var resolved = Resolve(
            1024,
            768,
            1920,
            1080);

        resolved.Should().Be((true, 1024, 768));
    }

    /// <summary>Nothing stored means the window follows the resolution dropdown, which is the caller's fallback.</summary>
    [Test]
    [Arguments(0, 0)]
    [Arguments(0, 768)]
    [Arguments(1024, 0)]
    public void StoredSize_IsRefusedWhenUnset(int storedWidth, int storedHeight)
    {
        var resolved = Resolve(
            storedWidth,
            storedHeight,
            1920,
            1080);

        resolved.Should().Be((false, 0, 0));
    }

    /// <summary>
    ///     The size is stored per character, so the file can have been written on a bigger monitor than the one it
    ///     is read on. A window taller than the display would put the title bar out of reach.
    /// </summary>
    [Test]
    public void StoredSize_IsBroughtBackOntoASmallerDisplay()
    {
        var resolved = Resolve(
            2560,
            1920,
            1280,
            1024);

        resolved.Should().Be((true, 1280, 960));
    }

    /// <summary>Clamping has to leave a 4:3 box, not just a smaller one.</summary>
    [Test]
    public void StoredSize_StaysFourThreeAfterClamping()
    {
        (_, var width, var height) = Resolve(
            3000,
            2000,
            1366,
            768);

        ((float)width / height).Should().BeApproximately((float)VIRTUAL_WIDTH / VIRTUAL_HEIGHT, 0.01f);
    }

    /// <summary>
    ///     Below one screen pixel per virtual pixel there is nothing to show, so the mode's own size is better than
    ///     honouring it.
    /// </summary>
    [Test]
    public void StoredSize_IsRefusedWhenItWouldBeSmallerThanTheVirtualScreen()
    {
        var resolved = Resolve(
            400,
            300,
            1920,
            1080);

        resolved.Should().Be((false, 0, 0));
    }

    /// <summary>Exactly 1x is the smallest size that is still worth honouring.</summary>
    [Test]
    public void StoredSize_AcceptsExactlyTheVirtualScreen()
    {
        var resolved = Resolve(
            VIRTUAL_WIDTH,
            VIRTUAL_HEIGHT,
            1920,
            1080);

        resolved.Should().Be((true, VIRTUAL_WIDTH, VIRTUAL_HEIGHT));
    }

    private static (int Width, int Height) Fit(int width, int height)
        => DisplaySettings.FitToAspect(
            width,
            height,
            VIRTUAL_WIDTH,
            VIRTUAL_HEIGHT);

    private static (bool Resolved, int Width, int Height) Resolve(
        int storedWidth,
        int storedHeight,
        int displayWidth,
        int displayHeight)
    {
        var resolved = DisplaySettings.TryResolveStoredSize(
            storedWidth,
            storedHeight,
            displayWidth,
            displayHeight,
            VIRTUAL_WIDTH,
            VIRTUAL_HEIGHT,
            out var width,
            out var height);

        return (resolved, width, height);
    }
}
