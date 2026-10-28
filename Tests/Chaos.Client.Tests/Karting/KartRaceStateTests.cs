#region
using Chaos.DarkAges.Karting;
using Chaos.Client.ViewModel;
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
    public void Snapshots_interpolate_position_and_short_heading_path()
    {
        var state = new KartRaceState();
        state.ApplyLayout(Layout(), 77);
        state.ApplySnapshot(new KartSnapshotArgs { ServerTick = 1, Racers = [new KartRacerSnapshot { Index = 2, X = 10, Y = 20, Heading = 6.2 }] }, 2.0);
        state.ApplySnapshot(new KartSnapshotArgs { ServerTick = 2, Racers = [new KartRacerSnapshot { Index = 2, X = 20, Y = 40, Heading = 0.1 }] }, 2.0 + 1.0 / 15);
        var middle = state.Interpolated(2, 2.0 + 1.0 / 15 + 1.0 / 30)!.Value;
        middle.X.Should().BeApproximately(15, 1e-9);
        middle.Y.Should().BeApproximately(30, 1e-9);
        (middle.Heading > 2 * Math.PI || middle.Heading < 0.2).Should().BeTrue();
        state.Interpolated(2, 2.0 + 2.0 / 15)!.Value.X.Should().BeApproximately(20, 1e-9);
        state.LatestTick.Should().Be(2);
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
}
