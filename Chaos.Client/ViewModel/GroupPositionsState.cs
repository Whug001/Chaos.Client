#region
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>One group member's tile on the player's map, as last reported by the server.</summary>
public readonly record struct GroupPosition(string Name, int X, int Y);

/// <summary>
///     Where the other group members on the player's map instance stand, as last reported by the server
///     (<c>SetGroupPositions</c>, opcode 166). Feeds the pins on the town map.
/// </summary>
/// <remarks>
///     Every packet replaces the whole list, and an empty one means no pins. Party and pin number are not here; they
///     come from <see cref="GroupMembersState" /> by name.
/// </remarks>
public sealed class GroupPositionsState
{
    public IReadOnlyList<GroupPosition> Members { get; private set; } = [];

    /// <summary>Stores a server list, replacing whatever was held before.</summary>
    public void Set(SetGroupPositionsArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Members = args.Members
                      .Select(member => new GroupPosition(member.Name, member.X, member.Y))
                      .ToList();
    }

    /// <summary>Drops every position. Called on a new map and on logout.</summary>
    public void Clear() => Members = [];
}
