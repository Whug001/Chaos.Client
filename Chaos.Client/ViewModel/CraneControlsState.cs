#region
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     The crane controls panel's state: whether the player is steering the claw (from the server's CraneControls
///     packet), the seconds of steering left, and which keycaps are lit by a recent press. Time only moves in
///     <see cref="Advance" />, so tests can step it.
/// </summary>
public sealed class CraneControlsState
{
    /// <summary>How long a pressed key's keycap stays lit.</summary>
    public const double LIGHT_MS = 150;

    //remaining light time per keycap, indexed by CraneKey
    private readonly double[] LitMs = new double[(int)CraneKey.Space + 1];

    /// <summary>Whether the player is steering now; the panel shows only then.</summary>
    public bool Active { get; private set; }

    public byte SecondsLeft { get; private set; }

    public string SecondsText => $"{SecondsLeft}s";

    public void Apply(CraneControlsArgs args)
    {
        if (!args.Active)
        {
            Clear();

            return;
        }

        Active = true;
        SecondsLeft = args.SecondsLeft;
    }

    /// <summary>Lights <paramref name="key" /> for <see cref="LIGHT_MS" />; nothing while hidden.</summary>
    public void Press(CraneKey key)
    {
        if (Active)
            LitMs[(int)key] = LIGHT_MS;
    }

    public bool IsLit(CraneKey key) => LitMs[(int)key] > 0;

    public void Advance(double elapsedMs)
    {
        for (var i = 0; i < LitMs.Length; i++)
            LitMs[i] = Math.Max(0, LitMs[i] - elapsedMs);
    }

    /// <summary>Hides the panel: steering ended, the map changed, or the connection closed.</summary>
    public void Clear()
    {
        Active = false;
        SecondsLeft = 0;
        Array.Clear(LitMs);
    }
}
