namespace Chaos.Client.ViewModel;

/// <summary>
///     How much air the player has left underwater, as last reported by the server (<c>SetOxygenState</c>, opcode
///     122). Never computed here - the server owns the meter and this only holds what it sent.
/// </summary>
/// <remarks>
///     Deliberately not part of <see cref="ClassResourceState" />. The four class resources are four ways of
///     colouring one bar and only one is ever live, because a character has one class; oxygen is orthogonal to all
///     of it. A Berserker underwater holds Rage and Oxygen at once, so the two need separate bars and separate
///     state.
///     <para />
///     Zero is a value to show, not a reason to hide: an empty bar is the only standing indication that a drowning
///     player is losing health to the water rather than to something in the room. Visibility is therefore driven by
///     whether the server has reported a meter at all, and <see cref="Reset" /> on leaving the map is what ends it -
///     the server applies and removes the meter as the player enters and leaves the water zone.
/// </remarks>
public sealed class OxygenState
{
    /// <summary>Air remaining, 0-100.</summary>
    public byte Amount { get; private set; }

    /// <summary>True while the player is carrying a meter, including when it has run dry.</summary>
    public bool HasValue { get; private set; }

    /// <summary>
    ///     Stores a server-reported amount. Any value shows the bar, zero included; anything above the maximum is
    ///     clamped rather than treated as a signal, so a stray byte cannot overfill the meter.
    /// </summary>
    public void Set(byte oxygen)
    {
        Amount = oxygen > MAX_OXYGEN ? MAX_OXYGEN : oxygen;
        HasValue = true;
    }

    public void Reset()
    {
        Amount = 0;
        HasValue = false;
    }

    /// <summary>A full breath, and the top of the bar. Matches the server's own maximum.</summary>
    private const byte MAX_OXYGEN = 100;
}
