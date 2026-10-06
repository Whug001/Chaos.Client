using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class QuizDocumentTests
{
    private static QuizDocument Fresh()
    {
        var document = new QuizDocument();
        document.New();

        return document;
    }

    [Test]
    public void New_gives_one_blank_question_that_is_clean()
    {
        var document = Fresh();

        document.Questions.Should().ContainSingle();
        document.Selected.Should().Be(0);
        document.Id.Should().BeEmpty();
        document.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Add_stops_at_20_and_selects_the_new_question()
    {
        var document = Fresh();

        for (var i = 1; i < 20; i++)
            document.Add().Should().BeTrue();

        document.Add().Should().BeFalse();
        document.Questions.Should().HaveCount(20);
        document.Selected.Should().Be(19);
    }

    [Test]
    public void Moving_keeps_the_question_selected()
    {
        var document = Fresh();
        document.SetText("first");
        document.Add();
        document.SetText("second");

        document.MoveUp().Should().BeTrue();

        document.Questions.Select(q => q.Text).Should().Equal("second", "first");
        document.Selected.Should().Be(0);
        document.MoveUp().Should().BeFalse();
        document.MoveDown().Should().BeTrue();
        document.Selected.Should().Be(1);
        document.MoveDown().Should().BeFalse();
    }

    [Test]
    public void Removing_the_last_question_leaves_a_blank_one()
    {
        var document = Fresh();
        document.SetText("only");

        document.Remove().Should().BeTrue();

        document.Questions.Should().ContainSingle().Which.Text.Should().BeEmpty();
        document.Selected.Should().Be(0);
    }

    [Test]
    public void Removing_the_bottom_question_selects_the_one_above()
    {
        var document = Fresh();
        document.Add();
        document.Add();

        document.Remove();

        document.Selected.Should().Be(1);
        document.Questions.Should().HaveCount(2);
    }

    [Test]
    public void Edits_mark_it_dirty_and_raise_changed_but_selecting_does_not_mark_it()
    {
        var document = Fresh();
        document.Add();
        document.MarkClean();
        var raised = 0;
        document.Changed += () => raised++;

        document.Select(0);
        document.IsDirty.Should().BeFalse();

        document.SetTitle("Mileth history");
        document.SetText("Who?");
        document.SetAnswer(0, "Danaan");
        document.MarkRight(0);
        document.SetText("Who?");

        document.IsDirty.Should().BeTrue();
        raised.Should().Be(5);
    }

    [Test]
    public void To_info_drops_trailing_blank_answers_and_keeps_inner_ones()
    {
        var document = Fresh();
        document.SetTitle("T");
        document.SetText("Q?");
        document.SetAnswer(0, "a");
        document.SetAnswer(2, "c");
        document.MarkRight(2);

        var question = document.ToInfo().Questions.Single();

        question.Answers.Should().Equal("a", "", "c");
        question.Right.Should().Be(2);
    }

    [Test]
    public void A_right_answer_that_was_dropped_or_never_marked_is_sent_as_none()
    {
        var document = Fresh();
        document.SetAnswer(0, "a");
        document.MarkRight(3);

        document.ToInfo().Questions.Single().Right.Should().Be(CollegeProtocol.NO_ANSWER);

        document.Add();
        document.SetAnswer(0, "a");

        document.ToInfo().Questions[1].Right.Should().Be(CollegeProtocol.NO_ANSWER);
    }

    [Test]
    public void Load_reads_the_quiz_and_cuts_long_text()
    {
        var document = new QuizDocument();

        document.Load(
            "abc",
            new CollegeQuizDraftInfo
            {
                Title = new string('t', 50),
                Questions =
                [
                    new CollegeQuizQuestionInfo { Text = "One?", Answers = ["a", "b"], Right = 1 },
                    new CollegeQuizQuestionInfo { Text = new string('q', 200), Answers = ["x"], Right = CollegeProtocol.NO_ANSWER }
                ]
            });

        document.Id.Should().Be("abc");
        document.Title.Should().HaveLength(40);
        document.Questions.Should().HaveCount(2);
        document.Questions[0].Answers.Should().Equal("a", "b", "", "");
        document.Questions[0].Right.Should().Be(1);
        document.Questions[1].Text.Should().HaveLength(150);
        document.Questions[1].Right.Should().Be(-1);
        document.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Labels_number_the_questions()
    {
        var document = Fresh();
        document.SetText("Who founded the temple?");
        document.Add();

        document.Label(0).Should().Be("1. Who founded the temple?");
        document.Label(1).Should().Be("2. (empty)");
    }
}
