using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class TowerLeaderboardStateTests
{
    private static TowerLeaderboardArgs Board(int entries, bool current = true, int daysLeft = 47)
        => new()
        {
            Season = 2,
            IsCurrent = current,
            DaysLeft = (ushort)daysLeft,
            OlderSeason = 1,
            Entries = Enumerable.Range(1, entries)
                                .Select(rank => new TowerLeaderboardEntryInfo { Rank = (byte)rank, Floor = (ushort)(50 - rank) })
                                .ToList()
        };

    [Test]
    public async Task Pages_hold_three_groups()
    {
        var state = new TowerLeaderboardState();
        state.Open(Board(10));

        state.Pages.Should().Be(4);
        state.PageItems.Should().HaveCount(3);
        state.TurnPage(5);
        state.Page.Should().Be(3);
        state.PageItems.Should().ContainSingle();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Open_goes_back_to_the_first_page()
    {
        var state = new TowerLeaderboardState();
        state.Open(Board(10));
        state.TurnPage(2);

        state.Open(Board(10));

        state.Page.Should().Be(0);
        await Task.CompletedTask;
    }

    [Test]
    public async Task An_empty_board_has_one_page_and_says_so()
    {
        var state = new TowerLeaderboardState();
        state.Open(Board(0));

        state.Pages.Should().Be(1);
        state.Empty.Should().Be("No group has climbed yet this season.");
        state.Open(Board(0, current: false));
        state.Empty.Should().Be("No group climbed this season.");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Title_and_subtitle()
    {
        var state = new TowerLeaderboardState();
        state.Open(Board(1));
        state.Title.Should().Be("Endless Tower: Season 2");
        state.Subtitle.Should().Be("47 days left");

        state.Open(Board(1, daysLeft: 1));
        state.Subtitle.Should().Be("1 day left");

        state.Open(Board(1, current: false));
        state.Subtitle.Should().Be("Final standings");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Time_text()
    {
        TowerLeaderboardState.TimeText(0).Should().Be("0:00");
        TowerLeaderboardState.TimeText(2292).Should().Be("38:12");
        TowerLeaderboardState.TimeText(3725).Should().Be("1:02:05");
        await Task.CompletedTask;
    }
}
