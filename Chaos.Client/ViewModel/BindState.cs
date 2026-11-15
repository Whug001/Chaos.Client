using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

/// <summary>
///     The monster tamer's orb bind meter, as last reported by the server (<c>SetBindState</c>, opcode 184). A
///     preview of the catch chance while an orb is readied, and a three-step fill during a throw that ends in
///     <c>BOUND</c> or <c>BROKE FREE</c>.
/// </summary>
/// <remarks>
///     A throw owns the meter from its first <c>Throw</c> packet until <see cref="OUTCOME_HOLD" /> after its outcome.
///     <c>Preview</c> and <c>Hidden</c> packets that arrive in that window are stored rather than shown, and the last
///     one is applied when the hold ends; with none stored the meter hides. The server already holds its previews
///     back while the player's own throw is pending -- this is the second layer, so a preview sent just before the
///     outcome cannot cut the result short.
///     <para />
///     Time is passed in rather than read, so the hold is deterministic under test. The screen feeds it game time.
/// </remarks>
public sealed class BindState
{
    /// <summary>How long <c>BOUND</c> or <c>BROKE FREE</c> stays up before the meter moves on.</summary>
    public static readonly TimeSpan OUTCOME_HOLD = TimeSpan.FromMilliseconds(1500);

    /// <summary>Segments in the throw bar: one per shake.</summary>
    public const int SEGMENTS = 3;

    private SetBindStateArgs? Deferred;
    private TimeSpan? HoldEndsAt;
    private bool ThrowActive;

    /// <summary>True while there is anything to draw.</summary>
    public bool Visible => ShownMode != BindMeterMode.Hidden;

    /// <summary>The text written on the bar.</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>How full the bar is, 0 to 1.</summary>
    public float Fill { get; private set; }

    /// <summary>Throw segments lit, 0 to 3. Always 0 outside a throw.</summary>
    public int SegmentsLit { get; private set; }

    public BindMeterMode ShownMode { get; private set; }

    public BindOutcome ShownOutcome { get; private set; }

    /// <summary>Takes a server packet. <paramref name="now" /> is the current game time.</summary>
    public void Apply(SetBindStateArgs args, TimeSpan now)
    {
        if (args.Mode == BindMeterMode.Throw)
        {
            ShowThrow(args, now);

            return;
        }

        if (ThrowActive)
        {
            Deferred = args;

            return;
        }

        Show(args);
    }

    /// <summary>Ends an outcome hold once it has run its course.</summary>
    public void Update(TimeSpan now)
    {
        if (!ThrowActive || HoldEndsAt is not { } endsAt || (now < endsAt))
            return;

        ThrowActive = false;
        HoldEndsAt = null;

        var next = Deferred;
        Deferred = null;

        if (next is null)
            Hide();
        else
            Show(next);
    }

    public void Reset()
    {
        ThrowActive = false;
        HoldEndsAt = null;
        Deferred = null;
        Hide();
    }

    private void ShowThrow(SetBindStateArgs args, TimeSpan now)
    {
        ThrowActive = true;

        var shakes = Math.Min((int)args.Shakes, SEGMENTS);

        ShownMode = BindMeterMode.Throw;
        ShownOutcome = args.Outcome;
        SegmentsLit = shakes;
        Fill = shakes / (float)SEGMENTS;

        switch (args.Outcome)
        {
            case BindOutcome.Bound:
                Label = "BOUND";
                HoldEndsAt = now + OUTCOME_HOLD;

                break;
            case BindOutcome.BrokeFree:
                Label = "BROKE FREE";
                HoldEndsAt = now + OUTCOME_HOLD;

                break;
            default:
                Label = TargetLabel(args);
                HoldEndsAt = null;

                break;
        }
    }

    private void Show(SetBindStateArgs args)
    {
        if (args.Mode != BindMeterMode.Preview)
        {
            Hide();

            return;
        }

        ShownMode = BindMeterMode.Preview;
        ShownOutcome = BindOutcome.Pending;
        SegmentsLit = 0;

        if (string.IsNullOrEmpty(args.MonsterName))
        {
            Label = $"{args.Grade} orb ready";
            Fill = 0f;
        } else
        {
            Label = TargetLabel(args);
            Fill = Math.Min((int)args.Chance, 100) / 100f;
        }
    }

    private void Hide()
    {
        ShownMode = BindMeterMode.Hidden;
        ShownOutcome = BindOutcome.Pending;
        Label = string.Empty;
        Fill = 0f;
        SegmentsLit = 0;
    }

    private static string TargetLabel(SetBindStateArgs args) => $"{args.MonsterName} — {args.Grade} {args.Chance}%";
}
