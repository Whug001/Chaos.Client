namespace Chaos.Client.ViewModel;

/// <summary>
///     What the client knows about the local player's class resource - the Plague Doctor's Ichor
///     (<c>SetIchorState</c>, opcode 115), the Berserker's Rage (<c>SetRageState</c>, opcode 116) or the
///     Assassin's Malice (<c>SetMaliceState</c>, opcode 118). Each is a single 0-100 byte and they all drive the
///     same strip, so they share one state object: whichever resource the server last reported is the one on
///     display, and no character ever carries more than one. Never computed here - the server is authoritative
///     and this holds whatever it last sent.
/// </summary>
public sealed class ClassResourceState
{
    /// <summary>Current amount of <see cref="Kind" />, 1-100, as last reported by the server.</summary>
    public byte Amount { get; private set; }

    /// <summary>
    ///     Which resource <see cref="Amount" /> belongs to, or <see cref="ClassResourceKind.None" /> while the
    ///     server has reported nothing this session (or reported the resource spent).
    /// </summary>
    public ClassResourceKind Kind { get; private set; }

    /// <summary>True while the player has a class resource worth showing.</summary>
    public bool HasValue => Kind != ClassResourceKind.None;

    public void SetIchor(byte ichor) => Set(ClassResourceKind.Ichor, ichor);

    public void SetRage(byte rage) => Set(ClassResourceKind.Rage, rage);

    public void SetMalice(byte malice) => Set(ClassResourceKind.Malice, malice);

    public void Reset()
    {
        Amount = 0;
        Kind = ClassResourceKind.None;
    }

    /// <summary>
    ///     Stores a server-reported amount. Zero means the resource is spent and the strip hides, so it clears the
    ///     state rather than displaying an empty bar. A zero for the resource we are not currently tracking is
    ///     ignored, so a stray packet cannot blank out the resource actually in use.
    /// </summary>
    private void Set(ClassResourceKind kind, byte amount)
    {
        if (amount == 0)
        {
            if (Kind == kind)
                Reset();

            return;
        }

        Amount = amount > 100 ? (byte)100 : amount;
        Kind = kind;
    }
}
