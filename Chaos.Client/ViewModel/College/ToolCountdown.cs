namespace Chaos.Client.ViewModel.College;

/// <summary>Counts down from the "seconds left" a server message gave, on the game's own clock after it arrived.</summary>
public sealed class ToolCountdown
{
    private double Elapsed;
    private int Seconds;

    public int Left => Math.Max(0, Seconds - (int)Math.Floor(Elapsed));

    public void Start(int seconds)
    {
        Seconds = seconds;
        Elapsed = 0;
    }

    public void Advance(double seconds) => Elapsed += seconds;
}
