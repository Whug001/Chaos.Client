namespace Chaos.Client.ViewModel;

/// <summary>
///     One pin on the town map: which <c>tmgroup.spf</c> frame to draw, the number on it, and the member's tile.
/// </summary>
public readonly record struct TownMapPin(int Frame, int Number, int TileX, int TileY);

/// <summary>
///     Matches the server's position list against the group snapshot to give each member their party colour and
///     their number within the party.
/// </summary>
/// <remarks>
///     Numbers come from <see cref="RaidRoster" /> so a pin always matches the member's place in the Y window and the
///     raid grid, the player's own spot included. The player gets no pin; the town map already draws their marker.
/// </remarks>
public static class TownMapPins
{
    public static IReadOnlyList<TownMapPin> Build(
        GroupMembersState group,
        IReadOnlyList<GroupPosition> positions,
        string playerName)
    {
        if ((positions.Count == 0) || (group.Members.Count == 0))
            return [];

        var playerLeads = !group.Members.Any(member => member.IsLeader);
        var entries = RaidRoster.Entries(group, playerName, playerLeads, 0, 0);
        var slots = new Dictionary<string, (int Party, int Number)>(StringComparer.OrdinalIgnoreCase);

        for (var party = 1; party <= GroupMembersState.PARTY_COUNT; party++)
        {
            var inParty = RaidRoster.InParty(entries, party);

            for (var i = 0; (i < inParty.Count) && (i < GroupMembersState.PARTY_SIZE); i++)
                slots.TryAdd(inParty[i].Name, (party, i + 1));
        }

        var pins = new List<TownMapPin>(positions.Count);

        foreach (var position in positions)
        {
            //a member who just joined can be listed here before the group snapshot naming them arrives
            if (!slots.TryGetValue(position.Name, out var slot))
                continue;

            pins.Add(
                new TownMapPin(
                    (slot.Party - 1) * GroupMembersState.PARTY_SIZE + slot.Number - 1,
                    slot.Number,
                    position.X,
                    position.Y));
        }

        pins.Sort(static (a, b) => a.Number != b.Number ? a.Number.CompareTo(b.Number) : a.Frame.CompareTo(b.Frame));

        return pins;
    }
}
