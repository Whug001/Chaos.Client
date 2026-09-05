#region
using Chaos.Networking.Entities.Server;
#endregion

using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel;

/// <summary>
///     One seat's public state, as seen by this recipient, sent as part of a Snapshot display.
/// </summary>
public sealed class PokerSeatInfo
{
    /// <summary>
    ///     The table-seat index this entry describes (not a hand-space index).
    /// </summary>
    public required int SeatIndex { get; init; }

    /// <summary>The world entity id of the player in this seat, or 0 when it is empty. See <c>PokerSeatEntry.EntityId</c>.</summary>
    public required uint EntityId { get; init; }

    /// <summary>
    ///     Empty when this seat is unoccupied.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     This seat's current gold on hand, not the amount committed to the pot this hand.
    /// </summary>
    public required int Gold { get; init; }

    /// <summary>
    ///     This seat occupant's bare hairstyle sprite, and <see cref="HairColor" /> its colour. Zero when the seat
    ///     is empty or the player has no hair.
    /// </summary>
    /// <remarks>
    ///     Needed because <c>DisplayAisling</c> reports one HeadSprite that is the over-helm, else the helmet,
    ///     else the hair -- so the hair under a hat never reaches the client by that route. The poker portrait
    ///     draws the head bare and puts this back on.
    /// </remarks>
    public ushort HairSprite { get; init; }

    /// <summary>The colour of <see cref="HairSprite" />.</summary>
    public DisplayColor HairColor { get; init; }

    /// <summary>
    ///     Whether this seat is occupied but sitting out of the current hand.
    /// </summary>
    public required bool IsSittingOut { get; init; }

    /// <summary>
    ///     Whether this seat has folded in the current hand.
    /// </summary>
    public required bool HasFolded { get; init; }

    /// <summary>
    ///     Gold this seat has committed to the current pot.
    /// </summary>
    public required int Committed { get; init; }

    /// <summary>
    ///     This seat's most recent action this street, for the action log. Empty when it has not acted.
    /// </summary>
    public required string LastAction { get; init; }

    /// <summary>
    ///     Card indices. The server only ever populates this for the recipient's own seat, and for every
    ///     unfolded seat at showdown -- it is empty for every other seat at every other time. That is the whole
    ///     protocol: an empty list here means face-down, full stop. This view model stores exactly what arrived
    ///     on this snapshot and must never guess, cache, or reconstruct a seat's cards from an earlier one --
    ///     <see cref="PokerTable.ApplySnapshot" /> replaces every seat wholesale for that reason.
    /// </summary>
    public IReadOnlyList<byte> HoleCards { get; init; } = [];

    /// <summary>The five cards that won this seat the pot at showdown; empty otherwise.</summary>
    public IReadOnlyList<byte> WinningCards { get; init; } = [];
}

/// <summary>
///     Authoritative poker table state, updated by server packets. Controls read this directly.
/// </summary>
public sealed class PokerTable
{
    /// <summary>
    ///     Whether the poker panel should currently be shown. Set by <see cref="ApplyOpen" />, cleared by
    ///     <see cref="Clear" />.
    /// </summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    ///     The current table's display name.
    /// </summary>
    public string TableName { get; private set; } = string.Empty;

    /// <summary>
    ///     The small bet size in gold (fixed-limit).
    /// </summary>
    public int SmallBet { get; private set; }

    /// <summary>
    ///     The big bet size in gold (fixed-limit).
    /// </summary>
    public int BigBet { get; private set; }

    /// <summary>
    ///     The gold a player must hold to be dealt in.
    /// </summary>
    public int MinimumBuyIn { get; private set; }

    /// <summary>
    ///     The recipient's own table-seat index (not a hand-space index). Unlike <see cref="ButtonIndex" /> and
    ///     <see cref="ActorIndex" />, the server always sends this as the recipient's real seat -- it carries no
    ///     "unseated" sentinel.
    /// </summary>
    public int YourSeatIndex { get; private set; }

    /// <summary>
    ///     The table-seat index currently holding the dealer button, or <see langword="null" /> when there is no
    ///     button -- between hands, and before the first hand of a session has been dealt. Decoded from the
    ///     wire's 255 sentinel in <see cref="ApplySnapshot" /> so that consumers must handle the absent case at
    ///     the type level rather than remembering to compare against the raw byte themselves. <b>Do not clamp a
    ///     null value to seat 0</b> -- that draws the button on a player who does not have it.
    /// </summary>
    public byte? ButtonIndex { get; private set; }

    /// <summary>
    ///     The byte value of <c>PokerStreet</c>: 0 = Preflop, 1 = Flop, 2 = Turn, 3 = River, 4 = Showdown.
    /// </summary>
    public int Street { get; private set; }

    /// <summary>
    ///     The live pot in gold. This is the running escrow committed by all seats this hand, not a cumulative
    ///     total across hands -- it is zeroed once the pot is settled.
    /// </summary>
    public int Pot { get; private set; }

