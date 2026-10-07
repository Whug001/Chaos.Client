using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class TabTargetingTests
{
    private const uint SELF_ID = 1;

    private static readonly TargetCandidate Self = new(SELF_ID, "Me", ClientEntityType.Aisling, CreatureType.Aisling, false, 10, 10);
    private static readonly TargetCandidate Friend = new(2, "Ally", ClientEntityType.Aisling, CreatureType.Aisling, false, 12, 10);
    private static readonly TargetCandidate Stranger = new(3, "Passerby", ClientEntityType.Aisling, CreatureType.Aisling, false, 10, 15);
    private static readonly TargetCandidate NearMonster = new(10, "Rat", ClientEntityType.Creature, CreatureType.Normal, false, 11, 10);
    private static readonly TargetCandidate FarMonster = new(11, "Bat", ClientEntityType.Creature, CreatureType.WalkThrough, false, 18, 18);
    private static readonly TargetCandidate DeadMonster = new(12, "Wolf", ClientEntityType.Creature, CreatureType.Normal, true, 10, 11);
    private static readonly TargetCandidate Merchant = new(20, "Shopkeep", ClientEntityType.Creature, CreatureType.Merchant, false, 13, 10);
    private static readonly TargetCandidate Ghost = new(21, "Mist", ClientEntityType.Creature, CreatureType.Intangible, false, 10, 12);
    private static readonly TargetCandidate Item = new(30, "Apple", ClientEntityType.GroundItem, CreatureType.Normal, false, 10, 10);

    private static readonly TargetCandidate[] Everyone =
    [
        Self,
        Friend,
        Stranger,
        NearMonster,
        FarMonster,
        DeadMonster,
        Merchant,
        Ghost,
        Item
    ];

    private static readonly string[] Group = ["Ally"];

    //suain / cradh / the wizard spells, and ard ioc / the cures, as the server sends them
    private const SpellTargetFilter Offensive = SpellTargetFilter.HostileOnly | SpellTargetFilter.AliveOnly;
    private const SpellTargetFilter Supportive = SpellTargetFilter.AliveOnly;

    private static List<TargetCandidate> Candidates(SpellTargetFilter filter)
        => TabTargeting.Candidates(filter, Everyone, SELF_ID, Self.TileX, Self.TileY, Group);

    private static List<uint> Ids(SpellTargetFilter filter)
        => TabTargeting.Candidates(filter, Everyone, SELF_ID, Self.TileX, Self.TileY, Group)
                       .Select(c => c.Id)
                       .ToList();

    [Test]
    public void NoFilter_TakesEveryCreatureAndAisling_NearestFirst()
        => Ids(SpellTargetFilter.None)
           .Should()
           .Equal(1, 10, 12, 2, 20, 3, 11);

    [Test]
    public void HostileAlive_TakesOnlyLivingMonsters()
        => Ids(SpellTargetFilter.HostileOnly | SpellTargetFilter.AliveOnly)
           .Should()
           .Equal(10, 11);

    [Test]
    public void Friendly_TakesAislingsOnly()
        => Ids(SpellTargetFilter.FriendlyOnly | SpellTargetFilter.AliveOnly)
           .Should()
           .Equal(1, 2, 3);

    [Test]
    public void SelfOnly_TakesOnlyThePlayer()
        => Ids(SpellTargetFilter.SelfOnly | SpellTargetFilter.AliveOnly)
           .Should()
           .Equal(1);

    [Test]
    public void GroupOthers_TakesGroupMembersButNotThePlayer()
        => Ids(SpellTargetFilter.GroupOnly | SpellTargetFilter.OthersOnly | SpellTargetFilter.AliveOnly)
           .Should()
           .Equal(2);

    [Test]
    public void NonHostile_KeepsAislingsAndMerchants()
        => Ids(SpellTargetFilter.NonHostileOnly | SpellTargetFilter.AliveOnly)
           .Should()
           .Equal(1, 2, 20, 3);

    /// <summary>A revive is supportive, so a dead monster is left out even though the server's filter would allow it.</summary>
    [Test]
    public void DeadOnly_LeavesDeadMonstersOut()
        => Ids(SpellTargetFilter.DeadOnly)
           .Should()
           .BeEmpty();

    /// <summary>Heals and cures are only AliveOnly on the server; tabbing must not offer the monsters it lets through.</summary>
    [Test]
    public void SupportiveAliveOnly_LeavesMonstersOut()
        => Ids(SpellTargetFilter.AliveOnly)
           .Should()
           .Equal(1, 2, 20, 3);

    //formatter:off
    [Test]
    [Arguments(SpellTargetFilter.HostileOnly | SpellTargetFilter.AliveOnly, true)]
    [Arguments(SpellTargetFilter.HostileOnly, true)]
    [Arguments(SpellTargetFilter.MonstersOnly, true)]
    [Arguments(SpellTargetFilter.None, true)]
    [Arguments(SpellTargetFilter.AliveOnly, false)]
    [Arguments(SpellTargetFilter.AliveOnly | SpellTargetFilter.AislingsOnly, false)]
    [Arguments(SpellTargetFilter.FriendlyOnly | SpellTargetFilter.GroupOnly, false)]
    [Arguments(SpellTargetFilter.SelfOnly | SpellTargetFilter.AliveOnly, false)]
    [Arguments(SpellTargetFilter.DeadOnly, false)]
    //formatter:on
    public void IsOffensive_ReadsTheFilter(SpellTargetFilter filter, bool offensive)
        => TabTargeting.IsOffensive(filter)
                       .Should()
                       .Be(offensive);

    [Test]
    public void Cycle_WrapsBothWays()
    {
        var candidates = TabTargeting.Candidates(SpellTargetFilter.HostileOnly, Everyone, SELF_ID, 10, 10, Group);
        var targeting = new TabTargeting();

        targeting.SelectFirst(candidates);
        targeting.SelectedId.Should().Be(10u);

        targeting.Cycle(candidates, 1);
        targeting.SelectedId.Should().Be(12u);

        targeting.Cycle(candidates, 1);
        targeting.SelectedId.Should().Be(11u);

        targeting.Cycle(candidates, 1);
        targeting.SelectedId.Should().Be(10u);

        targeting.Cycle(candidates, -1);
        targeting.SelectedId.Should().Be(11u);
    }

    [Test]
    public void Cycle_WithNothingSelected_StartsAtTheMatchingEnd()
    {
        var candidates = TabTargeting.Candidates(SpellTargetFilter.HostileOnly, Everyone, SELF_ID, 10, 10, Group);

        var forward = new TabTargeting();
        forward.Cycle(candidates, 1);
        forward.SelectedId.Should().Be(10u);

        var back = new TabTargeting();
        back.Cycle(candidates, -1);
        back.SelectedId.Should().Be(11u);
    }

    [Test]
    public void Cycle_WithNoCandidates_ClearsTheSelection()
    {
        var targeting = new TabTargeting();
        targeting.Select(10);

        targeting.Cycle([], 1);

        targeting.SelectedId.Should().BeNull();
    }

    [Test]
    public void Retarget_KeepsATargetStillThere()
    {
        var candidates = Candidates(Offensive);
        var targeting = new TabTargeting();
        targeting.Select(FarMonster.Id);

        targeting.Retarget(candidates, true, SELF_ID);

        targeting.SelectedId.Should().Be(FarMonster.Id);
    }

    [Test]
    public void Retarget_MovesOnWhenTheTargetLeaves()
    {
        var targeting = new TabTargeting();
        targeting.Select(FarMonster.Id);

        targeting.Retarget([NearMonster], true, SELF_ID);

        targeting.SelectedId.Should().Be(NearMonster.Id);
    }

    /// <summary>The fallback onto the player holds while no enemy is about, and gives way to the first one that shows up.</summary>
    [Test]
    public void Retarget_HoldsThePlayerFallbackUntilAnEnemyShowsUp()
    {
        var targeting = new TabTargeting();
        targeting.SelectInitial([], true, SELF_ID);
        targeting.SelectedId.Should().Be(SELF_ID);

        targeting.Retarget([], true, SELF_ID);
        targeting.SelectedId.Should().Be(SELF_ID);

        targeting.Retarget([NearMonster], true, SELF_ID);
        targeting.SelectedId.Should().Be(NearMonster.Id);
    }

    [Test]
    public void SelectFirstAndLast_PickNearestAndFurthestHostile()
    {
        var candidates = TabTargeting.Candidates(SpellTargetFilter.None, Everyone, SELF_ID, 10, 10, Group);
        var hostiles = TabTargeting.Hostiles(candidates);
        var targeting = new TabTargeting();

        targeting.SelectFirst(hostiles);
        targeting.SelectedId.Should().Be(NearMonster.Id);

        targeting.SelectLast(hostiles);
        targeting.SelectedId.Should().Be(FarMonster.Id);
    }

    [Test]
    public void SelectInitial_OffensiveSpell_StartsAtTheNearestEnemy()
    {
        var targeting = new TabTargeting();

        targeting.SelectInitial(Candidates(Offensive), true, SELF_ID);

        targeting.SelectedId.Should().Be(NearMonster.Id);
    }

    /// <summary>The player is at distance 0, so a plain nearest-first pick would open on them whenever they qualify.</summary>
    [Test]
    public void SelectInitial_UnknownFilter_PrefersAnEnemyOverThePlayer()
    {
        var targeting = new TabTargeting();

        targeting.SelectInitial(Candidates(SpellTargetFilter.None), true, SELF_ID);

        targeting.SelectedId.Should().Be(NearMonster.Id);
    }

    [Test]
    public void SelectInitial_OffensiveSpell_NoEnemyOnScreen_FallsBackToThePlayer()
    {
        var targeting = new TabTargeting();

        targeting.SelectInitial([], true, SELF_ID);

        targeting.SelectedId.Should().Be(SELF_ID);
    }

    /// <summary>A heal is AliveOnly, so a monster beside the player must not take the box from them.</summary>
    [Test]
    public void SelectInitial_SupportiveSpell_StartsAtThePlayer()
    {
        var targeting = new TabTargeting();

        targeting.SelectInitial(Candidates(Supportive), false, SELF_ID);

        targeting.SelectedId.Should().Be(SELF_ID);
    }

    [Test]
    public void SelectInitial_SupportiveSpell_WithoutThePlayer_TakesTheNearest()
    {
        var targeting = new TabTargeting();

        targeting.SelectInitial(Candidates(SpellTargetFilter.FriendlyOnly | SpellTargetFilter.OthersOnly), false, SELF_ID);

        targeting.SelectedId.Should().Be(Friend.Id);
    }

    [Test]
    public void SelectInitial_GoesBackToTheLastTargetOfItsKind()
    {
        var targeting = new TabTargeting();
        targeting.Remember(FarMonster.Id, true);

        targeting.SelectInitial(Candidates(Offensive), true, SELF_ID);

        targeting.SelectedId.Should().Be(FarMonster.Id);
    }

    /// <summary>
    ///     The reported case: suain on a monster, ard ioc on an ally, then each again. Each spell goes back to its own last
    ///     target, not to whichever was cast last.
    /// </summary>
    [Test]
    public void SelectInitial_OffensiveAndSupportiveKeepTheirOwnTargets()
    {
        var targeting = new TabTargeting();

        //suain on the far monster
        targeting.SelectInitial(Candidates(Offensive), true, SELF_ID);
        targeting.Select(FarMonster.Id);
        targeting.Remember(FarMonster.Id, true);

        //ard ioc on the ally
        targeting.SelectInitial(Candidates(Supportive), false, SELF_ID);
        targeting.SelectedId.Should().Be(SELF_ID);
        targeting.Select(Friend.Id);
        targeting.Remember(Friend.Id, false);

        //suain again: back on the monster, not the ally just healed
        targeting.SelectInitial(Candidates(Offensive), true, SELF_ID);
        targeting.SelectedId.Should().Be(FarMonster.Id);

        //ard ioc again: back on the ally, not the monster
        targeting.SelectInitial(Candidates(Supportive), false, SELF_ID);
        targeting.SelectedId.Should().Be(Friend.Id);
    }

    /// <summary>A spell without AliveOnly still lists the dead; the remembered target must be alive to be picked again.</summary>
    [Test]
    public void SelectInitial_SkipsADeadLastTarget()
    {
        var targeting = new TabTargeting();
        targeting.Remember(DeadMonster.Id, true);

        targeting.SelectInitial(Candidates(SpellTargetFilter.HostileOnly), true, SELF_ID);

        targeting.SelectedId.Should().Be(NearMonster.Id);
    }

    [Test]
    public void SelectInitial_LastTargetGone_FallsBack()
    {
        var targeting = new TabTargeting();
        targeting.Remember(Friend.Id, false);

        var withoutFriend = Candidates(Supportive)
                            .Where(c => c.Id != Friend.Id)
                            .ToList();

        targeting.SelectInitial(withoutFriend, false, SELF_ID);

        targeting.SelectedId.Should().Be(SELF_ID);
    }

    [Test]
    public void MoveAim_StepsOneTile()
    {
        var targeting = new TabTargeting();
        targeting.Aim(5, 5);

        targeting.MoveAim(0, -1, 20, 20);
        targeting.MoveAim(1, 0, 20, 20);

        targeting.AimTile.Should().Be((6, 4));
    }

    [Test]
    public void MoveAim_StopsAtTheMapEdge()
    {
        var targeting = new TabTargeting();
        targeting.Aim(0, 19);

        targeting.MoveAim(-1, 0, 20, 20);
        targeting.MoveAim(0, 1, 20, 20);

        targeting.AimTile.Should().Be((0, 19));
    }

    [Test]
    public void MoveAim_WithoutAnAim_DoesNothing()
    {
        var targeting = new TabTargeting();

        targeting.MoveAim(1, 0, 20, 20);

        targeting.AimTile.Should().BeNull();
    }

    [Test]
    public void ClearAim_HandsTheAimBack()
    {
        var targeting = new TabTargeting();
        targeting.Aim(3, 3);

        targeting.ClearAim();

        targeting.AimTile.Should().BeNull();
    }

    [Test]
    public void GroupMember_FollowsThePanelOrder()
    {
        var candidates = TabTargeting.Candidates(SpellTargetFilter.FriendlyOnly, Everyone, SELF_ID, 10, 10, ["Ally", "Passerby"]);
        string[] panelOrder = ["Passerby", "Ally"];

        TabTargeting.GroupMember(candidates, panelOrder, 0)?.Id.Should().Be(Stranger.Id);
        TabTargeting.GroupMember(candidates, panelOrder, 1)?.Id.Should().Be(Friend.Id);
        TabTargeting.GroupMember(candidates, panelOrder, 2).Should().BeNull();
    }

    [Test]
    public void GroupMember_IsNullWhenTheSpellCantReachThem()
    {
        var candidates = TabTargeting.Candidates(SpellTargetFilter.HostileOnly, Everyone, SELF_ID, 10, 10, Group);

        TabTargeting.GroupMember(candidates, Group, 0).Should().BeNull();
    }
}
