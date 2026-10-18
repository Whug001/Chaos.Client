using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>
/// The group packet carries pants, so the portrait shows them even when the member is off screen.
/// </summary>
public class GroupMemberPortraitTests
{
    [Test]
    public void PacketPantsReachThePortrait()
    {
        var group = new GroupMembersState();
        group.Set(Snapshot(Member("Aurelia", pants: DisplayColor.Blue)));

        group.Members[0].Appearance.PantsColor.Should().Be(DisplayColor.Blue);
    }

    [Test]
    public void MissingPantsStayMissing()
    {
        var group = new GroupMembersState();
        group.Set(Snapshot(Member("Aurelia")));

        group.Members[0].Appearance.PantsColor.Should().BeNull();
    }

    /// <summary>
    /// Headwear is dropped on purpose, so the packet's accessory layers must not leak into the portrait.
    /// </summary>
    [Test]
    public void PacketHeadwearStaysDropped()
    {
        var group = new GroupMembersState();

        group.Set(
            Snapshot(
                Member(
                    "Aurelia",
                    pants: DisplayColor.Blue,
                    accessory1Sprite: 12)));

        var appearance = group.Members[0].Appearance;

        appearance.PantsColor.Should().Be(DisplayColor.Blue);
        appearance.Accessory1Sprite.Should().Be(0);
        appearance.Accessory2Sprite.Should().Be(0);
        appearance.Accessory3Sprite.Should().Be(0);
    }

    private static SetGroupStateArgs Snapshot(params GroupMemberInfo[] members)
        => new()
        {
            Members = members
        };

    private static GroupMemberInfo Member(string name, DisplayColor? pants = null, ushort accessory1Sprite = 0)
        => new()
        {
            Name = name,
            BaseClass = BaseClass.Priest,
            CurrentHp = 100,
            MaximumHp = 100,
            CurrentMp = 100,
            MaximumMp = 100,
            BodySprite = BodySprite.Female,
            PantsColor = pants,
            AccessorySprite1 = accessory1Sprite
        };
}
