#region
using Chaos.Client.Rendering;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg.Match;

/// <summary>The match window's art files in setoa.dat, and whether they are all present.</summary>
public static class TcgMatchArt
{
    public const string TABLE = "tcgtable.spf";
    public const string SEAL = "tcgseal.spf";
    public const string GEM = "tcggem.spf";
    public const string TRAP = "tcgtrap.spf";
    public const string HOURGLASS = "tcghglass.spf";
    public const string BANNER = "tcgbanner.spf";
    public const string ZZZ = "tcgzzz.spf";
    public const string PEDESTAL = "tcgplinth.spf";
    public const string PUFF = "tcgpuff.spf";

    /// <summary>The shield marker by a shielded player's life. Optional: a purple square stands in when it is missing.</summary>
    public const string SHIELD = "tcgshield.spf";
    public const int PUFF_FRAMES = 5;
    public const int DARK_GEM_OFFSET = 5;
    public const int HOURGLASS_FRAMES = 8;

    /// <summary>False when this client's setoa.dat lacks the match art (old game files).</summary>
    public static bool ArtPresent()
        => UiRenderer.Instance is { } ui
           && (ui.GetSpfFrameCount(TABLE) >= 1)
           && (ui.GetSpfFrameCount(SEAL) >= 1)
           && (ui.GetSpfFrameCount(GEM) >= 10)
           && (ui.GetSpfFrameCount(TRAP) >= 1)
           && (ui.GetSpfFrameCount(HOURGLASS) >= HOURGLASS_FRAMES)
           && (ui.GetSpfFrameCount(BANNER) >= 3)
           && (ui.GetSpfFrameCount(ZZZ) >= 3)
           && (ui.GetSpfFrameCount(PEDESTAL) >= 1)
           && (ui.GetSpfFrameCount(PUFF) >= 5);
}
