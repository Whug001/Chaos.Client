#region
using Chaos.DarkAges.Karting;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     The short race updates under the minimap: hits, laps, finishes and new podium places. The newest line is first;
///     each lasts <see cref="LINE_SECONDS" /> and fades over its last <see cref="FADE_SECONDS" />.
/// </summary>
public sealed class KartRaceFeed
{
    public const int MAX_LINES = 3;
    public const double LINE_SECONDS = 5;
    public const double FADE_SECONDS = 1;
    private const byte NO_ATTACKER = 255;

    private readonly List<(string Text, double Until)> Entries = [];
    private readonly Dictionary<byte, byte> Places = [];

    public IReadOnlyList<(string Text, float Alpha)> Lines(double now)
    {
        Entries.RemoveAll(entry => entry.Until <= now);

        return Entries.Select(entry => (entry.Text, (float)Math.Clamp((entry.Until - now) / FADE_SECONDS, 0, 1))).ToList();
    }

    public void Add(string text, double now)
    {
        Entries.Insert(0, (text, now + LINE_SECONDS));

        if (Entries.Count > MAX_LINES)
            Entries.RemoveRange(MAX_LINES, Entries.Count - MAX_LINES);
    }

    /// <summary>Adds a line for each racer who moved up into the top three since the last snapshot.</summary>
    public void OnPlaces(IEnumerable<KartRacerSnapshot> racers, Func<byte, string> nameOf, byte? localIndex, double now)
    {
        foreach (var racer in racers)
        {
            var had = Places.GetValueOrDefault(racer.Index);
            Places[racer.Index] = racer.Place;

            if ((had == 0) || (racer.Place == 0) || (racer.Place >= had) || (racer.Place > 3) || (racer.Status == KartRacerStatus.Finished))
                continue;

            var who = Who(racer.Index, nameOf, localIndex);
            var verb = racer.Index == localIndex ? "moved up to" : "took";
            Add(racer.Place == 1 ? $"{who} took the lead" : $"{who} {verb} {KartTime.Ordinal(racer.Place)}", now);
        }
    }

    public static string? LineFor(KartRaceEventArgs args, Func<byte, string> nameOf, byte? localIndex, int laps)
    {
        var who = Who(args.Index, nameOf, localIndex);

        return args.Kind switch
        {
            KartRaceEventKind.Hit when args.Item == KartItemKind.RuneWard && args.By != NO_ATTACKER
                => $"{Possessive(args.Index, nameOf, localIndex)} ward blocked {Who(args.By, nameOf, localIndex, false)}",
            KartRaceEventKind.Hit when args.By != NO_ATTACKER && args.By != args.Index
                => $"{Who(args.By, nameOf, localIndex)} hit {Who(args.Index, nameOf, localIndex, false)} with {KartItems.Name(args.Item)}",
            KartRaceEventKind.LapDone when args.Lap + 1 == laps => $"{who} started the final lap",
            KartRaceEventKind.LapDone when args.Lap < laps      => $"{who} started lap {args.Lap + 1}",
            KartRaceEventKind.Finished                          => $"{who} finished",
            _                                                   => null
        };
    }

    public void Clear()
    {
        Entries.Clear();
        Places.Clear();
    }

    private static string Who(byte index, Func<byte, string> nameOf, byte? localIndex, bool start = true)
        => index == localIndex ? start ? "You" : "you" : nameOf(index);

    private static string Possessive(byte index, Func<byte, string> nameOf, byte? localIndex)
        => index == localIndex ? "Your" : $"{nameOf(index)}'s";
}
