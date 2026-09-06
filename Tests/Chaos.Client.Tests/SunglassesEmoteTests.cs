using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class SunglassesEmoteTests
{
    [Test]
    public async Task At_zero_the_glasses_sit_above_the_head()
    {
        var frame = SunglassesEmote.Resolve(0);

        frame.RowOffset.Should().Be(SunglassesEmote.START_ROW_OFFSET);
        frame.RowOffset.Should().BeLessThan(0);
        frame.SparkleStage.Should().Be(SunglassesEmote.NO_SPARKLE);
        frame.IsFinished.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Negative_elapsed_clamps_to_the_start_of_the_fall()
    {
        SunglassesEmote.Resolve(-50)
                       .RowOffset
                       .Should()
                       .Be(SunglassesEmote.START_ROW_OFFSET);

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_fall_never_moves_back_upward()
    {
        var previous = int.MinValue;

        for (var t = 0f; t <= SunglassesEmote.DROP_MS; t += 5f)
        {
            var offset = SunglassesEmote.Resolve(t)
                                        .RowOffset;

            offset.Should().BeGreaterThanOrEqualTo(previous);
            offset.Should().BeLessThanOrEqualTo(0);
            previous = offset;
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Halfway_through_the_fall_the_glasses_are_between_start_and_the_eyes()
    {
        var frame = SunglassesEmote.Resolve(SunglassesEmote.DROP_MS / 2f);

        frame.RowOffset.Should().BeGreaterThan(SunglassesEmote.START_ROW_OFFSET);
        frame.RowOffset.Should().BeLessThan(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_glasses_land_on_the_eyes_and_stay_there()
    {
        SunglassesEmote.Resolve(SunglassesEmote.DROP_MS)
                       .RowOffset
                       .Should()
                       .Be(0);

        SunglassesEmote.Resolve(SunglassesEmote.TOTAL_MS - 1f)
                       .RowOffset
                       .Should()
                       .Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task There_is_no_sparkle_before_the_glasses_land()
    {
        SunglassesEmote.Resolve(SunglassesEmote.DROP_MS - 1f)
                       .SparkleStage
                       .Should()
                       .Be(SunglassesEmote.NO_SPARKLE);

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_sparkle_runs_from_the_landing_until_the_sparkle_window_closes()
    {
        var sparkleEnd = SunglassesEmote.DROP_MS + SunglassesEmote.SPARKLE_MS;

        SunglassesEmote.Resolve(SunglassesEmote.DROP_MS)
                       .SparkleStage
                       .Should()
                       .Be(0);

        SunglassesEmote.Resolve(sparkleEnd - 1f)
                       .SparkleStage
                       .Should()
                       .Be(SunglassesEmote.SPARKLE_STAGES - 1);

        SunglassesEmote.Resolve(sparkleEnd)
                       .SparkleStage
                       .Should()
                       .Be(SunglassesEmote.NO_SPARKLE);

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_sparkle_visits_every_stage_in_order()
    {
        var seen = new List<int>();

        for (var t = SunglassesEmote.DROP_MS; t < SunglassesEmote.DROP_MS + SunglassesEmote.SPARKLE_MS; t += 1f)
        {
            var stage = SunglassesEmote.Resolve(t)
                                       .SparkleStage;

            if (seen.Count == 0 || seen[^1] != stage)
                seen.Add(stage);
        }

        seen.Should()
            .Equal(Enumerable.Range(0, SunglassesEmote.SPARKLE_STAGES));

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_emote_is_finished_once_the_total_duration_elapses()
    {
        SunglassesEmote.Resolve(SunglassesEmote.TOTAL_MS - 1f)
                       .IsFinished
                       .Should()
                       .BeFalse();

        SunglassesEmote.Resolve(SunglassesEmote.TOTAL_MS)
                       .IsFinished
                       .Should()
                       .BeTrue();

        SunglassesEmote.Resolve(SunglassesEmote.TOTAL_MS + 500f)
                       .IsFinished
                       .Should()
                       .BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_glasses_pixel_map_is_rectangular_and_fits_the_face()
    {
        var rows = SunglassesEmote.GlassesPixels;

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(r => r.Length == rows[0].Length);
        rows[0].Length.Should().Be(SunglassesEmote.GLASSES_WIDTH);
        rows.Count.Should().Be(SunglassesEmote.GLASSES_HEIGHT);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_pixel_character_is_a_known_colour()
    {
        foreach (var row in SunglassesEmote.GlassesPixels.Concat(SunglassesEmote.SparklePixels.SelectMany(s => s)))
        foreach (var c in row)
            SunglassesEmote.TryGetColor(c, out _)
                           .Should()
                           .BeTrue($"'{c}' should be a known palette character");

        await Task.CompletedTask;
    }

    [Test]
    public async Task There_is_one_sparkle_pixel_map_per_stage()
        => await Assert.That(SunglassesEmote.SparklePixels.Count)
                       .IsEqualTo(SunglassesEmote.SPARKLE_STAGES);

    [Test]
    public async Task The_glasses_follow_the_head_down_on_the_walk_frames_that_bob()
    {
        //measured off the face EPF: walk frames 7 and 9 render the head at rows 25-39, the rest at 24-38
        SunglassesEmote.HeadBobOffset(7, "01")
                       .Should()
                       .Be(1);

        SunglassesEmote.HeadBobOffset(9, "01")
                       .Should()
                       .Be(1);

        foreach (var frame in new[] { 5, 6, 8 })
            SunglassesEmote.HeadBobOffset(frame, "01")
                           .Should()
                           .Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Other_animations_get_no_head_bob_adjustment()
    {
        foreach (var suffix in new[] { "02", "03", "04" })
        foreach (var frame in new[] { 5, 7, 9 })
            SunglassesEmote.HeadBobOffset(frame, suffix)
                           .Should()
                           .Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Mirroring_matches_the_pixel_mapping_measured_off_the_real_sprite()
    {
        //rendering the face EPF through the composite's own flip puts the head at x49..59, where it
        //sits at x50..60 unflipped. A pixel spans [x, x+1), so it lands on column 2p-1-x, not 2p-x.
        SunglassesEmote.MirrorX(50)
                       .Should()
                       .Be(59);

        SunglassesEmote.MirrorX(60)
                       .Should()
                       .Be(49);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Mirroring_is_its_own_inverse()
    {
        for (var x = 0; x < 111; x++)
            SunglassesEmote.MirrorX(SunglassesEmote.MirrorX(x))
                           .Should()
                           .Be(x);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Flipping_puts_the_glasses_the_same_distance_from_the_mirrored_face()
    {
        var left = SunglassesEmote.ResolveLeftX(false);
        var right = left + SunglassesEmote.GLASSES_WIDTH - 1;

        //the flipped sprite's left edge is the mirror of the unflipped sprite's right edge
        SunglassesEmote.ResolveLeftX(true)
                       .Should()
                       .Be(SunglassesEmote.MirrorX(right));

        //and the two placements stay the same width apart from the head, which mirrors to x49..59
        (left - 50).Should()
                   .Be(59 - (SunglassesEmote.ResolveLeftX(true) + SunglassesEmote.GLASSES_WIDTH - 1));

        await Task.CompletedTask;
    }
}
