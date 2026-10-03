#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.Fishing;

/// <summary>
///     One moment of a fishing pose: which existing body frame to draw, and where the drawn pole, line and bobber go.
///     Points are composite pixels of the unflipped sprite (see <see cref="AislingRenderer.CANVAS_CENTER_X" />).
/// </summary>
/// <param name="AnimSuffix">The body sheet the frame comes from ("01" walk/stand or "03" hands up / wave).</param>
/// <param name="FrameIndex">The frame in that sheet. Already chosen for the facing; flipping is the caller's.</param>
/// <param name="Hand">Where the pole's butt sits: the hand of the near arm.</param>
/// <param name="Angle">Pole direction in degrees: 0 points right, 90 straight up.</param>
/// <param name="Bend">How far the pole curves toward the line. 0 is straight.</param>
/// <param name="LineEnd">Where the line ends, or null when it is still on the reel.</param>
/// <param name="ShowBobber">True when the bobber floats at <paramref name="LineEnd" />.</param>
/// <param name="Splash">Ring size step for the splash at the line's end, or -1 for none.</param>
public readonly record struct FishingPoseShot(
    string AnimSuffix,
    int FrameIndex,
    Point Hand,
    float Angle,
    float Bend,
    Point? LineEnd,
    bool ShowBobber,
    int Splash);

/// <summary>
///     Turns a fisher's pose and how long they have held it into a <see cref="FishingPoseShot" />. Every body frame is one
///     every armor and hair style already has: standing (01), and the wave from the hands up set (03). Only the pole,
///     line and bobber are drawn new.
/// </summary>
public static class FishingPoseAnimator
{
    /// <summary>Length of the cast. After this the fisher waits with the line out.</summary>
    public const float CAST_MS = 880f;

    /// <summary>The in-between frame played when the reel button changes.</summary>
    public const float SWAP_MS = 140f;

    private const float HOLD_STEP_MS = 160f;
    private const float RELEASE_STEP_MS = 180f;
    private const float BOB_STEP_MS = 600f;

    private static readonly Point[] Jitter = [new(-2, 0), new(3, -2), new(-4, 1), new(2, -1)];

    private static readonly Facing Front = new(
        new BodyFrame("01", 5, new Point(49, 53)),
        new BodyFrame("03", 9, new Point(49, 37)),
        new BodyFrame("03", 8, new Point(52, 39)),
        new Point(97, 91));

    private static readonly Facing Back = new(
        new BodyFrame("01", 0, new Point(60, 55)),
        new BodyFrame("03", 7, new Point(66, 38)),
        new BodyFrame("03", 6, new Point(64, 36)),
        new Point(97, 49));

    /// <summary>
    ///     The shot for <paramref name="pose" />, <paramref name="elapsedMs" /> after it began. <paramref name="previous" />
    ///     is the pose before it, so a change of reel button can play the in-between frame. Null for
    ///     <see cref="FishingPose.None" />.
    /// </summary>
    public static FishingPoseShot? Resolve(FishingPose pose, FishingPose previous, float elapsedMs, bool isFrontFacing)
    {
        var f = isFrontFacing ? Front : Back;
        elapsedMs = Math.Max(0f, elapsedMs);

        return pose switch
        {
            FishingPose.Cast     => Cast(f, elapsedMs),
            FishingPose.Waiting  => Waiting(f, elapsedMs),
            FishingPose.Holding  => Holding(f, previous, elapsedMs),
            FishingPose.Released => Released(f, previous, elapsedMs),
            _                    => null
        };
    }

    private static FishingPoseShot Cast(Facing f, float ms)
    {
        if (ms >= CAST_MS)
            return Waiting(f, ms - CAST_MS);

        if (ms < 400f)
            return Shot(f.Idle, 35f, 0f, null, false);

        if (ms < 620f)
            return Shot(f.Raised, 120f, 0f, null, false);

        if (ms < 740f)
            return Shot(f.Mid, 75f, 0f, null, false);

        //the line is in the air, part way to the water
        var tip = f.Idle.Hand;
        var flying = new Point(tip.X + (f.Bobber.X - tip.X) * 3 / 5, tip.Y + (f.Bobber.Y - tip.Y) * 3 / 5);

        return Shot(f.Idle, 25f, 0f, flying, true);
    }

    private static FishingPoseShot Waiting(Facing f, float ms)
    {
        var bob = (int)(ms / BOB_STEP_MS) % 2;

        return Shot(f.Idle, 35f, 0.3f, new Point(f.Bobber.X, f.Bobber.Y + bob), true);
    }

    private static FishingPoseShot Holding(Facing f, FishingPose previous, float ms)
    {
        if ((previous is FishingPose.Released) && (ms < SWAP_MS))
            return Shot(f.Mid, 60f, 1f, f.Bobber, false);

        var step = (int)(ms / HOLD_STEP_MS);
        var jitter = Jitter[step % Jitter.Length];
        var end = new Point(f.Bobber.X + jitter.X, f.Bobber.Y + jitter.Y);

        return Shot(f.Raised, 62f + (step % 2) * 4f, 1.6f + (step % 2) * 0.3f, end, false, step % 3);
    }

    private static FishingPoseShot Released(Facing f, FishingPose previous, float ms)
    {
        if ((previous is FishingPose.Holding) && (ms < SWAP_MS))
            return Shot(f.Mid, 60f, 1f, f.Bobber, false);

        var step = (int)(ms / RELEASE_STEP_MS);

        return Shot(f.Idle, 30f + (step % 2) * 3f, 0.5f, f.Bobber, false);
    }

    private static FishingPoseShot Shot(BodyFrame frame, float angle, float bend, Point? lineEnd, bool bobber, int splash = -1)
        => new(frame.Suffix, frame.Index, frame.Hand, angle, bend, lineEnd, bobber, splash);

    private readonly record struct BodyFrame(string Suffix, int Index, Point Hand);

    private readonly record struct Facing(BodyFrame Idle, BodyFrame Raised, BodyFrame Mid, Point Bobber);
}
