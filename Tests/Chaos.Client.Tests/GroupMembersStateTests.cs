using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GroupMembersStateTests
{
    [Test]
    public void StartsWithNobody()
    {
        var group = new GroupMembersState();

        group.HasMembers.Should().BeFalse();
        group.Members.Should().BeEmpty();
    }

    [Test]
    public void ASnapshotFillsTheColumn()
    {
        var group = new GroupMembersState();

        group.Set(Snapshot(Member("Aurelia"), Member("Bram")));

        group.HasMembers.Should().BeTrue();

        group.Members
             .Select(member => member.Name)
             .Should()
             .Equal("Aurelia", "Bram");
    }

    /// <summary>
    ///     Every packet is the whole group, so a member who left has to be gone the moment the next one lands
    ///     rather than lingering as a stale row.
    /// </summary>
    [Test]
    public void ASnapshotReplacesTheOneBeforeIt()
    {
        var group = new GroupMembersState();
        group.Set(Snapshot(Member("Aurelia"), Member("Bram")));

        group.Set(Snapshot(Member("Bram")));

        group.Members
             .Select(member => member.Name)
             .Should()
             .Equal("Bram");
    }

    /// <summary>An empty member list is how the server says the player is no longer grouped.</summary>
    [Test]
    public void AnEmptySnapshotTakesThePanelsDown()
    {
        var group = new GroupMembersState();
        group.Set(Snapshot(Member("Aurelia")));

        group.Set(Snapshot());

        group.HasMembers.Should().BeFalse();
    }

    [Test]
    public void ReplacingTheMembersRaisesChanged()
    {
        var group = new GroupMembersState();
        var raised = 0;
        group.Changed += () => raised++;

        group.Set(Snapshot(Member("Aurelia")));

        raised.Should().Be(1);
    }

    /// <summary>
    ///     Somebody who folded the panels away did not ask for them back when a member joins or leaves, so the
    ///     collapsed state has to outlive the list it hides.
    /// </summary>
    [Test]
    public void CollapsingSurvivesAGroupChange()
    {
        var group = new GroupMembersState
        {
            Collapsed = true
        };

        group.Set(Snapshot(Member("Aurelia"), Member("Bram")));

        group.Collapsed.Should().BeTrue();
    }

    [Test]
    public void ResetDropsEveryone()
    {
        var group = new GroupMembersState();
        group.Set(Snapshot(Member("Aurelia")));

        group.Reset();

        group.HasMembers.Should().BeFalse();
    }

    [Test]
    public void VitalsBecomeFractionsOfTheirMaximum()
    {
        var group = new GroupMembersState();

        group.Set(
            Snapshot(
                Member(
                    "Aurelia",
                    currentHp: 50,
                    maximumHp: 200,
                    currentMp: 30,
                    maximumMp: 120)));

        var member = group.Members[0];

        member.HealthPercent
              .Should()
              .BeApproximately(0.25, 0.0001);

        member.ManaPercent
              .Should()
              .BeApproximately(0.25, 0.0001);
    }

    /// <summary>
    ///     A maximum of zero should never reach the client, but a bar that divides by it would take the whole
    ///     panel down with it.
    /// </summary>
    [Test]
    public void AZeroMaximumReadsAsEmptyRatherThanDividing()
    {
        var group = new GroupMembersState();

        group.Set(
            Snapshot(
                Member(
                    "Aurelia",
                    currentHp: 10,
                    maximumHp: 0,
                    currentMp: 10,
                    maximumMp: 0)));

        var member = group.Members[0];

        member.HealthPercent.Should().Be(0);
        member.ManaPercent.Should().Be(0);
    }

    /// <summary>The effects arrive soonest-first and the panel draws them in that order.</summary>
    [Test]
    public void EffectIconsKeepTheOrderTheyArrivedIn()
    {
        var group = new GroupMembersState();
        var member = Member("Aurelia");

        member.Effects =
        [
            new GroupMemberEffectInfo
            {
                Icon = 7,
                SecondsRemaining = 3
            },
            new GroupMemberEffectInfo
            {
                Icon = 9,
                SecondsRemaining = 40
            }
        ];

        group.Set(Snapshot(member));

        group.Members[0]
             .Effects.Select(effect => effect.Icon)
             .Should()
             .Equal((byte)7, (byte)9);
    }

    /// <summary>
    ///     The packet carries the body sprite the world view carries, and the portrait has to read it the same way
    ///     -- gender off the sprite, and the ghost forms recoloured rather than drawn in their stored colour.
    /// </summary>
    [Test]
    public void AGhostIsRecolouredTheWayTheWorldViewRecoloursOne()
    {
        var group = new GroupMembersState();
        var member = Member("Aurelia");
        member.BodySprite = BodySprite.FemaleGhost;
        member.BodyColor = BodyColor.White;

        group.Set(Snapshot(member));

        var appearance = group.Members[0].Appearance;

        appearance.Gender.Should().Be(Gender.Female);
        appearance.BodyColor.Should().Be((int)BodyColor.LightBlue);
    }

    [Test]
    public void AppearanceCarriesTheGearAPortraitDraws()
    {
        var group = new GroupMembersState();
        var member = Member("Aurelia");
        member.HeadSprite = 42;
        member.HeadColor = DisplayColor.Blue;
        member.FaceSprite = 3;
        member.ArmorSprite = 118;
        member.OvercoatSprite = 77;

        group.Set(Snapshot(member));

        var appearance = group.Members[0].Appearance;

        appearance.HeadSprite.Should().Be(42);
        appearance.HeadColor.Should().Be(DisplayColor.Blue);
        appearance.FaceSprite.Should().Be(3);
        appearance.ArmorSprite.Should().Be(118);
        appearance.OvercoatSprite.Should().Be(77);
    }

    /// <summary>
    ///     Headwear is dropped on purpose: at portrait size a hat is most of what there is to see, and a group in
    ///     matching helms would be a column of identical pictures. The three accessory layers are where all of it
    ///     is drawn, and zero means skip the layer.
    /// </summary>
    [Test]
    public void AppearanceDropsAccessoriesSoHeadwearIsNotDrawn()
    {
        var group = new GroupMembersState();
        var member = Member("Aurelia");
        member.AccessorySprite1 = 12;
        member.AccessorySprite2 = 13;
        member.AccessorySprite3 = 14;

        group.Set(Snapshot(member));

        var appearance = group.Members[0].Appearance;

        appearance.Accessory1Sprite.Should().Be(0);
        appearance.Accessory2Sprite.Should().Be(0);
        appearance.Accessory3Sprite.Should().Be(0);
    }

    /// <summary>The panel puts a star beside whoever the server flagged as leading.</summary>
    [Test]
    public void TheLeaderFlagIsCarriedThrough()
    {
        var group = new GroupMembersState();
        var leader = Member("Aurelia");
        leader.IsLeader = true;

        group.Set(Snapshot(leader, Member("Bram")));

        group.Members[0]
             .IsLeader.Should()
             .BeTrue();

        group.Members[1]
             .IsLeader.Should()
             .BeFalse();
    }

    private static SetGroupStateArgs Snapshot(params GroupMemberInfo[] members)
        => new()
        {
            Members = members
        };

    private static GroupMemberInfo Member(
        string name,
        uint currentHp = 100,
        uint maximumHp = 100,
        uint currentMp = 100,
        uint maximumMp = 100)
        => new()
        {
            Name = name,
            BaseClass = BaseClass.Priest,
            CurrentHp = currentHp,
            MaximumHp = maximumHp,
            CurrentMp = currentMp,
            MaximumMp = maximumMp,
            BodySprite = BodySprite.Female
        };
}
