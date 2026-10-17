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
}
