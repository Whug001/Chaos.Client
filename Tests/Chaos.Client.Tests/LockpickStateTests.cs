using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class LockpickStateTests
{
    [Test]
    public async Task ApplyOpen_stores_the_window_fields()
    {
        var state = new LockpickState();

        state.ApplyOpen(
            new LockpickDisplayArgs
            {
                Type = LockpickDisplayType.Open,
                Difficulty = LockpickDifficulty.Hard,
                Title = "Hard Lock",
                LockpickCount = 4
            });

        state.Difficulty.Should().Be(LockpickDifficulty.Hard);
        state.Title.Should().Be("Hard Lock");
        state.LockpickCount.Should().Be(4);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ApplyTurnResult_updates_the_count_only()
    {
        var state = new LockpickState();
        state.ApplyOpen(new LockpickDisplayArgs { Type = LockpickDisplayType.Open, Title = "Easy Lock", LockpickCount = 3 });

        state.ApplyTurnResult(
            new LockpickDisplayArgs { Type = LockpickDisplayType.TurnResult, Outcome = LockpickTurnOutcome.Broke, LockpickCount = 2 });

        state.LockpickCount.Should().Be(2);
        state.Title.Should().Be("Easy Lock");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Clear_resets_everything()
    {
        var state = new LockpickState();
        state.ApplyOpen(new LockpickDisplayArgs { Type = LockpickDisplayType.Open, Difficulty = LockpickDifficulty.Hard, Title = "Hard Lock", LockpickCount = 3 });

        state.Clear();

        state.Difficulty.Should().Be(LockpickDifficulty.Easy);
        state.Title.Should().BeEmpty();
        state.LockpickCount.Should().Be(0);

        await Task.CompletedTask;
    }
}
