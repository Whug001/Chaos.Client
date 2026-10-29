#if DEBUG
using Chaos.Client.Systems.TestHook;
using FluentAssertions;

namespace Chaos.Client.Tests.TestHook;

public class TestCommandParserTests
{
    [Test]
    public void Key_DefaultsToTapForOneFrame()
    {
        TestCommandParser.TryParse("key Up", out var cmd, out _).Should().BeTrue();
        cmd.Should().Be(new KeyCommand("Up", KeyAction.Tap, 1));
    }

    [Test]
    public void Key_ParsesActionAndFrames()
    {
        TestCommandParser.TryParse("key Space down 30", out var cmd, out _).Should().BeTrue();
        cmd.Should().Be(new KeyCommand("Space", KeyAction.Down, 30));
    }

    [Test]
    public void Click_ParsesRightButton()
    {
        TestCommandParser.TryParse("click 10 20 right", out var cmd, out _).Should().BeTrue();
        cmd.Should().Be(new ClickCommand(10, 20, true));
    }

    [Test]
    [Arguments("click 640 10")]
    [Arguments("click -1 10")]
    [Arguments("move 10 480")]
    [Arguments("drag 0 0 700 0")]
    public void Coordinates_OutsideGameSpace_AreRejected(string line)
    {
        TestCommandParser.TryParse(line, out var cmd, out var error).Should().BeFalse();
        cmd.Should().BeNull();
        error.Should().Contain("outside 640x480");
    }

    [Test]
    public void Drag_ParsesFourCoordinates()
    {
        TestCommandParser.TryParse("drag 1 2 3 4", out var cmd, out _).Should().BeTrue();
        cmd.Should().Be(new DragCommand(1, 2, 3, 4));
    }

    [Test]
    public void Type_KeepsSpacesInText()
    {
        TestCommandParser.TryParse("type hello there", out var cmd, out _).Should().BeTrue();
        cmd.Should().Be(new TypeCommand("hello there"));
    }

    [Test]
    public void Screenshot_RequiresAbsolutePath()
    {
        TestCommandParser.TryParse("screenshot shot.png", out _, out var error).Should().BeFalse();
        error.Should().Contain("absolute");
        TestCommandParser.TryParse(@"screenshot C:\run\shot.png", out var cmd, out _).Should().BeTrue();
        cmd.Should().Be(new ScreenshotCommand(@"C:\run\shot.png"));
    }

    [Test]
    [Arguments("panels")]
    [Arguments("state")]
    [Arguments("state kart.")]
    [Arguments("state kart.track")]
    [Arguments("ping")]
    [Arguments("  PING  ")]
    public void NoArgCommands_Parse(string line)
        => TestCommandParser.TryParse(line, out _, out _).Should().BeTrue();

    [Test]
    public void WaitFrames_RequiresPositiveCount()
    {
        TestCommandParser.TryParse("wait-frames 0", out _, out _).Should().BeFalse();
        TestCommandParser.TryParse("wait-frames 5", out var cmd, out _).Should().BeTrue();
        cmd.Should().Be(new WaitFramesCommand(5));
    }

    [Test]
    [Arguments("")]
    [Arguments("fly 1 2")]
    [Arguments("click 1")]
    [Arguments("key")]
    [Arguments("key Up sideways")]
    [Arguments("state a b")]
    public void BadLines_ReturnError(string line)
    {
        TestCommandParser.TryParse(line, out var cmd, out var error).Should().BeFalse();
        cmd.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void State_WithoutFilter_HasNullFilter()
    {
        TestCommandParser.TryParse("state", out var cmd, out _).Should().BeTrue();
        cmd.Should().Be(new StateCommand(null));
    }

    [Test]
    public void State_WithFilter_KeepsPrefix()
    {
        TestCommandParser.TryParse("state kart.", out var cmd, out _).Should().BeTrue();
        cmd.Should().Be(new StateCommand("kart."));
        TestCommandParser.TryParse("state kart.track", out cmd, out _).Should().BeTrue();
        cmd.Should().Be(new StateCommand("kart.track"));
    }
}
#endif
