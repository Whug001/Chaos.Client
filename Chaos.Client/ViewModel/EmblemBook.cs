#region
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     The player's emblem book from the server's last EmblemBook packet. Fires <see cref="Changed" /> when a new one
///     arrives. Expiring emblems count down from <see cref="ReceivedAtUtc" />.
/// </summary>
public sealed class EmblemBook
{
    public IReadOnlyList<EmblemBookEntry> Entries { get; private set; } = [];
    public DateTime ReceivedAtUtc { get; private set; }
    public string ShownKey { get; private set; } = string.Empty;

    public event ChangedHandler? Changed;

    public void Apply(EmblemBookArgs args, DateTime nowUtc)
    {
        Entries = args.Entries.ToList();
        ShownKey = args.ShownKey;
        ReceivedAtUtc = nowUtc;
        Changed?.Invoke();
    }

    /// <summary>Empties the book (logout). Fires <see cref="Changed" /> so an open Emblem tab drops the old character's emblems.</summary>
    public void Clear()
    {
        Entries = [];
        ShownKey = string.Empty;
        ReceivedAtUtc = default;
        Changed?.Invoke();
    }

    /// <summary>Seconds left on an expiring emblem at <paramref name="nowUtc" />, never below 0. 0 for emblems that never expire.</summary>
    public long SecondsLeftAt(EmblemBookEntry entry, DateTime nowUtc)
        => entry.SecondsLeft == 0 ? 0 : Math.Max(0, entry.SecondsLeft - (long)(nowUtc - ReceivedAtUtc).TotalSeconds);
}
