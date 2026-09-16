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
///     player is losing health to the water rather than to something in the room. Surfacing is therefore reported as
///     a value above the range a meter can hold, and that is what takes the bar down.
///     <para />
///     Nothing else clears it while the player is in the world. The meter belongs to the player rather than to the
///     map they went under on, so it carries across a map change inside the water zone -- the bar holds its last
///     reading until the server says otherwise, instead of blanking every time they cross a boundary.
/// </remarks>
public sealed class OxygenState
{
    /// <summary>Air remaining, 0-100.</summary>
    public byte Amount { get; private set; }

    /// <summary>True while the player is carrying a meter, including when it has run dry.</summary>
    public bool HasValue { get; private set; }

    /// <summary>
    ///     Stores a server-reported reading. Anything inside the meter's range shows the bar, zero included; anything
    ///     above it is the server saying there is no meter left to show, and takes the bar down.
    /// </summary>
    public void Set(byte oxygen)
    {
        if (oxygen > MAX_OXYGEN)
        {
            Reset();

            return;
        }

        Amount = oxygen;
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
