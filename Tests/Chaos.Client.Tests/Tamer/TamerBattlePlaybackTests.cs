using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TamerBattlePlaybackTests
{
    private static TamerBattleFight Fight(
        List<TamerBattleMonsterView> monsters,
        List<TamerBattleEventView> events,
        TamerBattleViewerSide viewer = TamerBattleViewerSide.Attacker,
        ushort endTick = 600,
        TamerBattleSide winner = TamerBattleSide.Attacker,
        TamerBattleEndReason reason = TamerBattleEndReason.WipeOut,
        List<TamerBattleNameEntry>? names = null)
        => new(
            7, "Attacker", "Defender", viewer, "Test fight", monsters,
            names ?? DefaultNames(), events, winner, reason, endTick, []);

    private static List<TamerBattleNameEntry> DefaultNames()
        => [
            new("rake", "Rake"),
            new("shell_wall", "Shell wall"),
            new("bite_strike", "Bite strike"),
            new("ward", "Ward"),
            new("thorns", "Thorns"),
            new("thick_hide", "Thick hide"),
            new("fire_2", "Fire bond")
        ];

    private static TamerBattleMonsterView Monster(byte id, TamerBattleSide side = TamerBattleSide.Attacker, int max = 100, byte range = 1, byte col = 3, byte row = 0)
        => new(id, side, "wolf", id == 1 ? "Alpha" : "Beta", 10, 5, TamerWireElement.Fire, max, col, row, range);

    private static List<TamerBattleMonsterView> Duo()
        => [Monster(1), Monster(6, TamerBattleSide.Defender)];

    private static TamerBattleEventView Ev(ushort tick, TamerBattleEventType type, byte unit, byte target, int amount = 0, sbyte x = -1, sbyte y = -1, sbyte energy = -1, string key = "")
        => new(tick, type, unit, target, amount, x, y, energy, key);

    private static void AdvanceTo(TamerBattlePlayback playback, double ticks)
    {
        playback.Advance(TimeSpan.FromSeconds(ticks / 10.0));
    }

    private static TamerBattleUnitState Unit(TamerBattlePlayback playback, byte id)
        => playback.Units.Single(u => u.Id == id);

    [Test]
    public void Spawn_sets_the_board_cell_and_health_from_the_events()
    {
        var fight = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(0, TamerBattleEventType.Spawn, 6, 0, 100, 3, 2, -1, "wolf")
        ]);

        var playback = new TamerBattlePlayback(fight);

        Unit(playback, 1).EngineColumn.Should().Be(3);
        Unit(playback, 1).EngineRow.Should().Be(3);
        Unit(playback, 1).Health.Should().Be(100);
        Unit(playback, 6).EngineColumn.Should().Be(3);
        Unit(playback, 6).EngineRow.Should().Be(2);
    }

    [Test]
    public void Spawn_wins_over_the_start_cells_and_the_start_is_only_a_fallback()
    {
        var monsters = new List<TamerBattleMonsterView>
        {
            Monster(1, TamerBattleSide.Attacker, 100, 1, 0, 0),
            Monster(6, TamerBattleSide.Defender, 100, 1, 0, 0)
        };

        var withSpawn = Fight(monsters, [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 5, 5, -1, "wolf"),
            Ev(0, TamerBattleEventType.Spawn, 6, 0, 100, 1, 0, -1, "wolf")
        ]);
        var fallback = Fight(monsters, []);

        var playback = new TamerBattlePlayback(withSpawn);
        Unit(playback, 1).EngineColumn.Should().Be(5);
        Unit(playback, 1).EngineRow.Should().Be(5);
        Unit(playback, 6).EngineColumn.Should().Be(1);

        var fallbackPlayback = new TamerBattlePlayback(fallback);
        Unit(fallbackPlayback, 1).EngineColumn.Should().Be(0);
        Unit(fallbackPlayback, 1).EngineRow.Should().Be(3);
        Unit(fallbackPlayback, 6).EngineColumn.Should().Be(6);
        Unit(fallbackPlayback, 6).EngineRow.Should().Be(2);
    }

    [Test]
    public void Step_slides_between_cells_over_the_walk_time()
    {
        var fight = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(0, TamerBattleEventType.Spawn, 6, 0, 100, 3, 0, -1, "wolf"),
            Ev(10, TamerBattleEventType.Step, 1, 0, 0, 3, 2, -1)
        ]);

        var playback = new TamerBattlePlayback(fight);
        AdvanceTo(playback, 12);

        var unit = Unit(playback, 1);
        unit.Action.Should().Be(TamerBattleAction.Walk);
        unit.ActionProgress.Should().BeApproximately(0.5, 0.001);
        unit.DrawX.Should().BeApproximately(3, 0.001);
        unit.DrawY.Should().BeApproximately(2.5, 0.001);
        unit.EngineRow.Should().Be(2);
        unit.Facing.Should().Be(Direction.Up);
    }

    [Test]
    public void Leap_jumps_faster_than_a_step()
    {
        var fight = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(0, TamerBattleEventType.Spawn, 6, 0, 100, 3, 0, -1, "wolf"),
            Ev(10, TamerBattleEventType.Leap, 1, 6, 0, 3, 1, -1)
        ]);

        var playback = new TamerBattlePlayback(fight);
        AdvanceTo(playback, 11);

        var unit = Unit(playback, 1);
        unit.Action.Should().Be(TamerBattleAction.Leap);
        unit.ActionProgress.Should().BeApproximately(0.5, 0.001);
        unit.DrawY.Should().BeApproximately(2.0, 0.001);
    }

    [Test]
    public void Attack_sets_the_attackers_energy_and_faces_the_target()
    {
        var fight = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(0, TamerBattleEventType.Spawn, 6, 0, 100, 3, 1, -1, "wolf"),
            Ev(10, TamerBattleEventType.Attack, 1, 6, 0, -1, -1, 25)
        ]);

        var playback = new TamerBattlePlayback(fight);
        AdvanceTo(playback, 10);

        var unit = Unit(playback, 1);
        unit.Energy.Should().Be(25);
        unit.Action.Should().Be(TamerBattleAction.Attack);
        unit.Facing.Should().Be(Direction.Up);
    }

    [Test]
    public void Ability_sets_zero_energy_shows_the_name_and_logs()
    {
        var fight = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(0, TamerBattleEventType.Spawn, 6, 0, 100, 3, 1, -1, "wolf"),
            Ev(10, TamerBattleEventType.Ability, 1, 6, 0, -1, -1, 0, "rake")
        ]);

        var playback = new TamerBattlePlayback(fight);
        AdvanceTo(playback, 10);

        var unit = Unit(playback, 1);
        unit.Energy.Should().Be(0);
        unit.AbilityName.Should().Be("Rake");
        playback.LogLines.Should().ContainSingle().Which.Should().Be("Alpha uses Rake");
    }

    [Test]
    public void Damage_reduces_health_sets_the_targets_energy_and_floats_red()
    {
        var fight = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(0, TamerBattleEventType.Spawn, 6, 0, 100, 3, 1, -1, "wolf"),
            Ev(10, TamerBattleEventType.Attack, 1, 6, 0, -1, -1, 15),
            Ev(10, TamerBattleEventType.Damage, 1, 6, 30, -1, -1, 20)
        ]);

        var playback = new TamerBattlePlayback(fight);
        AdvanceTo(playback, 10);

        Unit(playback, 6).Health.Should().Be(70);
        Unit(playback, 6).Energy.Should().Be(20);
        playback.Floaters.Should().ContainSingle().Which.Kind.Should().Be(TamerBattleFloaterKind.Damage);
        playback.Floaters.Single().Amount.Should().Be(30);
    }

    [Test]
    public void Health_never_goes_below_zero_or_above_the_maximum()
    {
        var over = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(10, TamerBattleEventType.Damage, 6, 1, 500, -1, -1, 5)
        ]);
        var overPlayback = new TamerBattlePlayback(over);
        AdvanceTo(overPlayback, 10);
        Unit(overPlayback, 1).Health.Should().Be(0);

        var heal = new List<TamerBattleMonsterView> { Monster(1, max: 100), Monster(6, TamerBattleSide.Defender) };
        var overheal = Fight(heal, [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(10, TamerBattleEventType.Damage, 6, 1, 20, -1, -1, 5),
            Ev(20, TamerBattleEventType.Heal, 1, 1, 500)
        ]);
        var healPlayback = new TamerBattlePlayback(overheal);
        AdvanceTo(healPlayback, 20);
        Unit(healPlayback, 1).Health.Should().Be(100);
    }

    [Test]
    public void Heal_raises_health_and_floats_green()
    {
        var fight = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(10, TamerBattleEventType.Damage, 6, 1, 30, -1, -1, 5),
            Ev(20, TamerBattleEventType.Heal, 1, 1, 15)
        ]);

        var playback = new TamerBattlePlayback(fight);
        AdvanceTo(playback, 20);

        Unit(playback, 1).Health.Should().Be(85);
        playback.Floaters.Should().ContainSingle().Which.Kind.Should().Be(TamerBattleFloaterKind.Heal);
    }

    [Test]
    public void Shield_absorbs_damage_before_health_and_floats_white()
    {
        var fight = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(15, TamerBattleEventType.Shield, 1, 1, 40),
            Ev(20, TamerBattleEventType.Damage, 6, 1, 30, -1, -1, 5)
        ]);

        var playback = new TamerBattlePlayback(fight);
        AdvanceTo(playback, 20);

        Unit(playback, 1).Shield.Should().Be(10);
        Unit(playback, 1).Health.Should().Be(100);
        playback.Floaters.Should().Contain(f => f.Kind == TamerBattleFloaterKind.Damage);
        playback.Floaters.Should().Contain(f => f.Kind == TamerBattleFloaterKind.Shield);
    }

    [Test]
    public void Statuses_go_on_and_off()
    {
        var one = new TamerBattlePlayback(Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(10, TamerBattleEventType.StatusOn, 0, 1, 10, -1, -1, -1, "Stun"),
            Ev(20, TamerBattleEventType.StatusOn, 0, 1, 15, -1, -1, -1, "AttackUp"),
            Ev(30, TamerBattleEventType.StatusOff, 0, 1, 0, -1, -1, -1, "Stun")
        ]));
        AdvanceTo(one, 10);
        Unit(one, 1).StatusKeys.Should().Equal("Stun");
        AdvanceTo(one, 10);
        Unit(one, 1).StatusKeys.Should().Equal("AttackUp", "Stun");
        AdvanceTo(one, 10);
        Unit(one, 1).StatusKeys.Should().Equal("AttackUp");
    }

    [Test]
    public void A_down_monster_fades_out_and_its_row_greys()
    {
        var fight = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(20, TamerBattleEventType.Down, 1, 0)
        ]);

        var playback = new TamerBattlePlayback(fight);
        AdvanceTo(playback, 22);

        var unit = Unit(playback, 1);
        unit.Alive.Should().BeFalse();
        unit.Health.Should().Be(0);
        unit.Action.Should().Be(TamerBattleAction.Fading);
        unit.ActionProgress.Should().BeGreaterThan(0).And.BeLessThan(1);

        AdvanceTo(playback, 10);
        Unit(playback, 1).Action.Should().Be(TamerBattleAction.Idle);
        playback.LogLines.Should().Contain("Alpha falls");
    }

    [Test]
    public void Trait_and_set_bonus_flash_gold_and_log()
    {
        var fight = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(10, TamerBattleEventType.Trait, 1, 0, 0, -1, -1, -1, "thorns"),
            Ev(20, TamerBattleEventType.SetBonus, 1, 0, 0, -1, -1, -1, "fire_2")
        ]);

        var playback = new TamerBattlePlayback(fight);
        AdvanceTo(playback, 11);
        Unit(playback, 1).Flash.Should().Be(TamerBattleFlash.Gold);
        AdvanceTo(playback, 4);
        Unit(playback, 1).Flash.Should().Be(TamerBattleFlash.None);
        AdvanceTo(playback, 5);
        Unit(playback, 1).Flash.Should().Be(TamerBattleFlash.Gold);
        playback.LogLines.Should().Contain("Alpha: Thorns");
        playback.LogLines.Should().Contain("Alpha gains Fire bond");
    }

    [Test]
    public void Rising_damage_is_flat_before_45_seconds_then_rises()
    {
        var fight = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(450, TamerBattleEventType.RisingDamage, 0, 0, 100)
        ], endTick: 600);

        var playback = new TamerBattlePlayback(fight);
        playback.RisingPercent.Should().Be(100);
        AdvanceTo(playback, 449);
        playback.RisingPercent.Should().Be(100);
        AdvanceTo(playback, 1);
        playback.RisingPercent.Should().Be(100);
        AdvanceTo(playback, 10);
        playback.RisingPercent.Should().Be(110);
        AdvanceTo(playback, 140);
        playback.RisingPercent.Should().Be(250);
    }

    [Test]
    public void The_board_turns_180_for_a_defender_and_the_facing_turns_with_it()
    {
        var defenderFight = Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(0, TamerBattleEventType.Spawn, 6, 0, 100, 3, 0, -1, "wolf"),
            Ev(10, TamerBattleEventType.Step, 1, 0, 0, 3, 4, -1),
            Ev(10, TamerBattleEventType.Step, 6, 0, 0, 3, 1, -1)
        ], viewer: TamerBattleViewerSide.Defender);

        var playback = new TamerBattlePlayback(defenderFight);
        AdvanceTo(playback, 14);

        playback.ViewCell(0, 0).Should().Be((6, 5));
        playback.ViewCell(6, 5).Should().Be((0, 0));
        playback.ViewCell(3, 3).Should().Be((3, 2));

        var attacker = Unit(playback, 1);
        attacker.ViewColumn.Should().Be(3);
        attacker.ViewRow.Should().Be(1);
        attacker.DrawY.Should().BeApproximately(1, 0.001);
        attacker.Facing.Should().Be(Direction.Up);

        var defender = Unit(playback, 6);
        defender.ViewColumn.Should().Be(3);
        defender.ViewRow.Should().Be(4);
        defender.Facing.Should().Be(Direction.Up);
    }

    [Test]
    public void Attacker_and_neither_see_the_engine_board_as_is()
    {
        foreach (var viewer in new[] { TamerBattleViewerSide.Attacker, TamerBattleViewerSide.None })
        {
            var playback = new TamerBattlePlayback(Fight(Duo(), [
                Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 2, 4, -1, "wolf")
            ], viewer: viewer));

            playback.ViewCell(2, 4).Should().Be((2, 4));
            Unit(playback, 1).ViewColumn.Should().Be(2);
            Unit(playback, 1).ViewRow.Should().Be(4);
            Unit(playback, 1).Facing.Should().Be(Direction.Up);
        }
    }

    [Test]
    public void Pause_freezes_the_clock()
    {
        var playback = new TamerBattlePlayback(Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(20, TamerBattleEventType.Damage, 6, 1, 30, -1, -1, 5)
        ]));

        AdvanceTo(playback, 10);
        playback.Paused = true;
        playback.Advance(TimeSpan.FromSeconds(5));

        playback.ClockTicks.Should().BeApproximately(10, 0.001);
        Unit(playback, 1).Health.Should().Be(100);
    }

    [Test]
    public void Each_speed_multiplies_the_clock_and_cycles()
    {
        var playback = new TamerBattlePlayback(Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf")
        ], endTick: 600));

        playback.Speed.Should().Be(1);
        playback.Advance(TimeSpan.FromSeconds(1));
        playback.ClockTicks.Should().BeApproximately(10, 0.001);

        playback.CycleSpeed().Should().Be(2);
        playback.Advance(TimeSpan.FromSeconds(1));
        playback.ClockTicks.Should().BeApproximately(30, 0.001);

        playback.CycleSpeed().Should().Be(4);
        playback.Advance(TimeSpan.FromSeconds(1));
        playback.ClockTicks.Should().BeApproximately(70, 0.001);

        playback.CycleSpeed().Should().Be(1);
    }

    [Test]
    public void Skip_jumps_to_the_final_state_and_a_leap_does_not_hang()
    {
        var playback = new TamerBattlePlayback(Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(10, TamerBattleEventType.Leap, 1, 6, 0, 3, 1, -1),
            Ev(20, TamerBattleEventType.Damage, 6, 1, 30, -1, -1, 5)
        ], endTick: 600));

        AdvanceTo(playback, 11);
        Unit(playback, 1).Action.Should().Be(TamerBattleAction.Leap);
        playback.Skip();

        playback.ClockTicks.Should().BeApproximately(600, 0.001);
        playback.Finished.Should().BeTrue();
        var unit = Unit(playback, 1);
        unit.EngineRow.Should().Be(1);
        unit.Action.Should().Be(TamerBattleAction.Idle);
        unit.Health.Should().Be(70);
    }

    [Test]
    public void Replay_starts_again_from_tick_zero_after_the_end()
    {
        var playback = new TamerBattlePlayback(Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(20, TamerBattleEventType.Damage, 6, 1, 30, -1, -1, 5)
        ], endTick: 100));

        playback.Skip();
        playback.Finished.Should().BeTrue();

        playback.Replay();

        playback.ClockTicks.Should().Be(0);
        playback.Finished.Should().BeFalse();
        playback.Paused.Should().BeFalse();
        Unit(playback, 1).Health.Should().Be(100);
        Unit(playback, 1).EngineRow.Should().Be(3);
    }

    [Test]
    public void State_always_equals_a_fresh_playback_at_the_same_clock()
    {
        List<TamerBattleEventView> Events()
            => [
                Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
                Ev(0, TamerBattleEventType.Spawn, 6, 0, 100, 3, 0, -1, "wolf"),
                Ev(10, TamerBattleEventType.Step, 1, 0, 0, 3, 2, -1),
                Ev(10, TamerBattleEventType.Attack, 1, 6, 0, -1, -1, 20),
                Ev(10, TamerBattleEventType.Damage, 1, 6, 25, -1, -1, 10),
                Ev(30, TamerBattleEventType.StatusOn, 0, 1, 10, -1, -1, -1, "Stun")
            ];

        var busy = new TamerBattlePlayback(Fight(Duo(), Events(), endTick: 600));
        AdvanceTo(busy, 12);
        busy.CycleSpeed();
        AdvanceTo(busy, 20);
        busy.Skip();
        busy.Replay();
        busy.CycleSpeed();
        busy.CycleSpeed();
        AdvanceTo(busy, 35);

        var fresh = new TamerBattlePlayback(Fight(Duo(), Events(), endTick: 600));
        AdvanceTo(fresh, 35);

        busy.ClockTicks.Should().BeApproximately(fresh.ClockTicks, 0.001);

        foreach (var id in new byte[] { 1, 6 })
        {
            var a = Unit(busy, id);
            var b = Unit(fresh, id);
            a.Health.Should().Be(b.Health);
            a.EngineColumn.Should().Be(b.EngineColumn);
            a.EngineRow.Should().Be(b.EngineRow);
            a.Energy.Should().Be(b.Energy);
            a.StatusKeys.Should().Equal(b.StatusKeys);
        }

        busy.LogLines.Should().Equal(fresh.LogLines);
        busy.RisingPercent.Should().Be(fresh.RisingPercent);
    }

    [Test]
    public void A_frame_that_takes_two_seconds_advances_two_seconds()
    {
        var playback = new TamerBattlePlayback(Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(20, TamerBattleEventType.Damage, 6, 1, 30, -1, -1, 5)
        ]));

        playback.Advance(TimeSpan.FromSeconds(2));

        playback.ClockTicks.Should().BeApproximately(20, 0.001);
        Unit(playback, 1).Health.Should().Be(70);
    }

    [Test]
    public void Result_text_matches_the_viewer_side_and_reason()
    {
        var attackerWin = new TamerBattlePlayback(Fight(Duo(), [], viewer: TamerBattleViewerSide.Attacker, winner: TamerBattleSide.Attacker, reason: TamerBattleEndReason.WipeOut));
        attackerWin.ResultTitle.Should().Be("Victory");
        attackerWin.ResultReason.Should().Be("All foes down");

        var attackerLose = new TamerBattlePlayback(Fight(Duo(), [], viewer: TamerBattleViewerSide.Attacker, winner: TamerBattleSide.Defender, reason: TamerBattleEndReason.Time));
        attackerLose.ResultTitle.Should().Be("Defeat");
        attackerLose.ResultReason.Should().Be("Time: decided on remaining health");

        var defenderWin = new TamerBattlePlayback(Fight(Duo(), [], viewer: TamerBattleViewerSide.Defender, winner: TamerBattleSide.Defender, reason: TamerBattleEndReason.WipeOut));
        defenderWin.ResultTitle.Should().Be("Victory");

        var defenderLose = new TamerBattlePlayback(Fight(Duo(), [], viewer: TamerBattleViewerSide.Defender, winner: TamerBattleSide.Attacker, reason: TamerBattleEndReason.Time));
        defenderLose.ResultTitle.Should().Be("Defeat");

        var neitherAttacker = new TamerBattlePlayback(Fight(Duo(), [], viewer: TamerBattleViewerSide.None, winner: TamerBattleSide.Attacker));
        neitherAttacker.ResultTitle.Should().Be("Attacker wins");

        var neitherDefender = new TamerBattlePlayback(Fight(Duo(), [], viewer: TamerBattleViewerSide.None, winner: TamerBattleSide.Defender));
        neitherDefender.ResultTitle.Should().Be("Defender wins");
    }

    [Test]
    public void The_log_holds_the_last_four_lines_newest_at_the_bottom()
    {
        var playback = new TamerBattlePlayback(Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(10, TamerBattleEventType.Ability, 1, 6, 0, -1, -1, 0, "rake"),
            Ev(20, TamerBattleEventType.Trait, 1, 0, 0, -1, -1, -1, "thorns"),
            Ev(30, TamerBattleEventType.SetBonus, 1, 0, 0, -1, -1, -1, "fire_2"),
            Ev(40, TamerBattleEventType.Down, 1, 0),
            Ev(50, TamerBattleEventType.Trait, 6, 0, 0, -1, -1, -1, "thorns")
        ]));

        AdvanceTo(playback, 50);

        playback.LogLines.Should().HaveCount(4);
        playback.LogLines[0].Should().Be("Alpha: Thorns");
        playback.LogLines[1].Should().Be("Alpha gains Fire bond");
        playback.LogLines[2].Should().Be("Alpha falls");
        playback.LogLines[3].Should().Be("Beta: Thorns");
    }

    [Test]
    public void Floaters_carry_the_number_kind_cell_and_age()
    {
        var playback = new TamerBattlePlayback(Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(10, TamerBattleEventType.Damage, 6, 1, 25, -1, -1, 5)
        ]));

        AdvanceTo(playback, 10);
        var atSpawn = playback.Floaters.Single();
        atSpawn.Amount.Should().Be(25);
        atSpawn.Kind.Should().Be(TamerBattleFloaterKind.Damage);
        atSpawn.Age.Should().BeApproximately(0, 0.001);

        AdvanceTo(playback, 1);
        playback.Floaters.Single().Age.Should().BeApproximately(0.1, 0.001);

        AdvanceTo(playback, 10);
        playback.Floaters.Should().BeEmpty();
    }

    [Test]
    public void Ranged_attacks_send_a_projectile_and_melee_does_not()
    {
        var ranged = new List<TamerBattleMonsterView> { Monster(1, range: 2), Monster(6, TamerBattleSide.Defender, range: 1) };
        var playback = new TamerBattlePlayback(Fight(ranged, [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(0, TamerBattleEventType.Spawn, 6, 0, 100, 3, 0, -1, "wolf"),
            Ev(10, TamerBattleEventType.Attack, 1, 6, 0, -1, -1, 10),
            Ev(20, TamerBattleEventType.Attack, 6, 1, 0, -1, -1, 10)
        ]));

        AdvanceTo(playback, 11);
        playback.Projectiles.Should().ContainSingle();
        playback.Projectiles.Single().Progress.Should().BeApproximately(0.333, 0.01);

        AdvanceTo(playback, 10);
        playback.Projectiles.Should().BeEmpty();
    }

    [Test]
    public void The_ability_name_shows_for_a_second()
    {
        var playback = new TamerBattlePlayback(Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(10, TamerBattleEventType.Ability, 1, 6, 0, -1, -1, 0, "rake")
        ]));

        AdvanceTo(playback, 10);
        Unit(playback, 1).AbilityName.Should().Be("Rake");
        AdvanceTo(playback, 5);
        Unit(playback, 1).AbilityName.Should().Be("Rake");
        AdvanceTo(playback, 5);
        Unit(playback, 1).AbilityName.Should().BeNull();
    }

    [Test]
    public void Bad_events_are_skipped_without_a_crash()
    {
        var playback = new TamerBattlePlayback(Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            new TamerBattleEventView(10, (TamerBattleEventType)99, 1, 6, 99, -1, -1, (sbyte)99, "bogus"),
            Ev(10, TamerBattleEventType.Damage, 99, 1, 50, -1, -1, 5),
            Ev(10, TamerBattleEventType.Damage, 6, 99, 50, -1, -1, 5),
            Ev(20, TamerBattleEventType.Step, 1, 0, 0, 99, 99, -1),
            Ev(20, TamerBattleEventType.StatusOn, 0, 99, 5, -1, -1, -1, "Stun"),
            Ev(30, TamerBattleEventType.Trait, 99, 0, 0, -1, -1, -1, "thorns")
        ]));

        AdvanceTo(playback, 30);

        Unit(playback, 1).Health.Should().Be(100);
        Unit(playback, 1).EngineColumn.Should().Be(3);
        Unit(playback, 1).StatusKeys.Should().BeEmpty();
        playback.Floaters.Should().BeEmpty();
    }

    [Test]
    public void An_unknown_key_falls_back_to_the_key_itself()
    {
        var playback = new TamerBattlePlayback(Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(10, TamerBattleEventType.Ability, 1, 6, 0, -1, -1, 0, "mystery_key")
        ], names: [new("rake", "Rake")]));

        AdvanceTo(playback, 10);

        Unit(playback, 1).AbilityName.Should().Be("mystery_key");
        playback.LogLines.Should().ContainSingle().Which.Should().Be("Alpha uses mystery_key");
    }

    [Test]
    public void Name_lookup_is_case_insensitive()
    {
        var playback = new TamerBattlePlayback(Fight(Duo(), [
            Ev(0, TamerBattleEventType.Spawn, 1, 0, 100, 3, 3, -1, "wolf"),
            Ev(10, TamerBattleEventType.Ability, 1, 6, 0, -1, -1, 0, "RAKE")
        ]));

        AdvanceTo(playback, 10);
        Unit(playback, 1).AbilityName.Should().Be("Rake");
    }

    [Test]
    public void Real_engine_events_reach_the_expected_health_positions_and_energy()
    {
        //the first 200 lines of the server golden fight-1, as wire events; the start cells below
        //are deliberately wrong so the Spawn events must win
        var events = ParseGolden(Golden200);
        var monsters = new List<TamerBattleMonsterView>
        {
            new(1, TamerBattleSide.Attacker, "gold_wolf", "M1", 11, 5, TamerWireElement.Fire, 100000, 0, 0, 1),
            new(2, TamerBattleSide.Attacker, "gold_fox", "M2", 12, 5, TamerWireElement.Water, 2000, 0, 0, 1),
            new(3, TamerBattleSide.Attacker, "gold_bear", "M3", 13, 5, TamerWireElement.Earth, 100000, 0, 0, 1),
            new(4, TamerBattleSide.Attacker, "gold_cat", "M4", 14, 5, TamerWireElement.Wind, 100000, 0, 0, 1),
            new(5, TamerBattleSide.Attacker, "gold_bat", "M5", 15, 5, TamerWireElement.Fire, 100000, 0, 0, 1),
            new(6, TamerBattleSide.Defender, "gold_turtle", "M6", 16, 5, TamerWireElement.Water, 100000, 0, 0, 1),
            new(7, TamerBattleSide.Defender, "gold_golem", "M7", 17, 5, TamerWireElement.Earth, 100000, 0, 0, 1),
            new(8, TamerBattleSide.Defender, "gold_crab", "M8", 18, 5, TamerWireElement.Water, 100000, 0, 0, 1),
            new(9, TamerBattleSide.Defender, "gold_whale", "M9", 19, 5, TamerWireElement.Water, 100000, 0, 0, 1),
            new(10, TamerBattleSide.Defender, "gold_owl", "M10", 20, 5, TamerWireElement.Wind, 60, 0, 0, 1)
        };
        var names = new List<TamerBattleNameEntry>
        {
            new("fire_2", "Fire bond"), new("thick_hide", "Thick hide"), new("thorns", "Thorns"),
            new("shell_wall", "Shell wall"), new("bite_strike", "Bite strike"), new("ward", "Ward"),
            new("quake", "Quake"), new("snap", "Snap"), new("rally", "Rally"), new("mend", "Mend")
        };
        var fight = new TamerBattleFight(7, "Attacker", "Defender", TamerBattleViewerSide.Attacker, "Test fight", monsters, names, events, TamerBattleSide.Defender, TamerBattleEndReason.Time, 600, []);
        var playback = new TamerBattlePlayback(fight);
        AdvanceTo(playback, 120);

        Check(playback, 1, 99206, 3, 3, 85, true, 0);
        Check(playback, 2, 1866, 4, 1, 10, true, 0);
        Check(playback, 3, 99679, 4, 3, 0, true, 0);
        Check(playback, 4, 99888, 2, 2, 50, true, 0);
        Check(playback, 5, 99871, 0, 0, 20, true, 0);
        Check(playback, 6, 99517, 3, 1, 55, true, 0);
        Check(playback, 7, 100000, 0, 1, 10, true, 0);
        Check(playback, 8, 99862, 2, 1, 25, true, 0);
        Check(playback, 9, 99674, 4, 2, 50, true, 0);
        Check(playback, 10, 0, 2, 2, 30, false, 0);
    }

    private static void Check(TamerBattlePlayback playback, byte id, int health, int column, int row, int energy, bool alive, int shield)
    {
        var unit = Unit(playback, id);
        unit.Health.Should().Be(health);
        unit.EngineColumn.Should().Be(column);
        unit.EngineRow.Should().Be(row);
        unit.Energy.Should().Be(energy);
        unit.Alive.Should().Be(alive);
        unit.Shield.Should().Be(shield);
    }

    private static List<TamerBattleEventView> ParseGolden(string[] lines)
    {
        var list = new List<TamerBattleEventView>(lines.Length);

        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var tick = ushort.Parse(parts[0]);
            var type = Enum.Parse<TamerBattleEventType>(parts[1]);
            var unit = byte.Parse(parts[2]);
            var target = byte.Parse(parts[3]);
            var amount = int.Parse(parts[4]);
            var x = sbyte.Parse(parts[5]);
            var y = sbyte.Parse(parts[6]);
            var energy = sbyte.Parse(parts[7]);
            var key = parts.Length > 8 ? parts[8] : string.Empty;
            list.Add(new TamerBattleEventView(tick, type, unit, target, amount, x, y, energy, key));
        }

        return list;
    }

    private static readonly string[] Golden200 =
    [
        "0 Spawn 1 0 100000 3 3 -1 gold_wolf",
        "0 Spawn 2 0 2000 3 4 -1 gold_fox",
        "0 Spawn 3 0 100000 3 5 -1 gold_bear",
        "0 Spawn 4 0 100000 2 3 -1 gold_cat",
        "0 Spawn 5 0 100000 4 3 -1 gold_bat",
        "0 Spawn 6 0 100000 3 2 -1 gold_turtle",
        "0 Spawn 7 0 100000 3 1 -1 gold_golem",
        "0 Spawn 8 0 100000 3 0 -1 gold_crab",
        "0 Spawn 9 0 100000 4 2 -1 gold_whale",
        "0 Spawn 10 0 60 2 2 -1 gold_owl",
        "0 SetBonus 1 0 0 -1 -1 -1 fire_2",
        "0 StatusOn 0 1 15 -1 -1 -1 AttackUp",
        "0 SetBonus 5 0 0 -1 -1 -1 fire_2",
        "0 StatusOn 0 5 15 -1 -1 -1 AttackUp",
        "0 SetBonus 3 0 0 -1 -1 -1 fire_2",
        "0 StatusOn 0 3 15 -1 -1 -1 AttackUp",
        "0 SetBonus 2 0 0 -1 -1 -1 fire_2",
        "0 StatusOn 0 2 15 -1 -1 -1 AttackUp",
        "0 SetBonus 4 0 0 -1 -1 -1 fire_2",
        "0 StatusOn 0 4 15 -1 -1 -1 AttackUp",
        "0 Trait 1 0 0 -1 -1 -1 thick_hide",
        "0 StatusOn 0 1 20 -1 -1 -1 ArmorUp",
        "10 Attack 1 6 0 -1 -1 10",
        "10 Damage 1 6 44 -1 -1 5",
        "10 Attack 9 5 0 -1 -1 10",
        "10 Damage 9 5 63 -1 -1 5",
        "10 Leap 5 8 0 2 0 -1",
        "10 Step 3 0 0 4 5 -1",
        "10 Step 7 0 0 2 1 -1",
        "10 Step 2 0 0 2 4 -1",
        "10 Attack 6 1 0 -1 -1 15",
        "10 Damage 6 1 60 -1 -1 15",
        "10 Attack 10 4 0 -1 -1 10",
        "10 Damage 10 4 56 -1 -1 5",
        "10 Step 8 0 0 3 1 -1",
        "10 Attack 4 10 0 -1 -1 15",
        "10 Damage 4 10 41 -1 -1 15",
        "20 Attack 1 6 0 -1 -1 25",
        "20 Damage 1 6 44 -1 -1 20",
        "20 Attack 9 1 0 -1 -1 20",
        "20 Damage 9 1 60 -1 -1 30",
        "20 Step 5 0 0 3 0 -1",
        "20 Step 3 0 0 4 4 -1",
        "20 Step 7 0 0 2 0 -1",
        "20 Step 2 0 0 1 4 -1",
        "20 Attack 6 1 0 -1 -1 30",
        "20 Damage 6 1 61 -1 -1 35",
        "20 Attack 10 4 0 -1 -1 25",
        "20 Damage 10 4 56 -1 -1 20",
        "20 Step 8 0 0 2 1 -1",
        "20 Attack 4 10 0 -1 -1 30",
        "20 Damage 4 10 39 -1 -1 30",
        "20 Down 10 0 0 -1 -1 -1",
        "30 Attack 1 6 0 -1 -1 45",
        "30 Damage 1 6 44 -1 -1 35",
        "30 Attack 9 1 0 -1 -1 30",
        "30 Damage 9 1 64 -1 -1 50",
        "30 Step 5 0 0 3 1 -1",
        "30 Step 3 0 0 4 3 -1",
        "30 Step 7 0 0 3 0 -1",
        "30 Step 2 0 0 1 3 -1",
        "30 Attack 6 1 0 -1 -1 45",
        "30 Damage 6 1 65 -1 -1 55",
        "30 Step 8 0 0 2 2 -1",
        "30 Attack 4 8 0 -1 -1 40",
        "30 Damage 4 8 38 -1 -1 5",
        "40 Attack 1 6 0 -1 -1 65",
        "40 Damage 1 6 47 -1 -1 50",
        "40 Attack 9 3 0 -1 -1 40",
        "40 Damage 9 3 65 -1 -1 5",
        "40 Step 5 0 0 2 1 -1",
        "40 Attack 3 9 0 -1 -1 15",
        "40 Damage 3 9 39 -1 -1 45",
        "40 Step 7 0 0 3 1 -1",
        "40 Step 2 0 0 1 2 -1",
        "40 Attack 6 1 0 -1 -1 60",
        "40 Damage 6 1 60 -1 -1 70",
        "40 Attack 8 2 0 -1 -1 15",
        "40 Damage 8 2 68 -1 -1 5",
        "40 Trait 2 8 0 -1 -1 -1 thorns",
        "40 Damage 2 8 12 -1 -1 15",
        "40 Attack 4 8 0 -1 -1 50",
        "40 Damage 4 8 45 -1 -1 20",
        "50 Attack 1 6 0 -1 -1 80",
        "50 Damage 1 6 46 -1 -1 65",
        "50 Attack 9 3 0 -1 -1 55",
        "50 Damage 9 3 64 -1 -1 20",
        "50 Attack 5 8 0 -1 -1 15",
        "50 Damage 5 8 43 -1 -1 25",
        "50 Attack 3 9 0 -1 -1 30",
        "50 Damage 3 9 44 -1 -1 60",
        "50 Attack 7 5 0 -1 -1 10",
        "50 Damage 7 5 66 -1 -1 20",
        "50 Step 2 0 0 1 1 -1",
        "50 Attack 6 1 0 -1 -1 75",
        "50 Damage 6 1 56 -1 -1 85",
        "50 Step 8 0 0 1 2 -1",
        "50 Step 4 0 0 2 2 -1",
        "60 Attack 1 6 0 -1 -1 95",
        "60 Damage 1 6 38 -1 -1 80",
        "60 Attack 9 3 0 -1 -1 70",
        "60 Damage 9 3 63 -1 -1 35",
        "60 Step 5 0 0 2 0 -1",
        "60 Attack 3 9 0 -1 -1 45",
        "60 Damage 3 9 44 -1 -1 75",
        "60 Step 7 0 0 2 1 -1",
        "60 Step 2 0 0 0 1 -1",
        "60 Attack 6 1 0 -1 -1 90",
        "60 Damage 6 1 68 -1 -1 100",
        "60 Step 8 0 0 0 2 -1",
        "60 Step 4 0 0 1 2 -1",
        "70 Ability 1 6 0 -1 -1 0 shell_wall",
        "70 Shield 1 1 171 -1 -1 -1",
        "70 StatusOn 0 1 171 -1 -1 -1 Shield",
        "70 StatusOn 1 6 40 -1 -1 -1 Taunt",
        "70 StatusOn 1 9 40 -1 -1 -1 Taunt",
        "70 Attack 9 1 0 -1 -1 85",
        "70 Damage 9 1 56 -1 -1 5",
        "70 Step 5 0 0 3 0 -1",
        "70 Attack 3 9 0 -1 -1 55",
        "70 Damage 3 9 42 -1 -1 90",
        "70 Step 7 0 0 3 1 -1",
        "70 Step 2 0 0 1 1 -1",
        "70 Attack 6 1 0 -1 -1 100",
        "70 Damage 6 1 59 -1 -1 10",
        "70 Step 8 0 0 0 1 -1",
        "70 Step 4 0 0 0 2 -1",
        "80 Attack 1 6 0 -1 -1 20",
        "80 Damage 1 6 45 -1 -1 100",
        "80 Attack 9 1 0 -1 -1 100",
        "80 Damage 9 1 56 -1 -1 25",
        "80 StatusOff 0 1 0 -1 -1 -1 Shield",
        "80 Step 5 0 0 2 0 -1",
        "80 Attack 3 9 0 -1 -1 65",
        "80 Damage 3 9 39 -1 -1 100",
        "80 Step 7 0 0 2 1 -1",
        "80 Step 2 0 0 1 2 -1",
        "80 Ability 6 1 0 -1 -1 0 bite_strike",
        "80 Damage 6 1 115 -1 -1 30",
        "80 Step 8 0 0 1 1 -1",
        "80 Step 4 0 0 0 1 -1",
        "90 Attack 1 6 0 -1 -1 40",
        "90 Damage 1 6 44 -1 -1 5",
        "90 Ability 9 1 0 -1 -1 0 ward",
        "90 Shield 9 6 75 -1 -1 -1",
        "90 StatusOn 0 6 75 -1 -1 -1 Shield",
        "90 Shield 9 9 75 -1 -1 -1",
        "90 StatusOn 0 9 75 -1 -1 -1 Shield",
        "90 Step 5 0 0 1 0 -1",
        "90 Attack 3 9 0 -1 -1 75",
        "90 Damage 3 9 44 -1 -1 5",
        "90 Step 7 0 0 2 0 -1",
        "90 Step 2 0 0 2 2 -1",
        "90 Attack 6 1 0 -1 -1 15",
        "90 Damage 6 1 60 -1 -1 45",
        "90 Step 8 0 0 1 2 -1",
        "90 Step 4 0 0 1 1 -1",
        "100 Attack 1 6 0 -1 -1 55",
        "100 Damage 1 6 44 -1 -1 20",
        "100 Attack 9 1 0 -1 -1 15",
        "100 Damage 9 1 58 -1 -1 60",
        "100 Step 5 0 0 0 0 -1",
        "100 Attack 3 9 0 -1 -1 85",
        "100 Damage 3 9 46 -1 -1 20",
        "100 StatusOff 0 9 0 -1 -1 -1 Shield",
        "100 Step 7 0 0 1 0 -1",
        "100 Step 2 0 0 2 1 -1",
        "100 Attack 6 1 0 -1 -1 30",
        "100 Damage 6 1 67 -1 -1 65",
        "100 Step 8 0 0 2 2 -1",
        "100 Step 4 0 0 1 2 -1",
        "110 StatusOff 1 9 0 -1 -1 -1 Taunt",
        "110 StatusOff 1 6 0 -1 -1 -1 Taunt",
        "110 Attack 1 6 0 -1 -1 75",
        "110 Damage 1 6 43 -1 -1 35",
        "110 StatusOff 0 6 0 -1 -1 -1 Shield",
        "110 Attack 9 3 0 -1 -1 30",
        "110 Damage 9 3 66 -1 -1 90",
        "110 Step 5 0 0 0 1 -1",
        "110 Attack 3 9 0 -1 -1 100",
        "110 Damage 3 9 46 -1 -1 35",
        "110 Step 7 0 0 1 1 -1",
        "110 Step 2 0 0 3 1 -1",
        "110 Attack 6 2 0 -1 -1 45",
        "110 Damage 6 2 66 -1 -1 10",
        "110 Trait 2 6 0 -1 -1 -1 thorns",
        "110 Damage 2 6 12 -1 -1 45",
        "110 Step 8 0 0 2 1 -1",
        "110 Step 4 0 0 2 2 -1",
        "120 Attack 1 6 0 -1 -1 85",
        "120 Damage 1 6 43 -1 -1 50",
        "120 Attack 9 3 0 -1 -1 45",
        "120 Damage 9 3 63 -1 -1 100",
        "120 Step 5 0 0 0 0 -1",
        "120 Ability 3 9 0 -1 -1 0 quake",
        "120 Damage 3 6 64 -1 -1 55",
        "120 Damage 3 9 57 -1 -1 50",
        "120 Step 7 0 0 0 1 -1",
        "120 Step 2 0 0 4 1 -1",
        "120 Step 6 0 0 3 1 -1"
    ];
}
