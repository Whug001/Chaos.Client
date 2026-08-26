#region
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

public sealed class SlotSymbolInfo
{
    /// <summary>
    ///     The sprite id used to render this symbol on a reel.
    /// </summary>
    public required int SpriteId { get; init; }
}

public sealed class SlotPayRowInfo
{
    /// <summary>
    ///     The human-readable description of the winning combination this row represents.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    ///     The bet multiplier this combination pays out.
    /// </summary>
    public required int Multiplier { get; init; }

    /// <summary>
    ///     Whether this row represents the machine's jackpot combination.
    /// </summary>
    public required bool IsJackpot { get; init; }

    /// <summary>
    ///     Indices into <see cref="SlotMachine.Symbols" /> illustrating this row's combination, left to right (up
    ///     to three entries). A value of -1 is a wildcard/"any symbol" marker -- the rail renders it as a dim
    ///     placeholder rather than guessing at a creature. Empty when the server sent no illustration for this
    ///     row, in which case the rail falls back to <see cref="Label" />.
    /// </summary>
    public IReadOnlyList<int> SymbolIndices { get; init; } = [];
}

/// <summary>
///     Authoritative slot machine state, updated by server packets. Controls read this directly.
/// </summary>
public sealed class SlotMachine
{
    /// <summary>
    ///     Whether the slot machine panel should currently be shown. Set by <see cref="ApplyOpen" />, cleared by
    ///     <see cref="Clear" />.
    /// </summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    ///     The current machine's display name.
    /// </summary>
    public string MachineName { get; private set; } = string.Empty;

    /// <summary>
    ///     The gold cost of a single spin on the current machine.
    /// </summary>
    public int Bet { get; private set; }

    /// <summary>
    ///     The current progressive jackpot amount.
    /// </summary>
    public int JackpotAmount { get; private set; }

    /// <summary>
    ///     The symbol table used to render the reels.
    /// </summary>
    public IReadOnlyList<SlotSymbolInfo> Symbols { get; private set; } = [];

    /// <summary>
    ///     Three reels, each a list of symbol indices into <see cref="Symbols" />.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<byte>> Reels { get; private set; } = [];

    /// <summary>
    ///     The current machine's paytable rows.
    /// </summary>
    public IReadOnlyList<SlotPayRowInfo> Paytable { get; private set; } = [];

    // last result

    /// <summary>
    ///     The stop index each reel landed on for the most recent spin.
    /// </summary>
    public byte[] Stops { get; private set; } = [];

    /// <summary>
    ///     The bet multiplier the most recent spin paid out.
    /// </summary>
    public int LastMultiplier { get; private set; }

    /// <summary>
    ///     The gold amount the most recent spin paid out.
    /// </summary>
    public int LastPayout { get; private set; }

    /// <summary>
    ///     Whether the most recent spin won the progressive jackpot.
    /// </summary>
    public bool LastWasJackpot { get; private set; }

    /// <summary>
    ///     The human-readable description of the most recent spin's winning combination.
    /// </summary>
    public string LastLabel { get; private set; } = string.Empty;

    /// <summary>
    ///     The name of the player who most recently won the jackpot, from a JackpotAlert win announcement. Null
    ///     when the last JackpotAlert was only a pot-value update.
    /// </summary>
    public string? LastJackpotWinner { get; private set; }

    /// <summary>
    ///     Applies an Open display: the full machine definition (name, bet, symbol table, reel strips, paytable) plus the
    ///     current jackpot pot. Also clears any stale spin-result fields from a previously displayed machine.
    /// </summary>
    public void ApplyOpen(SlotMachineDisplayArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        IsOpen = true;
        MachineName = args.MachineName ?? string.Empty;
        Bet = args.Bet;
        JackpotAmount = args.JackpotAmount;

        Symbols = (args.Symbols ?? [])
                  .Select(s => new SlotSymbolInfo { SpriteId = s.SpriteId })
                  .ToList();

        //copy each inner reel list rather than aliasing the wire-deserialized List<List<byte>> -- the args
        //object is owned by the caller and must not be mutated out from under this view model
        Reels = (args.Reels ?? [])
                .Select(IReadOnlyList<byte> (r) => r.ToArray())
                .ToList();

        Paytable = (args.Paytable ?? [])
                   .Select(
                       p => new SlotPayRowInfo
                       {
                           Label = p.Label,
                           Multiplier = p.Multiplier,
                           IsJackpot = p.IsJackpot,
                           //defensive copy -- SymbolIndices is owned by the wire-deserialized args, not this view model
                           SymbolIndices = p.SymbolIndices.ToList()
                       })
                   .ToList();

        Stops = [];
        LastMultiplier = 0;
        LastPayout = 0;
        LastWasJackpot = false;
        LastLabel = string.Empty;
        LastJackpotWinner = null;
    }

    /// <summary>
    ///     Applies a SpinResult display: the reel stop indices and payout outcome for the just-completed spin.
    /// </summary>
    public void ApplySpinResult(SlotMachineDisplayArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        //copy defensively -- Stops comes straight off the wire as a caller-owned array
        Stops = args.Stops is null ? [] : (byte[])args.Stops.Clone();
        LastMultiplier = args.Multiplier;
        LastPayout = args.Payout;
        LastWasJackpot = args.IsJackpot;
        LastLabel = args.ResultLabel ?? string.Empty;
        JackpotAmount = args.JackpotAmount;
    }

    /// <summary>
    ///     Applies a JackpotAlert display: an updated pot value, and optionally a winner announcement.
    /// </summary>
    public void ApplyJackpot(SlotMachineDisplayArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        JackpotAmount = args.JackpotAmount;
        LastJackpotWinner = string.IsNullOrEmpty(args.WinnerName) ? null : args.WinnerName;
    }

    /// <summary>
    ///     Resets every field a stale panel could display. Invoke on Close, on disconnect, and on any other path that
    ///     tears down the panel -- a player stepping off one machine and onto another must never see the previous
    ///     machine's name, bet, paytable, or reels.
    /// </summary>
    public void Clear()
    {
        IsOpen = false;
        MachineName = string.Empty;
        Bet = 0;
        JackpotAmount = 0;
        Symbols = [];
        Reels = [];
        Paytable = [];
        Stops = [];
        LastMultiplier = 0;
        LastPayout = 0;
        LastWasJackpot = false;
        LastLabel = string.Empty;
        LastJackpotWinner = null;
    }
}
