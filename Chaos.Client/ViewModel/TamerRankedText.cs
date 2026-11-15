#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Fixed strings and built lines for the stable window's Ranked tab (ranked ladder spec section 11.3), and the
///     label widths of section 11.2 so a test can hold each string to its label. Pure: no textures. Every string is
///     plain ASCII, which is all the bitmap font draws.
/// </summary>
public static class TamerRankedText
{
    /// <summary>The left column and every full-width label in it.</summary>
    public const int LEFT_WIDTH = 186;

    /// <summary>The right column and every full-width label in it.</summary>
    public const int RIGHT_WIDTH = 384;

    public const int POINTS_WIDTH = 60;
    public const int RATING_WIDTH = LEFT_WIDTH - POINTS_WIDTH - 4;
    public const int SET_FROM_WIDTH = 96;
    public const int SQUAD_BUTTON_WIDTH = 26;
    public const int OPPONENTS_WIDTH = 60;

    /// <summary>The daily first win line, between "Opponents" and the attacks left.</summary>
    public const int DAILY_WIN_WIDTH = 94;

    public const int ATTACKS_WIDTH = 150;

    /// <summary>The window's bottom message line, where the server's refusal shows.</summary>
    public const int MESSAGE_WIDTH = TamerStableLayout.MESSAGE_WIDTH;

    /// <summary>The Refresh and Attack buttons.</summary>
    public const int ACTION_BUTTON_WIDTH = 70;

    /// <summary>An offer row's name label, and the tier and step under it.</summary>
    public const int OFFER_NAME_WIDTH = 80;

    /// <summary>The log list's 384 less its 16 wide scroll bar.</summary>
    public const int LOG_ROW_WIDTH = 368;

    /// <summary>The Watch and Revenge buttons.</summary>
    public const int LOG_BUTTON_WIDTH = 80;

    public const int HINT_WIDTH = 212;

    /// <summary>The window's choice box: its question label's width and the whole lines it shows.</summary>
    public const int QUESTION_WIDTH = 300;

    public const int QUESTION_LINES = 3;

    /// <summary>The lines <see cref="DefenceGone" /> takes in the left column.</summary>
    public const int DEFENCE_GONE_LINES = 3;

    public const string Reading = "Reading the ladder...";
    public const string NoDefence = "Save a squad of five as your defence to enter the ladder.";
    public const string NoOpponents = "No opponents yet. Check back later.";

    /// <summary>Shown in the no defence prompt's place when the server could not read the ladder; also why every button is off.</summary>
    public const string Unavailable = "The ladder is unavailable.";

    public const string DefenceGone = "Your defence uses a creature that no longer exists. Save it again.";
    public const string Opponents = "Opponents";
    public const string Refresh = "Refresh";
    public const string Attack = "Attack";
    public const string YourDefence = "Your defence";
    public const string SetFromSquad = "Set from squad:";
    public const string TopTen = "Top 10";
    public const string DefenceLog = "Defence log";
    public const string Watch = "Watch";
    public const string Revenge = "Revenge";
    public const string RevengeHint = "Revenge does not use an attack.";
    public const string Won = "won";
    public const string Lost = "lost";
    public const string TagRevenge = "revenge";
    public const string TagNoRank = "no rank";
    public const string TagRevenged = "revenged";
    public const string DailyWinOpen = "Daily win: open";
    public const string DailyWinPaid = "Daily win: paid";
    public const string RefreshQuestion = "Refresh the opponents? You can do this once a day.";

    //why a button is off; the tab only greys the button, the words are for the state's callers and its tests
    public const string NeedDefence = "Save a defence first.";
    public const string NoAttacksLeft = "You have no attacks left today.";
    public const string EmptyActiveSquad = "Your active squad is empty.";
    public const string NoOpponentThere = "There is no opponent there.";
    public const string NeedFive = "A defence needs a squad of exactly five.";
    public const string NoEntryChosen = "Choose a fight in the log first.";
    public const string NoRevengeThere = "That fight cannot be revenged.";
    public const string AlreadyRevenged = "You have already taken that revenge.";
    public const string NoRefreshLeft = "You have used today's refresh.";
    public const string NoReplayKept = "That fight keeps no replay.";

    private const int MINUTES_PER_HOUR = 60;
    private const int MINUTES_PER_DAY = 1440;
    private const uint SECONDS_PER_HOUR = 3600;
    private const uint SECONDS_PER_DAY = 86400;
    private const int MAX_POINTS = 100;
    private const int AGE_COLUMN = 4;
    private const int NAME_COLUMN = 12;
    private const int RESULT_COLUMN = 4;
    private const int CHANGE_COLUMN = 4;
    private const int PLACE_COLUMN = 2;

    /// <summary>The ladder's names, the card ladder's: Copper 3 (step 0) up to Mythril 1 (step 14).</summary>
    public static string StepName(int step) => TcgRankedState.StepName(step);

