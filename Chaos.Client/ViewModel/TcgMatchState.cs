#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

public sealed record TcgMatchEnd(TcgEndOutcome Outcome, string ReasonText, string StakesText);

/// <summary>The newest match Update plus what the client works out from the stream of Updates.</summary>
public sealed class TcgMatchState
{
    public const int MAX_LOG = 100;

    private readonly List<string> LogLines = [];

    public TcgDisplayArgs? Latest { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public bool ManaPlacedThisTurn { get; private set; }
    public TcgMatchEnd? Ended { get; private set; }
    public string Hint { get; private set; } = string.Empty;
    public IReadOnlyList<string> Log => LogLines;
    public string OpponentName => Latest is null ? string.Empty : Latest.Players[(int)TcgMatchRules.Other(Latest.YourSeat)].Name;

    private bool RedrawDone;

    public bool NeedsRedraw => Latest is { Phase: TcgPhase.Redraw } && !RedrawDone;

    public bool Spectating => Latest?.Spectating ?? false;

    public void Apply(TcgDisplayArgs update, DateTime now, Func<string, string> cardName)
    {
        if ((Latest is null) || !string.Equals(Latest.MatchId, update.MatchId, StringComparison.Ordinal))
            Reset();

        Latest = update;
        ReceivedAt = now;
        Hint = update.Hint ?? string.Empty;

        foreach (var e in update.Events)
        {
            if ((e.Type == TcgEventType.TurnStarted) && (e.Seat == update.YourSeat))
                ManaPlacedThisTurn = false;
            else if ((e.Type == TcgEventType.ManaPlaced) && (e.Seat == update.YourSeat))
                ManaPlacedThisTurn = true;
            else if ((e.Type == TcgEventType.Redrew) && (e.Seat == update.YourSeat))
                RedrawDone = true;

            if (TcgMatchText.LogLine(e, update.YourSeat, update.Players.Select(p => p.Name).ToList(), cardName) is { } line)
                AddLog(line);
        }

        if (update.Rejected == TcgRejectReason.AlreadyPlacedMana)
            ManaPlacedThisTurn = true;

        if (update.Rejected == TcgRejectReason.AlreadyRedrew)
            RedrawDone = true;

        //a refused redraw was never applied: offer Keep again while the phase still waits
        if ((update.Phase == TcgPhase.Redraw)
            && (update.Rejected != TcgRejectReason.None)
            && (update.Rejected != TcgRejectReason.AlreadyRedrew))
            RedrawDone = false;

        if (update.Rejected != TcgRejectReason.None)
            AddLog(TcgMatchText.RejectText(update.Rejected));
    }

    public void MarkRedrawSent() => RedrawDone = true;

    public void End(TcgDisplayArgs close, Func<string, string> cardName)
    {
        var you = Latest?.YourSeat ?? TcgSeat.A;

        var outcome = !close.HasWinner || (close.EndReason == TcgEndReason.Draw)
            ? TcgEndOutcome.Draw
            : close.Winner == you ? TcgEndOutcome.Victory : TcgEndOutcome.Defeat;

        var stakes = Latest is null
            ? string.Empty
            : TcgMatchText.StakesResult(outcome, Latest.Stakes, Latest.StakeGold, Latest.Ante, you, cardName);

        Ended = new TcgMatchEnd(outcome, TcgMatchText.EndReasonText(close.EndReason), stakes);
    }

    public int DecisionSecondsLeft(DateTime now) => Latest is null ? 0 : SecondsLeft(Latest.DecisionMs, now);

    public int TurnSecondsLeft(DateTime now)
    {
        if (Latest is null)
            return 0;

        //the server only runs the turn clock while the active player decides Main
        return Latest.Decision == TcgDecision.Main
            ? SecondsLeft(Latest.TurnMs, now)
            : (int)Math.Ceiling(Latest.TurnMs / 1000.0);
    }

    /// <summary>The active player's reserve: it only drains once the turn time is used up.</summary>
    public int ReserveSecondsLeft(DateTime now)
    {
        if (Latest is null)
            return 0;

        var reserve = Latest.Players[(int)Latest.ActiveSeat].ReserveMs;

        if (Latest.Decision != TcgDecision.Main)
            return (int)Math.Ceiling(reserve / 1000.0);

        var overrun = Math.Max(0, (now - ReceivedAt).TotalMilliseconds - Latest.TurnMs);

        return (int)Math.Ceiling(Math.Max(0, reserve - overrun) / 1000.0);
    }

    public void Reset()
    {
        Latest = null;
        Ended = null;
        Hint = string.Empty;
        ManaPlacedThisTurn = false;
        RedrawDone = false;
        LogLines.Clear();
    }

    private void AddLog(string line)
    {
        LogLines.Add(line);

        if (LogLines.Count > MAX_LOG)
            LogLines.RemoveAt(0);
    }

    private int SecondsLeft(uint ms, DateTime now)
        => (int)Math.Ceiling(Math.Max(0, ms - (now - ReceivedAt).TotalMilliseconds) / 1000.0);
}
