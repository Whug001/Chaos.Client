#region
using Chaos.DarkAges.Karting;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

public sealed record KartResultLine(string Place, string Name, string Time, string BestLap, int Medal);

public static class KartResultsView
{
    public const int PAGE_SIZE = 12;

    public static IReadOnlyList<KartResultLine> Lines(IReadOnlyList<KartResultEntry> results, IReadOnlyDictionary<byte, KartRacerInfo> racers)
        => results.Select(row => Line(row, racers)).ToList();

    public static int Pages(int count) => Math.Max(1, (count + PAGE_SIZE - 1) / PAGE_SIZE);

    private static KartResultLine Line(KartResultEntry row, IReadOnlyDictionary<byte, KartRacerInfo> racers)
    {
        var name = racers.TryGetValue(row.Index, out var info) ? info.Name : $"Racer {row.Index + 1}";
        var place = row.Finished ? KartTime.Ordinal(row.Place) : "-";
        var time = row.Finished ? KartTime.Format(row.TotalMs) : "DNF";
        var best = row.BestLapMs > 0 ? KartTime.Format(row.BestLapMs) : "-";
        var medal = row.Finished && row.Place is >= 1 and <= 3 ? row.Place : 0;

        return new KartResultLine(place, name, time, best, medal);
    }
}
