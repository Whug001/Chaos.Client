using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Rendering;

/// <summary>How one Pumpkin Carving pumpkin looks: blank, carving (dark holes) or lit (candle glow).</summary>
public sealed record PumpkinLook(uint EntityId, PumpkinLookState State, byte[] Grid)
{
    public bool Lit => State == PumpkinLookState.Lit;
}
