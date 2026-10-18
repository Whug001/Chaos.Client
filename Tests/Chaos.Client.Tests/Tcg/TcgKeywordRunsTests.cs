using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TcgKeywordRunsTests
{
    [Test]
    public void Finds_each_keyword_with_its_position()
    {
        var runs = TcgKeywordRuns.Find("Flying, Ward. Draw a card.");

        runs.Should().Equal(new TcgKeywordRun(0, 6, "Flying"), new TcgKeywordRun(8, 4, "Ward"));
    }

    [Test]
    public void Two_word_keywords_are_one_run()
        => TcgKeywordRuns.Find("First Strike.").Should().Equal(new TcgKeywordRun(0, 12, "First Strike"));

    [Test]
    public void Lowercase_and_buried_words_are_not_keywords()
    {
        TcgKeywordRuns.Find("Give a creature flying.").Should().BeEmpty();
        TcgKeywordRuns.Find("Guardian of the Wardens.").Should().BeEmpty();
    }

    [Test]
    public void Empty_line_has_no_runs()
        => TcgKeywordRuns.Find(string.Empty).Should().BeEmpty();

    [Test]
    public void Phased_is_a_hoverable_term()
        => TcgKeywordRuns.Find("you become Phased.").Should().Equal(new TcgKeywordRun(11, 6, "Phased"));

    [Test]
    public void Split_keyword_reports_a_fragment_run_on_each_line()
    {
        var runs = TcgKeywordRuns.FindInLines(["has First", "Strike now"]);

        runs.Should().HaveCount(2);
        runs[0].Should().Equal(new TcgKeywordRun(4, 5, "First Strike"));
        runs[1].Should().Equal(new TcgKeywordRun(0, 6, "First Strike"));
    }

    [Test]
    public void Unsplit_keywords_are_unaffected()
    {
        var runs = TcgKeywordRuns.FindInLines(["Flying,", "Ward."]);

        runs[0].Should().Equal(new TcgKeywordRun(0, 6, "Flying"));
        runs[1].Should().Equal(new TcgKeywordRun(0, 4, "Ward"));
    }

    [Test]
    public void Adjacent_words_that_form_no_keyword_have_no_runs()
    {
        var runs = TcgKeywordRuns.FindInLines(["First", "Aid"]);

        runs[0].Should().BeEmpty();
        runs[1].Should().BeEmpty();
    }

    [Test]
    public void Split_keyword_still_needs_whole_words()
    {
        TcgKeywordRuns.FindInLines(["Firstborn", "Strike"])[1].Should().BeEmpty();
        TcgKeywordRuns.FindInLines(["First", "Strikes"])[0].Should().BeEmpty();
        TcgKeywordRuns.FindInLines(["First", "Strikes"])[1].Should().BeEmpty();
    }

    [Test]
    public void Split_keyword_match_is_case_sensitive()
    {
        var runs = TcgKeywordRuns.FindInLines(["first", "strike"]);

        runs[0].Should().BeEmpty();
        runs[1].Should().BeEmpty();
    }

    [Test]
    public void No_lines_means_no_runs()
        => TcgKeywordRuns.FindInLines(Array.Empty<string>()).Should().BeEmpty();
}
