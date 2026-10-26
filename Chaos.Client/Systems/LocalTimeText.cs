#region
using System.Globalization;
using System.Text.RegularExpressions;
#endregion

namespace Chaos.Client.Systems;

/// <summary>
///     Rewrites the UTC timestamps the server writes as English text into the player's local time. Pure and
///     graphics-free: takes the text, the current UTC instant, and the zone, and returns display text.
/// </summary>
/// <remarks>
///     The server writes times in seven forms (always <see cref="CultureInfo.InvariantCulture" /> English):
///     <list type="bullet">
///         <item>Full date, e.g. "Sat Oct 31 01:00 UTC". The year is the one that puts the instant nearest to now.</item>
///         <item>Weekday time, e.g. "Sat 01:00 UTC". The weekday nearest to now (within -3..+3 days).</item>
///         <item>Time today, e.g. "01:00 UTC" or "01:00 (UTC)". The UTC date of now.</item>
///         <item>Long month day, e.g. "November 3, 02:00 UTC". The year nearest to now.</item>
///         <item>ISO date, e.g. "2026-11-05 14:00 UTC".</item>
///         <item>Lottery date, e.g. "Nov 07, 2026 03:00 UTC".</item>
///         <item>Moderation stamp, e.g. "27 Oct 2026 18:00 UTC".</item>
///     Every form also accepts " bell" in place of " UTC" (College text keeps the word "bell").
///     </list>
///     Full and weekday forms render as "ddd h:mm tt" in the local weekday (e.g. "Fri 6:00 PM"); the full form
///     adds " MMM d" after the weekday when the date is more than 6 days away. The time-today form renders as
///     "h:mm tt", with the local weekday in front when the local date differs from the UTC date.
///     Dated forms render the converted local date in the style the input used (e.g. "November 2, 9:00 PM",
///     "2026-11-05 9:00 AM", "Nov 06, 2026 10:00 PM", "27 Oct 2026 2:00 PM"). Text with no
///     match is returned unchanged (the same instance).
/// </remarks>
public static class LocalTimeText
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, int> Months = new(StringComparer.Ordinal)
    {
        ["Jan"] = 1,
        ["Feb"] = 2,
        ["Mar"] = 3,
        ["Apr"] = 4,
        ["May"] = 5,
        ["Jun"] = 6,
        ["Jul"] = 7,
        ["Aug"] = 8,
        ["Sep"] = 9,
        ["Oct"] = 10,
        ["Nov"] = 11,
        ["Dec"] = 12
    };

    private static readonly Dictionary<string, DayOfWeek> Weekdays = new(StringComparer.Ordinal)
    {
        ["Mon"] = DayOfWeek.Monday,
        ["Tue"] = DayOfWeek.Tuesday,
        ["Wed"] = DayOfWeek.Wednesday,
        ["Thu"] = DayOfWeek.Thursday,
        ["Fri"] = DayOfWeek.Friday,
        ["Sat"] = DayOfWeek.Saturday,
        ["Sun"] = DayOfWeek.Sunday
    };

    private static readonly Dictionary<string, int> FullMonths = new(StringComparer.Ordinal)
    {
        ["January"] = 1,
        ["February"] = 2,
        ["March"] = 3,
        ["April"] = 4,
        ["May"] = 5,
        ["June"] = 6,
        ["July"] = 7,
        ["August"] = 8,
        ["September"] = 9,
        ["October"] = 10,
        ["November"] = 11,
        ["December"] = 12
    };

    private const string WeekdayNames = "Mon|Tue|Wed|Thu|Fri|Sat|Sun";
    private const string MonthNames = "Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec";
    private const string FullMonthNames = "January|February|March|April|May|June|July|August|September|October|November|December";

    /// <summary>Every form ends in "UTC", "(UTC)" or the College " bell"; "cdbell" never matches (no time, no space).</summary>
    private const string Suffix = @"(?:UTC\b|\(UTC\)|bell\b)";

    private static readonly Regex LongMonthDay = new(
        $@"\b({FullMonthNames}) (\d{{1,2}}), (\d{{1,2}}):(\d{{2}}) {Suffix}",
        RegexOptions.Compiled);

    private static readonly Regex IsoDateTime = new(
        $@"\b(\d{{4}})-(\d{{2}})-(\d{{2}}) (\d{{1,2}}):(\d{{2}}) {Suffix}",
        RegexOptions.Compiled);

    private static readonly Regex LotteryDate = new(
        $@"\b({MonthNames}) (\d{{1,2}}), (\d{{4}}) (\d{{1,2}}):(\d{{2}}) {Suffix}",
        RegexOptions.Compiled);

    private static readonly Regex ModerationStamp = new(
        $@"\b(\d{{1,2}}) ({MonthNames}) (\d{{4}}) (\d{{1,2}}):(\d{{2}}) {Suffix}",
        RegexOptions.Compiled);

    private static readonly Regex FullDate = new(
        $@"\b({WeekdayNames}) ({MonthNames}) (\d{{1,2}}) (\d{{1,2}}):(\d{{2}}) {Suffix}",
        RegexOptions.Compiled);

    private static readonly Regex WeekdayTime = new(
        $@"\b({WeekdayNames}) (\d{{1,2}}):(\d{{2}}) {Suffix}",
        RegexOptions.Compiled);

    private static readonly Regex TimeToday = new(
        $@"\b(\d{{1,2}}):(\d{{2}}) {Suffix}",
        RegexOptions.Compiled);

    /// <summary>
    ///     Returns <paramref name="text" /> with every server UTC timestamp replaced by its local-time form.
    /// </summary>
    /// <param name="text">Player-visible text that may contain server UTC timestamps.</param>
    /// <param name="utcNow">The current UTC instant, used to resolve dateless forms.</param>
    /// <param name="zone">The player's local zone.</param>
    public static string Rewrite(string text, DateTime utcNow, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(zone);

        //cheap gate: every form ends in "UTC" or " bell", so text without either cannot match
        if (!text.Contains("UTC", StringComparison.Ordinal) && !text.Contains(" bell", StringComparison.Ordinal))
            return text;

        //dated forms first: their "HH:mm UTC" tail would otherwise match the shorter forms
        var result = LongMonthDay.Replace(text, m => ReplaceLongMonthDay(m, utcNow, zone));
        result = IsoDateTime.Replace(result, m => ReplaceIsoDateTime(m, zone));
        result = LotteryDate.Replace(result, m => ReplaceLotteryDate(m, zone));
        result = ModerationStamp.Replace(result, m => ReplaceModerationStamp(m, zone));
        result = FullDate.Replace(result, m => ReplaceFullDate(m, utcNow, zone));
        result = WeekdayTime.Replace(result, m => ReplaceWeekdayTime(m, utcNow, zone));
        result = TimeToday.Replace(result, m => ReplaceTimeToday(m, utcNow, zone));

        return result;
    }

    private static string ReplaceLongMonthDay(Match match, DateTime utcNow, TimeZoneInfo zone)
    {
        if (!FullMonths.TryGetValue(match.Groups[1].Value, out var month))
            return match.Value;

        if (!int.TryParse(match.Groups[2].Value, NumberStyles.None, Invariant, out var day))
            return match.Value;

        if (!TryParseTime(match.Groups[3].Value, match.Groups[4].Value, out var hour, out var minute))
            return match.Value;

        //the server omits the year: pick the adjacent-year candidate nearest to now
        DateTime? best = null;
        var bestDistance = TimeSpan.MaxValue;

        for (var year = utcNow.Year - 1; year <= utcNow.Year + 1; year++)
        {
            DateTime candidate;

            try
            {
                candidate = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            var distance = (candidate - utcNow).Duration();

            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        if (best is null)
            return match.Value;

        var local = TimeZoneInfo.ConvertTimeFromUtc(best.Value, zone);

        return $"{local.ToString("MMMM d", Invariant)}, {local.ToString("h:mm tt", Invariant)}";
    }

    private static string ReplaceIsoDateTime(Match match, TimeZoneInfo zone)
    {
        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, Invariant, out var year))
            return match.Value;

        if (!int.TryParse(match.Groups[2].Value, NumberStyles.None, Invariant, out var month))
            return match.Value;

        if (!int.TryParse(match.Groups[3].Value, NumberStyles.None, Invariant, out var day))
            return match.Value;

        if (!TryParseTime(match.Groups[4].Value, match.Groups[5].Value, out var hour, out var minute))
            return match.Value;

        DateTime candidate;

        try
        {
            candidate = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException)
        {
            return match.Value;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(candidate, zone);

        return $"{local.ToString("yyyy-MM-dd", Invariant)} {local.ToString("h:mm tt", Invariant)}";
    }

    private static string ReplaceLotteryDate(Match match, TimeZoneInfo zone)
    {
        if (!Months.TryGetValue(match.Groups[1].Value, out var month))
            return match.Value;

        if (!int.TryParse(match.Groups[2].Value, NumberStyles.None, Invariant, out var day))
            return match.Value;

        if (!int.TryParse(match.Groups[3].Value, NumberStyles.None, Invariant, out var year))
            return match.Value;

        if (!TryParseTime(match.Groups[4].Value, match.Groups[5].Value, out var hour, out var minute))
            return match.Value;

        DateTime candidate;

        try
        {
            candidate = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException)
        {
            return match.Value;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(candidate, zone);

        return $"{local.ToString("MMM dd, yyyy", Invariant)} {local.ToString("h:mm tt", Invariant)}";
    }

    private static string ReplaceModerationStamp(Match match, TimeZoneInfo zone)
    {
        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, Invariant, out var day))
            return match.Value;

        if (!Months.TryGetValue(match.Groups[2].Value, out var month))
            return match.Value;

        if (!int.TryParse(match.Groups[3].Value, NumberStyles.None, Invariant, out var year))
            return match.Value;

        if (!TryParseTime(match.Groups[4].Value, match.Groups[5].Value, out var hour, out var minute))
            return match.Value;

        DateTime candidate;

        try
        {
            candidate = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException)
        {
            return match.Value;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(candidate, zone);

        return $"{local.Day} {local.ToString("MMM yyyy", Invariant)} {local.ToString("h:mm tt", Invariant)}";
    }

    private static string ReplaceFullDate(Match match, DateTime utcNow, TimeZoneInfo zone)
    {
        if (!Months.TryGetValue(match.Groups[2].Value, out var month))
            return match.Value;

        if (!TryParseTime(match.Groups[4].Value, match.Groups[5].Value, out var hour, out var minute))
            return match.Value;

        if (!int.TryParse(match.Groups[3].Value, NumberStyles.None, Invariant, out var day))
            return match.Value;

        //the server omits the year: pick the adjacent-year candidate nearest to now
        DateTime? best = null;
        var bestDistance = TimeSpan.MaxValue;

        for (var year = utcNow.Year - 1; year <= utcNow.Year + 1; year++)
        {
            DateTime candidate;

            try
            {
                candidate = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            var distance = (candidate - utcNow).Duration();

            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        if (best is null)
            return match.Value;

        var local = TimeZoneInfo.ConvertTimeFromUtc(best.Value, zone);
        var time = local.ToString("h:mm tt", Invariant);

        if (Math.Abs((best.Value.Date - utcNow.Date).TotalDays) > 6)
            return $"{local.ToString("ddd", Invariant)} {local.ToString("MMM d", Invariant)} {time}";

        return $"{local.ToString("ddd", Invariant)} {time}";
    }

    private static string ReplaceWeekdayTime(Match match, DateTime utcNow, TimeZoneInfo zone)
    {
        if (!Weekdays.TryGetValue(match.Groups[1].Value, out var weekday))
            return match.Value;

        if (!TryParseTime(match.Groups[2].Value, match.Groups[3].Value, out var hour, out var minute))
            return match.Value;

        //the weekday must fall within -3..+3 days of now, which holds exactly one match
        DateTime? best = null;
        var bestDistance = double.MaxValue;

        for (var offset = -3; offset <= 3; offset++)
        {
            var candidate = new DateTime(
                utcNow.Year,
                utcNow.Month,
                utcNow.Day,
                hour,
                minute,
                0,
                DateTimeKind.Utc).AddDays(offset);

            if (candidate.DayOfWeek != weekday)
                continue;

            var distance = Math.Abs((candidate - utcNow).TotalSeconds);

            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        if (best is null)
            return match.Value;

        var local = TimeZoneInfo.ConvertTimeFromUtc(best.Value, zone);

        return $"{local.ToString("ddd", Invariant)} {local.ToString("h:mm tt", Invariant)}";
    }

    private static string ReplaceTimeToday(Match match, DateTime utcNow, TimeZoneInfo zone)
    {
        if (!TryParseTime(match.Groups[1].Value, match.Groups[2].Value, out var hour, out var minute))
            return match.Value;

        var candidate = new DateTime(
            utcNow.Year,
            utcNow.Month,
            utcNow.Day,
            hour,
            minute,
            0,
            DateTimeKind.Utc);

        var local = TimeZoneInfo.ConvertTimeFromUtc(candidate, zone);
        var time = local.ToString("h:mm tt", Invariant);

        //name the local weekday when the conversion crossed midnight
        if (local.Date != candidate.Date)
            return $"{local.ToString("ddd", Invariant)} {time}";

        return time;
    }

    private static bool TryParseTime(string hourText, string minuteText, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;

        return int.TryParse(hourText, NumberStyles.None, Invariant, out hour)
               && int.TryParse(minuteText, NumberStyles.None, Invariant, out minute)
               && (hour is >= 0 and <= 23)
               && (minute is >= 0 and <= 59);
    }
}
