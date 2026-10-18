#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgSoundsTests
{
    private static TcgEventView Event(TcgEventType type, TcgSeat seat = TcgSeat.A) => new(type, seat, 1, 2, 3, "");

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
}
