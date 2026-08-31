#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
#endregion

namespace Chaos.Client.Controls.World.Popups.Slots;

/// <summary>
///     One settled spin, kept for the spin log rail. Holds what the reels showed and what it paid, so a row can be
///     rebound from it without going back to the view model -- which by then describes a later spin.
/// </summary>
internal sealed class SpinLogEntry
{
    /// <summary>The centre symbol each reel came to rest on, as an index into the Open payload's symbol table.</summary>
    public required IReadOnlyList<int> SymbolIndices { get; init; }

    public required int Payout { get; init; }
    public required bool WasJackpot { get; init; }
}

/// <summary>
///     A single line of the spin log: the three symbols the reels landed on, then what the spin paid.
/// </summary>
/// <remarks>
///     Pass-through for hit testing. The log is something to read, not something to click, and leaving it hit-testable
///     would put an invisible target over the panel for every row.
/// </remarks>
internal sealed class SpinLogRow : UIPanel
{
    /// <summary>Matches the paytable rail's row pitch, so the two columns read on the same rhythm.</summary>
    public const int HEIGHT = 18;

    private const int ICON_BLOCK_WIDTH = (PaytableIconRow.ICON_SIZE * PaytableIconRow.SLOT_COUNT) + (ICON_GAP * (PaytableIconRow.SLOT_COUNT - 1));
    private const int ICON_GAP = 3;
    private const int PAYOUT_GAP = 6;

    private readonly PaytableIconRow Icons;
    private readonly UILabel PayoutLabel;

    public SpinLogRow(CreatureRenderer creatureRenderer, int width)
    {
        Width = width;
        Height = HEIGHT;
        IsPassThrough = true;

        Icons = new PaytableIconRow(creatureRenderer)
        {
            X = 0,
            Y = (HEIGHT - PaytableIconRow.ICON_SIZE) / 2,
            Width = ICON_BLOCK_WIDTH,
            Height = PaytableIconRow.ICON_SIZE,
            IsHitTestVisible = false
        };
        AddChild(Icons);

        PayoutLabel = new UILabel
        {
            X = ICON_BLOCK_WIDTH + PAYOUT_GAP,
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = Math.Max(0, width - ICON_BLOCK_WIDTH - PAYOUT_GAP),
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.Gray,
            IsHitTestVisible = false
        };
        AddChild(PayoutLabel);
    }

    /// <summary>
    ///     Repaints this row from <paramref name="entry" />. The sprite table comes from the caller rather than the
    ///     entry because it belongs to the machine, not to the spin, and every row on screen shares one.
    /// </summary>
    public void Bind(SpinLogEntry entry, IReadOnlyList<int> spriteIdsBySymbolIndex)
    {
        Icons.SetSymbols(entry.SymbolIndices, spriteIdsBySymbolIndex);

        //colour carries the outcome at a glance, matching the message line: gold for a jackpot, yellow for a win,
        //gray for a dead spin
        (PayoutLabel.Text, PayoutLabel.ForegroundColor) = entry switch
        {
            { WasJackpot: true } => ("JACKPOT", LegendColors.Gold),
            { Payout: > 0 }      => ($"+{entry.Payout:N0}", LegendColors.PastelYellow),
            _                    => ("--", LegendColors.Gray)
        };
    }
}
