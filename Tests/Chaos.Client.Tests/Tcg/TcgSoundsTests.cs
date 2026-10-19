#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using static Chaos.Client.Tests.TcgMatchFixtures;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgSoundsTests
{
    private static TcgEventView Event(TcgEventType type, TcgSeat seat = TcgSeat.A) => new(type, seat, 1, 2, 3, "");

    private static TcgEventView Cast(string key) => new(TcgEventType.CardCast, TcgSeat.A, 1, 2, 0, key);

    [Test]
    [Arguments(TcgEventType.CreatureEntered, 188)]
    [Arguments(TcgEventType.CardCast, 189)]
    [Arguments(TcgEventType.TrapRevealed, 191)]
    [Arguments(TcgEventType.Attacked, 192)]
    [Arguments(TcgEventType.Blocked, 193)]
    [Arguments(TcgEventType.Damaged, 194)]
    [Arguments(TcgEventType.PlayerDamaged, 195)]
    [Arguments(TcgEventType.CreatureDestroyed, 196)]
    public void Queued_events_map_to_their_sound(TcgEventType type, int sound)
    {
        TcgSounds.ForQueuedEvent(Event(type)).Should().Be(sound);
    }

    [Test]
    public void A_creature_cast_is_silent_until_it_enters()
    {
        TcgSounds.ForQueuedEvent(Cast("bear"), Lookup).Should().BeNull();
    }

    [Test]
    public void A_non_creature_cast_still_plays()
    {
        TcgSounds.ForQueuedEvent(Cast("bolt"), Lookup).Should().Be(189);
    }

    [Test]
    public void A_cast_of_an_unknown_card_still_plays()
    {
        TcgSounds.ForQueuedEvent(Cast("missing"), Lookup).Should().Be(189);
    }

    [Test]
    public void A_cast_without_a_catalog_still_plays()
    {
        TcgSounds.ForQueuedEvent(Cast("bear")).Should().Be(189);
    }

    [Test]
    public void Other_queued_events_are_silent()
    {
        var mapped = new[]
        {
            TcgEventType.CreatureEntered, TcgEventType.CardCast, TcgEventType.TrapRevealed, TcgEventType.Attacked,
            TcgEventType.Blocked, TcgEventType.Damaged, TcgEventType.PlayerDamaged, TcgEventType.CreatureDestroyed
        };

        foreach (var type in Enum.GetValues<TcgEventType>().Except(mapped))
            TcgSounds.ForQueuedEvent(Event(type)).Should().BeNull(type.ToString());
    }

    [Test]
    public void Applied_events_play_draw_and_trap_set_only()
    {
        foreach (var type in Enum.GetValues<TcgEventType>())
        {
            int? expected = type switch
            {
                TcgEventType.CardDrawn => 187,
                TcgEventType.TrapSet   => 190,
                _                      => null
            };

            TcgSounds.ForAppliedEvent(Event(type)).Should().Be(expected, type.ToString());
        }
    }

    [Test]
    public void Your_turn_chimes()
    {
        TcgSounds.ForTurnStart(Event(TcgEventType.TurnStarted, TcgSeat.B), TcgSeat.B, false).Should().Be(197);
    }

    [Test]
    public void Opponent_turn_is_silent()
    {
        TcgSounds.ForTurnStart(Event(TcgEventType.TurnStarted, TcgSeat.A), TcgSeat.B, false).Should().BeNull();
    }

    [Test]
    public void Spectators_hear_no_turn_chime()
    {
        TcgSounds.ForTurnStart(Event(TcgEventType.TurnStarted, TcgSeat.A), TcgSeat.A, true).Should().BeNull();
    }

    [Test]
    [Arguments(TcgEndOutcome.Victory, 198)]
    [Arguments(TcgEndOutcome.Defeat, 199)]
    [Arguments(TcgEndOutcome.Draw, 200)]
    [Arguments(TcgEndOutcome.Watched, 200)]
    public void Outcomes_map_to_their_sound(TcgEndOutcome outcome, int sound)
    {
        TcgSounds.ForOutcome(outcome).Should().Be(sound);
    }

    private static TcgDisplayArgs Board(TcgDecision decision, TcgSeat decider, bool spectating = false)
        => new()
        {
            Type = TcgDisplayType.Update,
            Decision = decision,
            Decider = decider,
            YourSeat = TcgSeat.A,
            Spectating = spectating
        };

    [Test]
    [Arguments(TcgDecision.Redraw)]
    [Arguments(TcgDecision.Main)]
    [Arguments(TcgDecision.Respond)]
    [Arguments(TcgDecision.Block)]
    [Arguments(TcgDecision.DamageOrder)]
    public void Your_decision_ticks_in_its_last_five_seconds(TcgDecision decision)
    {
        var board = Board(decision, TcgSeat.A);

        foreach (var second in new[] { 5, 4, 3, 2, 1 })
            TcgSounds.ShouldTickDecision(board, second, null).Should().BeTrue(second.ToString());
    }

    [Test]
    [Arguments(0)]
    [Arguments(6)]
    [Arguments(30)]
    public void Outside_the_last_five_seconds_stays_silent(int second)
    {
        TcgSounds.ShouldTickDecision(Board(TcgDecision.Block, TcgSeat.A), second, null).Should().BeFalse();
    }

    [Test]
    public void The_opponents_decision_stays_silent()
    {
        TcgSounds.ShouldTickDecision(Board(TcgDecision.Block, TcgSeat.B), 3, null).Should().BeFalse();
    }

    [Test]
    public void Spectators_never_hear_the_tick()
    {
        TcgSounds.ShouldTickDecision(Board(TcgDecision.Block, TcgSeat.A, spectating: true), 3, null).Should().BeFalse();
    }

    [Test]
    public void No_decision_means_no_tick()
    {
        TcgSounds.ShouldTickDecision(Board(TcgDecision.None, TcgSeat.A), 3, null).Should().BeFalse();
    }

    [Test]
    public void Each_second_ticks_only_once()
    {
        var board = Board(TcgDecision.Block, TcgSeat.A);

        TcgSounds.ShouldTickDecision(board, 5, null).Should().BeTrue();
        TcgSounds.ShouldTickDecision(board, 5, 5).Should().BeFalse();
        TcgSounds.ShouldTickDecision(board, 4, 5).Should().BeTrue();
    }
}
