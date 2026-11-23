using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class CraneControlsStateTests
{
    private static CraneControlsState Steering(byte seconds = 15)
    {
        var state = new CraneControlsState();
        state.Apply(new CraneControlsArgs { Active = true, SecondsLeft = seconds });

        return state;
    }

    [Test]
    public void New_IsHidden()
    {
        var state = new CraneControlsState();

        state.Active.Should().BeFalse();
        state.SecondsLeft.Should().Be(0);
    }

    [Test]
    public void Apply_Active_ShowsWithTheSeconds_AndUpdates()
    {
        var state = Steering(15);
        state.Active.Should().BeTrue();
        state.SecondsLeft.Should().Be(15);
        state.SecondsText.Should().Be("15s");

        state.Apply(new CraneControlsArgs { Active = true, SecondsLeft = 9 });
        state.SecondsLeft.Should().Be(9);
        state.SecondsText.Should().Be("9s");
    }

    [Test]
    public void Apply_Inactive_HidesAndForgetsLitKeys()
    {
        var state = Steering();
        state.Press(CraneKey.Space);

        state.Apply(new CraneControlsArgs { Active = false, SecondsLeft = 0 });

        state.Active.Should().BeFalse();
        state.SecondsLeft.Should().Be(0);
        state.IsLit(CraneKey.Space).Should().BeFalse();
    }

    [Test]
    public void Press_LightsOnlyThatKey_ForTheLightTime()
    {
        var state = Steering();

        state.Press(CraneKey.Left);
        state.IsLit(CraneKey.Left).Should().BeTrue();
        state.IsLit(CraneKey.Right).Should().BeFalse();
        state.IsLit(CraneKey.Space).Should().BeFalse();

        state.Advance(CraneControlsState.LIGHT_MS - 1);
        state.IsLit(CraneKey.Left).Should().BeTrue();

        state.Advance(1);
        state.IsLit(CraneKey.Left).Should().BeFalse();
    }

    [Test]
    public void LightTime_IsAboutAHundredAndFiftyMilliseconds()
        => CraneControlsState.LIGHT_MS.Should().Be(150);

    [Test]
    public void Press_Again_RestartsTheLight()
    {
        var state = Steering();

        state.Press(CraneKey.Up);
        state.Advance(100);
        state.Press(CraneKey.Up);
        state.Advance(100);

        state.IsLit(CraneKey.Up).Should().BeTrue("the second press restarted the light");
    }

    [Test]
    public void Press_WhileHidden_LightsNothing()
    {
        var state = new CraneControlsState();

        state.Press(CraneKey.Down);

        state.IsLit(CraneKey.Down).Should().BeFalse();
    }

    [Test]
    public void Clear_HidesEverything()
    {
        var state = Steering();
        state.Press(CraneKey.Up);

        state.Clear();

        state.Active.Should().BeFalse();
        state.SecondsLeft.Should().Be(0);
        state.IsLit(CraneKey.Up).Should().BeFalse();
    }
}
