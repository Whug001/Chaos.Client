using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class RaidRosterTests
{
    private static GroupMemberInfo Member(string name, byte party, bool leader = false, bool absent = false, uint hp = 100)
        => new()
        {
            Name = name,
            BaseClass = BaseClass.Priest,
            CurrentHp = hp,
            MaximumHp = 100,
            MaximumMp = 1,
            IsLeader = leader,
            Party = party,
            Absent = absent,
            BodySprite = BodySprite.Female
        };

    private static GroupMembersState Raid(byte ownParty, params GroupMemberInfo[] members)
    {
        var state = new GroupMembersState();

        state.Set(
            new SetGroupStateArgs
            {
                IsRaid = true,
                OwnParty = ownParty,
                GroupSize = (byte)(members.Length + 1),
                Members = members
            });

        return state;
    }

    [Test]
    public void ThePlayerIsAddedToTheirOwnParty()
    {
        var state = Raid(2, Member("Aurelia", 1, leader: true), Member("Bram", 2));

        var entries = RaidRoster.Entries(state, "Me", false, 50, 100);

        RaidRoster.InParty(entries, 2).Select(entry => entry.Name).Should().Equal("Bram", "Me");
        RaidRoster.InParty(entries, 2)[1].IsSelf.Should().BeTrue();
        RaidRoster.InParty(entries, 2)[1].HealthPercent.Should().BeApproximately(0.5, 0.0001);
    }

    [Test]
    public void ALeadingPlayerHeadsTheirParty()
    {
        var state = Raid(1, Member("Aurelia", 1));

        var entries = RaidRoster.Entries(state, "Me", true, 100, 100);

        RaidRoster.InParty(entries, 1).Select(entry => entry.Name).Should().Equal("Me", "Aurelia");
        RaidRoster.InParty(entries, 1)[0].IsLeader.Should().BeTrue();
    }

    [Test]
    public void AnAbsentMemberIsNotShownAsDead()
    {
        var state = Raid(1, Member("Bram", 3, absent: true, hp: 0), Member("Cara", 3, hp: 0));

        var entries = RaidRoster.InParty(RaidRoster.Entries(state, "Me", false, 100, 100), 3);

        entries[0].Absent.Should().BeTrue();
        entries[0].Dead.Should().BeFalse();
        entries[1].Dead.Should().BeTrue();
    }

    [Test]
    public void LinesPutEachPartyUnderItsHeading()
    {
        var state = Raid(2, Member("Aurelia", 1, leader: true), Member("Bram", 2), Member("Cara", 4));

        var lines = RaidRoster.Lines(RaidRoster.Entries(state, "Me", false, 100, 100));

        lines.Select(line => line.Name ?? $"#{line.Party}")
             .Should()
             .Equal("#1", "Aurelia", "#2", "Bram", "Me", "#3", "#4", "Cara");
    }
}
