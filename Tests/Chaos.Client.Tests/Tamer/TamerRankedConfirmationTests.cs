using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>
///     A ranked question is asked against the data on screen; a Yes after a new Ranked display or stable snapshot was
///     applied sends nothing, because the question shown no longer matches what the server holds.
/// </summary>
public sealed class TamerRankedConfirmationTests
{
    private const TamerRankedLogFlags CanRevengeFlags =
        TamerRankedLogFlags.AttackerWon | TamerRankedLogFlags.RevengeAvailable | TamerRankedLogFlags.ReplayKept;

    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static List<string> Five(string prefix) => [$"{prefix}1", $"{prefix}2", $"{prefix}3", $"{prefix}4", $"{prefix}5"];

    private static TamerDisplayArgs Ranked(
        List<string>? aoifeDefence = null,
        TamerRankedLogFlags entryFlags = CanRevengeFlags,
        byte attacksLeft = 3,
        TamerWireDefenceState defence = TamerWireDefenceState.Good)
        => new()
        {
            Kind = TamerDisplayKind.Ranked,
            Season = 2,
            SeasonSecondsLeft = 86400,
            AttacksLeft = attacksLeft,
            AttacksPerDay = 5,
            RefreshLeft = 1,
            DefenceState = defence,
            Defence = Five("d"),
            Offers = [new("Aoife", 6, aoifeDefence ?? Five("a")), new("Bran", 8, Five("b"))],
            Log = [new(Id(1), "Cian", entryFlags, -7, 5), new(Id(2), "Bran", TamerRankedLogFlags.AttackerWon, -7, 9)],
            Top = []
        };

    private static TamerDisplayArgs Snapshot(int first = 5, int second = 5, byte active = 0)
        => new()
        {
            Kind = TamerDisplayKind.Snapshot,
            SquadSlots = 5,
            ActiveSquad = active,
            Squads = [Members(first, 0), Members(second, 10), []]
        };

    private static List<TamerDisplaySquadMember> Members(int count, int offset)
        => Enumerable.Range(0, count).Select(i => new TamerDisplaySquadMember(Id(100 + offset + i), (byte)i, 0)).ToList();

    private static (TamerRankedState Ranked, TamerStableState Stable) Shown()
    {
        var ranked = new TamerRankedState();
        var stable = new TamerStableState();

        stable.ApplySnapshot(Snapshot());
        ranked.Apply(Ranked());

        return (ranked, stable);
    }

    //what the panel does on Yes: send what the confirmation answers, and count it
    private static List<TamerActionArgs> Yes(
        TamerRankedConfirmation question,
        TamerRankedState ranked,
        TamerStableState stable,
        out TamerRankedAnswer answer)
    {
        var sent = new List<TamerActionArgs>();

        if (question.Confirm(ranked, stable, out answer) is { } action)
            sent.Add(action);

        return sent;
    }

    [Test]
    public void Every_applied_display_and_snapshot_moves_the_revision_on()
    {
        var (ranked, stable) = Shown();
        var rankedBefore = ranked.Revision;
        var stableBefore = stable.Revision;

        //the same data again still counts: the question cannot know what changed
        ranked.Apply(Ranked());
        stable.ApplySnapshot(Snapshot());

        ranked.Revision.Should().NotBe(rankedBefore);
        stable.Revision.Should().NotBe(stableBefore);

        //a display of another kind, or selecting a log row, is not new data
        var rankedNow = ranked.Revision;
        var stableNow = stable.Revision;

        ranked.Apply(Snapshot());
        ranked.Select(Id(2));
        stable.ApplySnapshot(Ranked());
        stable.CycleSort();

        ranked.Revision.Should().Be(rankedNow);
        stable.Revision.Should().Be(stableNow);

        ranked.Clear();
        stable.Clear();

        ranked.Revision.Should().NotBe(rankedNow);
        stable.Revision.Should().NotBe(stableNow);
    }

