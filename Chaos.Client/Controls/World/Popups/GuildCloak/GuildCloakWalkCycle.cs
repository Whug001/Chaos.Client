namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>
///     Which walk step the cloak preview shows, and whether it moves on by itself. Playing moves on one step every
///     <paramref name="stepMs" />; stepping by hand pauses, so a frame can be looked at.
/// </summary>
public sealed class GuildCloakWalkCycle(int stepCount, double stepMs)
{
    private double ElapsedMs;

    public bool Paused { get; private set; }
    public int Step { get; private set; }

    public void Advance(double elapsedMs)
    {
        if (Paused)
            return;

        ElapsedMs += elapsedMs;

        if (ElapsedMs < stepMs)
            return;

        ElapsedMs = 0;
        Step = (Step + 1) % stepCount;
    }

    /// <summary>Pauses and moves <paramref name="delta" /> steps, wrapping around the cycle.</summary>
    public void StepBy(int delta)
    {
        Paused = true;
        ElapsedMs = 0;
        Step = (((Step + delta) % stepCount) + stepCount) % stepCount;
    }

    public void TogglePause()
    {
        Paused = !Paused;
        ElapsedMs = 0;
    }
}
