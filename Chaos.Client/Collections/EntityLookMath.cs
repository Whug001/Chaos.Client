using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Collections;

public static class EntityLookMath
{
    public const float FLOAT_BASE_PIXELS = 12f;
    public const float FLOAT_BOB_PIXELS = 3f;
    public const double FLOAT_PERIOD_SECONDS = 2.0;

    public static float FloatRaise(double seconds)
        => FLOAT_BASE_PIXELS + FLOAT_BOB_PIXELS * (float)Math.Sin(2 * Math.PI * seconds / FLOAT_PERIOD_SECONDS);

    public static float Scale(EntityLookArgs? look) => look is null ? 1f : look.ScalePercent / 100f;
}
