#region
using Chaos.Client.Networking.Arena;
#endregion

namespace Chaos.Client.ViewModel;

public sealed class ArenaPoll
{
    //the server only broadcasts to players on an arena map (~1/sec). If updates stop arriving
    //(the player left the arena map), hide the panel after this grace window.
    private const double STALE_SECONDS = 2.5;

    public readonly record struct Option(byte MatchType, string Name, int Votes);

    public byte PollId { get; private set; }
    public bool IsOpen { get; private set; }
    public bool IsClosed { get; private set; }
    public int SecondsRemaining { get; private set; }
    public int WinningIndex { get; private set; } = -1;
    public IReadOnlyList<Option> Options { get; private set; } = [];
    public int MyVoteIndex { get; set; } = -1;

    private double SecondsSinceUpdate = double.MaxValue;

    //shown only while the poll is open AND the server is still sending us updates (i.e. we're on
    //an arena map). Closes immediately on a Closed snapshot, or after the staleness window if we
    //leave the arena map and updates stop.
    public bool ShouldShow => IsOpen && (SecondsSinceUpdate < STALE_SECONDS);

    public void Apply(ArenaPollArgs args)
    {
        if (args.PollId != PollId)
        {
            PollId = args.PollId;
            MyVoteIndex = -1; //new poll: clear local selection
        }

        Options = args.Options
                      .Select(o => new Option(o.MatchType, o.Name, o.Votes))
                      .ToList();
        SecondsRemaining = args.SecondsRemaining;

        var nowClosed = args.State == ArenaPollState.Closed;
        IsClosed = nowClosed;
        IsOpen = !nowClosed;
        WinningIndex = nowClosed ? args.WinningIndex : -1;

        SecondsSinceUpdate = 0; //fresh update received
    }

    //called every frame by the panel; tracks how long since the last server update
    public void Tick(double deltaSeconds) => SecondsSinceUpdate += deltaSeconds;
}
