namespace Chaos.Client.ViewModel;

/// <summary>
///     Where every control of the Ranked tab sits (ranked ladder spec section 11.2), in the tab's own coordinates:
///     580 by 312, with a left column 186 wide and a right column 384 wide. Offer row and log row parts are in their
///     row's coordinates. Pure: no textures, so a test proves nothing overlaps or leaves the tab.
/// </summary>
public static class TamerRankedLayout
{
    public const int WIDTH = TamerStableLayout.CONTENT_WIDTH;
    public const int HEIGHT = TamerStableLayout.CONTENT_HEIGHT;
    public const int TEXT_HEIGHT = TamerStableLayout.TEXT_HEIGHT;
    public const int BUTTON_HEIGHT = TamerStableLayout.BUTTON_HEIGHT;

    //the left column
    public const int LEFT_X = 0;
    public const int LEFT_WIDTH = TamerRankedText.LEFT_WIDTH;
    public const int STEP_TOP = 0;
    public const int BAR_TOP = 16;
    public const int BAR_HEIGHT = 10;
    public const int POINTS_TOP = 28;
    public const int POINTS_WIDTH = TamerRankedText.POINTS_WIDTH;
    public const int RATING_WIDTH = TamerRankedText.RATING_WIDTH;
    public const int RATING_LEFT = LEFT_X + LEFT_WIDTH - RATING_WIDTH;
    public const int BEST_TOP = 44;
    public const int SEASON_TOP = 58;
    public const int YOUR_DEFENCE_TOP = 78;
    public const int DEFENCE_TOP = 92;
    public const int DEFENCE_PORTRAIT = 34;
    public const int DEFENCE_STEP = 38;
    public const int DEFENCE_COUNT = TamerRankedState.MAX_DEFENCE;

    /// <summary>The "save it again" line takes the portraits' place: three lines and the label's 1 pixel padding.</summary>
    public const int DEFENCE_GONE_HEIGHT = (TamerRankedText.DEFENCE_GONE_LINES * TEXT_HEIGHT) + 2;

    public const int SET_FROM_TOP = 135;
    public const int SET_FROM_WIDTH = TamerRankedText.SET_FROM_WIDTH;
    public const int SQUAD_BUTTON_LEFT = 100;
    public const int SQUAD_BUTTON_TOP = 130;
    public const int SQUAD_BUTTON_WIDTH = TamerRankedText.SQUAD_BUTTON_WIDTH;
    public const int SQUAD_BUTTON_STEP = 29;
    public const int SQUAD_BUTTON_COUNT = TamerRankedState.SQUAD_COUNT;
    public const int TOP_TITLE_TOP = 156;
    public const int TOP_ROWS_TOP = 170;
    public const int TOP_ROW_STEP = 14;
    public const int TOP_ROW_COUNT = TamerRankedState.MAX_TOP;

    //the right column
    public const int RIGHT_X = 196;
    public const int RIGHT_WIDTH = TamerRankedText.RIGHT_WIDTH;
    public const int HEADER_TEXT_TOP = 5;
    public const int OPPONENTS_WIDTH = TamerRankedText.OPPONENTS_WIDTH;
    public const int DAILY_WIN_LEFT = 258;
    public const int DAILY_WIN_WIDTH = TamerRankedText.DAILY_WIN_WIDTH;
    public const int ATTACKS_LEFT = 356;
    public const int ATTACKS_WIDTH = TamerRankedText.ATTACKS_WIDTH;
    public const int ACTION_BUTTON_WIDTH = TamerRankedText.ACTION_BUTTON_WIDTH;
    public const int REFRESH_LEFT = 510;
    public const int REFRESH_TOP = 0;
    public const int OFFER_TOP = 26;
    public const int OFFER_STEP = 46;
    public const int OFFER_HEIGHT = 44;
    public const int OFFER_COUNT = TamerRankedState.MAX_OFFERS;

    //inside one offer row
    public const int OFFER_NAME_LEFT = 0;
    public const int OFFER_NAME_TOP = 8;
    public const int OFFER_STEP_TOP = 22;
    public const int OFFER_NAME_WIDTH = TamerRankedText.OFFER_NAME_WIDTH;
    public const int OFFER_PORTRAIT_LEFT = 86;
    public const int OFFER_PORTRAIT_TOP = 2;
    public const int OFFER_PORTRAIT = 40;
    public const int OFFER_PORTRAIT_STEP = 42;
    public const int OFFER_PORTRAIT_COUNT = 5;
    public const int ATTACK_LEFT = 310;
    public const int ATTACK_TOP = 11;

