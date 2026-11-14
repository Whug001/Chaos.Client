namespace Chaos.Client.ViewModel;

/// <summary>A control's rectangle as plain numbers, so a layout can be checked without a texture.</summary>
public readonly record struct TamerBox(string Name, int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Intersects(TamerBox other)
        => (X < other.Right) && (other.X < Right) && (Y < other.Bottom) && (other.Y < Bottom);

    public bool IsInside(TamerBox outer)
        => (X >= outer.X) && (Y >= outer.Y) && (Right <= outer.Right) && (Bottom <= outer.Bottom);

    /// <summary>The same box moved by an offset, for a child placed inside a parent.</summary>
    public TamerBox Offset(int x, int y, string? name = null) => new(name ?? Name, X + x, Y + y, Width, Height);
}

/// <summary>Which tab of the stable window is showing.</summary>
public enum TamerStableTab : byte
{
    Creatures = 0,
    Squads = 1,
    Ranked = 2
}

/// <summary>
///     The stable window's frame numbers and its top line (ranked ladder spec section 11.1): three tabs, then the
///     essence, feed and creature count. Window coordinates. Pure: no textures.
/// </summary>
public static class TamerStableLayout
{
    public const int PANEL_WIDTH = 620;
    public const int PANEL_HEIGHT = 440;

    /// <summary>The margin left and right of everything in the window.</summary>
    public const int LEFT = 20;

    public const int CONTENT_TOP = 52;
    public const int CONTENT_WIDTH = PANEL_WIDTH - (2 * LEFT);
    public const int CONTENT_HEIGHT = 312;

    /// <summary>A framed button's height: the text height plus 5 pixels above and below.</summary>
    public const int BUTTON_HEIGHT = 22;

    public const int TEXT_HEIGHT = 12;

    public const int TAB_TOP = 24;
    public const int TAB_WIDTH = 70;
    public const int TAB_STEP = 74;
    public const int CREATURES_TAB_LEFT = LEFT;
    public const int SQUADS_TAB_LEFT = CREATURES_TAB_LEFT + TAB_STEP;
    public const int RANKED_TAB_LEFT = SQUADS_TAB_LEFT + TAB_STEP;

    public const int INFO_TOP = TAB_TOP + ((BUTTON_HEIGHT - TEXT_HEIGHT) / 2);
    public const int ESSENCE_LEFT = 244;
    public const int ESSENCE_WIDTH = 161;
    public const int FEED_LEFT = 410;
    public const int FEED_WIDTH = 80;
    public const int COUNT_LEFT = 495;
    public const int COUNT_WIDTH = 105;

    /// <summary>The bottom message line, left of the Close button: the server's last refusal.</summary>
    public const int MESSAGE_LEFT = LEFT;

    public const int MESSAGE_WIDTH = 460;

    /// <summary>The whole window.</summary>
    public static TamerBox Panel => new("Window", 0, 0, PANEL_WIDTH, PANEL_HEIGHT);

    /// <summary>The strip the top line may use: between the side margins, above the tab content.</summary>
    public static TamerBox TopStrip => new("Top line", LEFT, TAB_TOP, CONTENT_WIDTH, CONTENT_TOP - TAB_TOP);

    /// <summary>The area a tab draws in.</summary>
    public static TamerBox Content => new("Content", LEFT, CONTENT_TOP, CONTENT_WIDTH, CONTENT_HEIGHT);

    /// <summary>Every control on the top line.</summary>
    public static IReadOnlyList<TamerBox> TopLine()
        =>
        [
            new("Creatures tab", CREATURES_TAB_LEFT, TAB_TOP, TAB_WIDTH, BUTTON_HEIGHT),
            new("Squads tab", SQUADS_TAB_LEFT, TAB_TOP, TAB_WIDTH, BUTTON_HEIGHT),
            new("Ranked tab", RANKED_TAB_LEFT, TAB_TOP, TAB_WIDTH, BUTTON_HEIGHT),
            new("Essence", ESSENCE_LEFT, INFO_TOP, ESSENCE_WIDTH, TEXT_HEIGHT),
            new("Feed", FEED_LEFT, INFO_TOP, FEED_WIDTH, TEXT_HEIGHT),
            new("Creature count", COUNT_LEFT, INFO_TOP, COUNT_WIDTH, TEXT_HEIGHT)
        ];
}
