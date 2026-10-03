#region
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Collections;

/// <summary>
///     Pumpkin Carving looks by entity id (spec 2026-10-03-pumpkin-carving-design.md, 5.2). A look may arrive before or
///     after its pumpkin; the server resends it whenever it displays the pumpkin.
/// </summary>
public sealed class PumpkinLookStore
{
    private readonly Dictionary<uint, PumpkinLook> Looks = [];
    private int Version;

    public PumpkinLook Apply(uint entityId, bool lit, byte[]? grid)
    {
        var look = new PumpkinLook(lit, lit && grid is not null ? grid.ToArray() : PumpkinGrid.Empty(), ++Version);
        Looks[entityId] = look;

        return look;
    }

    public void Clear() => Looks.Clear();

    public PumpkinLook? Get(uint entityId) => Looks.GetValueOrDefault(entityId);

    /// <summary>A lit pumpkin carries a small light, so it shows when the house lights are off.</summary>
    public static LanternSize LanternFor(PumpkinLook? look) => look is { Lit: true } ? LanternSize.Small : LanternSize.None;

    public void Remove(uint entityId) => Looks.Remove(entityId);
}
