using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class WordListDocumentTests
{
    private static WordListDocument Fresh(WordListEditMode mode = WordListEditMode.Saved)
    {
        var document = new WordListDocument();
        document.New(mode);

        return document;
    }

    private static CollegeWordListDraftInfo Info(int words)
        => new()
        {
            Title = "Mileth",
            Words = Enumerable.Range(0, words).Select(i => new CollegeWordInfo { Text = $"WORD{(char)('A' + i)}", Hint = $"hint {i}" }).ToList()
        };

    [Test]
    public void New_gives_one_blank_word_that_is_clean()
    {
        var document = Fresh();

        document.Words.Should().ContainSingle();
        document.Selected.Should().Be(0);
        document.Id.Should().BeEmpty();
        document.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Load_then_ToInfo_round_trips()
    {
        var document = new WordListDocument();
        document.Load("abc", Info(3), WordListEditMode.Saved);

        document.Id.Should().Be("abc");
        document.IsDirty.Should().BeFalse();

        var info = document.ToInfo();

        info.Title.Should().Be("Mileth");
        info.Words.Select(w => (w.Text, w.Hint)).Should().Equal(Info(3).Words.Select(w => (w.Text, w.Hint)));
    }

    [Test]
    public void Add_stops_at_30_and_selects_the_new_word()
    {
        var document = Fresh();

        for (var i = 1; i < 30; i++)
            document.Add().Should().BeTrue();

        document.Add().Should().BeFalse();
        document.Words.Should().HaveCount(30);
        document.Selected.Should().Be(29);
        document.MaxWords.Should().Be(30);
    }

    [Test]
    public void Add_stops_at_10_in_quick_mode()
    {
        var document = Fresh(WordListEditMode.Quick);

        for (var i = 1; i < 10; i++)
            document.Add().Should().BeTrue();

        document.Add().Should().BeFalse();
        document.Words.Should().HaveCount(10);
        document.MaxWords.Should().Be(10);
    }

    [Test]
    public void Remove_keeps_the_selection_in_range()
    {
        var document = new WordListDocument();
        document.Load("a", Info(3), WordListEditMode.Saved);
        document.Select(2);

        document.Remove().Should().BeTrue();

        document.Words.Should().HaveCount(2);
        document.Selected.Should().Be(1);
    }

    [Test]
    public void Remove_of_the_last_word_leaves_a_blank_one()
    {
        var document = Fresh();
        document.SetText("CAT");

        document.Remove().Should().BeTrue();

        document.Words.Should().ContainSingle();
        document.Current!.Text.Should().BeEmpty();
    }

    [Test]
    public void MoveUp_at_the_top_and_MoveDown_at_the_bottom_do_nothing()
    {
        var document = new WordListDocument();
        document.Load("a", Info(3), WordListEditMode.Saved);

        document.MoveUp().Should().BeFalse();
        document.IsDirty.Should().BeFalse();

        document.Select(2);
        document.MoveDown().Should().BeFalse();
        document.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Move_swaps_and_follows_the_selection()
    {
        var document = new WordListDocument();
        document.Load("a", Info(3), WordListEditMode.Saved);

        document.MoveDown().Should().BeTrue();

        document.Selected.Should().Be(1);
        document.Words[1].Text.Should().Be("WORDA");
        document.IsDirty.Should().BeTrue();
    }

    [Test]
    public void Preview_mode_refuses_every_edit()
    {
        var document = new WordListDocument();
        document.Load("a", Info(3), WordListEditMode.Preview);
        document.Select(1);

        document.IsReadOnly.Should().BeTrue();
        document.Add().Should().BeFalse();
        document.Remove().Should().BeFalse();
        document.MoveUp().Should().BeFalse();
        document.MoveDown().Should().BeFalse();
        document.SetTitle("X").Should().BeFalse();
        document.SetText("CAT").Should().BeFalse();
        document.SetHint("hi").Should().BeFalse();
        document.IsDirty.Should().BeFalse();
        document.Words.Should().HaveCount(3);
    }

    [Test]
    public void Text_edits_are_filtered_and_mark_dirty()
    {
        var document = Fresh();

        document.SetText("mi1leth  vil").Should().BeTrue();

        document.Current!.Text.Should().Be("MILETH VIL");
        document.IsDirty.Should().BeTrue();
    }

    [Test]
    public void Setting_the_same_text_is_not_a_change()
    {
        var document = Fresh();

        document.SetHint("").Should().BeFalse();
        document.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Hint_is_cut_at_60_characters()
    {
        var document = Fresh();
        document.SetHint(new string('a', 70));

        document.Current!.Hint.Should().HaveLength(60);
    }

    [Test]
    public void FilterWordInput_keeps_letters_and_single_spaces_in_upper_case()
    {
        WordListDocument.FilterWordInput("mi1leth  vil").Should().Be("MILETH VIL");
    }

    [Test]
    public void FilterWordInput_caps_at_20_characters()
    {
        WordListDocument.FilterWordInput(new string('a', 25)).Should().Be(new string('A', 20));
    }
}
