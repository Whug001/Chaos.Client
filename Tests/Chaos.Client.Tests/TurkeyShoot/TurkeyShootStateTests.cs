using Chaos.Client.Controls.World.Popups.TurkeyShoot;
using Chaos.DarkAges.Definitions;
using Chaos.DarkAges.TurkeyShoot;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using static Chaos.DarkAges.TurkeyShoot.TurkeyShootRules;

namespace Chaos.Client.Tests.TurkeyShoot;

public sealed class TurkeyShootStateTests
{
    private const int SEED = 777;

    private static TurkeyShootState Opened(int hunters = 2)
    {
        var state = new TurkeyShootState();

        state.Open(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.Open,
                RoundId = 5,
                Roster = Enumerable.Range(0, hunters)
                                   .Select(slot => new TurkeyShootHunterEntry { Slot = (byte)slot, Name = $"Hunter{slot}" })
                                   .ToList(),
                YourSlot = 0,
                FlockCount = FLOCK_COUNT
            });

        return state;
    }

    private static TurkeyShootCue StartFlock(TurkeyShootState state, int flockIndex = 0, int hunters = 2)
        => state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.FlockStart,
                FlockIndex = (byte)flockIndex,
                Seed = SEED,
                HuntersAtStart = (byte)hunters,
                StartsInMs = LEAD_MS
            });

    private static TurkeyShootState Flying()
    {
        var state = Opened();
        StartFlock(state);
        state.Tick(LEAD_MS + state.Flights.Min(flight => flight.TakeoffDelayMs) + 100);

        return state;
    }

    [Test]
    public async Task Flock_start_plans_the_same_flights_as_the_server()
    {
        var state = Opened();

        StartFlock(state, 3, 2);

        state.Flights.Should().Equal(TurkeyFlightPlanner.Plan(SEED, 3, 2));
        state.BoltsLeft.Should().Be(BOLTS_PER_FLOCK);
        state.Title.Should().Be("Harvest Turkey Shoot - Flock 4 of 10");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Clock_starts_after_the_lead()
    {
        var state = Opened();
        StartFlock(state);

        state.Tick(LEAD_MS - 1);
        state.ClockRunning.Should().BeFalse();

        state.Tick(5);
        state.ClockRunning.Should().BeTrue();
        state.FlockClockMs.Should().Be(4);

        state.Tick(100);
        state.FlockClockMs.Should().Be(104);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Shot_spends_a_bolt_and_waits_for_the_reload()
    {
        var state = Flying();

        var first = state.TryShoot(100, 100);

        first.Should().NotBeNull();
        first!.Type.Should().Be(TurkeyShootActionType.Shoot);
        first.FlockIndex.Should().Be(0);
        first.FlockTimeMs.Should().Be(state.FlockClockMs);
        first.AimX.Should().Be(100);
        first.AimY.Should().Be(100);
        state.BoltsLeft.Should().Be(BOLTS_PER_FLOCK - 1);

        state.TryShoot(100, 100).Should().BeNull();
        state.Tick(RELOAD_MS);
        state.TryShoot(100, 100).Should().NotBeNull();
        state.Tick(RELOAD_MS);
        state.TryShoot(100, 100).Should().NotBeNull();
        state.Tick(RELOAD_MS);
        state.TryShoot(100, 100).Should().BeNull();
        state.BoltsLeft.Should().Be(0);

        await Task.CompletedTask;
    }

    //formatter:off
    [Test]
    [Arguments(-1, 10)]
    [Arguments(384, 10)]
    [Arguments(10, -1)]
    [Arguments(10, 216)]
    //formatter:on
    public async Task Shot_outside_the_field_is_refused(int x, int y)
    {
        var state = Flying();

        state.TryShoot(x, y).Should().BeNull();
        state.BoltsLeft.Should().Be(BOLTS_PER_FLOCK);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Shot_before_the_clock_or_after_the_flock_is_refused()
    {
        var state = Opened();
        StartFlock(state);

        state.TryShoot(100, 100).Should().BeNull();

        state.Tick(LEAD_MS + 2_000);
        state.Apply(new TurkeyShootDisplayArgs { Type = TurkeyShootDisplayType.FlockOver, FlockIndex = 0, EscapedCount = 1 });

        state.TryShoot(100, 100).Should().BeNull();
        state.BoltsLeft.Should().Be(BOLTS_PER_FLOCK);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Shot_after_every_turkey_is_gone_is_refused()
    {
        var state = Opened();
        StartFlock(state);
        state.Tick(LEAD_MS + state.Flights.Max(flight => flight.EndMs) + 1);

        state.TryShoot(100, 100).Should().BeNull();
        state.BoltsLeft.Should().Be(BOLTS_PER_FLOCK);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hit_turkey_puffs_then_falls()
    {
        var state = Flying();

        TurkeyFlight? target = null;

        for (var guard = 0; guard < 60; guard++)
        {
            target = state.Flights.FirstOrDefault(flight => flight.TryGetPosition(state.FlockClockMs, out var at) && (at.Y <= 160));

            if (target is not null)
                break;

            state.Tick(100);
        }

        target.Should().NotBeNull();

        var flight = target!;
        flight.TryGetPosition(state.FlockClockMs, out var hitAt);

        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.ShotResult,
                Slot = 1,
                FlockIndex = 0,
                TurkeyIndex = (sbyte)flight.Index,
                Points = (byte)flight.Points,
                NewScore = 7
            });

        cue.Should().Be(flight.Golden ? TurkeyShootCue.GoldenHit : TurkeyShootCue.TurkeyHit);
        state.ScoreOf(1).Should().Be(7);

        var puff = state.VisibleTurkeys().Single(turkey => turkey.Index == flight.Index);
        puff.Frame.Should().Be(TurkeyShootState.HIT_FRAME);
        puff.Y.Should().BeApproximately(hitAt.Y, 0.001);

        state.Tick(TurkeyShootState.HIT_PUFF_MS + 500);

        var falling = state.VisibleTurkeys().Single(turkey => turkey.Index == flight.Index);
        falling.Frame.Should().Be(TurkeyShootState.FALL_FRAME);
        falling.X.Should().BeApproximately(hitAt.X, 0.001);
        falling.Y.Should().BeApproximately(hitAt.Y + TurkeyShootState.FALL_SPEED * 0.5, 0.001);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Miss_shows_a_puff_briefly()
    {
        var state = Flying();

        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.ShotResult,
                Slot = 0,
                FlockIndex = 0,
                AimX = 50,
                AimY = 60,
                TurkeyIndex = -1,
                NewScore = 0
            });

        cue.Should().Be(TurkeyShootCue.Miss);
        state.VisibleMisses().Should().Equal(new[] { ((short)50, (short)60) });

        state.Tick(TurkeyShootState.MISS_PUFF_MS);
        state.VisibleMisses().Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Flock_over_with_no_hits_means_the_birds_escaped()
    {
        var state = Opened();
        StartFlock(state);
        state.Tick(LEAD_MS);

        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.FlockOver,
                FlockIndex = 0,
                EscapedCount = (byte)state.Flights.Count
            });

        cue.Should().Be(TurkeyShootCue.FlockEscaped);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Flock_over_with_a_hit_and_an_escape_means_the_flock_cleared()
    {
        var state = Flying();
        var flight = state.Flights.First(each => each.TryGetPosition(state.FlockClockMs, out _));

        state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.ShotResult,
                Slot = 0,
                FlockIndex = 0,
                TurkeyIndex = (sbyte)flight.Index,
                Points = (byte)flight.Points,
                NewScore = 1
            });

        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.FlockOver,
                FlockIndex = 0,
                EscapedCount = (byte)(state.Flights.Count - 1)
            });

        cue.Should().Be(TurkeyShootCue.FlockCleared);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Results_name_one_winner_with_points()
    {
        var state = Opened();

        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.Results,
                Ranking = [new TurkeyShootStanding { Slot = 1, Points = 12 }, new TurkeyShootStanding { Slot = 0, Points = 4 }],
                WinnerSlots = [1],
                CountsAsWin = true
            });

        cue.Should().Be(TurkeyShootCue.Results);
        state.ResultHeadline().Should().Be("Hunter1 wins with 12 points!");
        state.ResultNote().Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Results_for_a_shared_win_and_for_no_winner()
    {
        var shared = Opened(3);
        shared.Apply(new TurkeyShootDisplayArgs { Type = TurkeyShootDisplayType.Results, WinnerSlots = [0, 2], CountsAsWin = true });

        var none = Opened(1);
        none.Apply(new TurkeyShootDisplayArgs { Type = TurkeyShootDisplayType.Results, WinnerSlots = [], CountsAsWin = false });

        shared.ResultHeadline().Should().Be("A shared win: Hunter0 and Hunter2!");
        none.ResultHeadline().Should().Be("No winner this time.");
        none.ResultNote().Should().Be("A lone hunt earns prize points, not wins.");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Duplicate_flock_start_for_the_running_flock_is_ignored()
    {
        var state = Flying();
        state.TryShoot(100, 100);

        var flight = state.Flights.First(each => each.TryGetPosition(state.FlockClockMs, out _));

        state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.ShotResult,
                Slot = 0,
                FlockIndex = 0,
                TurkeyIndex = (sbyte)flight.Index,
                Points = (byte)flight.Points,
                NewScore = 1
            });
        state.Tick(100);

        var bolts = state.BoltsLeft;
        var clock = state.FlockClockMs;
        var before = state.VisibleTurkeys().Single(turkey => turkey.Index == flight.Index);

        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.FlockStart,
                FlockIndex = 0,
                Seed = SEED + 1,
                HuntersAtStart = 2,
                StartsInMs = LEAD_MS
            });

        cue.Should().Be(TurkeyShootCue.None);
        state.FlockIndex.Should().Be(0);
        state.BoltsLeft.Should().Be(bolts);
        state.FlockClockMs.Should().Be(clock);
        state.ClockRunning.Should().BeTrue();
        state.Flights.Should().Equal(TurkeyFlightPlanner.Plan(SEED, 0, 2));
        state.VisibleTurkeys().Single(turkey => turkey.Index == flight.Index).Should().Be(before);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Flock_start_with_an_older_index_is_ignored()
    {
        var state = Opened();
        StartFlock(state, 1);

        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.FlockStart,
                FlockIndex = 0,
                Seed = SEED + 1,
                HuntersAtStart = 2,
                StartsInMs = LEAD_MS
            });

        cue.Should().Be(TurkeyShootCue.None);
        state.FlockIndex.Should().Be(1);
        state.Flights.Should().Equal(TurkeyFlightPlanner.Plan(SEED, 1, 2));
        state.BoltsLeft.Should().Be(BOLTS_PER_FLOCK);
        state.Title.Should().Be("Harvest Turkey Shoot - Flock 2 of 10");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Flock_start_after_results_is_ignored()
    {
        var state = Opened();
        StartFlock(state);
        state.Tick(LEAD_MS);

        state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.Results,
                Ranking = [new TurkeyShootStanding { Slot = 1, Points = 12 }, new TurkeyShootStanding { Slot = 0, Points = 4 }],
                WinnerSlots = [1],
                CountsAsWin = true
            });

        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.FlockStart,
                FlockIndex = 1,
                Seed = SEED,
                HuntersAtStart = 2,
                StartsInMs = LEAD_MS
            });

        cue.Should().Be(TurkeyShootCue.None);
        state.FlockIndex.Should().Be(0);
        state.FlockOver.Should().BeTrue();
        state.Results.Should().NotBeNull();
        state.ResultHeadline().Should().Be("Hunter1 wins with 12 points!");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Open_mid_round_resets_state_and_accepts_flock_zero()
    {
        var state = Flying();
        state.TryShoot(100, 100);

        state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.ShotResult,
                Slot = 0,
                FlockIndex = 0,
                TurkeyIndex = -1,
                AimX = 50,
                AimY = 60,
                NewScore = 4
            });

        state.Open(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.Open,
                RoundId = 6,
                Roster = Enumerable.Range(0, 2)
                                   .Select(slot => new TurkeyShootHunterEntry { Slot = (byte)slot, Name = $"Hunter{slot}" })
                                   .ToList(),
                YourSlot = 0,
                FlockCount = FLOCK_COUNT
            });

        state.FlockIndex.Should().Be(-1);
        state.BoltsLeft.Should().Be(0);
        state.ScoreOf(0).Should().Be(0);
        state.Flights.Should().BeEmpty();
        state.Title.Should().Be("Harvest Turkey Shoot");

        var cue = StartFlock(state);

        cue.Should().BeOneOf(TurkeyShootCue.FlockTakeoff, TurkeyShootCue.GoldenTakeoff);
        state.FlockIndex.Should().Be(0);
        state.BoltsLeft.Should().Be(BOLTS_PER_FLOCK);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Shot_result_for_a_stale_flock_updates_the_score_but_changes_no_flight_state()
    {
        var state = Opened();
        StartFlock(state, 1);
        state.Tick(LEAD_MS + 900);

        state.VisibleTurkeys().Should().NotBeEmpty();

        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.ShotResult,
                Slot = 1,
                FlockIndex = 0,
                TurkeyIndex = 0,
                AimX = 50,
                AimY = 60,
                NewScore = 5
            });

        cue.Should().Be(TurkeyShootCue.None);
        state.ScoreOf(1).Should().Be(5);
        state.FlockIndex.Should().Be(1);
        state.VisibleMisses().Should().BeEmpty();
        state.VisibleTurkeys().Should().OnlyContain(turkey => (turkey.Frame != TurkeyShootState.HIT_FRAME) && (turkey.Frame != TurkeyShootState.FALL_FRAME));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Shot_result_with_an_unknown_turkey_index_gives_no_cue_and_no_puff()
    {
        var state = Flying();

        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.ShotResult,
                Slot = 0,
                FlockIndex = 0,
                TurkeyIndex = (sbyte)state.Flights.Count,
                AimX = 50,
                AimY = 60,
                NewScore = 0
            });

        cue.Should().Be(TurkeyShootCue.None);
        state.VisibleMisses().Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Duplicate_hit_on_the_same_turkey_keeps_the_first_hit_time_and_gives_no_cue()
    {
        var state = Flying();

        TurkeyFlight? target = null;

        for (var guard = 0; guard < 60; guard++)
        {
            target = state.Flights.FirstOrDefault(flight => !flight.Golden && flight.TryGetPosition(state.FlockClockMs, out var at) && (at.Y <= 160));

            if (target is not null)
                break;

            state.Tick(100);
        }

        target.Should().NotBeNull();

        var flight = target!;
        var first = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.ShotResult,
                Slot = 0,
                FlockIndex = 0,
                TurkeyIndex = (sbyte)flight.Index,
                Points = (byte)flight.Points,
                NewScore = 1
            });

        first.Should().Be(TurkeyShootCue.TurkeyHit);
        state.Tick(TurkeyShootState.HIT_PUFF_MS + 500);

        var before = state.VisibleTurkeys().Single(turkey => turkey.Index == flight.Index);

        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.ShotResult,
                Slot = 1,
                FlockIndex = 0,
                TurkeyIndex = (sbyte)flight.Index,
                Points = (byte)flight.Points,
                NewScore = 2
            });

        cue.Should().Be(TurkeyShootCue.None);
        state.ScoreOf(1).Should().Be(2);
        state.VisibleTurkeys().Single(turkey => turkey.Index == flight.Index).Should().Be(before);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hunter_left_for_your_own_slot_leaves_the_next_flock_without_bolts()
    {
        var state = Flying();

        state.Apply(new TurkeyShootDisplayArgs { Type = TurkeyShootDisplayType.HunterLeft, Slot = 0 });
        StartFlock(state, 1);
        state.Tick(LEAD_MS + 100);

        state.BoltsLeft.Should().Be(0);
        state.TryShoot(100, 100).Should().BeNull();
        state.BoltsLeft.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Flock_over_with_the_wrong_flock_index_changes_nothing()
    {
        var state = Opened();
        StartFlock(state);
        state.Tick(LEAD_MS);

        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.FlockOver,
                FlockIndex = 1,
                EscapedCount = (byte)state.Flights.Count
            });

        cue.Should().Be(TurkeyShootCue.None);
        state.FlockOver.Should().BeFalse();
        state.ClockRunning.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Starting_flock_with_a_golden_turkey_gives_the_golden_takeoff_cue()
    {
        var seed = SEED;

        while (!TurkeyFlightPlanner.Plan(seed, 0, 2).Any(flight => flight.Golden))
            seed++;

        var state = Opened();
        var cue = state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.FlockStart,
                FlockIndex = 0,
                Seed = seed,
                HuntersAtStart = 2,
                StartsInMs = LEAD_MS
            });

        cue.Should().Be(TurkeyShootCue.GoldenTakeoff);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Standings_follow_roster_order_while_the_round_runs()
    {
        var state = Opened(3);

        var standings = state.Standings();

        standings.Select(row => row.Slot).Should().Equal(new[] { 0, 1, 2 });
        standings.Select(row => row.Name).Should().Equal(new[] { "Hunter0", "Hunter1", "Hunter2" });
        standings.Should().OnlyContain(row => row.Place == null);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Standings_rank_hunters_by_points_after_results()
    {
        var state = Opened();

        state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.Results,
                Ranking = [new TurkeyShootStanding { Slot = 1, Points = 12 }, new TurkeyShootStanding { Slot = 0, Points = 4 }],
                WinnerSlots = [1],
                CountsAsWin = true
            });

        var standings = state.Standings();

        standings.Select(row => row.Slot).Should().Equal(new[] { 1, 0 });
        standings.Select(row => row.Points).Should().Equal(new[] { 12, 4 });
        standings.Select(row => row.Place).Should().Equal(new int?[] { 1, 2 });

        await Task.CompletedTask;
    }

    [Test]
    public async Task Standings_share_one_place_for_a_tie()
    {
        var state = Opened(3);

        state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.Results,
                Ranking =
                [
                    new TurkeyShootStanding { Slot = 2, Points = 12 },
                    new TurkeyShootStanding { Slot = 0, Points = 12 },
                    new TurkeyShootStanding { Slot = 1, Points = 9 }
                ],
                WinnerSlots = [2, 0],
                CountsAsWin = true
            });

        var standings = state.Standings();

        standings.Select(row => row.Slot).Should().Equal(new[] { 2, 0, 1 });
        standings.Select(row => row.Place).Should().Equal(new int?[] { 1, 1, 3 });

        await Task.CompletedTask;
    }

    [Test]
    public async Task Standings_keep_a_hunter_who_left()
    {
        var state = Opened();

        state.Apply(new TurkeyShootDisplayArgs { Type = TurkeyShootDisplayType.HunterLeft, Slot = 1 });
        state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.Results,
                Ranking = [new TurkeyShootStanding { Slot = 0, Points = 5 }, new TurkeyShootStanding { Slot = 1, Points = 3 }],
                WinnerSlots = [0],
                CountsAsWin = true
            });

        var standings = state.Standings();

        standings.Should().HaveCount(2);
        standings.Single(row => row.Slot == 1).Gone.Should().BeTrue();
        state.HasLeft(1).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Results_for_a_solo_round_score_points_instead_of_a_win()
    {
        var state = Opened(1);

        state.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.Results,
                Ranking = [new TurkeyShootStanding { Slot = 0, Points = 7 }],
                WinnerSlots = [0],
                CountsAsWin = false
            });

        state.ResultHeadline().Should().Be("Hunter0 scored 7 points.");
        state.ResultNote().Should().Be("A lone hunt earns prize points, not wins.");

        var single = Opened(1);

        single.Apply(
            new TurkeyShootDisplayArgs
            {
                Type = TurkeyShootDisplayType.Results,
                Ranking = [new TurkeyShootStanding { Slot = 0, Points = 1 }],
                WinnerSlots = [0],
                CountsAsWin = false
            });

        single.ResultHeadline().Should().Be("Hunter0 scored 1 point.");

        await Task.CompletedTask;
    }
}
