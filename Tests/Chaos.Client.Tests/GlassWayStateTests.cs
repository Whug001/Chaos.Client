#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public class GlassWayStateTests
{
    private static GlassWayStateArgs Layout(GlassWayPhase phase = GlassWayPhase.Show, ushort phaseMs = 0)
        => new()
        {
            Kind = GlassWayMessageKind.Layout,
            Round = 2,
            RoundCap = 10,
            Rows = 7,
            Phase = phase,
            PhaseMs = phaseMs,
            PhaseLengthMs = 5000,
            StepMs = 600,
            Lanes = [new GlassLaneInfo { Lane = 10, EntityId = 1 }, new GlassLaneInfo { Lane = 9, EntityId = 2 }]
        };

    [Test]
    public void A_layout_activates_and_back_dates_the_phase()
    {
        var state = new GlassWayState();

        state.Apply(Layout(GlassWayPhase.Show, 1500), 10);

        state.IsActive.Should().BeTrue();
        state.Round.Should().Be(2);
        state.Rows.Should().Be(7);
        state.PhaseStartedAt.Should().BeApproximately(8.5, 1e-9);
        state.LanesInUse.Should().BeEquivalentTo(new[] { 10, 9 });
        state.LaneOf(2).Should().Be(9);
        state.LaneOf(3).Should().BeNull();
    }

    [Test]
    public void A_lit_pane_fades_in_holds_and_fades_out()
    {
        var state = new GlassWayState();
        state.Apply(Layout(), 10);

        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.StepLit, Row = 1, Column = 0, GlowMs = 600 }, 10);

        state.GlowAt(1, 0, 10).Should().Be(0);
        state.GlowAt(1, 0, 10.06).Should().BeApproximately(0.5, 1e-9);
        state.GlowAt(1, 0, 10.3).Should().Be(1);
        state.GlowAt(1, 0, 10.65).Should().Be(0);
        state.GlowAt(1, 1, 10.3).Should().Be(0);
    }

    [Test]
    public void Go_starts_the_crossing_and_clears_the_glow()
    {
        var state = new GlassWayState();
        state.Apply(Layout(), 10);
        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.StepLit, Row = 0, Column = 1, GlowMs = 600 }, 10);

        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.Go, CrossMs = 9000, MsSince = 100 }, 20);

        state.Phase.Should().Be(GlassWayPhase.Cross);
        state.CrossSecondsLeft(20).Should().BeApproximately(8.9, 1e-9);
        state.GlowAt(0, 1, 10.3).Should().Be(0);
        state.ShowsGo(20).Should().BeTrue();
        state.ShowsGo(20.8).Should().BeFalse();
    }

    [Test]
    public void Steps_mark_the_players_lane_until_the_round_ends()
    {
        var state = new GlassWayState();
        state.Apply(Layout(), 10);

        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.Stepped, EntityId = 2, Row = 0, Column = 1 }, 11);

        state.IsMarked(9, 0, 1).Should().BeTrue();
        state.IsMarked(10, 0, 1).Should().BeFalse();

        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.RoundEnd, Survivors = [1, 2] }, 12);

        state.IsMarked(9, 0, 1).Should().BeFalse();
        state.Phase.Should().Be(GlassWayPhase.Pause);
    }

    [Test]
    public void A_crack_plays_sink_dive_land_then_done()
    {
        var state = new GlassWayState();
        state.Apply(Layout(), 10);

        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.Cracked, EntityId = 1, Row = 2, Column = 0 }, 20);

        var sink = state.FallFrame(1, 20.35)!.Value;
        sink.Phase.Should().Be(GlassFallPhase.Sink);
        sink.Progress.Should().BeApproximately(0.5, 1e-9);
        state.FallFrame(1, 21.05)!.Value.Phase.Should().Be(GlassFallPhase.Dive);
        var land = state.FallFrame(1, 22)!.Value;
        land.Phase.Should().Be(GlassFallPhase.Land);
        land.Progress.Should().BeApproximately(0.5, 1e-9);
        state.FallFrame(1, 23).Should().Be(new GlassFallFrame(GlassFallPhase.Done, 1));
        state.FallFrame(2, 20.35).Should().BeNull();
        state.HasFallen(1).Should().BeTrue();
    }

    [Test]
    public void Shards_play_briefly_and_holes_last_until_the_next_round()
    {
        var state = new GlassWayState();
        state.Apply(Layout(), 10);
        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.Cracked, EntityId = 1, Row = 2, Column = 0 }, 20);
        var into = new List<GlassCrack>();

        state.CollectShattering(20.3, into);
        into.Should().ContainSingle(c => (c.Lane == 10) && (c.Row == 2) && (c.Column == 0));
        state.CollectShattering(21, into);
        into.Should().BeEmpty();
        state.CollectHoles(into);
        into.Should().ContainSingle();

        state.Apply(Layout(), 30);

        state.CollectHoles(into);
        into.Should().BeEmpty();
    }

    [Test]
    public void Safe_rows_are_past_the_path_during_the_show_and_crossing()
    {
        var state = new GlassWayState();
        state.Apply(Layout(GlassWayPhase.Show), 10);

        state.IsSafeRow(6).Should().BeFalse();
        state.IsSafeRow(7).Should().BeTrue();

        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.RoundEnd }, 11);

        state.IsSafeRow(7).Should().BeFalse();
    }

    [Test]
    public void A_mid_show_layout_restores_lit_panes_and_marks()
    {
        var state = new GlassWayState();
        var args = Layout(GlassWayPhase.Show, 700);
        args.Lit = [new GlassPaneInfo { Row = 0, Column = 1, MsSince = 300 }];
        args.Marks = [new GlassMarkInfo { EntityId = 1, Row = 0, Column = 1 }];

        state.Apply(args, 10);

        state.GlowAt(0, 1, 10).Should().Be(1);
        state.IsMarked(10, 0, 1).Should().BeTrue();
    }

    [Test]
    public void Status_and_clear()
    {
        var state = new GlassWayState();
        state.Apply(Layout(), 10);
        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.Status, PlayersLeft = 5 }, 10);

        state.PlayersLeft.Should().Be(5);

        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.Clear }, 11);

        state.IsActive.Should().BeFalse();
        state.LanesInUse.Should().BeEmpty();
        state.PlayersLeft.Should().Be(0);
        state.Round.Should().Be(0);
    }

    [Test]
    public void Clear_empties_panes_marks_holes_falls_and_lanes()
    {
        var state = new GlassWayState();
        state.Apply(Layout(), 10);
        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.StepLit, Row = 0, Column = 1, GlowMs = 600 }, 10);
        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.Stepped, EntityId = 2, Row = 0, Column = 1 }, 10);
        state.Apply(new GlassWayStateArgs { Kind = GlassWayMessageKind.Cracked, EntityId = 1, Row = 2, Column = 0 }, 10);
        var into = new List<GlassCrack>();

        state.Clear();

        state.CollectHoles(into);
        into.Should().BeEmpty();
        state.CollectShattering(10.1, into);
        into.Should().BeEmpty();
        state.HasFallen(1).Should().BeFalse();
        state.FallFrame(1, 10.1).Should().BeNull();
        state.GlowAt(0, 1, 10.3).Should().Be(0);
        state.IsMarked(9, 0, 1).Should().BeFalse();
        state.LanesInUse.Should().BeEmpty();
        state.LaneOf(1).Should().BeNull();
        state.IsActive.Should().BeFalse();
        state.Phase.Should().Be(GlassWayPhase.Countdown);
    }
}
