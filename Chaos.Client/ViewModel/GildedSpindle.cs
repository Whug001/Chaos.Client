#region
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     One segment of a wheel (main or bonus), sent as part of a tier in an Open display.
/// </summary>
public sealed class WheelSegmentInfo
{
    /// <summary>
    ///     0 = Multiplier, 1 = Bonus, 2 = Jackpot. Mirrors the server's WheelSegmentKind, which is not visible to
    ///     the client project -- this is a raw copy of the wire value, not a shared enum.
    /// </summary>
    public required byte Kind { get; init; }

    /// <summary>
    ///     The bet multiplier this segment pays, when <see cref="Kind" /> is Multiplier.
    /// </summary>
    public required int Multiplier { get; init; }

    /// <summary>
    ///     The human-readable description shown on the wheel for this segment.
    /// </summary>
    public required string Label { get; init; }
}

/// <summary>
///     One stake tier, sent as part of an Open display. <see cref="GildedSpindle.SelectedTierIndex" /> is an index
///     into this list's order.
/// </summary>
public sealed class WheelTierInfo
{
    /// <summary>
    ///     The tier's display name (e.g. "Copper", "Gilded").
    /// </summary>
    public required string TierName { get; init; }

    /// <summary>
    ///     The gold cost of a single spin at this tier.
    /// </summary>
    public required int Stake { get; init; }

    /// <summary>
    ///     Whether this tier's bonus wheel carries a JACKPOT segment. Drives the panel's pot display.
    /// </summary>
    public required bool IsJackpotEligible { get; init; }

    /// <summary>
    ///     This tier's main wheel segments, in wheel order.
    /// </summary>
    public IReadOnlyList<WheelSegmentInfo> MainWheel { get; init; } = [];

    /// <summary>
    ///     Empty when this tier has no bonus wheel.
    /// </summary>
    public IReadOnlyList<WheelSegmentInfo> BonusWheel { get; init; } = [];
}

/// <summary>
///     Authoritative Gilded Spindle wheel state, updated by server packets. Controls read this directly.
/// </summary>
public sealed class GildedSpindle
{
    /// <summary>
    ///     Whether the wheel panel should currently be shown. Set by <see cref="ApplyOpen" />, cleared by
    ///     <see cref="Clear" />.
    /// </summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    ///     The current machine's display name.
    /// </summary>
    public string MachineName { get; private set; } = string.Empty;

    /// <summary>
    ///     Every stake tier for the current machine, sent up front with the Open display.
    /// </summary>
    public IReadOnlyList<WheelTierInfo> Tiers { get; private set; } = [];

    /// <summary>
    ///     The current progressive jackpot amount.
    /// </summary>
    public int JackpotAmount { get; private set; }

    /// <summary>
    ///     The tier the player currently has selected for their next spin, as an index into <see cref="Tiers" />.
    ///     This is <b>client-only</b> state: it is never received from the server and is only ever sent as the
    ///     stake index on a spin packet. There is no server-held "selected stake" for it to desync from, because
    ///     the server bills exactly the tier index the client sends and validates it against the same tier list
    ///     it handed out in the Open display -- so this property must never trigger a round-trip of its own.
    /// </summary>
    public int SelectedTierIndex { get; set; }

    // last result

    /// <summary>
    ///     The segment index the main wheel landed on for the most recent spin.
    /// </summary>
    public byte LastMainStop { get; private set; }

    /// <summary>
    ///     The bonus wheel's landed segment index for the most recent spin, or <see langword="null" /> if it
    ///     never opened. Decoded from the wire's <see cref="WheelDisplayArgs.NoBonusStop" /> sentinel in
    ///     <see cref="ApplySpinResult" /> so that consumers must handle the absent case at the type level rather
    ///     than remembering to compare against the raw sentinel value themselves.
    /// </summary>
    public byte? LastBonusStop { get; private set; }

    /// <summary>
    ///     The tier index the most recent spin ran at, echoed back by the server so a stale panel cannot
    ///     mis-attribute the result.
    /// </summary>
    public byte LastStakeIndex { get; private set; }