    /// <summary>
    ///     The table-seat index of the seat currently to act, or <see langword="null" /> when nobody is to act --
    ///     whenever no hand is running or the hand has just settled. Decoded from the wire's 255 sentinel in
    ///     <see cref="ApplySnapshot" /> for the same reason as <see cref="ButtonIndex" />. <b>Do not clamp a null
    ///     value to seat 0</b> -- that puts the action highlight and shot clock on a player whose turn it is not.
    /// </summary>
    public byte? ActorIndex { get; private set; }

    /// <summary>
    ///     Seconds left on the actor's shot clock. Counts down to zero.
    /// </summary>
    public int SecondsRemaining { get; private set; }

    /// <summary>
    ///     Community card indices revealed so far: 0, 3, 4 or 5 entries.
    /// </summary>
    public IReadOnlyList<byte> Board { get; private set; } = [];

    /// <summary>
    ///     The full seat roster from the most recent snapshot. Replaced wholesale on every
    ///     <see cref="ApplySnapshot" /> call -- a snapshot is a complete authoritative state, not a delta, so a
    ///     seat that is no longer present must not linger from a previous snapshot.
    /// </summary>
    public IReadOnlyList<PokerSeatInfo> Seats { get; private set; } = [];

    /// <summary>
    ///     The actions the recipient may legally take right now, as <c>PokerAction</c> byte values. This is a
    ///     server-provided hint for which buttons to enable, not a client-side legality authority -- the server
    ///     re-checks every action, so a stale or empty hint costs a refused click, never a wrong outcome.
    /// </summary>
    public IReadOnlyList<byte> LegalActions { get; private set; } = [];

    /// <summary>
    ///     Human-readable line describing what just happened, for the table log.
    /// </summary>
    public string EventText { get; private set; } = string.Empty;

    /// <summary>The seats paid by the hand that just completed. Empty until a hand completes; empty again on the next hand's first snapshot.</summary>
    public IReadOnlyList<int> WinnerSeats { get; private set; } = [];

    /// <summary>The winning hand's category byte (1–9), or 0 when the hand was won by everyone else folding. See <c>PokerTableDisplayArgs.WinningHand</c>.</summary>
    public byte WinningHand { get; private set; }

    /// <summary>
    ///     Applies an Open display: the table's fixed properties (name, bet sizes, minimum buy-in).
    /// </summary>
    public void ApplyOpen(PokerTableDisplayArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        IsOpen = true;
        TableName = args.TableName ?? string.Empty;
        SmallBet = args.SmallBet;
        BigBet = args.BigBet;
        MinimumBuyIn = args.MinimumBuyIn;
    }

    /// <summary>
    ///     Applies a Snapshot display: the complete authoritative table state -- seat roster, board, pot, whose
    ///     turn it is, and the recipient's legal actions. Replaces every field wholesale rather than merging into
    ///     what was there; a merge would leave a stale seat visible after someone stood up.
    /// </summary>
    public void ApplySnapshot(PokerTableDisplayArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        YourSeatIndex = args.YourSeatIndex;
        ButtonIndex = args.ButtonIndex == byte.MaxValue ? null : args.ButtonIndex;
        Street = args.Street;
        Pot = args.Pot;
        ActorIndex = args.ActorIndex == byte.MaxValue ? null : args.ActorIndex;
        SecondsRemaining = args.SecondsRemaining;
        Board = args.Board?.ToList() ?? [];
        LegalActions = args.LegalActions?.ToList() ?? [];
        EventText = args.EventText ?? string.Empty;
        WinnerSeats = (args.WinnerSeats ?? []).Select(seat => (int)seat).ToList();
        WinningHand = args.WinningHand;

        //defensive copy -- Seats and each entry's HoleCards come straight off the wire and are owned by the
        //caller, not this view model. Every seat is rebuilt from this snapshot alone; nothing here reads or
        //merges with the previous Seats list.
        Seats = (args.Seats ?? [])
                .Select(
                    seat => new PokerSeatInfo
                    {
                        SeatIndex = seat.SeatIndex,
                        EntityId = seat.EntityId,
                        Name = seat.Name,
                        Gold = seat.Gold,
                        IsSittingOut = seat.IsSittingOut,
                        HasFolded = seat.HasFolded,
                        Committed = seat.Committed,
                        LastAction = seat.LastAction,
                        HairSprite = seat.HairSprite,
                        HairColor = seat.HairColor,
                        HoleCards = seat.HoleCards.ToList(),
                        WinningCards = seat.WinningCards.ToList()
                    })
                .ToList();
    }

    /// <summary>
    ///     Resets every field a stale panel could display. Invoke on Close, on disconnect, and on any other path
    ///     that tears down the panel -- a player stepping off one table and onto another must never see the
    ///     previous table's name, seats, or board. Note: this resets the view model's fields only -- it does not
    ///     by itself hide the panel; see the callers in <see cref="Collections.WorldState" /> for that caveat.
    /// </summary>
    public void Clear()
    {
        IsOpen = false;
        TableName = string.Empty;
        SmallBet = 0;
        BigBet = 0;
        MinimumBuyIn = 0;
        YourSeatIndex = 0;
        ButtonIndex = null;
        Street = 0;
        Pot = 0;
        ActorIndex = null;
        SecondsRemaining = 0;
        Board = [];
        Seats = [];
        LegalActions = [];
        EventText = string.Empty;
        WinnerSeats = [];
        WinningHand = 0;
    }
}
