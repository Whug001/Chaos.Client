using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TamerBattleAssemblyTests
{
    private static TamerBattleDisplayArgs Start(
        ushort fight = 7,
        List<TamerBattleMonsterView>? monsters = null,
        List<TamerBattleNameEntry>? names = null,
        string attacker = "Attacker",
        string defender = "Defender",
        TamerBattleViewerSide viewer = TamerBattleViewerSide.Attacker,
        string title = "Test fight")
        => new()
        {
            Kind = TamerBattleDisplayKind.Start,
            FightNumber = fight,
            AttackerName = attacker,
            DefenderName = defender,
            ViewerSide = viewer,
            Title = title,
            Monsters = monsters ?? [Monster(1), Monster(6, TamerBattleSide.Defender)],
            Names = names ?? [new("rake", "Rake")]
        };

    private static TamerBattleMonsterView Monster(byte id, TamerBattleSide side = TamerBattleSide.Attacker)
        => new(id, side, "wolf", "Wolf", 10, 5, TamerWireElement.Fire, 100, 3, 0, 1);

    private static TamerBattleDisplayArgs Events(ushort fight, byte part, List<TamerBattleEventView> events)
        => new() { Kind = TamerBattleDisplayKind.Events, FightNumber = fight, PartNumber = part, Events = events };

    private static TamerBattleDisplayArgs End(
        ushort fight,
        byte eventParts,
        TamerBattleSide winner = TamerBattleSide.Attacker,
        TamerBattleEndReason reason = TamerBattleEndReason.WipeOut,
        ushort endTick = 600,
        List<string>? lines = null)
        => new()
        {
            Kind = TamerBattleDisplayKind.End,
            FightNumber = fight,
            EventParts = eventParts,
            Winner = winner,
            Reason = reason,
            EndTick = endTick,
            ResultLines = lines ?? []
        };

    private static TamerBattleEventView Ev(ushort tick, byte unit, byte target)
        => new(tick, TamerBattleEventType.Attack, unit, target, 0, -1, -1, 10, "");

    [Test]
    public void A_whole_fight_in_order_is_ready_with_the_fight()
    {
        var assembly = new TamerBattleAssembly();

        var first = assembly.Accept(Start(7));
        var second = assembly.Accept(Events(7, 0, [Ev(10, 1, 6), Ev(10, 6, 1)]));
        var third = assembly.Accept(End(7, 1));

        first.Status.Should().Be(TamerBattleAcceptance.Waiting);
        second.Status.Should().Be(TamerBattleAcceptance.Waiting);
        third.Status.Should().Be(TamerBattleAcceptance.Ready);
        third.Fight.Should().NotBeNull();
        third.Fight!.FightNumber.Should().Be(7);
        third.Fight.AttackerName.Should().Be("Attacker");
        third.Fight.Events.Should().HaveCount(2);
        third.Fight.Events[0].Tick.Should().Be(10);
        third.Fight.Winner.Should().Be(TamerBattleSide.Attacker);
        third.Fight.EndTick.Should().Be(600);
    }

    [Test]
    public void Events_parts_out_of_order_are_reassembled_in_part_order()
    {
        var assembly = new TamerBattleAssembly();
        assembly.Accept(Start(7));

        assembly.Accept(Events(7, 1, [Ev(20, 1, 6)]));
        assembly.Accept(Events(7, 0, [Ev(10, 1, 6)]));
        var ready = assembly.Accept(End(7, 2));

        ready.Status.Should().Be(TamerBattleAcceptance.Ready);
        ready.Fight!.Events.Should().HaveCount(2);
        ready.Fight.Events[0].Tick.Should().Be(10);
        ready.Fight.Events[1].Tick.Should().Be(20);
    }

    [Test]
    public void A_missing_part_is_broken_and_the_fight_is_dropped()
    {
        var assembly = new TamerBattleAssembly();
        assembly.Accept(Start(7));
        assembly.Accept(Events(7, 0, [Ev(10, 1, 6)]));

        var result = assembly.Accept(End(7, 2));

        result.Status.Should().Be(TamerBattleAcceptance.Broken);
        result.Fight.Should().BeNull();
        assembly.HasActive.Should().BeFalse();
    }

    [Test]
    public void An_End_with_no_Start_is_broken()
    {
        var assembly = new TamerBattleAssembly();

        var result = assembly.Accept(End(7, 0));

        result.Status.Should().Be(TamerBattleAcceptance.Broken);
        assembly.HasActive.Should().BeFalse();
    }

    [Test]
    public void An_Events_part_with_no_Start_is_broken()
    {
        var assembly = new TamerBattleAssembly();

        var result = assembly.Accept(Events(7, 0, [Ev(10, 1, 6)]));

        result.Status.Should().Be(TamerBattleAcceptance.Broken);
        assembly.HasActive.Should().BeFalse();
    }

    [Test]
    public void A_second_fight_replacing_the_first_discards_the_unfinished_one()
    {
        var assembly = new TamerBattleAssembly();
        assembly.Accept(Start(5));
        assembly.Accept(Events(5, 0, [Ev(10, 1, 6)]));

        var discarded = assembly.Accept(Start(6));
        discarded.Status.Should().Be(TamerBattleAcceptance.Waiting);
        assembly.ActiveFightNumber.Should().Be(6);

        assembly.Accept(Events(6, 0, [Ev(10, 1, 6)]));
        var ready = assembly.Accept(End(6, 1));

        ready.Status.Should().Be(TamerBattleAcceptance.Ready);
        ready.Fight!.FightNumber.Should().Be(6);
    }

    [Test]
    public void A_duplicate_part_is_ignored_and_the_fight_still_completes()
    {
        var assembly = new TamerBattleAssembly();
        assembly.Accept(Start(7));
        assembly.Accept(Events(7, 0, [Ev(10, 1, 6)]));
        var duplicate = assembly.Accept(Events(7, 0, [Ev(10, 1, 6)]));

        duplicate.Status.Should().Be(TamerBattleAcceptance.Waiting);

        var ready = assembly.Accept(End(7, 1));
        ready.Status.Should().Be(TamerBattleAcceptance.Ready);
        ready.Fight!.Events.Should().HaveCount(1);
    }

    [Test]
    public void A_repeated_Start_for_the_same_fight_keeps_the_parts()
    {
        var assembly = new TamerBattleAssembly();
        assembly.Accept(Start(7));
        assembly.Accept(Events(7, 0, [Ev(10, 1, 6)]));
        assembly.Accept(Start(7));
        var ready = assembly.Accept(End(7, 1));

        ready.Status.Should().Be(TamerBattleAcceptance.Ready);
        ready.Fight!.Events.Should().HaveCount(1);
    }

    [Test]
    public void A_part_for_a_different_fight_is_broken_but_the_active_fight_survives()
    {
        var assembly = new TamerBattleAssembly();
        assembly.Accept(Start(5));

        var stray = assembly.Accept(Events(6, 0, [Ev(10, 1, 6)]));

        stray.Status.Should().Be(TamerBattleAcceptance.Broken);
        assembly.HasActive.Should().BeTrue();
        assembly.ActiveFightNumber.Should().Be(5);

        assembly.Accept(Events(5, 0, [Ev(10, 1, 6)]));
        var ready = assembly.Accept(End(5, 1));
        ready.Status.Should().Be(TamerBattleAcceptance.Ready);
    }

    [Test]
    public void Null_and_unknown_kinds_are_broken()
    {
        var assembly = new TamerBattleAssembly();
        assembly.Accept(null).Status.Should().Be(TamerBattleAcceptance.Broken);

        var unknown = new TamerBattleDisplayArgs { Kind = (TamerBattleDisplayKind)99, FightNumber = 7 };
        assembly.Accept(unknown).Status.Should().Be(TamerBattleAcceptance.Broken);
    }

    [Test]
    public void Monster_and_name_counts_over_the_caps_are_broken()
    {
        var tooManyMonsters = new List<TamerBattleMonsterView>();

        for (var i = 0; i < 11; i++)
            tooManyMonsters.Add(Monster((byte)(i + 1)));

        new TamerBattleAssembly().Accept(Start(7, monsters: tooManyMonsters)).Status.Should().Be(TamerBattleAcceptance.Broken);

        var tooManyNames = new List<TamerBattleNameEntry>();

        for (var i = 0; i < 65; i++)
            tooManyNames.Add(new TamerBattleNameEntry($"k{i}", $"N{i}"));

        new TamerBattleAssembly().Accept(Start(7, names: tooManyNames)).Status.Should().Be(TamerBattleAcceptance.Broken);
    }

    [Test]
    public void An_events_part_over_1000_events_is_broken()
    {
        var assembly = new TamerBattleAssembly();
        assembly.Accept(Start(7));

        var many = new List<TamerBattleEventView>();

        for (var i = 0; i < 1001; i++)
            many.Add(Ev(10, 1, 6));

        assembly.Accept(Events(7, 0, many)).Status.Should().Be(TamerBattleAcceptance.Broken);
        assembly.HasActive.Should().BeFalse();
    }

    [Test]
    public void An_end_over_64_parts_or_4_lines_is_broken()
    {
        var tooManyParts = new TamerBattleAssembly();
        tooManyParts.Accept(Start(7));
        tooManyParts.Accept(End(7, 65)).Status.Should().Be(TamerBattleAcceptance.Broken);

        var tooManyLines = new TamerBattleAssembly();
        tooManyLines.Accept(Start(7));
        tooManyLines.Accept(End(7, 0, lines: ["a", "b", "c", "d", "e"])).Status.Should().Be(TamerBattleAcceptance.Broken);
    }

    [Test]
    public void A_part_number_over_63_is_broken()
    {
        var assembly = new TamerBattleAssembly();
        assembly.Accept(Start(7));

        assembly.Accept(Events(7, 64, [Ev(10, 1, 6)])).Status.Should().Be(TamerBattleAcceptance.Broken);
        assembly.HasActive.Should().BeFalse();
    }

    [Test]
    public void Clear_drops_an_unfinished_fight()
    {
        var assembly = new TamerBattleAssembly();
        assembly.Accept(Start(7));
        assembly.Accept(Events(7, 0, [Ev(10, 1, 6)]));

        assembly.Clear();

        assembly.HasActive.Should().BeFalse();
        assembly.Accept(End(7, 1)).Status.Should().Be(TamerBattleAcceptance.Broken);
    }

    [Test]
    public void Late_parts_for_a_completed_fight_are_ignored()
    {
        var assembly = new TamerBattleAssembly();
        assembly.Accept(Start(7));
        assembly.Accept(Events(7, 0, [Ev(10, 1, 6)]));
        var ready = assembly.Accept(End(7, 1));

        ready.Status.Should().Be(TamerBattleAcceptance.Ready);
        assembly.HasActive.Should().BeFalse();

        assembly.Accept(Events(7, 0, [Ev(10, 1, 6)])).Status.Should().Be(TamerBattleAcceptance.Waiting);
        assembly.Accept(End(7, 1)).Status.Should().Be(TamerBattleAcceptance.Waiting);
        assembly.Accept(Start(7)).Status.Should().Be(TamerBattleAcceptance.Waiting);
        assembly.HasActive.Should().BeFalse();

        assembly.Accept(Start(8));
        assembly.Accept(Events(8, 0, [Ev(10, 1, 6)]));
        var next = assembly.Accept(End(8, 1));
        next.Status.Should().Be(TamerBattleAcceptance.Ready);
        next.Fight!.FightNumber.Should().Be(8);
    }

    [Test]
    public void A_broken_fight_is_reported_once_and_its_remaining_parts_are_ignored()
    {
        var assembly = new TamerBattleAssembly();
        assembly.Accept(Start(7));
        assembly.Accept(Events(7, 0, [Ev(10, 1, 6)]));

        var broken = assembly.Accept(End(7, 2));

        broken.Status.Should().Be(TamerBattleAcceptance.Broken);
        assembly.HasActive.Should().BeFalse();

        assembly.Accept(Events(7, 1, [Ev(20, 1, 6)])).Status.Should().Be(TamerBattleAcceptance.Waiting);
        assembly.Accept(End(7, 1)).Status.Should().Be(TamerBattleAcceptance.Waiting);
        assembly.HasActive.Should().BeFalse();

        assembly.Accept(Start(8));
        assembly.Accept(Events(8, 0, [Ev(10, 1, 6)]));
        var next = assembly.Accept(End(8, 1));
        next.Status.Should().Be(TamerBattleAcceptance.Ready);
        next.Fight!.FightNumber.Should().Be(8);
    }
}