    /// <summary>
    ///     The stake multiplier the most recent spin paid out.
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
    ///     Applies an Open display: the machine's name and full tier list, plus the current jackpot pot. Also
    ///     clears any stale spin-result fields from a previously displayed machine, and clamps
    ///     <see cref="SelectedTierIndex" /> to the new tier list -- a stale index from a previous machine must not
    ///     survive into this one.
    /// </summary>
    public void ApplyOpen(WheelDisplayArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        IsOpen = true;
        MachineName = args.MachineName ?? string.Empty;
        JackpotAmount = args.JackpotAmount;

        //copy every level defensively -- args and its nested lists come straight off the wire and are owned by
        //the caller, not this view model
        Tiers = (args.Tiers ?? [])
                .Select(
                    t => new WheelTierInfo
                    {
                        TierName = t.TierName,
                        Stake = t.Stake,
                        IsJackpotEligible = t.IsJackpotEligible,
                        MainWheel = CopySegments(t.MainWheel),
                        BonusWheel = CopySegments(t.BonusWheel)
                    })
                .ToList();

        SelectedTierIndex = Tiers.Count == 0 ? 0 : Math.Clamp(SelectedTierIndex, 0, Tiers.Count - 1);

        LastMainStop = 0;
        LastBonusStop = null;
        LastStakeIndex = 0;
        LastMultiplier = 0;
        LastPayout = 0;
        LastWasJackpot = false;
        LastLabel = string.Empty;
        LastJackpotWinner = null;
    }

    /// <summary>
    ///     Applies a SpinResult display: the main/bonus stop indices and payout outcome for the just-completed spin.
    /// </summary>
    public void ApplySpinResult(WheelDisplayArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        LastMainStop = args.MainStop;

        //decode the wire sentinel to an absent value here, at the deserialization boundary, rather than
        //forwarding the raw byte -- a real bonus wheel has as many as 255 segments, so "no bonus round" (the
        //common case, since most spins don't land on BONUS) must not be representable as a valid-looking index
        LastBonusStop = args.BonusStop == WheelDisplayArgs.NoBonusStop ? null : args.BonusStop;

        LastStakeIndex = args.StakeIndex;
        LastMultiplier = args.Multiplier;
        LastPayout = args.Payout;
        LastWasJackpot = args.IsJackpot;
        LastLabel = args.ResultLabel ?? string.Empty;
        JackpotAmount = args.JackpotAmount;
    }

    /// <summary>
    ///     Applies a JackpotAlert display: an updated pot value, and optionally a winner announcement.
    /// </summary>
    public void ApplyJackpot(WheelDisplayArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        JackpotAmount = args.JackpotAmount;
        LastJackpotWinner = string.IsNullOrEmpty(args.WinnerName) ? null : args.WinnerName;
    }

    /// <summary>
    ///     Resets every field a stale panel could display. Invoke on Close, on disconnect, and on any other path
    ///     that tears down the panel -- a player stepping off one machine and onto another must never see the
    ///     previous machine's name, tiers, or last result. Note: this resets the view model's fields only -- it
    ///     does not by itself hide the panel; see the callers in <see cref="Collections.WorldState" /> for that
    ///     caveat.
    /// </summary>
    public void Clear()
    {
        IsOpen = false;
        MachineName = string.Empty;
        Tiers = [];
        JackpotAmount = 0;
        SelectedTierIndex = 0;
        LastMainStop = 0;
        LastBonusStop = null;
        LastStakeIndex = 0;
        LastMultiplier = 0;
        LastPayout = 0;
        LastWasJackpot = false;
        LastLabel = string.Empty;
        LastJackpotWinner = null;
    }

    private static IReadOnlyList<WheelSegmentInfo> CopySegments(List<WheelSegmentEntry>? segments)
        => (segments ?? [])
           .Select(
               s => new WheelSegmentInfo
               {
                   Kind = s.Kind,
                   Multiplier = s.Multiplier,
                   Label = s.Label
               })
           .ToList();
}
