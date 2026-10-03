namespace Chaos.Client.Rendering;

/// <summary>
///     How one Pumpkin Carving pumpkin looks. <see cref="Version" /> changes with every new look, so painted frames are
///     cached per look.
/// </summary>
public sealed record PumpkinLook(bool Lit, byte[] Grid, int Version);
