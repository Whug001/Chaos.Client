#region
using Chaos.Client.Systems.Karting;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Karting;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public sealed class KartHazardVisualsTests
{
    private static KartTimedHazard Vent(int offset = 0) => new(2, 2, 4, 4, 180, 30, 30, offset);

    //formatter:off
    [Test]
    [Arguments(90u, KartHazardPhase.Dormant)]
    [Arguments(219u, KartHazardPhase.Dormant)]
    [Arguments(220u, KartHazardPhase.Warning)]
    [Arguments(249u, KartHazardPhase.Warning)]
    [Arguments(250u, KartHazardPhase.Active)]
    [Arguments(279u, KartHazardPhase.Active)]
    [Arguments(280u, KartHazardPhase.Dormant)]
    //formatter:on
    public void Visuals_use_the_same_go_relative_phase_as_the_server(uint clock, KartHazardPhase expected)
        => KartHazardVisuals.Phase(Vent(), clock, 100).Should().Be(expected);

    [Test]
    public void An_unknown_go_tick_stays_dormant_even_with_an_offset()
    {
        KartHazardVisuals.Phase(Vent(120), 150, null).Should().Be(KartHazardPhase.Dormant);
        KartHazardVisuals.Phase(Vent(120), 90, 100).Should().Be(KartHazardPhase.Dormant);
    }

    [Test]
    public void A_late_layout_and_a_resync_keep_the_clock_and_replace_hazards()
    {
        var state = new KartRaceState();
        var layout = new KartTrackLayoutArgs
        {
            TrackKey = "caldera", Width = 10, Height = 10,
            Runs = [new KartSurfaceRun(SurfaceKind.Road, 100)],
            Stats = new KartStats().ToValues().ToList(),
            Racers = [new KartRacerInfo(2, 77, "Me", 0)],
            Hazards = [Vent()]
        };
        state.ApplyLayout(layout, 77);
        state.ApplyEvent(new KartRaceEventArgs { Kind = KartRaceEventKind.Countdown, StartStep = 10, GoStep = 100 });
        state.ApplySnapshot(new KartSnapshotArgs { ServerTick = 250 });
        KartHazardVisuals.Phase(state.Hazards[0], state.LatestTick, state.GoStep).Should().Be(KartHazardPhase.Active);
        layout.Hazards = [Vent(), Vent(60)];
        state.ApplyLayout(layout, 77);
        state.ApplySnapshot(new KartSnapshotArgs { ServerTick = 249 });
        state.Hazards.Should().HaveCount(2);
        KartHazardVisuals.Phase(state.Hazards[0], state.LatestTick, state.GoStep).Should().Be(KartHazardPhase.Warning);
        state.ApplySnapshot(new KartSnapshotArgs { ServerTick = 250 });
        KartHazardVisuals.Phase(state.Hazards[0], state.LatestTick, state.GoStep).Should().Be(KartHazardPhase.Active);
        state.IsLocalGloamingHit(new KartRaceEventArgs { Kind = KartRaceEventKind.HazardHit, Index = 2 }).Should().BeFalse();

        state.Clear();
        state.Hazards.Should().BeEmpty();
    }
}