    [Test]
    public void A_question_is_asked_only_when_its_button_is_on()
    {
        var (ranked, stable) = Shown();

        TamerRankedConfirmation.Attack(ranked, stable, 2).Should().BeNull();
        TamerRankedConfirmation.SetDefence(ranked, stable, 2).Should().BeNull();

        ranked.Select(Id(2));
        TamerRankedConfirmation.Revenge(ranked, stable).Should().BeNull();

        var unavailable = new TamerRankedState();
        unavailable.Apply(Ranked(defence: TamerWireDefenceState.Unavailable));

        TamerRankedConfirmation.Attack(unavailable, stable, 0).Should().BeNull();
        TamerRankedConfirmation.SetDefence(unavailable, stable, 0).Should().BeNull();
        TamerRankedConfirmation.Revenge(unavailable, stable).Should().BeNull();
        TamerRankedConfirmation.Refresh(unavailable, stable).Should().BeNull();
    }

    [Test]
    public void The_questions_are_the_ones_the_tab_shows()
    {
        var (ranked, stable) = Shown();

        TamerRankedConfirmation.Attack(ranked, stable, 0)!.Question.Should().Be(TamerRankedText.AttackQuestion("Aoife", 3));
        TamerRankedConfirmation.SetDefence(ranked, stable, 1)!.Question.Should().Be(TamerRankedText.SetDefenceQuestion(1));
        TamerRankedConfirmation.Revenge(ranked, stable)!.Question.Should().Be(TamerRankedText.RevengeQuestion("Cian", false));
        TamerRankedConfirmation.Refresh(ranked, stable)!.Question.Should().Be(TamerRankedText.RefreshQuestion);
    }

    [Test]
    public void Attack_yes_with_nothing_changed_sends_exactly_one_attack()
    {
        var (ranked, stable) = Shown();
        var question = TamerRankedConfirmation.Attack(ranked, stable, 0)!;

        var sent = Yes(question, ranked, stable, out var answer);

        answer.Should().Be(TamerRankedAnswer.Send);
        sent.Should().ContainSingle();
        sent[0].Type.Should().Be(TamerActionType.RankedAttack);
        sent[0].Squad.Should().Be(0);
        sent[0].Name.Should().Be("Aoife");
    }

    [Test]
    public void Attack_yes_after_a_display_with_the_same_name_and_a_changed_defence_sends_nothing()
    {
        var (ranked, stable) = Shown();
        var question = TamerRankedConfirmation.Attack(ranked, stable, 0)!;

        ranked.Apply(Ranked(Five("z")));

        ranked.OfferAt(0)!.Name.Should().Be("Aoife");
        Yes(question, ranked, stable, out var answer).Should().BeEmpty();
        answer.Should().Be(TamerRankedAnswer.Changed);
    }

    [Test]
    public void Attack_yes_after_a_snapshot_sends_nothing()
    {
        var (ranked, stable) = Shown();
        var question = TamerRankedConfirmation.Attack(ranked, stable, 0)!;

        stable.ApplySnapshot(Snapshot(active: 1));

        Yes(question, ranked, stable, out var answer).Should().BeEmpty();
        answer.Should().Be(TamerRankedAnswer.Changed);
    }

    [Test]
    public void Revenge_yes_with_nothing_changed_sends_exactly_one_revenge()
    {
        var (ranked, stable) = Shown();
        var question = TamerRankedConfirmation.Revenge(ranked, stable)!;

        var sent = Yes(question, ranked, stable, out var answer);

        answer.Should().Be(TamerRankedAnswer.Send);
        sent.Should().ContainSingle();
        sent[0].Type.Should().Be(TamerActionType.RankedRevenge);
        sent[0].MonsterId.Should().Be(Id(1));
    }

    [Test]
    public void Revenge_yes_after_the_fought_today_flag_changed_sends_nothing()
    {
        var (ranked, stable) = Shown();
        var question = TamerRankedConfirmation.Revenge(ranked, stable)!;

        //the plain question was shown; the warning one is now the right one
        ranked.Apply(Ranked(entryFlags: CanRevengeFlags | TamerRankedLogFlags.FoughtToday));

        ranked.CanRevenge(stable, out _).Should().BeTrue();
        ranked.RevengeQuestion().Should().NotBe(question.Question);
        Yes(question, ranked, stable, out var answer).Should().BeEmpty();
        answer.Should().Be(TamerRankedAnswer.Changed);
    }

