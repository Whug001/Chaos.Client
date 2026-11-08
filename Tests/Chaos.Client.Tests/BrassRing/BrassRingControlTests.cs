using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.BrassRing;
using Chaos.Client.Definitions;
using Chaos.DarkAges.BrassRing;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using static Chaos.DarkAges.BrassRing.BrassRingRules;

namespace Chaos.Client.Tests.BrassRing;

[NotInParallel]
public sealed class BrassRingControlTests
{
    private static (InputDispatcher Dispatcher, BrassRingControl Ring) LiveRing()
    {
        var dispatcher = new InputDispatcher();
        var ring = new BrassRingControl(null!, null!) { Visible = true };
        dispatcher.PushControl(ring);

        return (dispatcher, ring);
    }

    private static BrassRingDisplayArgs Tick(byte stamina, params BrassRingEvent[] events)
        => new()
        {
            Type = BrassRingDisplayType.Tick,
            Phase = BrassRingPhase.Round,
            PhaseMs = 30000,
            Round = 1,
            Fighters =
            [
                new BrassRingFighterState { Health = MAX_HEALTH, Stamina = stamina, Pose = BrassRingPose.Idle },
                new BrassRingFighterState { Health = MAX_HEALTH, Stamina = MAX_STAMINA, Pose = BrassRingPose.Idle }
            ],
            Events = events.ToList()
        };

    [Test]
    public void A_live_ring_reclaims_the_top_when_a_popup_pushes_over_it()
    {
        var (dispatcher, ring) = LiveRing();
        var popup = new UIPanel();
        dispatcher.PushControl(popup);
        dispatcher.TopControl.Should().Be(popup);

        ring.Update(50f);

        dispatcher.TopControl.Should().Be(ring);
    }

    [Test]
    public void A_popup_keeps_the_top_once_the_result_is_showing()
    {
        var (dispatcher, ring) = LiveRing();
        ring.ApplyTick(Tick(MAX_STAMINA, new BrassRingEvent(BrassRingEventType.MatchEnded)));
        var popup = new UIPanel();
        dispatcher.PushControl(popup);

        ring.Update(50f);

        dispatcher.TopControl.Should().Be(popup);
    }

    [Test]
    public void A_held_block_is_sent_again_when_the_next_round_starts()
    {
        var (_, ring) = LiveRing();
        var sent = new List<BrassRingMove>();
        ring.MoveRequested += sent.Add;
        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.S });
        sent.Should().ContainSingle().Which.Should().Be(BrassRingMove.BlockStart);
        sent.Clear();

        ring.ApplyTick(Tick(MAX_STAMINA, new BrassRingEvent(BrassRingEventType.RoundStarted)));

        sent.Should().ContainSingle().Which.Should().Be(BrassRingMove.BlockStart);
    }

    [Test]
    public void No_block_is_resent_when_the_key_is_up_at_round_start()
    {
        var (_, ring) = LiveRing();
        var sent = new List<BrassRingMove>();
        ring.MoveRequested += sent.Add;

        ring.ApplyTick(Tick(MAX_STAMINA, new BrassRingEvent(BrassRingEventType.RoundStarted)));

        sent.Should().BeEmpty();
    }

    [Test]
    public void No_block_is_resent_without_the_stamina_to_hold_it()
    {
        var (_, ring) = LiveRing();
        var sent = new List<BrassRingMove>();
        ring.MoveRequested += sent.Add;
        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.S });
        sent.Clear();

        ring.ApplyTick(Tick(0, new BrassRingEvent(BrassRingEventType.RoundStarted)));

        sent.Should().BeEmpty();
    }

    [Test]
    public void An_unknown_event_with_an_out_of_range_fighter_is_ignored()
    {
        var (_, ring) = LiveRing();

        var act = () => ring.ApplyTick(Tick(MAX_STAMINA, new BrassRingEvent((BrassRingEventType)99, 5)));

        act.Should().NotThrow();
    }
}
