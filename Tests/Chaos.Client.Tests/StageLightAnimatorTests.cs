using Chaos.Client.Systems;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class StageLightAnimatorTests
{
    private static StageLightInfo Light(byte id, int tileX = 4, int tileY = 16)
        => new() { Id = id, X = (ushort)(tileX * 16), Y = (ushort)(tileY * 16), X2 = (ushort)(tileX * 16), Y2 = (ushort)(tileY * 16) };

    private static StageLightingStateArgs State(ushort fadeMs, byte house, params StageLightState[] lights)
        => new() { StageX = 0, StageY = 12, StageWidth = 9, StageHeight = 9, HouseLevel = house, FadeMs = fadeMs, Lights = lights };

    private static StageLightState Entry(StageLightInfo light, uint motionMs = 0, uint effectMs = 0)
        => new() { Light = light, MotionElapsedMs = motionMs, EffectElapsedMs = effectMs };

    private static List<StageLightFrame> Frames(StageLightAnimator animator, long now, Func<uint, Vector2?>? lookup = null)
    {
        var frames = new List<StageLightFrame>();
        animator.Evaluate(now, lookup ?? (_ => null), frames);

        return frames;
    }

    [Test]
    public void ToTile_turns_sixteenths_into_tiles()
    {
        var tile = StageLightAnimator.ToTile(64, 264);
        tile.X.Should().Be(4f);
        tile.Y.Should().Be(16.5f);
    }

    [Test]
    public void Sweep_goes_there_and_back_with_easing()
    {
        var a = new Vector2(0, 12);
        var b = new Vector2(8, 12);

        //speed 3 → 8 s there and back
        StageLightAnimator.SweepPosition(a, b, 0, 3).X.Should().BeApproximately(0f, 0.001f);
        StageLightAnimator.SweepPosition(a, b, 2000, 3).X.Should().BeApproximately(4f, 0.001f);
        StageLightAnimator.SweepPosition(a, b, 4000, 3).X.Should().BeApproximately(8f, 0.001f);
        StageLightAnimator.SweepPosition(a, b, 8000, 3).X.Should().BeApproximately(0f, 0.001f);
        StageLightAnimator.SweepPosition(a, b, 500, 3).X.Should().BeLessThan(1f, "it eases out of the start");
    }

    [Test]
    public void Circle_keeps_its_radius()
    {
        var centre = new Vector2(4, 16);
        var edge = new Vector2(6, 16);

        foreach (var ms in new long[] { 0, 1000, 2500, 7999 })
            Vector2.Distance(StageLightAnimator.CirclePosition(centre, edge, ms, 3), centre).Should().BeApproximately(2f, 0.001f);
    }

    [Test]
    public void Pulse_runs_from_30_to_100_percent()
    {
        StageLightAnimator.PulseFactor(0, 3).Should().BeApproximately(0.30f, 0.001f);
        StageLightAnimator.PulseFactor(1000, 3).Should().BeApproximately(1.0f, 0.001f);   //speed 3 → 2 s cycle
    }

    [Test]
    public void Flicker_is_repeatable_and_in_range()
    {
        for (var ms = 0; ms < 5000; ms += 37)
        {
            var value = StageLightAnimator.FlickerFactor(3, ms, 3);
            value.Should().BeInRange(0.55f, 1.0f);
            StageLightAnimator.FlickerFactor(3, ms, 3).Should().Be(value);
        }
    }

    [Test]
    public void Color_cycle_starts_at_the_base_hue_and_turns()
    {
        var red = new Color(255, 0, 0);

        StageLightAnimator.CycleColor(red, 0, 3).Should().Be(new Color(255, 0, 0));

        //speed 3 → 10 s per wheel; a third of the way is green
        var third = StageLightAnimator.CycleColor(red, 3333, 3);
        third.G.Should().BeGreaterThan(200);
        third.R.Should().BeLessThan(40);
    }

    [Test]
    public void Hsv_round_trips()
    {
        var colour = HsvColor.FromHsv(200f, 0.6f, 0.8f);
        var (h, s, v) = HsvColor.ToHsv(colour);

        h.Should().BeApproximately(200f, 1.5f);
        s.Should().BeApproximately(0.6f, 0.01f);
        v.Should().BeApproximately(0.8f, 0.01f);
    }

    [Test]
    public void Entity_tile_turns_the_walking_offset_back_into_tiles()
    {
        //half a step toward +X: (Δx − Δy)·28 = 14, (Δx + Δy)·14 = 7
        var tile = StageLightAnimator.EntityTile(3, 15, new Vector2(14, 7));
        tile.X.Should().BeApproximately(3.5f, 0.001f);
        tile.Y.Should().BeApproximately(15f, 0.001f);
    }

    [Test]
    public void Follow_holds_to_the_stage_and_falls_back_to_its_own_spot()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100, Entry(Light(1) with { Motion = StageLightMotion.Follow, FollowId = 44 })), 0);

        var seen = Frames(animator, 10, id => id == 44 ? new Vector2(20, 30) : null)[0];
        seen.Tile.Should().Be(new Vector2(8, 20));

        var gone = Frames(animator, 20)[0];
        gone.Tile.Should().Be(new Vector2(4, 16));
    }

    [Test]
    public void Server_elapsed_time_anchors_the_sweep()
    {
        var animator = new StageLightAnimator();
        var light = Light(1, 0, 12) with { Motion = StageLightMotion.Sweep, X2 = 8 * 16, Y2 = 12 * 16 };
        animator.Apply(State(0, 100, Entry(light, motionMs: 4000)), 1000);

        Frames(animator, 1000)[0].Tile.X.Should().BeApproximately(8f, 0.001f);
    }

    [Test]
    public void First_setup_does_not_fade_but_later_ones_do()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(1000, 100, Entry(Light(1, 2, 14))), 0);
        Frames(animator, 0)[0].Tile.Should().Be(new Vector2(2, 14));

        animator.Apply(State(1000, 100, Entry(Light(1, 6, 14))), 100);
        Frames(animator, 600)[0].Tile.X.Should().BeApproximately(4f, 0.001f);
        Frames(animator, 1100)[0].Tile.X.Should().BeApproximately(6f, 0.001f);
    }

    [Test]
    public void New_lights_fade_in_and_removed_lights_fade_out()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100, Entry(Light(1) with { Brightness = 100 })), 0);
        Frames(animator, 0);

        animator.Apply(State(1000, 100, Entry(Light(2) with { Brightness = 100 })), 0);

        var half = Frames(animator, 500);
        half.Single(f => f.Id == 2).Strength.Should().BeApproximately(0.5f, 0.001f);
        half.Single(f => f.Id == 1).Strength.Should().BeApproximately(0.5f, 0.001f);

        Frames(animator, 1000).Should().ContainSingle(f => f.Id == 2);
    }

    [Test]
    public void House_darkness_blends()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100), 0);
        animator.Apply(State(400, 0), 0);

        animator.CurrentHouseDarkness(200).Should().BeApproximately(0.5f, 0.001f);
        animator.CurrentHouseDarkness(400).Should().BeApproximately(1f, 0.001f);
    }

    [Test]
    public void Held_light_ignores_the_server_until_it_catches_up()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100, Entry(Light(1, 2, 14))), 0);

        var held = Light(1, 7, 19);
        animator.Hold(held);
        animator.Apply(State(0, 100, Entry(Light(1, 3, 15))), 10);
        Frames(animator, 10)[0].Tile.Should().Be(new Vector2(7, 19));

        animator.Release(1, 20);
        animator.Apply(State(0, 100, Entry(Light(1, 3, 15))), 30);
        Frames(animator, 30)[0].Tile.Should().Be(new Vector2(7, 19), "still waiting for the server to match");

        animator.Apply(State(0, 100, Entry(held)), 40);
        Frames(animator, 40)[0].Tile.Should().Be(new Vector2(7, 19));
        animator.Apply(State(0, 100, Entry(Light(1, 1, 13))), 50);
        Frames(animator, 50)[0].Tile.Should().Be(new Vector2(1, 13), "the hold ended once the server matched");
    }

    [Test]
    public void Released_hold_times_out()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100, Entry(Light(1, 2, 14))), 0);
        animator.Hold(Light(1, 7, 19));
        animator.Release(1, 0);

        Frames(animator, 1500)[0].Tile.Should().Be(new Vector2(2, 14));
    }

    [Test]
    public void Clear_forgets_everything()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 20, Entry(Light(1))), 0);
        animator.Clear();

        animator.HasSetup.Should().BeFalse();
        animator.Lights.Should().BeEmpty();
        Frames(animator, 0).Should().BeEmpty();
    }

    [Test]
    public void Evaluate_publishes_LatestFrames_for_the_window_to_reuse()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100, Entry(Light(1)), Entry(Light(2))), 0);

        var frames = Frames(animator, 0);
        animator.LatestFrames.Should().HaveCount(frames.Count);
        animator.LatestFrames.Select(f => f.Id).Should().BeEquivalentTo(frames.Select(f => f.Id));

        animator.Clear();
        animator.LatestFrames.Should().BeEmpty();
    }

    [Test]
    public void Removed_light_keeps_fading_across_an_unrelated_apply()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100, Entry(Light(1) with { Brightness = 100 })), 0);
        Frames(animator, 0);

        animator.Apply(State(1000, 100), 0); //light 1 removed, fades out over 1 s from t=0

        var atT500 = Frames(animator, 500).Single(f => f.Id == 1).Strength;

        animator.Apply(State(150, 100, Entry(Light(2) with { Brightness = 100 })), 500); //unrelated change

        var atT600 = Frames(animator, 600).Single(f => f.Id == 1).Strength;

        atT600.Should().BeLessThan(atT500);
        atT600.Should().BeApproximately(0.4f, 0.01f);
    }

    [Test]
    public void Reappearing_light_fades_continuously_from_its_decayed_strength()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100, Entry(Light(1) with { Brightness = 100 })), 0);
        Frames(animator, 0);

        animator.Apply(State(1000, 100), 0); //light 1 removed, fades out over 1 s from t=0
        animator.Apply(State(1000, 100, Entry(Light(1) with { Brightness = 100 })), 500); //re-added mid-fade

        var atReturn = Frames(animator, 500).Single(f => f.Id == 1).Strength;
        atReturn.Should().BeApproximately(0.5f, 0.01f);

        var later = Frames(animator, 1000).Single(f => f.Id == 1).Strength;
        later.Should().BeGreaterThan(atReturn);
    }

    [Test]
    public void Two_setups_without_an_evaluate_between_still_fade_from_the_live_look()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100, Entry(Light(1, 2, 14))), 0); //first setup, no fade
        animator.Apply(State(1000, 100, Entry(Light(1, 6, 14))), 100); //moved, no Evaluate call in between

        var frame = Frames(animator, 600)[0];
        frame.Tile.X.Should().BeApproximately(4f, 0.001f);
        frame.Strength.Should().BeApproximately(0.8f, 0.001f); //default brightness, no dip from a missing FadeFrom
    }
}
