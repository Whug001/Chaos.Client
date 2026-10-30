#region
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

public sealed class Poll
{
    //the server only broadcasts to the poll's audience (~1/sec). If updates stop arriving
    //(the player left the audience set, e.g. the arena map), hide the panel after this grace window.
    private const double STALE_SECONDS = 2.5;

    public readonly record struct Option(byte Index, string Text, int Votes);

    public byte PollId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public bool IsOpen { get; private set; }
    public bool IsClosed { get; private set; }
    public int SecondsRemaining { get; private set; }
    public int WinningIndex { get; private set; } = -1;
    public IReadOnlyList<Option> Options { get; private set; } = [];
    public int MyVoteIndex { get; set; } = -1;

    private double SecondsSinceUpdate = double.MaxValue;

    /// <summary>
    ///     Forgets the poll and the local vote. Call on logout / character switch, so the next
    ///     character never sees the last one's selection or a stale open poll.
    /// </summary>
    public void Clear()
    {
        Title = string.Empty;
        IsOpen = false;
        IsClosed = false;
        SecondsRemaining = 0;
        WinningIndex = -1;
        Options = [];
        MyVoteIndex = -1;
        SecondsSinceUpdate = double.MaxValue;
    }

    //shown only while the poll is open AND the server is still sending us updates (i.e. we're in
    //the audience). Closes immediately on a Closed snapshot, or after the staleness window if we
    //leave the audience and updates stop.
    public bool ShouldShow => IsOpen && (SecondsSinceUpdate < STALE_SECONDS);

    public void Apply(PollArgs args)
    {
        if (args.PollId != PollId)
        {
            PollId = args.PollId;
            MyVoteIndex = -1; //new poll: clear local selection
        }

        Title = args.Title;

        Options = args.Options
                      .Select(o => new Option(o.Index, o.Text, o.Votes))
                      .ToList();
        SecondsRemaining = args.SecondsRemaining;

        var nowClosed = args.State == PollState.Closed;
        IsClosed = nowClosed;
        IsOpen = !nowClosed;
        WinningIndex = nowClosed ? args.WinningIndex : -1;

        SecondsSinceUpdate = 0; //fresh update received
    }

    //called every frame by the panel; tracks how long since the last server update
    public void Tick(double deltaSeconds) => SecondsSinceUpdate += deltaSeconds;
}
