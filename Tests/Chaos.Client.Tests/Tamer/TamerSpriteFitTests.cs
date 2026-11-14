using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TamerSpriteFitTests
{
    //an idle frame, two walk frames and a wide attack frame, anchored near the feet
    private static readonly TamerSpriteFrameBox[] Cycle =
    [
        new(40, 60, 20, 52),
        new(44, 58, 23, 50),
        new(42, 62, 19, 54),
        new(96, 60, 30, 52)
    ];

    [Test]
    public void Every_frame_of_a_cycle_is_drawn_at_the_one_scale()
    {
        var layout = TamerSpriteFit.Fit(Cycle, 40, 40, TamerSpriteAnchor.Feet);

        layout.IsEmpty.Should().BeFalse();

        foreach (var frame in Cycle)
        {
            var placed = layout.Place(frame);

            placed.Width.Should().Be(Math.Max(1, (int)MathF.Round(frame.Width * layout.Scale)));
            placed.Height.Should().Be(Math.Max(1, (int)MathF.Round(frame.Height * layout.Scale)));
        }
    }

    [Test]
    public void A_wide_attack_frame_does_not_change_the_scale_of_the_other_frames()
    {
        var layout = TamerSpriteFit.Fit(Cycle, 40, 40, TamerSpriteAnchor.Feet);
        var idle = layout.Place(Cycle[0]);
        var attack = layout.Place(Cycle[3]);

        //the idle frame is 40 wide and the attack frame 96: both shrink by the same factor
        ((float)idle.Width / Cycle[0].Width).Should().BeApproximately(layout.Scale, 0.02f);
        ((float)attack.Width / Cycle[3].Width).Should().BeApproximately(layout.Scale, 0.02f);
        ((float)idle.Height / Cycle[0].Height).Should().BeApproximately((float)attack.Height / Cycle[3].Height, 0.02f);
    }

    [Test]
    public void The_scale_is_the_same_whichever_frame_is_asked_for_first()
    {
        var forward = TamerSpriteFit.Fit(Cycle, 40, 40, TamerSpriteAnchor.Feet);
        var backward = TamerSpriteFit.Fit(Cycle.Reverse().ToArray(), 40, 40, TamerSpriteAnchor.Feet);

        backward.Should().Be(forward);
    }

    [Test]
    [Arguments(TamerSpriteAnchor.Feet, 40, 40)]
    [Arguments(TamerSpriteAnchor.Centre, 110, 110)]
    [Arguments(TamerSpriteAnchor.Centre, 36, 36)]
    public void The_anchor_lands_on_the_same_point_of_the_box_for_every_frame(TamerSpriteAnchor anchor, int boxWidth, int boxHeight)
    {
        var layout = TamerSpriteFit.Fit(Cycle, boxWidth, boxHeight, anchor);

        foreach (var frame in Cycle)
        {
            var placed = layout.Place(frame);

            //whole pixels, so at most half a pixel of rounding either way
            (placed.X + (frame.DrawnAnchorX * layout.Scale)).Should().BeApproximately(layout.AnchorX, 0.51f);
            (placed.Y + (frame.AnchorY * layout.Scale)).Should().BeApproximately(layout.AnchorY, 0.51f);
        }
    }

    [Test]
    public void Every_frame_stays_inside_the_box()
    {
        foreach (var anchor in new[] { TamerSpriteAnchor.Feet, TamerSpriteAnchor.Centre })
        {
            var layout = TamerSpriteFit.Fit(Cycle, 40, 40, anchor);

            foreach (var frame in Cycle)
            {
                var placed = layout.Place(frame);

                placed.X.Should().BeGreaterThanOrEqualTo(-1);
                placed.Y.Should().BeGreaterThanOrEqualTo(-1);
                (placed.X + placed.Width).Should().BeLessThanOrEqualTo(41);
                (placed.Y + placed.Height).Should().BeLessThanOrEqualTo(41);
            }
        }
    }

    [Test]
    public void Feet_stand_on_the_bottom_centre_of_the_box()
    {
        var layout = TamerSpriteFit.Fit(Cycle, 40, 40, TamerSpriteAnchor.Feet);

        layout.AnchorX.Should().Be(20);

        //the frame reaching lowest under its anchor (60 - 52 = 62 - 54 = 8 pixels) touches the bottom edge
        var lowest = Cycle.Max(frame => layout.Place(frame).Y + layout.Place(frame).Height);

        lowest.Should().BeInRange(39, 41);
    }

    [Test]
    public void A_centred_sprite_has_its_cycle_bounds_in_the_middle_of_the_box()
    {
        TamerSpriteFrameBox[] frames = [new(20, 30, 10, 25), new(24, 30, 12, 25)];

        var layout = TamerSpriteFit.Fit(frames, 110, 110, TamerSpriteAnchor.Centre);

        //bounds over the cycle are 24 by 30: 3 times fits 110 by 110, 4 times is 120 tall
        layout.Scale.Should().Be(3f);

        var left = frames.Min(frame => layout.Place(frame).X);
        var right = frames.Max(frame => layout.Place(frame).X + layout.Place(frame).Width);
        var top = frames.Min(frame => layout.Place(frame).Y);
        var bottom = frames.Max(frame => layout.Place(frame).Y + layout.Place(frame).Height);

        (left + right).Should().BeInRange(109, 111);
        (top + bottom).Should().BeInRange(109, 111);
    }

    [Test]
    public void A_sprite_larger_than_the_box_shrinks_and_a_smaller_one_grows_in_whole_steps()
    {
        TamerSpriteFit.Fit([new TamerSpriteFrameBox(200, 100, 100, 90)], 40, 40, TamerSpriteAnchor.Centre).Scale.Should().Be(0.2f);
        TamerSpriteFit.Fit([new TamerSpriteFrameBox(30, 30, 15, 28)], 110, 110, TamerSpriteAnchor.Centre).Scale.Should().Be(3f);
        TamerSpriteFit.Fit([new TamerSpriteFrameBox(30, 30, 15, 28)], 40, 40, TamerSpriteAnchor.Centre).Scale.Should().Be(1f);
    }

    [Test]
    public void A_flipped_frame_mirrors_its_anchor()
    {
        var plain = new TamerSpriteFrameBox(40, 60, 10, 52);
        var flipped = plain with { Flip = true };

        plain.DrawnAnchorX.Should().Be(10);
        flipped.DrawnAnchorX.Should().Be(30);

        var layout = TamerSpriteFit.Fit([plain, flipped], 80, 80, TamerSpriteAnchor.Feet);

        (layout.Place(plain).X + (10 * layout.Scale)).Should().BeApproximately(layout.AnchorX, 0.51f);
        (layout.Place(flipped).X + (30 * layout.Scale)).Should().BeApproximately(layout.AnchorX, 0.51f);
    }

    [Test]
    public void No_frames_or_no_room_is_an_empty_layout()
    {
        TamerSpriteFit.Fit([], 40, 40, TamerSpriteAnchor.Feet).IsEmpty.Should().BeTrue();
        TamerSpriteFit.Fit(Cycle, 0, 40, TamerSpriteAnchor.Feet).IsEmpty.Should().BeTrue();
        TamerSpriteFit.Fit([new TamerSpriteFrameBox(0, 0, 0, 0)], 40, 40, TamerSpriteAnchor.Feet).IsEmpty.Should().BeTrue();
    }
}
