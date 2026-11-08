using Chaos.Client.Controls.World.Popups.BrassRing;
using Chaos.Client.Definitions;
using Chaos.DarkAges.BrassRing;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using static Chaos.DarkAges.BrassRing.BrassRingRules;

namespace Chaos.Client.Tests.BrassRing;

public sealed class BrassRingViewTests
{
    private static BrassRingFighter In(BrassRingPose pose, int ms = 0) => new() { Pose = pose, PoseMs = ms };

    [Test]
    public void Idle_uses_the_standing_frame_for_each_side()
    {
        BrassRingView.Fighter(In(BrassRingPose.Idle), true, false).Should().Match<BrassRingSprite>(s => s.Suffix == "01" && s.Frame == 0);
        BrassRingView.Fighter(In(BrassRingPose.Idle), false, false).Should().Match<BrassRingSprite>(s => s.Suffix == "01" && s.Frame == 5);
    }

    [Test]
    public void Punch_extends_only_at_the_end_of_the_wind_up()
    {
        BrassRingView.Fighter(In(BrassRingPose.WindUpLeft, WIND_UP_MS), true, false).Frame.Should().Be(0);
        BrassRingView.Fighter(In(BrassRingPose.WindUpLeft, 100), true, false).Frame.Should().Be(1);
        BrassRingView.Fighter(In(BrassRingPose.WindUpLeft, 100), false, false).Frame.Should().Be(3);
    }

    [Test]
    public void Dodges_slide_opposite_ways_for_near_and_far()
    {
        var near = BrassRingView.Fighter(In(BrassRingPose.DodgeLeft, DODGE_ACTIVE_MS), true, false);
        var far = BrassRingView.Fighter(In(BrassRingPose.DodgeLeft, DODGE_ACTIVE_MS), false, false);

        near.Offset.X.Should().BeLessThan(0);
        far.Offset.X.Should().BeGreaterThan(0);
    }

    [Test]
    public void A_stunned_fighter_flashes_on_its_own()
        => BrassRingView.Fighter(new BrassRingFighter { Pose = BrassRingPose.Stunned, PoseMs = 100 }, true, false).Flash.Should().BeTrue();

    [Test]
    public void Down_tilts_and_stun_flashes()
    {
        BrassRingView.Fighter(In(BrassRingPose.Down), true, false).Rotation.Should().NotBe(0f);
        BrassRingView.Fighter(In(BrassRingPose.Idle), true, true).Flash.Should().BeTrue();
    }

    [Test]
    public void Gloves_sit_at_two_different_points()
    {
        var gloves = BrassRingView.GloveOffsets(BrassRingView.Fighter(In(BrassRingPose.Idle), true, false), Gender.Male);

        gloves.Left.Should().NotBe(gloves.Right);
    }

    [Test]
    public void Wind_up_raises_the_glove_on_the_punching_side()
    {
        var nearLeft = BrassRingView.Fighter(In(BrassRingPose.WindUpLeft, WIND_UP_MS), true, false);
        var farLeft = BrassRingView.Fighter(In(BrassRingPose.WindUpLeft, WIND_UP_MS), false, false);

        nearLeft.Raised.Should().Be(BrassRingGlove.ScreenLeft);
        farLeft.Raised.Should().Be(BrassRingGlove.ScreenRight);

        var neutral = BrassRingView.GloveOffsets(nearLeft with { Raised = BrassRingGlove.None }, Gender.Male);
        var raised = BrassRingView.GloveOffsets(nearLeft, Gender.Male);

        raised.Left.Y.Should().BeLessThan(neutral.Left.Y);
        raised.Right.Should().Be(neutral.Right);
    }

    [Test]
    public void Only_the_far_fighter_is_flipped()
    {
        BrassRingView.Fighter(In(BrassRingPose.Idle), true, false).Flip.Should().BeFalse();
        BrassRingView.Fighter(In(BrassRingPose.Idle), false, false).Flip.Should().BeTrue();
    }

    [Test]
    public void Gloves_map_sheet_pixels_to_the_renderer_feet_anchor()
    {
        var gloves = BrassRingView.GloveOffsets(BrassRingView.Fighter(In(BrassRingPose.Idle), true, false), Gender.Male);

        gloves.Left.Should().Be(new Microsoft.Xna.Framework.Vector2(18, -81));
        gloves.Right.Should().Be(new Microsoft.Xna.Framework.Vector2(21, -48));
    }

    [Test]
    public void Flipped_gloves_are_mirrored_onto_their_screen_sides()
    {
        var gloves = BrassRingView.GloveOffsets(BrassRingView.Fighter(In(BrassRingPose.Idle), false, false), Gender.Female);

        gloves.Left.Should().Be(new Microsoft.Xna.Framework.Vector2(-21, -72));
        gloves.Right.Should().Be(new Microsoft.Xna.Framework.Vector2(6, -60));
    }

    [Test]
    public void Each_gender_uses_its_own_hand_table()
    {
        var male = BrassRingView.GloveOffsets(BrassRingView.Fighter(In(BrassRingPose.Idle), true, false), Gender.Male);
        var female = BrassRingView.GloveOffsets(BrassRingView.Fighter(In(BrassRingPose.Idle), true, false), Gender.Female);

        male.Left.Should().NotBe(female.Left);
    }

    [Test]
    public void Keys_map_to_moves()
    {
        BrassRingKeys.Pressed(Keycode.A, false).Should().Be(BrassRingMove.PunchLeft);
        BrassRingKeys.Pressed(Keycode.D, false).Should().Be(BrassRingMove.PunchRight);
        BrassRingKeys.Pressed(Keycode.S, false).Should().Be(BrassRingMove.BlockStart);
        BrassRingKeys.Pressed(Keycode.Left, false).Should().Be(BrassRingMove.DodgeLeft);
        BrassRingKeys.Pressed(Keycode.Right, false).Should().Be(BrassRingMove.DodgeRight);
        BrassRingKeys.Released(Keycode.S).Should().Be(BrassRingMove.BlockEnd);
        BrassRingKeys.Released(Keycode.A).Should().BeNull();
    }

    [Test]
    public void Held_key_repeats_are_ignored()
        => BrassRingKeys.Pressed(Keycode.A, true).Should().BeNull();

    [Test]
    public void Left_glove_hides_behind_a_near_fighter_who_is_standing()
        => BrassRingView.LeftGloveBehindBody(BrassRingView.Fighter(In(BrassRingPose.Idle), true, false), true).Should().BeTrue();

    [Test]
    public void Left_glove_stays_visible_for_a_near_attack()
        => BrassRingView.LeftGloveBehindBody(BrassRingView.Fighter(In(BrassRingPose.WindUpLeft, WIND_UP_MS), true, false), true).Should().BeFalse();

    [Test]
    public void Left_glove_stays_visible_for_a_far_fighter()
        => BrassRingView.LeftGloveBehindBody(BrassRingView.Fighter(In(BrassRingPose.Idle), false, false), false).Should().BeFalse();
}
