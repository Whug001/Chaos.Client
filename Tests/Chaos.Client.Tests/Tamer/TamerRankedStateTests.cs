using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TamerRankedStateTests
{
    private const TamerRankedLogFlags Won = TamerRankedLogFlags.AttackerWon;
    private const TamerRankedLogFlags CanRevengeFlags = Won | TamerRankedLogFlags.RevengeAvailable | TamerRankedLogFlags.ReplayKept;

    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static List<string> Five(string prefix = "s") => [$"{prefix}1", $"{prefix}2", $"{prefix}3", $"{prefix}4", $"{prefix}5"];

    private static TamerRankedLogView Entry(int id, string attacker = "Aoife", TamerRankedLogFlags flags = Won, sbyte change = -7, ushort age = 5)
        => new(Id(id), attacker, flags, change, age);

    private static TamerDisplayArgs Ranked(
        TamerWireDefenceState defence = TamerWireDefenceState.Good,
        byte attacksLeft = 3,
        byte refreshLeft = 1,
        List<TamerRankedOfferView>? offers = null,
        List<TamerRankedLogView>? log = null,
        List<TamerRankedTopView>? top = null,
        string message = "")
        => new()
        {
            Kind = TamerDisplayKind.Ranked,
            Season = 2,
            SeasonSecondsLeft = 23 * 86400,
            Step = 7,
            Points = 40,
            BestStep = 9,
            Rating = 1042,
            LadderPosition = 4,
            AttacksLeft = attacksLeft,
            AttacksPerDay = 5,
            RefreshLeft = refreshLeft,
            DailyWinOpen = true,
            DefenceState = defence,
            Defence = defence == TamerWireDefenceState.None ? [] : Five("d"),
            Offers = offers ?? [new("Aoife", 6, Five("a")), new("Bran", 8, Five("b")), new("Bot-Dummy", 3, Five("c"))],
            Log = log ?? [Entry(1), Entry(2, "Bran", TamerRankedLogFlags.None, 10, 120)],
            Top = top ?? [new("Aoife", 14, 100), new("Bran", 10, 20)],
            Message = message
        };

    /// <summary>A stable whose three squads hold the given numbers of members; the active squad is <paramref name="active" />.</summary>
    private static TamerStableState Stable(int first = 5, int second = 0, int third = 0, byte active = 0)
    {
        var state = new TamerStableState();

        state.ApplySnapshot(
            new TamerDisplayArgs
            {
                Kind = TamerDisplayKind.Snapshot,
                SquadSlots = 5,
                ActiveSquad = active,
                Squads = [Members(first, 0), Members(second, 10), Members(third, 20)]
            });

        return state;

        static List<TamerDisplaySquadMember> Members(int count, int offset)
            => Enumerable.Range(0, count).Select(i => new TamerDisplaySquadMember(Id(100 + offset + i), (byte)i, 0)).ToList();
    }

    private static TamerRankedState Applied(TamerDisplayArgs args)
    {
        var state = new TamerRankedState();
        state.Apply(args);

        return state;
    }

    [Test]
    public void A_new_state_has_no_view()
    {
        var state = new TamerRankedState();

        state.HasView.Should().BeFalse();
        state.HasDefence.Should().BeFalse();
        state.Defence.Should().BeEmpty();
        state.Offers.Should().BeEmpty();
        state.Log.Should().BeEmpty();
        state.Top.Should().BeEmpty();
        state.SelectedEntry.Should().BeNull();
        state.LastMessage.Should().BeEmpty();
        state.CanRefresh.Should().BeFalse();
        state.CanWatch.Should().BeFalse();
        state.RevengeQuestion().Should().BeNull();
    }

    [Test]
    public void Applying_a_display_takes_every_field()
    {
        var state = Applied(Ranked(message: "That defence has changed. Look again."));

        state.HasView.Should().BeTrue();
        state.Season.Should().Be(2);
        state.SeasonSecondsLeft.Should().Be(23u * 86400);
        state.Step.Should().Be(7);
        state.Points.Should().Be(40);
        state.BestStep.Should().Be(9);
        state.Rating.Should().Be(1042);
        state.LadderPosition.Should().Be(4);
        state.AttacksLeft.Should().Be(3);
        state.AttacksPerDay.Should().Be(5);
        state.RefreshLeft.Should().Be(1);
        state.DailyWinOpen.Should().BeTrue();
        state.DefenceState.Should().Be(TamerWireDefenceState.Good);
        state.HasDefence.Should().BeTrue();
        state.Defence.Should().Equal("d1", "d2", "d3", "d4", "d5");
        state.Offers.Select(offer => offer.Name).Should().Equal("Aoife", "Bran", "Bot-Dummy");
        state.Log.Select(entry => entry.Id).Should().Equal(Id(1), Id(2));
        state.Top.Select(place => place.Name).Should().Equal("Aoife", "Bran");
        state.LastMessage.Should().Be("That defence has changed. Look again.");
    }

    [Test]
    public void Other_display_kinds_leave_the_state_alone()
    {
        var state = new TamerRankedState();

        state.Apply(new TamerDisplayArgs { Kind = TamerDisplayKind.Snapshot, Message = "Not for this tab." });
        state.Apply(new TamerDisplayArgs { Kind = TamerDisplayKind.Catalog });
        state.Apply(null!);

        state.HasView.Should().BeFalse();
        state.LastMessage.Should().BeEmpty();
    }

    [Test]
    public void A_later_display_replaces_the_earlier_one()
    {
        var state = Applied(Ranked(message: "Refused."));

        state.Apply(Ranked(attacksLeft: 2, refreshLeft: 0, offers: [new("Cian", 2, Five())]));

        state.AttacksLeft.Should().Be(2);
        state.RefreshLeft.Should().Be(0);
        state.Offers.Select(offer => offer.Name).Should().Equal("Cian");
        state.LastMessage.Should().BeEmpty();
    }

    [Test]
    public void The_state_keeps_its_own_copy_of_the_lists()
    {
        var args = Ranked();
        var state = Applied(args);

        args.Offers.Clear();
        args.Log.Clear();
        args.Top.Clear();
        args.Defence.Clear();

        state.Offers.Should().HaveCount(3);
        state.Log.Should().HaveCount(2);
        state.Top.Should().HaveCount(2);
        state.Defence.Should().HaveCount(5);
    }

    [Test]
    public void Lists_over_their_caps_are_cut_and_null_lists_are_empty()
    {
        var args = Ranked(
            offers: Enumerable.Range(0, 5).Select(i => new TamerRankedOfferView($"P{i}", 1, Five())).ToList(),
            log: Enumerable.Range(1, 14).Select(i => Entry(i)).ToList(),
            top: Enumerable.Range(0, 13).Select(i => new TamerRankedTopView($"T{i}", 1, 1)).ToList());
        args.Defence = ["a", "b", "c", "d", "e", "f", "g"];

        var state = Applied(args);

        state.Offers.Should().HaveCount(TamerRankedState.MAX_OFFERS);
        state.Log.Should().HaveCount(TamerRankedState.MAX_LOG);
        state.Top.Should().HaveCount(TamerRankedState.MAX_TOP);
        state.Defence.Should().HaveCount(TamerRankedState.MAX_DEFENCE);

        var empty = Ranked();
        empty.Offers = null!;
        empty.Log = null!;
        empty.Top = null!;
        empty.Defence = null!;
        empty.Message = null!;
        state.Apply(empty);

        state.Offers.Should().BeEmpty();
        state.Log.Should().BeEmpty();
        state.Top.Should().BeEmpty();
        state.Defence.Should().BeEmpty();
        state.LastMessage.Should().BeEmpty();
        state.SelectedEntry.Should().BeNull();
    }

    [Test]
    public void Clear_forgets_everything()
    {
        var state = Applied(Ranked(message: "Refused."));
        state.Select(Id(2));

        state.Clear();

        state.HasView.Should().BeFalse();
        state.HasDefence.Should().BeFalse();
        state.Season.Should().Be(0);
        state.Rating.Should().Be(0);
        state.AttacksLeft.Should().Be(0);
        state.RefreshLeft.Should().Be(0);
        state.DailyWinOpen.Should().BeFalse();
        state.Defence.Should().BeEmpty();
        state.Offers.Should().BeEmpty();
        state.Log.Should().BeEmpty();
        state.Top.Should().BeEmpty();
        state.SelectedEntry.Should().BeNull();
        state.LastMessage.Should().BeEmpty();
    }

    [Test]
    public void A_display_with_no_defence_no_offers_and_an_empty_log()
    {
        var state = Applied(Ranked(TamerWireDefenceState.None, offers: [], log: [], top: []));
        var stable = Stable();

        state.HasView.Should().BeTrue();
        state.HasDefence.Should().BeFalse();
        state.Defence.Should().BeEmpty();
        state.OfferAt(0).Should().BeNull();
        state.SelectedEntry.Should().BeNull();
        state.CanRefresh.Should().BeFalse();
        state.CanWatch.Should().BeFalse();
        state.CanAttack(0, stable, out _).Should().BeFalse();
        state.CanRevenge(stable, out var reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.NoEntryChosen);
        state.RevengeQuestion().Should().BeNull();

        //entering the ladder is the one thing that works with no defence
        state.CanSetDefence(0, stable, out _).Should().BeTrue();
    }

    [Test]
    public void A_defence_that_needs_saving_again_still_counts_as_a_defence()
    {
        var state = Applied(Ranked(TamerWireDefenceState.NeedsSaving));

        state.HasDefence.Should().BeTrue();
        state.DefenceNeedsSaving.Should().BeTrue();
        Applied(Ranked()).DefenceNeedsSaving.Should().BeFalse();
    }

    [Test]
    public void Refresh_is_enabled_while_one_is_left()
    {
        Applied(Ranked(refreshLeft: 1)).CanRefresh.Should().BeTrue();
        Applied(Ranked(refreshLeft: 0)).CanRefresh.Should().BeFalse();
    }

    [Test]
    public void Attack_is_enabled_with_a_defence_an_attack_left_and_a_fielded_active_squad()
    {
        var state = Applied(Ranked());

        state.CanAttack(0, Stable(), out var reason).Should().BeTrue();
        reason.Should().BeNull();
        state.CanAttack(2, Stable(first: 1), out _).Should().BeTrue();
    }

    [Test]
    public void Attack_is_refused_with_its_reason()
    {
        var stable = Stable();

        new TamerRankedState().CanAttack(0, stable, out var reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.Reading);

        Applied(Ranked(TamerWireDefenceState.None)).CanAttack(0, stable, out reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.NeedDefence);

        Applied(Ranked(attacksLeft: 0)).CanAttack(0, stable, out reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.NoAttacksLeft);

        Applied(Ranked()).CanAttack(0, Stable(first: 0), out reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.EmptyActiveSquad);

        //squad 1 is full but squad 2 is the active one and is empty
        Applied(Ranked()).CanAttack(0, Stable(first: 5, second: 0, active: 1), out reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.EmptyActiveSquad);

        Applied(Ranked()).CanAttack(0, null!, out reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.EmptyActiveSquad);
    }

    [Test]
    public void Attack_is_refused_for_an_empty_or_unknown_slot()
    {
        var state = Applied(Ranked(offers: [new("Aoife", 6, Five())]));
        var stable = Stable();

        state.CanAttack(0, stable, out _).Should().BeTrue();
        state.CanAttack(1, stable, out var reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.NoOpponentThere);
        state.CanAttack(-1, stable, out _).Should().BeFalse();
        state.CanAttack(3, stable, out _).Should().BeFalse();
        state.CanAttack(200, stable, out _).Should().BeFalse();

        Applied(Ranked(offers: [new(string.Empty, 6, Five())])).CanAttack(0, stable, out reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.NoOpponentThere);
    }

    [Test]
    public void Set_from_squad_is_enabled_for_a_squad_of_exactly_five()
    {
        var state = Applied(Ranked());
        var stable = Stable(first: 5, second: 4, third: 0);

        state.CanSetDefence(0, stable, out var reason).Should().BeTrue();
        reason.Should().BeNull();

        state.CanSetDefence(1, stable, out reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.NeedFive);

        state.CanSetDefence(2, stable, out reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.NeedFive);

        state.CanSetDefence(3, stable, out reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.NeedFive);

        state.CanSetDefence(0, null!, out _).Should().BeFalse();

        new TamerRankedState().CanSetDefence(0, stable, out reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.Reading);
    }

    [Test]
    public void Watch_is_enabled_when_the_selected_entry_keeps_a_replay()
    {
        var state = Applied(Ranked(log: [Entry(1, flags: Won | TamerRankedLogFlags.ReplayKept), Entry(2, flags: Won)]));

        state.SelectedEntry!.Id.Should().Be(Id(1));
        state.CanWatch.Should().BeTrue();

        state.Select(Id(2));
        state.CanWatch.Should().BeFalse();
    }

    [Test]
    public void Revenge_is_enabled_by_the_entry_flag_and_a_fielded_active_squad()
    {
        var state = Applied(Ranked(log: [Entry(1, flags: CanRevengeFlags), Entry(2, flags: Won)]));

        state.CanRevenge(Stable(), out var reason).Should().BeTrue();
        reason.Should().BeNull();

        state.CanRevenge(Stable(first: 0), out reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.EmptyActiveSquad);

        state.Select(Id(2));
        state.CanRevenge(Stable(), out reason).Should().BeFalse();
        reason.Should().Be(TamerRankedText.NoRevengeThere);
    }

    [Test]
    public void Revenge_needs_no_attack_left()
    {
        var state = Applied(Ranked(attacksLeft: 0, log: [Entry(1, flags: CanRevengeFlags)]));

        state.CanRevenge(Stable(), out _).Should().BeTrue();
    }

    [Test]
    public void The_revenge_question_names_the_attacker()
    {
        var state = Applied(Ranked(log: [Entry(1, "Cian", CanRevengeFlags)]));

        state.RevengeQuestion().Should().Be("Take revenge on Cian? This does not use an attack.");
    }

    [Test]
    public void The_revenge_question_warns_when_the_pair_has_fought_today()
    {
        var state = Applied(Ranked(log: [Entry(1, "Cian", CanRevengeFlags | TamerRankedLogFlags.FoughtToday), Entry(2, "Bran", CanRevengeFlags)]));

        state.RevengeQuestion()
             .Should()
             .Be("You and Cian have already fought today, so this revenge will change no rank and will be used up. Take it anyway?");

        state.Select(Id(2));
        state.RevengeQuestion().Should().Be("Take revenge on Bran? This does not use an attack.");
    }

    [Test]
    public void Flag_32_is_read_from_the_log_entry()
    {
        var state = Applied(Ranked(log: [Entry(1, flags: (TamerRankedLogFlags)(1 | 4 | 32)), Entry(2, flags: (TamerRankedLogFlags)(1 | 4))]));

        state.SelectedFoughtToday.Should().BeTrue();

        state.Select(Id(2));
        state.SelectedFoughtToday.Should().BeFalse();
    }

    [Test]
    public void Unknown_flag_bits_change_nothing()
    {
        var state = Applied(Ranked(log: [Entry(1, flags: (TamerRankedLogFlags)(64 | 128))]));

        state.CanWatch.Should().BeFalse();
        state.CanRevenge(Stable(), out _).Should().BeFalse();
        state.SelectedFoughtToday.Should().BeFalse();
    }

    [Test]
    public void The_newest_entry_is_selected_first()
    {
        Applied(Ranked()).SelectedEntry!.Id.Should().Be(Id(1));
    }

    [Test]
    public void Log_selection_is_kept_by_id_across_displays()
    {
        var state = Applied(Ranked(log: [Entry(1), Entry(2), Entry(3)]));
        state.Select(Id(2));

        //a new attack lands on top and the row moves down
        state.Apply(Ranked(log: [Entry(9), Entry(1), Entry(2, flags: Won | TamerRankedLogFlags.ReplayKept), Entry(3)]));

        state.SelectedEntry!.Id.Should().Be(Id(2));
        state.SelectedIndex.Should().Be(2);
        state.CanWatch.Should().BeTrue();
    }

    [Test]
    public void Log_selection_moves_to_the_newest_when_the_entry_is_gone()
    {
        var state = Applied(Ranked(log: [Entry(1), Entry(2), Entry(3)]));
        state.Select(Id(3));

        state.Apply(Ranked(log: [Entry(8), Entry(9)]));

        state.SelectedEntry!.Id.Should().Be(Id(8));
        state.SelectedIndex.Should().Be(0);

        state.Apply(Ranked(log: []));

        state.SelectedEntry.Should().BeNull();
        state.SelectedIndex.Should().Be(-1);
    }

    [Test]
    public void Selecting_an_id_that_is_not_in_the_log_changes_nothing()
    {
        var state = Applied(Ranked());

        state.Select(Id(77));
        state.Select(Guid.Empty);

        state.SelectedEntry!.Id.Should().Be(Id(1));
    }

    [Test]
    public void Offer_at_answers_only_for_a_filled_slot()
    {
        var state = Applied(Ranked());

        state.OfferAt(0)!.Name.Should().Be("Aoife");
        state.OfferAt(2)!.Name.Should().Be("Bot-Dummy");
        state.OfferAt(3).Should().BeNull();
        state.OfferAt(-1).Should().BeNull();
    }

    [Test]
    public void The_attack_question_uses_the_slot_name_and_the_attacks_left()
    {
        var state = Applied(Ranked(attacksLeft: 3));

        state.AttackQuestion(1).Should().Be("Attack Bran? This uses 1 of your 3 attacks today.");
        state.AttackQuestion(3).Should().BeNull();
    }

    [Test]
    public void The_info_and_refresh_requests_carry_only_their_type()
    {
        var info = TamerRankedState.InfoAction();
        var refresh = TamerRankedState.RefreshAction();

        info.Type.Should().Be(TamerActionType.RankedInfo);
        ((byte)info.Type).Should().Be(8);
        refresh.Type.Should().Be(TamerActionType.RankedRefresh);
        ((byte)refresh.Type).Should().Be(10);
        info.Name.Should().BeEmpty();
        info.MonsterId.Should().Be(Guid.Empty);
    }

    [Test]
    public void The_set_defence_request_carries_the_squad()
    {
        var action = TamerRankedState.SetDefenceAction(2)!;

        action.Type.Should().Be(TamerActionType.RankedSetDefence);
        ((byte)action.Type).Should().Be(9);
        action.Squad.Should().Be(2);
        TamerRankedState.SetDefenceAction(3).Should().BeNull();
    }

    [Test]
    public void The_attack_request_carries_the_slot_in_squad_and_the_name_as_shown()
    {
        var action = TamerRankedState.AttackAction(1, "Bot-Dummy")!;

        action.Type.Should().Be(TamerActionType.RankedAttack);
        ((byte)action.Type).Should().Be(11);
        action.Squad.Should().Be(1);
        action.Name.Should().Be("Bot-Dummy");

        TamerRankedState.AttackAction(3, "Aoife").Should().BeNull();
        TamerRankedState.AttackAction(0, string.Empty).Should().BeNull();
        TamerRankedState.AttackAction(0, null!).Should().BeNull();
    }

    [Test]
    public void The_revenge_and_watch_requests_carry_the_entry_id_in_monster_id()
    {
        var revenge = TamerRankedState.RevengeAction(Id(5))!;
        var watch = TamerRankedState.WatchAction(Id(6))!;

        revenge.Type.Should().Be(TamerActionType.RankedRevenge);
        ((byte)revenge.Type).Should().Be(12);
        revenge.MonsterId.Should().Be(Id(5));
        watch.Type.Should().Be(TamerActionType.RankedWatch);
        ((byte)watch.Type).Should().Be(13);
        watch.MonsterId.Should().Be(Id(6));

        TamerRankedState.RevengeAction(Guid.Empty).Should().BeNull();
        TamerRankedState.WatchAction(Guid.Empty).Should().BeNull();
    }

    [Test]
    public void The_friendly_request_carries_the_trimmed_name()
    {
        var action = TamerRankedState.FriendlyAction(" Aoife ")!;

        action.Type.Should().Be(TamerActionType.Friendly);
        ((byte)action.Type).Should().Be(14);
        action.Name.Should().Be("Aoife");

        TamerRankedState.FriendlyAction("  ").Should().BeNull();
        TamerRankedState.FriendlyAction(null!).Should().BeNull();
    }
}
