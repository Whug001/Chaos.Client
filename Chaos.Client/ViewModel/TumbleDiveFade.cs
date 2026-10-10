#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

public enum TumbleDiveStyle
{
    /// <summary>The floor being left scales up and fades while the floor below rises and brightens.</summary>
    DiveThrough,

    /// <summary>"Reduce Tumble Tower Motion": the dive is a fade through black. The sink and landing do not change.</summary>
    Fade
}

/// <param name="ShowUpper">True while the floor being left is still the one drawn; false once the floor below is.</param>
/// <param name="Black">How black the screen is over the drawn floor, 0 to 1.</param>
public readonly record struct TumbleFadeFrame(bool ShowUpper, double Black);

/// <summary>The reduce-motion switch and its fade: 0.25 s to black on the floor being left, then fade in on the floor below.</summary>
public static class TumbleDiveFade
{
    public const double TO_BLACK_SECONDS = 0.25;

    public static TumbleDiveStyle StyleFor(bool reduceMotion) => reduceMotion ? TumbleDiveStyle.Fade : TumbleDiveStyle.DiveThrough;

    /// <summary>Where the fade is during one dive segment. A dive into darkness stays black after the fade out.</summary>
    public static TumbleFadeFrame At(TumbleDiveFrame frame)
    {
        var length = frame.Segment == 0 ? TumbleTowerTiming.DIVE_SECONDS : TumbleTowerTiming.CHAIN_DIVE_SECONDS;
        var toBlack = Math.Min(TO_BLACK_SECONDS, length / 2);
        var t = Math.Clamp(frame.Progress, 0, 1) * length;

        if (t < toBlack)
            return new TumbleFadeFrame(true, t / toBlack);

        if (frame.LowerFloor is null)
            return new TumbleFadeFrame(false, 1);

        return new TumbleFadeFrame(false, 1 - (t - toBlack) / (length - toBlack));
    }
}
