#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Data.Models;

/// <summary>
///     A single parsed ability (skill or spell) from an SClass metadata file.
/// </summary>
public sealed record AbilityMetadataEntry
{
    public int AbilityLevel { get; init; }

    /// <summary>
    ///     The advanced class this ability belongs to, or <see cref="Chaos.DarkAges.Definitions.AdvClass.None" /> when
    ///     every member of the base class can learn it.
    /// </summary>
    public AdvClass AdvClass { get; init; }

    public byte Con { get; init; }
    public string Description { get; init; } = string.Empty;
    public byte Dex { get; init; }
    public ushort IconSprite { get; init; }
    public byte Int { get; init; }
    public required bool IsSpell { get; init; }
    public int Level { get; init; }
    public required string Name { get; init; }
    public byte PreReq1Level { get; init; }
    public string? PreReq1Name { get; init; }
    public byte PreReq2Level { get; init; }
    public string? PreReq2Name { get; init; }
    public bool RequiresMaster { get; init; }
    public byte Str { get; init; }
    public byte Wis { get; init; }
}