using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.DarkAges.BrassRing;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.BrassRing;

public enum BrassRingGlove : byte { None, ScreenLeft, ScreenRight }

public readonly record struct BrassRingSprite(string Suffix, int Frame, Vector2 Offset, float Rotation, bool Flash, BrassRingGlove Raised = BrassRingGlove.None, bool Flip = false);

public static class BrassRingView
{
    private const string STAND_SUFFIX = "01";
    private const string ATTACK_SUFFIX = "02";
    private const int NEAR_STAND = 0;
    private const int FAR_STAND = 5;
    private const int NEAR_ATTACK = 0;
    private const int FAR_ATTACK = 2;
    private const int EXTEND_MS = 100;
    private const float PULL_BACK = 8f;
    private const float LUNGE = 24f;
    private const float DODGE_SLIDE = 30f;
    private const float STUN_PUSH = 10f;
    private const float DOWN_TILT = 1.45f;
    private const float GLOVE_RAISE = 30f;
    private const int SCALE = 3;
    private const int FOOT_X = AislingRenderer.BODY_CENTER_X;
    private const int FOOT_Y = AislingRenderer.CANVAS_CENTER_Y;

    //attack frames are hand positions read from the bare body sheets, stand frames are a boxing guard held towards the opponent, all in unscaled sheet pixels
    private static readonly Dictionary<(string Suffix, int Frame), (Point Left, Point Right)> MaleHands = new()
    {
        [(STAND_SUFFIX, NEAR_STAND)] = (new Point(34, 43), new Point(35, 54)),
        [(STAND_SUFFIX, FAR_STAND)] = (new Point(27, 51), new Point(36, 47)),
        [(ATTACK_SUFFIX, 0)] = (new Point(22, 32), new Point(33, 35)),
        [(ATTACK_SUFFIX, 1)] = (new Point(27, 43), new Point(43, 41)),
        [(ATTACK_SUFFIX, 2)] = (new Point(34, 47), new Point(36, 32)),
        [(ATTACK_SUFFIX, 3)] = (new Point(26, 51), new Point(40, 48))
    };

    private static readonly Dictionary<(string Suffix, int Frame), (Point Left, Point Right)> FemaleHands = new()
    {
        [(STAND_SUFFIX, NEAR_STAND)] = (new Point(33, 43), new Point(33, 51)),
        [(STAND_SUFFIX, FAR_STAND)] = (new Point(26, 50), new Point(35, 46)),
        [(ATTACK_SUFFIX, 0)] = (new Point(22, 32), new Point(33, 35)),
        [(ATTACK_SUFFIX, 1)] = (new Point(27, 44), new Point(42, 43)),
        [(ATTACK_SUFFIX, 2)] = (new Point(35, 48), new Point(33, 40)),
        [(ATTACK_SUFFIX, 3)] = (new Point(29, 51), new Point(40, 48))
    };

    public static BrassRingSprite Fighter(BrassRingFighter fighter, bool near, bool flash)
    {
        var forward = near ? new Vector2(1f, -0.5f) : new Vector2(-1f, 0.5f);
        var leftward = near ? new Vector2(-1f, -0.5f) : new Vector2(1f, 0.5f);
        var stand = near ? NEAR_STAND : FAR_STAND;
        var attack = near ? NEAR_ATTACK : FAR_ATTACK;

        var sprite = fighter.Pose switch
        {
            BrassRingPose.WindUpLeft or BrassRingPose.WindUpRight when fighter.PoseMs <= EXTEND_MS
                => new BrassRingSprite(ATTACK_SUFFIX, attack + 1, forward * LUNGE, 0f, flash),
            BrassRingPose.WindUpLeft or BrassRingPose.WindUpRight
                => new BrassRingSprite(ATTACK_SUFFIX, attack, -forward * PULL_BACK, 0f, flash, RaisedGlove(fighter.Pose, near)),
            BrassRingPose.Block => new BrassRingSprite(STAND_SUFFIX, stand, -forward * PULL_BACK, 0f, flash),
            BrassRingPose.DodgeLeft => new BrassRingSprite(STAND_SUFFIX, stand, leftward * DODGE_SLIDE, 0f, flash),
            BrassRingPose.DodgeRight => new BrassRingSprite(STAND_SUFFIX, stand, -leftward * DODGE_SLIDE, 0f, flash),
            BrassRingPose.Stunned => new BrassRingSprite(STAND_SUFFIX, stand, -forward * STUN_PUSH, 0f, true),
            BrassRingPose.Down => new BrassRingSprite(STAND_SUFFIX, stand, Vector2.Zero, near ? -DOWN_TILT : DOWN_TILT, flash),
            _ => new BrassRingSprite(STAND_SUFFIX, stand, Vector2.Zero, 0f, flash)
        };

        return sprite with { Flip = !near };
    }

    public static (Vector2 Left, Vector2 Right) GloveOffsets(BrassRingSprite sprite, Gender gender)
    {
        var (first, second) = (gender == Gender.Female ? FemaleHands : MaleHands)[(sprite.Suffix, sprite.Frame)];
        var (left, right) = sprite.Flip ? (Mirror(second), Mirror(first)) : (first, second);
        var lift = new Vector2(0f, -GLOVE_RAISE);

        return (
            ToOffset(left, sprite.Offset) + (sprite.Raised == BrassRingGlove.ScreenLeft ? lift : Vector2.Zero),
            ToOffset(right, sprite.Offset) + (sprite.Raised == BrassRingGlove.ScreenRight ? lift : Vector2.Zero));
    }

    //a standing near fighter faces away, so only the glove closest to the camera stays in front of the body
    public static bool LeftGloveBehindBody(BrassRingSprite sprite, bool near) => near && (sprite.Suffix == STAND_SUFFIX);

    private static Point Mirror(Point hand) => new(2 * FOOT_X - hand.X, hand.Y);

    //a far fighter faces the camera, so their left hand is on the right of the screen
    private static BrassRingGlove RaisedGlove(BrassRingPose pose, bool near)
        => (pose == BrassRingPose.WindUpLeft) == near ? BrassRingGlove.ScreenLeft : BrassRingGlove.ScreenRight;

    private static Vector2 ToOffset(Point hand, Vector2 offset)
        => new Vector2((hand.X - FOOT_X) * SCALE, (hand.Y - FOOT_Y) * SCALE) + offset;
}

public static class BrassRingKeys
{
    public static BrassRingMove? Pressed(Keycode key, bool isRepeat)
    {
        if (isRepeat)
            return null;

        return key switch
        {
            Keycode.A => BrassRingMove.PunchLeft,
            Keycode.D => BrassRingMove.PunchRight,
            Keycode.S => BrassRingMove.BlockStart,
            Keycode.Left => BrassRingMove.DodgeLeft,
            Keycode.Right => BrassRingMove.DodgeRight,
            _ => null
        };
    }

    public static BrassRingMove? Released(Keycode key) => key == Keycode.S ? BrassRingMove.BlockEnd : null;
}
