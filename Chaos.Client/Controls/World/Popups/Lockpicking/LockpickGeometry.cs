namespace Chaos.Client.Controls.World.Popups.Lockpicking;

/// <summary>Angle maths for the lockpick window. Pick angles: 0 = left, 90 = straight up, 180 = right.</summary>
public static class LockpickGeometry
{
    public const float MAX_PICK_DEGREES = 180f;
    public const int CYLINDER_FRAME_COUNT = 19;
    public const float CYLINDER_FRAME_STEP_DEGREES = 5f;

    /// <summary>
    ///     The pick angle for a point given relative to the lock's centre (screen y grows downward). A point below the
    ///     centre snaps to the nearer end, and the centre itself is straight up.
    /// </summary>
    public static float PickDegreesFrom(float dx, float dy)
    {
        if ((dx == 0f) && (dy == 0f))
            return 90f;

        //maths angle: right 0, up 90, left 180, and negative below the centre
        var theta = MathF.Atan2(-dy, dx) * (180f / MathF.PI);

        if (theta < 0f)
            theta = theta < -90f ? 180f : 0f;

        return MAX_PICK_DEGREES - theta;
    }

    /// <summary>The cylinder sprite frame for a turn angle: one frame per 5 degrees, 0 to 18.</summary>
    public static int CylinderFrame(float cylinderDegrees)
        => Math.Clamp((int)MathF.Round(cylinderDegrees / CYLINDER_FRAME_STEP_DEGREES), 0, CYLINDER_FRAME_COUNT - 1);
}