    /// <summary>The one line shown in the first offer row's place when there is no defence or nobody to offer.</summary>
    public const int NOTE_TOP = OFFER_TOP + ((OFFER_HEIGHT - TEXT_HEIGHT) / 2);

    public const int LOG_TITLE_TOP = 168;
    public const int LOG_TOP = 182;
    public const int LOG_ROW_HEIGHT = 16;
    public const int LOG_ROWS_SHOWN = 6;
    public const int LOG_HEIGHT = LOG_ROWS_SHOWN * LOG_ROW_HEIGHT;
    public const int SCROLL_BAR_WIDTH = 16;
    public const int LOG_ROW_WIDTH = RIGHT_WIDTH - SCROLL_BAR_WIDTH;
    public const int LOG_BUTTON_TOP = 284;
    public const int LOG_BUTTON_WIDTH = TamerRankedText.LOG_BUTTON_WIDTH;
    public const int WATCH_LEFT = RIGHT_X;
    public const int REVENGE_LEFT = 282;
    public const int HINT_LEFT = 368;
    public const int HINT_TOP = LOG_BUTTON_TOP + ((BUTTON_HEIGHT - TEXT_HEIGHT) / 2);
    public const int HINT_WIDTH = TamerRankedText.HINT_WIDTH;

    /// <summary>The "Reading the ladder..." line, alone in the middle of the tab.</summary>
    public const int READING_TOP = (HEIGHT - TEXT_HEIGHT) / 2;

    /// <summary>The whole tab.</summary>
    public static TamerBox Area => new("Ranked tab", 0, 0, WIDTH, HEIGHT);

    public static TamerBox LeftColumn => new("Left column", LEFT_X, 0, LEFT_WIDTH, HEIGHT);
    public static TamerBox RightColumn => new("Right column", RIGHT_X, 0, RIGHT_WIDTH, HEIGHT);

    public static TamerBox OfferRowArea => new("Offer row", 0, 0, RIGHT_WIDTH, OFFER_HEIGHT);

    public static TamerBox Step => new("Tier and step", LEFT_X, STEP_TOP, LEFT_WIDTH, TEXT_HEIGHT);
    public static TamerBox Bar => new("Points bar", LEFT_X, BAR_TOP, LEFT_WIDTH, BAR_HEIGHT);
    public static TamerBox Points => new("Points", LEFT_X, POINTS_TOP, POINTS_WIDTH, TEXT_HEIGHT);
    public static TamerBox Rating => new("Rating", RATING_LEFT, POINTS_TOP, RATING_WIDTH, TEXT_HEIGHT);
    public static TamerBox Best => new("Best", LEFT_X, BEST_TOP, LEFT_WIDTH, TEXT_HEIGHT);
    public static TamerBox Season => new("Season", LEFT_X, SEASON_TOP, LEFT_WIDTH, TEXT_HEIGHT);
    public static TamerBox YourDefence => new("Your defence", LEFT_X, YOUR_DEFENCE_TOP, LEFT_WIDTH, TEXT_HEIGHT);
    public static TamerBox DefenceGone => new("Defence gone line", LEFT_X, DEFENCE_TOP, LEFT_WIDTH, DEFENCE_GONE_HEIGHT);
    public static TamerBox SetFrom => new("Set from squad", LEFT_X, SET_FROM_TOP, SET_FROM_WIDTH, TEXT_HEIGHT);
    public static TamerBox TopTitle => new("Top 10", LEFT_X, TOP_TITLE_TOP, LEFT_WIDTH, TEXT_HEIGHT);
    public static TamerBox Opponents => new("Opponents", RIGHT_X, HEADER_TEXT_TOP, OPPONENTS_WIDTH, TEXT_HEIGHT);
    public static TamerBox DailyWin => new("Daily win", DAILY_WIN_LEFT, HEADER_TEXT_TOP, DAILY_WIN_WIDTH, TEXT_HEIGHT);
    public static TamerBox Attacks => new("Attacks left", ATTACKS_LEFT, HEADER_TEXT_TOP, ATTACKS_WIDTH, TEXT_HEIGHT);
    public static TamerBox Refresh => new("Refresh button", REFRESH_LEFT, REFRESH_TOP, ACTION_BUTTON_WIDTH, BUTTON_HEIGHT);
    public static TamerBox Note => new("No defence / no opponents line", RIGHT_X, NOTE_TOP, RIGHT_WIDTH, TEXT_HEIGHT);
    public static TamerBox LogTitle => new("Defence log", RIGHT_X, LOG_TITLE_TOP, RIGHT_WIDTH, TEXT_HEIGHT);
    public static TamerBox LogList => new("Log list", RIGHT_X, LOG_TOP, RIGHT_WIDTH, LOG_HEIGHT);
    public static TamerBox Watch => new("Watch button", WATCH_LEFT, LOG_BUTTON_TOP, LOG_BUTTON_WIDTH, BUTTON_HEIGHT);
    public static TamerBox Revenge => new("Revenge button", REVENGE_LEFT, LOG_BUTTON_TOP, LOG_BUTTON_WIDTH, BUTTON_HEIGHT);
    public static TamerBox Hint => new("Revenge hint", HINT_LEFT, HINT_TOP, HINT_WIDTH, TEXT_HEIGHT);
    public static TamerBox Reading => new("Reading line", 0, READING_TOP, WIDTH, TEXT_HEIGHT);

