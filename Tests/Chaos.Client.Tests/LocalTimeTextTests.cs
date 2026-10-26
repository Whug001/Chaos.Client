using Chaos.Client.Systems;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class LocalTimeTextTests
{
    private static TimeZoneInfo PacificZone()
    {
        foreach (var id in new[] { "America/Los_Angeles", "Pacific Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        //fallback so the tests run anywhere: US Pacific rules (UTC-8, DST +1h,
        //second Sunday of March through first Sunday of November)
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2000, 1, 1),
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday));

        return TimeZoneInfo.CreateCustomTimeZone(
            "Test Pacific",
            TimeSpan.FromHours(-8),
            "Test Pacific",
            "Test Pacific",
            "Test Pacific Daylight",
            [rule]);
    }

    private static readonly TimeZoneInfo Pacific = PacificZone();

    private static TimeZoneInfo EasternZone()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        //fallback so the tests run anywhere: US Eastern rules (UTC-5, DST +1h,
        //second Sunday of March through first Sunday of November)
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2000, 1, 1),
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday));

        return TimeZoneInfo.CreateCustomTimeZone(
            "Test Eastern",
            TimeSpan.FromHours(-5),
            "Test Eastern",
            "Test Eastern",
            "Test Eastern Daylight",
            [rule]);
    }

    private static readonly TimeZoneInfo Eastern = EasternZone();

    private static DateTime Utc(int year, int month, int day, int hour, int minute)
        => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Test]
    public void FullDate_Near_RewritesToLocalWeekdayAndTime()
    {
        //Sat Oct 31 01:00 UTC is Fri Oct 30 6:00 PM in PDT (UTC-7)
        var now = Utc(2026, 10, 31, 12, 0);

        LocalTimeText.Rewrite("The event begins Sat Oct 31 01:00 UTC, do not be late.", now, Pacific)
                     .Should()
                     .Be("The event begins Fri 6:00 PM, do not be late.");
    }

    [Test]
    public void FullDate_AcrossDstChange_UsesStandardOffset()
    {
        //Nov 2 2026 is past the Nov 1 fallback: 01:00 UTC is Nov 1 5:00 PM in PST (UTC-8)
        var now = Utc(2026, 11, 2, 12, 0);

        LocalTimeText.Rewrite("Mon Nov 2 01:00 UTC", now, Pacific).Should().Be("Sun 5:00 PM");
    }

    [Test]
    public void FullDate_Far_IncludesMonthAndDay()
    {
        //nearest Jan 3 to Oct 31 2026 is Jan 3 2027 (a Sunday); in PST that is Sat Jan 2 5:00 PM
        var now = Utc(2026, 10, 31, 12, 0);

        LocalTimeText.Rewrite("Sat Jan 3 01:00 UTC", now, Pacific).Should().Be("Sat Jan 2 5:00 PM");
    }

    [Test]
    public void FullDate_AcrossYearBoundary_PicksNearestYear()
    {
        //Jan 1 2027 (a Thursday) is one day after Dec 31 2026, so no month is added;
        //01:00 UTC is Dec 31 5:00 PM in PST
        var now = Utc(2026, 12, 31, 12, 0);

        LocalTimeText.Rewrite("Thu Jan 1 01:00 UTC", now, Pacific).Should().Be("Thu 5:00 PM");
    }

    [Test]
    public void WeekdayTime_ResolvesNearestWeekday()
    {
        var now = Utc(2026, 10, 31, 12, 0);

        LocalTimeText.Rewrite("Meet Sat 01:00 UTC at the gate.", now, Pacific)
                     .Should()
                     .Be("Meet Fri 6:00 PM at the gate.");
    }

    [Test]
    public void WeekdayTime_PreviousDay_WhenCloser()
    {
        //00:30 UTC Sunday: the nearest Saturday 01:00 UTC is yesterday, still PDT
        var now = Utc(2026, 11, 1, 0, 30);

        LocalTimeText.Rewrite("Sat 01:00 UTC", now, Pacific).Should().Be("Fri 6:00 PM");
    }

    [Test]
    public void TimeToday_SameDate_OmitsWeekday()
    {
        var now = Utc(2026, 10, 31, 12, 0);

        LocalTimeText.Rewrite("Meet at 18:00 UTC.", now, Pacific).Should().Be("Meet at 11:00 AM.");
        LocalTimeText.Rewrite("Meet at 18:00 (UTC).", now, Pacific).Should().Be("Meet at 11:00 AM.");
    }

    [Test]
    public void TimeToday_CrossesMidnight_AddsLocalWeekday()
    {
        var now = Utc(2026, 10, 31, 12, 0);

        LocalTimeText.Rewrite("Doors open 01:00 UTC.", now, Pacific).Should().Be("Doors open Fri 6:00 PM.");
        LocalTimeText.Rewrite("Doors open 01:00 (UTC).", now, Pacific).Should().Be("Doors open Fri 6:00 PM.");
    }

    [Test]
    public void MultipleMatches_AllReplaced()
    {
        var now = Utc(2026, 10, 31, 12, 0);

        LocalTimeText.Rewrite("From 18:00 UTC to 20:00 UTC.", now, Pacific)
                     .Should()
                     .Be("From 11:00 AM to 1:00 PM.");
    }

    [Test]
    public void NoMatch_ReturnsSameInstance()
    {
        var now = Utc(2026, 10, 31, 12, 0);
        var text = "Hello, no times here.";

        ReferenceEquals(LocalTimeText.Rewrite(text, now, Pacific), text).Should().BeTrue();
    }

    [Test]
    public void UtcWithoutTimestamp_LeftUnchanged()
    {
        var now = Utc(2026, 10, 31, 12, 0);

        LocalTimeText.Rewrite("The UTC offset confuses me.", now, Pacific)
                     .Should()
                     .Be("The UTC offset confuses me.");
        LocalTimeText.Rewrite("Meet at 25:00 UTC.", now, Pacific).Should().Be("Meet at 25:00 UTC.");
    }

    [Test]
    public void LongMonthDay_CrossesMidnight_UsesThatDatesOffset()
    {
        //Nov 3 2026 is past the Nov 1 fallback, so 02:00 UTC is Nov 2 9:00 PM in EST (UTC-5)
        var now = Utc(2026, 10, 26, 12, 0);

        LocalTimeText.Rewrite("Entries close November 3, 02:00 UTC", now, Eastern)
                     .Should()
                     .Be("Entries close November 2, 9:00 PM");
    }

    [Test]
    public void LongMonthDay_AcrossDstChange_UsesStandardOffset()
    {
        //Nov 2 2026 is past the Nov 1 fallback: 06:30 UTC is 1:30 AM in EST (UTC-5)
        var now = Utc(2026, 10, 26, 12, 0);

        LocalTimeText.Rewrite("November 2, 06:30 UTC", now, Eastern).Should().Be("November 2, 1:30 AM");
    }

    [Test]
    public void IsoDateTime_ConvertsWithThatDatesOffset()
    {
        //Nov 5 2026 is EST (UTC-5): 14:00 UTC is 9:00 AM the same local date
        var now = Utc(2026, 10, 26, 12, 0);

        LocalTimeText.Rewrite("Season 3 ends 2026-11-05 14:00 UTC.", now, Eastern)
                     .Should()
                     .Be("Season 3 ends 2026-11-05 9:00 AM.");
    }

    [Test]
    public void LotteryDate_CrossesMidnight_ShowsLocalDate()
    {
        //Nov 7 2026 03:00 UTC is Nov 6 10:00 PM in EST (UTC-5)
        var now = Utc(2026, 10, 26, 12, 0);

        LocalTimeText.Rewrite("Next Drawing: Nov 07, 2026 03:00 UTC", now, Eastern)
                     .Should()
                     .Be("Next Drawing: Nov 06, 2026 10:00 PM");
    }

    [Test]
    public void ModerationStamp_ConvertsWithThatDatesOffset()
    {
        //Oct 27 2026 is still EDT (UTC-4): 18:00 UTC is 2:00 PM the same local date
        var now = Utc(2026, 10, 26, 12, 0);

        LocalTimeText.Rewrite("You are muted until 27 Oct 2026 18:00 UTC", now, Eastern)
                     .Should()
                     .Be("You are muted until 27 Oct 2026 2:00 PM");
    }

    [Test]
    public void BellSuffix_FullDate_MatchesUtcResult()
    {
        var now = Utc(2026, 10, 26, 12, 0);

        var utc = LocalTimeText.Rewrite("The event begins Sat Oct 31 01:00 UTC, do not be late.", now, Eastern);
        var bell = LocalTimeText.Rewrite("The event begins Sat Oct 31 01:00 bell, do not be late.", now, Eastern);

        bell.Should().Be(utc);
        bell.Should().Be("The event begins Fri 9:00 PM, do not be late.");
    }

    [Test]
    public void BellSuffix_WeekdayTime_ResolvesNearestWeekday()
    {
        //Mon Oct 26 14:00 UTC is Mon 10:00 AM in EDT (UTC-4)
        var now = Utc(2026, 10, 26, 12, 0);

        LocalTimeText.Rewrite("Ana, Mon 14:00 bell", now, Eastern).Should().Be("Ana, Mon 10:00 AM");
    }

    [Test]
    public void BellSuffix_DatedForm_MatchesUtcResult()
    {
        var now = Utc(2026, 10, 26, 12, 0);

        LocalTimeText.Rewrite("Season 3 ends 2026-11-05 14:00 bell.", now, Eastern)
                     .Should()
                     .Be(LocalTimeText.Rewrite("Season 3 ends 2026-11-05 14:00 UTC.", now, Eastern));
    }

    [Test]
    public void BellWithoutTime_LeftUnchanged()
    {
        var now = Utc(2026, 10, 26, 12, 0);

        LocalTimeText.Rewrite("the cdbell rang", now, Eastern).Should().Be("the cdbell rang");
        LocalTimeText.Rewrite("ring the bell", now, Eastern).Should().Be("ring the bell");
    }
}
