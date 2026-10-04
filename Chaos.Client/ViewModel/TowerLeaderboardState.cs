using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

/// <summary>What the tower leaderboard window shows: one season's lineups, three per page, and its title lines.</summary>
public sealed class TowerLeaderboardState
{
    public const int PAGE_SIZE = 3;

    public TowerLeaderboardArgs Args { get; private set; } = new();
    public int Page { get; private set; }

    public int Pages => Math.Max(1, (Args.Entries.Count + PAGE_SIZE - 1) / PAGE_SIZE);

    public IReadOnlyList<TowerLeaderboardEntryInfo> PageItems => Args.Entries.Skip(Page * PAGE_SIZE).Take(PAGE_SIZE).ToList();

    public string Title => $"Endless Tower: Season {Args.Season}";

    public string Subtitle
        => !Args.IsCurrent ? "Final standings"
            : Args.DaysLeft == 1 ? "1 day left"
            : $"{Args.DaysLeft} days left";

    public string Empty => Args.IsCurrent ? "No group has climbed yet this season." : "No group climbed this season.";

    public void Open(TowerLeaderboardArgs args)
    {
        Args = args;
        Page = 0;
    }

    public void TurnPage(int delta) => Page = Math.Clamp(Page + delta, 0, Pages - 1);

    public static string TimeText(uint seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);

        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
    }
}
