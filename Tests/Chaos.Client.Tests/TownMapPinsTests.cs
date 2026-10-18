using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class TownMapPinsTests
{
    private static GroupMemberInfo Member(string name, byte party = 1, bool leader = false)
        => new()
        {
            Name = name,
            BaseClass = BaseClass.Priest,
            CurrentHp = 100,
            MaximumHp = 100,
            MaximumMp = 1,
            IsLeader = leader,
            Party = party,
            BodySprite = BodySprite.Female
        };

    private static GroupMembersState Group(bool raid, params GroupMemberInfo[] members)
    {
        var state = new GroupMembersState();

        state.Set(
            new SetGroupStateArgs
            {
                IsRaid = raid,
                OwnParty = 1,
                GroupSize = (byte)(members.Length + 1),
                Members = members
            });

        return state;
    }

    private static GroupPosition At(string name, int x = 1, int y = 1) => new(name, x, y);

    [Test]
    public void A_normal_group_led_by_another_member_numbers_in_order()
    {
        var group = Group(false, Member("Bo", leader: true), Member("Cy"));

        TownMapPins.Build(group, [At("Cy", 3, 4), At("Bo", 1, 2)], "Me")
                   .Should()
                   .Equal(new TownMapPin(0, 1, 1, 2), new TownMapPin(1, 2, 3, 4));
    }

    [Test]
    public void A_leading_player_takes_number_one()
    {
        var group = Group(false, Member("Bo"), Member("Cy"));

        TownMapPins.Build(group, [At("Bo"), At("Cy")], "Me")
                   .Select(pin => pin.Number)
                   .Should()
                   .Equal(2, 3);
    }

    [Test]
    public void A_raid_colors_each_pin_by_party()
    {
        var group = Group(
            true,
            Member("P1a", 1, true),
            Member("P2a", 2),
            Member("P2b", 2),
            Member("P3a", 3),
            Member("P4a", 4));

        TownMapPins.Build(group, [At("P1a"), At("P2a"), At("P2b"), At("P3a"), At("P4a")], "Me")
                   .Select(pin => pin.Frame)
                   .Should()
                   .BeEquivalentTo([0, 6, 7, 12, 18]);
    }

    [Test]
    public void Unknown_names_and_numbers_above_six_get_no_pin()
    {
        var group = Group(
            false,
            Member("M1", leader: true),
            Member("M2"),
            Member("M3"),
            Member("M4"),
            Member("M5"),
            Member("M6"),
            Member("M7"));

        TownMapPins.Build(group, [At("M7"), At("Stranger"), At("M6")], "Me")
                   .Should()
                   .Equal(new TownMapPin(5, 6, 1, 1));
    }

    [Test]
    public void Higher_numbers_come_last_so_they_draw_on_top()
    {
        var group = Group(false, Member("A", leader: true), Member("B"), Member("C"));

        TownMapPins.Build(group, [At("C"), At("A"), At("B")], "Me")
                   .Select(pin => pin.Number)
                   .Should()
                   .Equal(1, 2, 3);
    }

    [Test]
    public void No_group_means_no_pins()
        => TownMapPins.Build(new GroupMembersState(), [At("Bo")], "Me")
                      .Should()
                      .BeEmpty();
}
