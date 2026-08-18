namespace Chaos.Client.ViewModel;

/// <summary>
///     What the client knows about the Plague Doctor's Ichor. Never computed here - the server is authoritative
///     and this holds whatever it last sent.
/// </summary>
public sealed class IchorState
{
    /// <summary>Current Ichor, 0-100, as last reported by the server.</summary>
    public byte Ichor { get; private set; }

    /// <summary>True once the server has sent at least one value this session.</summary>
    public bool HasValue { get; private set; }

    public void SetIchor(byte ichor)
    {
        Ichor = ichor > 100 ? (byte)100 : ichor;
        HasValue = true;
    }

    public void Reset()
    {
        Ichor = 0;
        HasValue = false;
    }
}
