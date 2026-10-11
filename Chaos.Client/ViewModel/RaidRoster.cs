namespace Chaos.Client.ViewModel;

/// <summary>One raid member as the raid grid and the Y window show them, the player included.</summary>
public sealed record RaidRosterEntry(
    string Name,
    int Party,
    bool IsLeader,
    bool IsSelf,
    bool Absent,
    bool Dead,
    double HealthPercent);

/// <summary>One row of the Y window during a raid: a party heading (no name) or a member.</summary>
public sealed record RaidRosterLine(int Party, string? Name, bool IsLeader, bool Absent);

/// <summary>
///     Turns the group snapshot into the raid's parties. The player's own row is never in the snapshot -- their vitals
///     are on the hud -- so it is added here from their own name and health.
/// </summary>
public static class RaidRoster
{
    public static IReadOnlyList<RaidRosterEntry> Entries(
        GroupMembersState state,
        string playerName,
        bool playerLeads,
        uint playerHp,
        uint playerMaxHp)
    {
        var entries = new List<RaidRosterEntry>(state.Members.Count + 1);

        var self = new RaidRosterEntry(
            playerName,
            state.OwnParty,
            playerLeads,
            true,
            false,
            (playerHp == 0) && (playerMaxHp > 0),
            playerMaxHp == 0 ? 1 : Math.Clamp(playerHp / (double)playerMaxHp, 0, 1));

        //the server lists the leader first, so a leading player heads the list too
        if (playerLeads)
            entries.Add(self);

        foreach (var member in state.Members)
            entries.Add(
                new RaidRosterEntry(
                    member.Name,
                    member.Party,
                    member.IsLeader,
                    false,
                    member.Absent,
                    !member.Absent && (member.CurrentHp == 0),
                    member.HealthPercent));

        if (!playerLeads)
            entries.Add(self);

        return entries;
    }

    /// <summary>Every party's heading, each followed by that party's members. Empty parties keep their heading.</summary>
    public static IReadOnlyList<RaidRosterLine> Lines(IReadOnlyList<RaidRosterEntry> entries)
    {
        var lines = new List<RaidRosterLine>(entries.Count + GroupMembersState.PARTY_COUNT);

        for (var party = 1; party <= GroupMembersState.PARTY_COUNT; party++)
        {
            lines.Add(new RaidRosterLine(party, null, false, false));

            foreach (var entry in InParty(entries, party))
                lines.Add(new RaidRosterLine(party, entry.Name, entry.IsLeader, entry.Absent));
        }

        return lines;
    }

    public static IReadOnlyList<RaidRosterEntry> InParty(IReadOnlyList<RaidRosterEntry> entries, int party)
        => entries.Where(entry => entry.Party == party)
                  .ToList();
}