    public static TamerBox DefencePortrait(int index)
        => new($"Defence portrait {index + 1}", LEFT_X + (index * DEFENCE_STEP), DEFENCE_TOP, DEFENCE_PORTRAIT, DEFENCE_PORTRAIT);

    public static TamerBox SquadButton(int index)
        => new($"Squad button {index + 1}", SQUAD_BUTTON_LEFT + (index * SQUAD_BUTTON_STEP), SQUAD_BUTTON_TOP, SQUAD_BUTTON_WIDTH, BUTTON_HEIGHT);

    public static TamerBox TopRow(int index)
        => new($"Top 10 row {index + 1}", LEFT_X, TOP_ROWS_TOP + (index * TOP_ROW_STEP), LEFT_WIDTH, TEXT_HEIGHT);

    public static TamerBox OfferRow(int slot)
        => new($"Offer row {slot + 1}", RIGHT_X, OFFER_TOP + (slot * OFFER_STEP), RIGHT_WIDTH, OFFER_HEIGHT);

    /// <summary>The parts of one offer row, in the row's own coordinates.</summary>
    public static IReadOnlyList<TamerBox> OfferRowParts()
    {
        var parts = new List<TamerBox>
        {
            new("name", OFFER_NAME_LEFT, OFFER_NAME_TOP, OFFER_NAME_WIDTH, TEXT_HEIGHT),
            new("tier and step", OFFER_NAME_LEFT, OFFER_STEP_TOP, OFFER_NAME_WIDTH, TEXT_HEIGHT)
        };

        for (var i = 0; i < OFFER_PORTRAIT_COUNT; i++)
            parts.Add(OfferPortrait(i));

        parts.Add(new TamerBox("Attack button", ATTACK_LEFT, ATTACK_TOP, ACTION_BUTTON_WIDTH, BUTTON_HEIGHT));

        return parts;
    }

    public static TamerBox OfferPortrait(int index)
        => new($"portrait {index + 1}", OFFER_PORTRAIT_LEFT + (index * OFFER_PORTRAIT_STEP), OFFER_PORTRAIT_TOP, OFFER_PORTRAIT, OFFER_PORTRAIT);

    /// <summary>
    ///     Every control the tab shows at once with a defence and a full offer, in the tab's coordinates, offer row
    ///     parts included. The three lines that take another control's place are in <see cref="Replacements" />.
    /// </summary>
    public static IReadOnlyList<TamerBox> Controls()
    {
        var boxes = new List<TamerBox> { Step, Bar, Points, Rating, Best, Season, YourDefence };

        for (var i = 0; i < DEFENCE_COUNT; i++)
            boxes.Add(DefencePortrait(i));

        boxes.Add(SetFrom);

        for (var i = 0; i < SQUAD_BUTTON_COUNT; i++)
            boxes.Add(SquadButton(i));

        boxes.Add(TopTitle);

        for (var i = 0; i < TOP_ROW_COUNT; i++)
            boxes.Add(TopRow(i));

        boxes.Add(Opponents);
        boxes.Add(DailyWin);
        boxes.Add(Attacks);
        boxes.Add(Refresh);

        for (var slot = 0; slot < OFFER_COUNT; slot++)
        {
            var row = OfferRow(slot);

            foreach (var part in OfferRowParts())
                boxes.Add(part.Offset(row.X, row.Y, $"{row.Name} {part.Name}"));
        }

        boxes.Add(LogTitle);
        boxes.Add(LogList);
        boxes.Add(Watch);
        boxes.Add(Revenge);
        boxes.Add(Hint);

        return boxes;
    }

    /// <summary>
    ///     The lines shown in place of other controls, each with the name prefix of what it replaces: the defence gone
    ///     line for the defence portraits, the note for the offer rows, and the reading line for everything.
    /// </summary>
    public static IReadOnlyList<(TamerBox Box, string Replaces)> Replacements()
        => [(DefenceGone, "Defence portrait"), (Note, "Offer row"), (Reading, string.Empty)];
}
