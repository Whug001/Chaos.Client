using Chaos.CannonFair;
using static Chaos.CannonFair.CannonFairRules;

namespace Chaos.Client.Controls.World.Popups.CannonFair;

public sealed class CannonFairAim
{
    public const float MAX_DRAG_PIXELS = 120f;
    public const float MIN_DRAG_PIXELS = 12f;

    public float AngleDegrees { get; private set; } = 45f;
    public float Power { get; private set; } = 0.75f;

    public bool Drag(float pressX, float pressY, float mouseX, float mouseY)
    {
        var dx = pressX - mouseX;
        var dy = mouseY - pressY;
        var length = MathF.Sqrt((dx * dx) + (dy * dy));

        if (length < MIN_DRAG_PIXELS)
            return false;

        AngleDegrees = ClampAngle(MathF.Atan2(dy, dx) * 180f / MathF.PI);
        Power = ClampPower(length / MAX_DRAG_PIXELS);

        return true;
    }

    public void Nudge(float angleDelta, float powerDelta)
    {
        AngleDegrees = ClampAngle(AngleDegrees + angleDelta);
        Power = ClampPower(Power + powerDelta);
    }

    public (float X, float Y) Path(float cannonX, float seconds, ShotKind kind)
    {
        var radians = MathF.PI * AngleDegrees / 180f;
        var speed = MAX_SPEED * Power * Spec(kind).SpeedScale;

        return (cannonX + (MathF.Cos(radians) * speed * seconds),
            CANNON_Y - (MathF.Sin(radians) * speed * seconds) + (0.5f * GRAVITY * seconds * seconds));
    }
}