    /// <summary>"5m" under an hour, "2h" under a day, then "3d".</summary>
    public static string Age(int minutes)
    {
        var clamped = Math.Max(0, minutes);

        if (clamped < MINUTES_PER_HOUR)
            return $"{clamped}m";

        return clamped < MINUTES_PER_DAY ? $"{clamped / MINUTES_PER_HOUR}h" : $"{clamped / MINUTES_PER_DAY}d";
    }

    /// <summary>"Season 2: 23 days left", in whole days, then whole hours, then "under 1 hour".</summary>
    public static string SeasonLine(int season, uint secondsLeft)
    {
        if (secondsLeft >= SECONDS_PER_DAY)
            return $"Season {season}: {Count(secondsLeft / SECONDS_PER_DAY, "day")} left";

        return secondsLeft >= SECONDS_PER_HOUR
            ? $"Season {season}: {Count(secondsLeft / SECONDS_PER_HOUR, "hour")} left"
            : $"Season {season}: under 1 hour";

        static string Count(uint count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";
    }

    public static string PointsLine(int points) => $"{points}/{MAX_POINTS}";

    /// <summary>How full the points bar is, 0 to 1.</summary>
    public static double PointsFraction(int points) => Math.Clamp(points, 0, MAX_POINTS) / (double)MAX_POINTS;

    public static string RatingLine(int rating) => $"Rating {rating}";

    public static string BestLine(int bestStep) => $"Best: {StepName(bestStep)}";

    public static string AttacksLine(int left, int perDay) => $"Attacks left: {left} of {perDay}";

    /// <summary>Whether today's first win reward is still to be had.</summary>
    public static string DailyWinLine(bool open) => open ? DailyWinOpen : DailyWinPaid;

    /// <summary>A Set from squad button's caption for the 0-based squad.</summary>
    public static string SquadButton(byte squad) => $"{squad + 1}";

    /// <summary>The question for the 0-based squad, numbered as the player counts them.</summary>
    public static string SetDefenceQuestion(byte squad) => $"Save squad {squad + 1} as your ranked defence?";

    public static string AttackQuestion(string name, int attacksLeft)
        => $"Attack {name}? This uses 1 of your {attacksLeft} attacks today.";

    /// <summary>
    ///     The revenge question. When the two have already fought today the revenge would change no rank and still be
    ///     spent (the pair rule), so the question warns instead.
    /// </summary>
    public static string RevengeQuestion(string name, bool foughtToday)
        => foughtToday
            ? $"You and {name} have already fought today, so this revenge will change no rank and will be used up. Take it anyway?"
            : $"Take revenge on {name}? This does not use an attack.";

    /// <summary>
    ///     One defence log row in fixed columns: age, attacker, "won" or "lost", the viewer's point change, and a tag for
    ///     a revenge attack, a fight that changed no rank, or a revenge the viewer has taken. The font is fixed width,
    ///     so the columns line up.
    /// </summary>
    public static string LogRow(TamerRankedLogView? entry)
    {
        if (entry is null)
            return string.Empty;

        var result = entry.Flags.HasFlag(TamerRankedLogFlags.AttackerWon) ? Won : Lost;
        var change = entry.PointChange > 0 ? $"+{entry.PointChange}" : $"{entry.PointChange}";

        var row = $"{Age(entry.AgeMinutes).PadRight(AGE_COLUMN)} {(entry.Attacker ?? string.Empty).PadRight(NAME_COLUMN)} "
                  + $"{result.PadRight(RESULT_COLUMN)} {change.PadRight(CHANGE_COLUMN)} {LogTag(entry.Flags)}";

        return row.TrimEnd();
    }

    /// <summary>
    ///     "revenge" for a revenge attack, "no rank" for a fight that changed none, "revenged" once the viewer has taken
    ///     their revenge on it; more than one is joined with commas, none is empty.
    /// </summary>
    public static string LogTag(TamerRankedLogFlags flags)
    {
        var tags = new List<string>(3);

        if (flags.HasFlag(TamerRankedLogFlags.Revenge))
            tags.Add(TagRevenge);

        if (flags.HasFlag(TamerRankedLogFlags.NoRankChange))
            tags.Add(TagNoRank);

        if (flags.HasFlag(TamerRankedLogFlags.RevengeUsed))
            tags.Add(TagRevenged);

        return string.Join(", ", tags);
    }

    /// <summary>One top 10 row: the place, the name, and the tier and step.</summary>
    public static string TopRow(int place, TamerRankedTopView? entry)
    {
        if (entry is null)
            return string.Empty;

        return $"{$"{place}".PadLeft(PLACE_COLUMN)} {(entry.Name ?? string.Empty).PadRight(NAME_COLUMN)} {StepName(entry.Step)}";
    }
}
