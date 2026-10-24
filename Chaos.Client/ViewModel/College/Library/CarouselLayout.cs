namespace Chaos.Client.ViewModel.College.Library;

public readonly record struct CarouselPlace(bool Visible, float X, float Scale, float Lift);

public sealed class CarouselLayout(int count, float centerX, float radius)
{
    public const int VISIBLE_EACH_SIDE = 4;
    public const int COVER_RANGE = 2;
    private const float STEP_ANGLE = 0.33f;
    private const float SPIN_SPEED = 8f;

    public int Count { get; } = count;
    public float Current { get; private set; }
    public int Target { get; private set; }
    public int Front => Count == 0 ? -1 : Math.Clamp((int)MathF.Round(Current), 0, Count - 1);

    public void JumpTo(int index)
    {
        Target = Math.Clamp(index, 0, Math.Max(0, Count - 1));
        Current = Target;
    }

    public void Spin(int by) => Target = Math.Clamp(Target + by, 0, Math.Max(0, Count - 1));

    public void BringToFront(int index) => Target = Math.Clamp(index, 0, Math.Max(0, Count - 1));

    public void Step(float seconds)
    {
        var gap = Target - Current;
        var move = SPIN_SPEED * seconds * MathF.Max(1f, MathF.Abs(gap));
        Current = MathF.Abs(gap) <= move ? Target : Current + (MathF.Sign(gap) * move);
    }

    public CarouselPlace Place(int index)
    {
        var offset = index - Current;
        if (MathF.Abs(offset) > VISIBLE_EACH_SIDE + 0.5f)
            return new CarouselPlace(false, 0, 0, 0);
        var angle = offset * STEP_ANGLE;
        var x = centerX + (MathF.Sin(angle) * radius);
        var scale = MathF.Max(0.4f, 1f - (0.12f * MathF.Abs(offset)));
        var lift = (1f - MathF.Cos(angle)) * radius * 0.15f;
        return new CarouselPlace(true, x, scale, lift);
    }

    public bool ShouldLoadCover(int index) => (Front >= 0) && (Math.Abs(index - Front) <= COVER_RANGE);

    public IReadOnlyList<int> DrawOrder()
        => Enumerable.Range(0, Count)
                     .Where(i => Place(i).Visible)
                     .OrderByDescending(i => MathF.Abs(i - Current))
                     .ThenBy(i => i)
                     .ToList();
}
