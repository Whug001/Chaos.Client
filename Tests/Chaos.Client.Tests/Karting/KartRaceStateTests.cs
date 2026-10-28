#region
using Chaos.DarkAges.Karting;
using Chaos.Client.ViewModel;
using Chaos.Client.Systems.Karting;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartRaceStateTests
{
    private static KartTrackLayoutArgs Layout()
        => new()
        {
            TrackKey = "test",
            Width = 120,
            Height = 120,
            Laps = 3,
            Runs = [new KartSurfaceRun(SurfaceKind.Road, 120 * 120)],
            Racers = [new KartRacerInfo(0, 5, "One", 1), new KartRacerInfo(1, 6, "Two", 2), new KartRacerInfo(2, 77, "Local", 3)],
            Stats = new KartStats().ToValues().ToList()
        };

    [Test]
    public void Layout_builds_surface_stats_and_local_roster()
    {
        var state = new KartRaceState();
        var layout = Layout();
        state.ApplyLayout(layout, 77);
        state.Active.Should().BeTrue();
        state.LocalIndex.Should().Be(2);
        state.Surface!.Width.Should().Be(120);
        state.Surface.Height.Should().Be(120);
        state.Stats.Should().BeEquivalentTo(KartStats.FromValues(layout.Stats));
        state.Racers.Should().HaveCount(3);
        state.IndexOfEntity(77).Should().Be((byte)2);
    }

    [Test]
    public void A_same_track_layout_keeps_the_race_stage_steps_and_results()
    {
        var state = new KartRaceState();
        var layout = Layout();
        state.ApplyLayout(layout, 77);
        state.ApplyEvent(new KartRaceEventArgs { Kind = KartRaceEventKind.Countdown, StartStep = 100, GoStep = 130 });
        state.ApplyEvent(new KartRaceEventArgs { Kind = KartRaceEventKind.LapDone, Index = 2, LapMs = 45000 });
        state.ApplyLayout(layout, 77);

        state.Stage.Should().Be(KartRaceStage.Countdown);
        state.StartStep.Should().Be(100);
        state.GoStep.Should().Be(130);
        state.LapTimes(2).Should().ContainSingle().Which.Should().Be(45000);

        state.ApplyEvent(new KartRaceEventArgs { Kind = KartRaceEventKind.Finished, Index = 2, TotalMs = 120000, BestLapMs = 40000 });
        state.ApplyLayout(layout, 77);

        state.Stage.Should().Be(KartRaceStage.Finishing);
        state.Finish(2).Should().Be((120000u, 40000u));
    }

    [Test]
    public void A_different_track_layout_resets_the_race_state()
    {
        var state = new KartRaceState();
        state.ApplyLayout(Layout(), 77);
        state.ApplyEvent(new KartRaceEventArgs { Kind = KartRaceEventKind.Countdown, StartStep = 100, GoStep = 130 });
        state.ApplyEvent(new KartRaceEventArgs { Kind = KartRaceEventKind.LapDone, Index = 2, LapMs = 45000 });
        state.ApplyEvent(new KartRaceEventArgs { Kind = KartRaceEventKind.Finished, Index = 2, TotalMs = 120000, BestLapMs = 40000 });
        var newTrack = Layout();
        newTrack.TrackKey = "other";

        state.ApplyLayout(newTrack, 77);

        state.Stage.Should().Be(KartRaceStage.Waiting);
        state.StartStep.Should().Be(0);
        state.GoStep.Should().Be(0);
        state.LapTimes(2).Should().BeEmpty();
        state.Finish(2).Should().BeNull();
    }

    [Test]
    public void Race_events_track_stages_and_times_and_clear_state()
    {
        var state = new KartRaceState();
        state.ApplyLayout(Layout(), 77);
        state.ApplyEvent(new KartRaceEventArgs { Kind = KartRaceEventKind.Countdown, StartStep = 100, GoStep = 130 });
        state.Stage.Should().Be(KartRaceStage.Countdown);
        state.GoStep.Should().Be(130);
        state.StageAt(129).Should().Be(KartRaceStage.Countdown);
        state.StageAt(130).Should().Be(KartRaceStage.Racing);
        state.ApplyEvent(new KartRaceEventArgs { Kind = KartRaceEventKind.LapDone, Index = 2, LapMs = 45000 });
        state.ApplyEvent(new KartRaceEventArgs { Kind = KartRaceEventKind.Finished, Index = 2, TotalMs = 120000, BestLapMs = 40000 });
        state.LapTimes(2).Should().ContainSingle().Which.Should().Be(45000);
        state.Finish(2).Should().Be((120000u, 40000u));
        state.ApplyEvent(new KartRaceEventArgs { Kind = KartRaceEventKind.RaceOver });
        state.Stage.Should().Be(KartRaceStage.Results);
        state.Clear();
        state.Active.Should().BeFalse();
        state.Racers.Should().BeEmpty();
    }

    private static KartRacerSnapshot Racer(byte index, byte place, ushort gapCs = 0, KartRacerStatus status = KartRacerStatus.Normal)
        => new() { Index = index, Place = place, GapCs = gapCs, Status = status };

    [Test]
    public void Results_are_kept_until_a_new_track_or_clear()
    {
        var state = new KartRaceState();
        state.ApplyLayout(Layout(), 77);
        var rows = new List<KartResultEntry> { new(1, 1, true, 90_000, 29_000), new(0, 2, false, 0, 0) };
        state.ApplyEvent(new KartRaceEventArgs { Kind = KartRaceEventKind.Results, Results = rows });

        state.Results.Should().Equal(rows);
        state.Stage.Should().Be(KartRaceStage.Results);
        state.ApplyLayout(Layout(), 77);
        state.Results.Should().Equal(rows);

        var other = Layout();
        other.TrackKey = "other";
        state.ApplyLayout(other, 77);
        state.Results.Should().BeEmpty();
    }

    [Test]
    public void Standings_neighbours_drawables_and_spectators_use_snapshot_status_and_place()
    {
        var state = new KartRaceState();
        state.ApplyLayout(Layout(), 77);
        state.ApplySnapshot(new KartSnapshotArgs { ServerTick = 2, Racers = [Racer(9, 1), Racer(0, 2, 125), Racer(1, 3, status: KartRacerStatus.Dnf), Racer(2, 4)] });

        state.Standings.Select(racer => racer.Index).Should().Equal(9, 0, 1, 2);
        state.Neighbours(0).Ahead!.Index.Should().Be(9);
        state.Neighbours(0).Behind!.Index.Should().Be(2);
        state.DrawableIndexes().Should().Equal(0, 2, 9);
        state.SpectateCandidates().Should().Equal(9, 0, 2);
        state.Neighbours(8).Should().Be((null, null));
    }

    [Test]
    public void Dnf_racers_are_not_neighbours()
    {
        var state = new KartRaceState();
        state.ApplyLayout(Layout(), 77);
        state.ApplySnapshot(new KartSnapshotArgs
        {
            ServerTick = 2,
            Racers = [Racer(0, 1), Racer(1, 2, status: KartRacerStatus.Dnf), Racer(2, 3)]
        });

        var (ahead, behind) = state.Neighbours(0);

        ahead.Should().BeNull();
        behind!.Index.Should().Be(2);
    }

    [Test]
    public void A_local_racer_spectates_only_after_finishing()
    {
        var state = new KartRaceState();
        state.ApplyLayout(Layout(), 77);
        state.ApplySnapshot(new KartSnapshotArgs { ServerTick = 2, Racers = [Racer(2, 1)] });
        state.Spectating.Should().BeFalse();
        state.ApplySnapshot(new KartSnapshotArgs { ServerTick = 4, Racers = [Racer(2, 1, status: KartRacerStatus.Finished)] });
        state.LocalFinished.Should().BeTrue();
        state.Spectating.Should().BeTrue();

    }

    [Test]
    public void Finish_capture_decision_handles_event_then_snapshot_order()
    {
        var state = new KartRaceState();
        state.ApplyLayout(Layout(), 77);
        var finish = new KartRaceEventArgs { Kind = KartRaceEventKind.Finished, Index = 2 };
        var snapshot = new KartSnapshotArgs { ServerTick = 10, Racers = [Racer(2, 1, status: KartRacerStatus.Finished)] };
        var handover = new KartFinishHandover();
        var predictedBody = new KartBody { X = 12, Y = 13, Heading = 0.5 };

        handover.CaptureForFinish(state, finish, predictedBody, 1).Should().BeTrue();
        state.ApplyEvent(finish);
        handover.CaptureForFinish(state, snapshot, new KartBody { X = 90, Y = 90 }, 2).Should().BeFalse();
        handover.BodyFor(null, 1.1).Should().Be(predictedBody);
    }

    [Test]
    public void Finish_capture_decision_handles_snapshot_then_event_order()
    {
        var state = new KartRaceState();
        state.ApplyLayout(Layout(), 77);
        var finish = new KartRaceEventArgs { Kind = KartRaceEventKind.Finished, Index = 2 };
        var snapshot = new KartSnapshotArgs { ServerTick = 10, Racers = [Racer(2, 1, status: KartRacerStatus.Finished)] };
        var handover = new KartFinishHandover();
        var predictedBody = new KartBody { X = 12, Y = 13, Heading = 0.5 };

        handover.CaptureForFinish(state, snapshot, predictedBody, 1).Should().BeTrue();
        state.ApplySnapshot(snapshot);
        handover.CaptureForFinish(state, finish, new KartBody { X = 90, Y = 90 }, 2).Should().BeFalse();
        handover.BodyFor(null, 1.1).Should().Be(predictedBody);
    }

    [Test]
    public void An_onlooker_spectates()
    {
        var state = new KartRaceState();
        state.ApplyLayout(Layout(), 500);

        state.LocalIndex.Should().BeNull();
        state.Spectating.Should().BeTrue();
    }

    [Test]
    public void An_inactive_state_does_not_spectate()
    {
        var state = new KartRaceState();

        state.Spectating.Should().BeFalse();
    }

    [Test]
    public void Place_and_gap_text()
    {
        KartRaceState.PlaceText(3, 24).Should().Be("3rd / 24");
        KartRaceState.PlaceText(0, 24).Should().BeEmpty();
        KartRaceState.GapText(205, 125).Should().Be("-0.80");
        KartRaceState.GapText(125, 250).Should().Be("+1.25");
        KartRaceState.GapText(40, 40).Should().Be("+0.00");
    }

    [Test]
    public void Banners_for_the_final_lap_and_the_finish()
    {
        KartRaceState.BannerFor(new KartRaceEventArgs { Kind = KartRaceEventKind.LapDone, Index = 2, Lap = 2 }, 2, 3).Should().Be("FINAL LAP");
    }

    [Test]
    public void BannerFor_returns_null_for_a_non_final_lap()
    {
        KartRaceState.BannerFor(new KartRaceEventArgs { Kind = KartRaceEventKind.LapDone, Index = 2, Lap = 1 }, 2, 3).Should().BeNull();
    }

    [Test]
    public void BannerFor_returns_null_for_another_racer()
    {
        KartRaceState.BannerFor(new KartRaceEventArgs { Kind = KartRaceEventKind.LapDone, Index = 0, Lap = 2 }, 2, 3).Should().BeNull();
    }

    [Test]
    public void BannerFor_returns_null_for_a_one_lap_race()
    {
        KartRaceState.BannerFor(new KartRaceEventArgs { Kind = KartRaceEventKind.LapDone, Index = 2, Lap = 1 }, 2, 1).Should().BeNull();
    }

    [Test]
    public void BannerFor_returns_null_without_a_local_index()
    {
        KartRaceState.BannerFor(new KartRaceEventArgs { Kind = KartRaceEventKind.Finished, Index = 2 }, null, 3).Should().BeNull();
    }

    [Test]
    public void Banners_for_finished_and_banner_durations()
    {
        KartRaceState.BannerFor(new KartRaceEventArgs { Kind = KartRaceEventKind.Finished, Index = 2 }, 2, 3).Should().Be("FINISHED");
        KartRaceState.BannerFor(new KartRaceEventArgs { Kind = KartRaceEventKind.Finished, Index = 0 }, 2, 3).Should().BeNull();
        KartRaceState.BannerSeconds("FINISHED").Should().Be(3);
        KartRaceState.BannerSeconds("FINAL LAP").Should().Be(2);
    }
}