    [Test]
    public void Revenge_yes_for_an_entry_no_longer_selected_sends_nothing()
    {
        var (ranked, stable) = Shown();
        var question = TamerRankedConfirmation.Revenge(ranked, stable)!;

        ranked.Select(Id(2));

        Yes(question, ranked, stable, out var answer).Should().BeEmpty();
        answer.Should().Be(TamerRankedAnswer.Refused);
    }

    [Test]
    public void Set_defence_yes_with_nothing_changed_sends_exactly_one_save()
    {
        var (ranked, stable) = Shown();
        var question = TamerRankedConfirmation.SetDefence(ranked, stable, 1)!;

        var sent = Yes(question, ranked, stable, out var answer);

        answer.Should().Be(TamerRankedAnswer.Send);
        sent.Should().ContainSingle();
        sent[0].Type.Should().Be(TamerActionType.RankedSetDefence);
        sent[0].Squad.Should().Be(1);
    }

    [Test]
    public void Set_defence_yes_after_the_squad_changed_sends_nothing()
    {
        var (ranked, stable) = Shown();
        var question = TamerRankedConfirmation.SetDefence(ranked, stable, 1)!;

        //still five members, but not the five the player looked at
        stable.ApplySnapshot(
            new TamerDisplayArgs
            {
                Kind = TamerDisplayKind.Snapshot,
                SquadSlots = 5,
                Squads = [Members(5, 0), Members(5, 40), []]
            });

        ranked.CanSetDefence(1, stable, out _).Should().BeTrue();
        Yes(question, ranked, stable, out var answer).Should().BeEmpty();
        answer.Should().Be(TamerRankedAnswer.Changed);
    }

    [Test]
    public void Set_defence_yes_after_a_display_sends_nothing()
    {
        var (ranked, stable) = Shown();
        var question = TamerRankedConfirmation.SetDefence(ranked, stable, 0)!;

        ranked.Apply(Ranked());

        Yes(question, ranked, stable, out var answer).Should().BeEmpty();
        answer.Should().Be(TamerRankedAnswer.Changed);
    }

    [Test]
    public void Refresh_yes_sends_once_and_nothing_after_a_display()
    {
        var (ranked, stable) = Shown();

        var sent = Yes(TamerRankedConfirmation.Refresh(ranked, stable)!, ranked, stable, out var answer);

        answer.Should().Be(TamerRankedAnswer.Send);
        sent.Should().ContainSingle().Which.Type.Should().Be(TamerActionType.RankedRefresh);

        var question = TamerRankedConfirmation.Refresh(ranked, stable)!;

        ranked.Apply(Ranked());

        Yes(question, ranked, stable, out answer).Should().BeEmpty();
        answer.Should().Be(TamerRankedAnswer.Changed);
    }

    [Test]
    public void Refresh_does_not_depend_on_the_stable()
    {
        var (ranked, stable) = Shown();
        var question = TamerRankedConfirmation.Refresh(ranked, stable)!;

        stable.ApplySnapshot(Snapshot(first: 2));

        Yes(question, ranked, stable, out var answer).Should().ContainSingle();
        answer.Should().Be(TamerRankedAnswer.Send);
    }

    [Test]
    public void The_changed_line_is_short_ascii_and_fits_the_bottom_line()
    {
        TamerRankedText.Changed.Should().Be("That changed. Look again.");
        TamerDrawableText.IsPrintableAscii(TamerRankedText.Changed).Should().BeTrue();
        (TamerRankedText.Changed.Length * 6).Should().BeLessThanOrEqualTo(TamerRankedText.MESSAGE_WIDTH - 2);
    }

    [Test]
    public void A_local_line_shows_on_the_bottom_line_until_the_next_display_and_is_not_new_data()
    {
        var (ranked, _) = Shown();
        var revision = ranked.Revision;

        ranked.ShowLocalMessage(TamerRankedText.Changed);

        ranked.LastMessage.Should().Be(TamerRankedText.Changed);
        ranked.Revision.Should().Be(revision);

        ranked.Apply(Ranked());
        ranked.LastMessage.Should().BeEmpty();
    }
}
