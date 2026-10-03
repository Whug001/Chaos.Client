using Chaos.Client.Rendering.Fishing;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class FishingPoseTests
{
    private static readonly string[] UniversalSheets = ["01", "03"];

    [Test]
    public async Task No_pose_draws_nothing()
    {
        FishingPoseAnimator.Resolve(FishingPose.None, FishingPose.Waiting, 500f, true).Should().BeNull();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Cast_flicks_from_the_shoulder_then_waits_with_the_bobber_out()
    {
        var start = FishingPoseAnimator.Resolve(FishingPose.Cast, FishingPose.None, 0f, true)!.Value;
        var back = FishingPoseAnimator.Resolve(FishingPose.Cast, FishingPose.None, 450f, true)!.Value;
        var done = FishingPoseAnimator.Resolve(FishingPose.Cast, FishingPose.None, FishingPoseAnimator.CAST_MS + 10f, true)!.Value;

        start.LineEnd.Should().BeNull();
        (back.AnimSuffix, back.FrameIndex).Should().Be(("03", 9));
        back.LineEnd.Should().BeNull();
        done.Should().Be(FishingPoseAnimator.Resolve(FishingPose.Waiting, FishingPose.Cast, 10f, true));
        done.ShowBobber.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Holding_raises_the_near_arm_and_splashes()
    {
        var shot = FishingPoseAnimator.Resolve(FishingPose.Holding, FishingPose.Released, 500f, true)!.Value;

        (shot.AnimSuffix, shot.FrameIndex).Should().Be(("03", 9));
        shot.Splash.Should().BeGreaterThanOrEqualTo(0);
        shot.ShowBobber.Should().BeFalse();
        shot.Bend.Should().BeGreaterThan(1f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Changing_the_reel_button_plays_the_in_between_frame_first()
    {
        var up = FishingPoseAnimator.Resolve(FishingPose.Holding, FishingPose.Released, 0f, true)!.Value;
        var down = FishingPoseAnimator.Resolve(FishingPose.Released, FishingPose.Holding, 0f, true)!.Value;
        var settled = FishingPoseAnimator.Resolve(FishingPose.Released, FishingPose.Holding, FishingPoseAnimator.SWAP_MS, true)!.Value;

        (up.AnimSuffix, up.FrameIndex).Should().Be(("03", 8));
        (down.AnimSuffix, down.FrameIndex).Should().Be(("03", 8));
        (settled.AnimSuffix, settled.FrameIndex).Should().Be(("01", 5));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Facing_away_uses_the_back_view_frames()
    {
        var waiting = FishingPoseAnimator.Resolve(FishingPose.Waiting, FishingPose.Cast, 0f, false)!.Value;
        var holding = FishingPoseAnimator.Resolve(FishingPose.Holding, FishingPose.Waiting, 0f, false)!.Value;

        (waiting.AnimSuffix, waiting.FrameIndex).Should().Be(("01", 0));
        (holding.AnimSuffix, holding.FrameIndex).Should().Be(("03", 7));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_shot_uses_a_sheet_every_armor_has()
    {
        FishingPose[] poses = [FishingPose.Cast, FishingPose.Waiting, FishingPose.Holding, FishingPose.Released];

        foreach (var pose in poses)
        foreach (var previous in poses)
        foreach (var front in new[] { true, false })
            for (var ms = 0f; ms < 2000f; ms += 20f)
            {
                var shot = FishingPoseAnimator.Resolve(pose, previous, ms, front)!.Value;
                UniversalSheets.Should().Contain(shot.AnimSuffix);
            }

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_straight_pole_runs_from_the_hand_at_its_angle()
    {
        var points = FishingPoleRenderer.PolePoints(new Point(50, 50), 0f, 0f, null);

        points[0].Should().Be(new Point(50, 50));
        points[^1].Should().Be(new Point(50 + FishingPoleRenderer.POLE_LENGTH, 50));
        await Task.CompletedTask;
    }

    [Test]
    public async Task A_bent_pole_tip_curves_toward_the_line()
    {
        var straight = FishingPoleRenderer.PolePoints(new Point(50, 50), 45f, 0f, new Point(100, 100))[^1];
        var bent = FishingPoleRenderer.PolePoints(new Point(50, 50), 45f, 1.5f, new Point(100, 100))[^1];

        bent.Y.Should().BeGreaterThan(straight.Y);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Line_points_join_both_ends_without_gaps()
    {
        var points = FishingPoleRenderer.LinePoints(new Point(0, 0), new Point(5, 9)).ToList();

        points[0].Should().Be(new Point(0, 0));
        points[^1].Should().Be(new Point(5, 9));

        for (var i = 1; i < points.Count; i++)
        {
            Math.Abs(points[i].X - points[i - 1].X).Should().BeLessThanOrEqualTo(1);
            Math.Abs(points[i].Y - points[i - 1].Y).Should().BeLessThanOrEqualTo(1);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Each_pole_tier_keeps_its_own_colour()
    {
        int[] sprites = [169, 207, 206, 210, 211];

        sprites.Select(FishingPoleRenderer.ColorsFor).Distinct().Should().HaveCount(sprites.Length);
        FishingPoleRenderer.ColorsFor(0).Should().Be(FishingPoleRenderer.ColorsFor(169));
        await Task.CompletedTask;
    }
}
