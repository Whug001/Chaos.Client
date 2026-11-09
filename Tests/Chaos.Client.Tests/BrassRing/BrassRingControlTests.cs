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

    private static BrassRingDisplayArgs OpenArgs(byte yourIndex)
        => new()
        {
            Type = BrassRingDisplayType.Open,
            YourIndex = yourIndex,
            Names = ["Alia", "Bram"],
            Looks = [null, null]
        };

    [Test]
    public void Can_open_accepts_a_spectator_index()
    {
        BrassRingControl.CanOpen(OpenArgs(SPECTATOR_INDEX), out var spectator).Should().BeTrue();

        spectator.Should().BeTrue();
    }

    [Test]
    public void Can_open_rejects_a_third_fighter()
    {
        BrassRingControl.CanOpen(OpenArgs(2), out _).Should().BeFalse();
    }

    [Test]
    public void Can_open_needs_two_names_and_looks()
    {
        var withoutName = OpenArgs(0);
        withoutName.Names.RemoveAt(0);

        BrassRingControl.CanOpen(withoutName, out _).Should().BeFalse();

        var withoutLook = OpenArgs(0);
        withoutLook.Looks.RemoveAt(0);

        BrassRingControl.CanOpen(withoutLook, out _).Should().BeFalse();
    }

    [Test]
    public void A_spectator_sends_no_moves_and_shows_no_preview()
    {
        var (_, ring) = LiveRing();
        ring.State.Reset(0, true);
        ring.ApplyTick(Tick(MAX_STAMINA));
        var sent = new List<BrassRingMove>();
        ring.MoveRequested += sent.Add;

        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.A });
        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.D });
        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.S });
        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Left });
        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Right });
        ring.OnKeyUp(new KeyUpEvent { Keycode = Keycode.S });

        sent.Should().BeEmpty();
        ring.State.Drawn(0).Pose.Should().Be(BrassRingPose.Idle);
    }

    [Test]
    public void Spectator_buttons_raise_cheers()
    {
        var (_, ring) = LiveRing();
        ring.State.Reset(0, true);
        var cheers = new List<BrassRingCheer>();
        ring.CheerRequested += cheers.Add;

        ring.OnMouseDown(new MouseDownEvent { Button = MouseButton.Left, ScreenX = 180, ScreenY = 465 });
        ring.OnMouseDown(new MouseDownEvent { Button = MouseButton.Left, ScreenX = 250, ScreenY = 465 });
        ring.OnMouseDown(new MouseDownEvent { Button = MouseButton.Left, ScreenX = 330, ScreenY = 465 });

        cheers.Should().HaveCount(3);
        cheers[0].Should().Be(BrassRingCheer.Cheer);
        cheers[1].Should().Be(BrassRingCheer.Ooh);
        cheers[2].Should().Be(BrassRingCheer.Boo);
    }

    [Test]
    public void A_switch_side_click_swaps_the_near_fighter_for_a_spectator()
    {
        var (_, ring) = LiveRing();
        ring.State.Reset(0, true);
        ring.Names = ["Alia", "Bram"];

        ring.OnMouseDown(new MouseDownEvent { Button = MouseButton.Left, ScreenX = 450, ScreenY = 465 });

        ring.State.You.Should().Be(1);
    }

    [Test]
    public void Fighter_clicks_neither_cheer_nor_switch_sides()
    {
        var (_, ring) = LiveRing();
        ring.State.Reset(0);
        var cheers = new List<BrassRingCheer>();
        ring.CheerRequested += cheers.Add;

        ring.OnMouseDown(new MouseDownEvent { Button = MouseButton.Left, ScreenX = 180, ScreenY = 465 });
        ring.OnMouseDown(new MouseDownEvent { Button = MouseButton.Left, ScreenX = 450, ScreenY = 465 });

        cheers.Should().BeEmpty();
        ring.State.You.Should().Be(0);
    }

    [Test]
    public void Escape_closes_a_spectator_at_once_and_stops_watching()
    {
        var (_, ring) = LiveRing();
        ring.State.Reset(0, true);
        var stopped = 0;
        ring.StopWatchingRequested += () => stopped++;

        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Escape });

        ring.Visible.Should().BeFalse();
        stopped.Should().Be(1);
    }

    [Test]
    public void A_fighter_still_forfeits_with_two_escapes()
    {
        var (_, ring) = LiveRing();
        ring.State.Reset(0);
        var forfeits = 0;
        ring.ForfeitRequested += () => forfeits++;

        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Escape });
        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Escape });

        forfeits.Should().Be(1);
    }

    [Test]
    public void A_cheered_event_adds_a_reaction_without_a_sound_service()
    {
        var (_, ring) = LiveRing();
        ring.State.Reset(0, true);

        ring.ApplyTick(Tick(MAX_STAMINA, new BrassRingEvent(BrassRingEventType.Cheered, Damage: (int)BrassRingCheer.Ooh)));

        ring.State.Reactions.Should().ContainSingle().Which.Kind.Should().Be(BrassRingCheer.Ooh);
    }

    [Test]
    public void Reactions_show_and_fade_from_the_window_over_time()
    {
        var (_, ring) = LiveRing();
        ring.State.Reset(0, true);
        ring.ApplyTick(Tick(MAX_STAMINA, new BrassRingEvent(BrassRingEventType.Cheered, Damage: (int)BrassRingCheer.Cheer)));

        ring.Update(0.05f);

        ring.ReactionLabels[0].Visible.Should().BeTrue();
        ring.ReactionLabels[0].Text.Should().Be("Cheer!");
        ring.ReactionLabels[1].Visible.Should().BeFalse();

        ring.Update(1.1f);

        ring.State.Reactions.Should().BeEmpty();
        ring.ReactionLabels[0].Visible.Should().BeFalse();
    }

    [Test]
    public void A_stuck_block_releases_when_the_key_is_up()
    {
        var (_, ring) = LiveRing();
        ring.IsKeyHeld = _ => false;
        ring.IsWindowActive = () => true;
        var sent = new List<BrassRingMove>();
        ring.MoveRequested += sent.Add;

        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.S });
        sent.Should().ContainSingle().Which.Should().Be(BrassRingMove.BlockStart);

        ring.Update(0.05f);

        sent.Should().HaveCount(2);
        sent[1].Should().Be(BrassRingMove.BlockEnd);

        ring.Update(0.05f);

        sent.Should().HaveCount(2);
    }
    [Test]
    public void No_second_release_after_key_up_already_sent_one()
    {
        var (_, ring) = LiveRing();
        ring.IsKeyHeld = _ => false;
        ring.IsWindowActive = () => true;
        var sent = new List<BrassRingMove>();
        ring.MoveRequested += sent.Add;

        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.S });
        ring.OnKeyUp(new KeyUpEvent { Keycode = Keycode.S });
        sent.Should().HaveCount(2);

        ring.Update(0.05f);

        sent.Should().HaveCount(2);
    }
    [Test]
    public void A_stuck_block_releases_when_the_window_loses_focus()
    {
        var (_, ring) = LiveRing();
        ring.IsKeyHeld = _ => true;
        ring.IsWindowActive = () => false;
        var sent = new List<BrassRingMove>();
        ring.MoveRequested += sent.Add;

        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.S });
        ring.Update(0.05f);

        sent.Should().HaveCount(2);
        sent[1].Should().Be(BrassRingMove.BlockEnd);
    }
    [Test]
    public void A_held_block_stays_while_the_key_is_down()
    {
        var (_, ring) = LiveRing();
        Keycode? heldArg = null;
        ring.IsKeyHeld = key =>
        {
            heldArg = key;

            return true;
        };
        ring.IsWindowActive = () => true;
        var sent = new List<BrassRingMove>();
        ring.MoveRequested += sent.Add;

        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.S });
        ring.Update(0.05f);

        sent.Should().ContainSingle().Which.Should().Be(BrassRingMove.BlockStart);
        heldArg.Should().Be((Keycode?)Keycode.S);
    }
    [Test]
    public void A_stuck_block_releases_when_another_control_covers_the_ring()
    {
        var (dispatcher, ring) = LiveRing();
        ring.IsKeyHeld = _ => true;
        ring.IsWindowActive = () => true;
        var sent = new List<BrassRingMove>();
        ring.MoveRequested += sent.Add;

        ring.OnKeyDown(new KeyDownEvent { Keycode = Keycode.S });
        dispatcher.PushControl(new UIPanel());
        ring.Update(0.05f);

        sent.Should().HaveCount(2);
        sent[1].Should().Be(BrassRingMove.BlockEnd);
    }
    [Test]
    public void Reactions_stop_below_the_banner()
    {
        var (_, ring) = LiveRing();
        ring.State.Reset(0, true);
        ring.ApplyTick(Tick(MAX_STAMINA, new BrassRingEvent(BrassRingEventType.Cheered, Damage: (int)BrassRingCheer.Ooh)));

        var first = -1;
        var last = -1;
        for (var i = 0; i < 20; i++)
        {
            ring.Update(0.05f);

            if (!ring.ReactionLabels[0].Visible)
                continue;

            if (first < 0)
                first = ring.ReactionLabels[0].Y;

            last = ring.ReactionLabels[0].Y;
            ring.ReactionLabels[0].Y.Should().BeGreaterThanOrEqualTo(397);
        }

        last.Should().BeLessThan(first);
    }
    [Test]
    public void A_spectator_sees_names_instead_of_victory_or_defeat()
    {
        var (_, ring) = LiveRing();
        ring.State.Reset(0, true);
        ring.Names = ["Alia", "Bram"];

        ring.ApplyTick(Tick(MAX_STAMINA, new BrassRingEvent(BrassRingEventType.RoundEnded, 1)));

        ring.BannerText.Should().Be("Bram takes the round");

        ring.ApplyTick(Tick(MAX_STAMINA, new BrassRingEvent(BrassRingEventType.MatchEnded, 0)));

        ring.BannerText.Should().Be("Alia wins");
    }

    [Test]
    public void Should_clear_get_ready_for_a_spectator_past_intro()
        => BrassRingControl.ShouldClearGetReady(true, BrassRingPhase.Round, "Get ready").Should().BeTrue();

    [Test]
    public void Should_keep_get_ready_for_a_fighter()
        => BrassRingControl.ShouldClearGetReady(false, BrassRingPhase.Round, "Get ready").Should().BeFalse();

    [Test]
    public void Should_keep_get_ready_for_a_spectator_in_intro()
        => BrassRingControl.ShouldClearGetReady(true, BrassRingPhase.Intro, "Get ready").Should().BeFalse();

    [Test]
    public void Should_keep_other_banner_text_for_a_spectator()
        => BrassRingControl.ShouldClearGetReady(true, BrassRingPhase.Round, "Bram takes the round").Should().BeFalse();

    [Test]
    public void A_bad_leaver_index_closes_as_no_contest()
    {
        var (_, ring) = LiveRing();

        ring.CloseFromServer(BrassRingCloseReason.FighterLeft, 5);

        ring.BannerText.Should().Be("No contest");
    }

    [Test]
    public void A_leaving_fighter_is_named_and_the_window_closes_after_the_result()
    {
        var (_, ring) = LiveRing();
        ring.State.Reset(0, true);
        ring.Names = ["Alia", "Bram"];

        ring.CloseFromServer(BrassRingCloseReason.FighterLeft, 1);

        ring.BannerText.Should().Be("Bram has left the ring");
        ring.Visible.Should().BeTrue();

        ring.Update(6f);

        ring.Visible.Should().BeFalse();
    }
}
