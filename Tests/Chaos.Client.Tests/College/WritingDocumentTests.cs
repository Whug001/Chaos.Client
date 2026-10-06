using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class WritingDocumentTests
{
    private static readonly string HashA = new('a', 64);
    private static readonly string HashB = new('b', 64);

    private static WritingDocument WithText(string text)
    {
        var doc = new WritingDocument();
        doc.SetText(0, text);

        return doc;
    }

    private static string Shape(WritingDocument doc)
        => string.Join(" | ", doc.Blocks.Select(b => b.Kind switch
        {
            WritingBlockKind.Heading => "H:" + b.Text,
            WritingBlockKind.Picture => "P",
            _                        => "T:" + b.Text
        }));

    [Test]
    public void A_new_document_has_one_empty_text_block()
        => Shape(new WritingDocument()).Should().Be("T:");

    [Test]
    public void Heading_takes_the_caret_line_and_splits_the_text()
    {
        var doc = WithText("one\ntwo\nthree");

        var (index, caret) = doc.ToggleHeading(0, 5);

        Shape(doc).Should().Be("T:one | H:two | T:three");
        (index, caret).Should().Be((1, 1));
    }

    [Test]
    public void Heading_on_the_only_line_keeps_a_text_block_after_it()
    {
        var doc = WithText("Title line");

        doc.ToggleHeading(0, 0);

        Shape(doc).Should().Be("H:Title line | T:");
    }

    [Test]
    public void Heading_is_cut_to_sixty_and_the_rest_moves_down()
    {
        var doc = WithText(new string('h', 70));

        doc.ToggleHeading(0, 0);

        Shape(doc).Should().Be($"H:{new string('h', 60)} | T:{new string('h', 10)}");
    }

    [Test]
    public void Toggling_a_heading_back_merges_with_its_neighbours()
    {
        var doc = WithText("one\ntwo\nthree");
        doc.ToggleHeading(0, 5);

        var (index, caret) = doc.ToggleHeading(1, 2);

        Shape(doc).Should().Be("T:one\ntwo\nthree");
        (index, caret).Should().Be((0, 6));
    }

    [Test]
    public void A_picture_splits_text_at_the_caret()
    {
        var doc = WithText("before\nafter");

        var (index, caret) = doc.InsertPicture(0, 7, HashA)!.Value;

        Shape(doc).Should().Be("T:before | P | T:after");
        (index, caret).Should().Be((2, 0));
        doc.PictureCount.Should().Be(1);
    }

    [Test]
    public void A_sixth_or_repeated_picture_is_refused()
    {
        var doc = new WritingDocument();

        for (var i = 0; i < 5; i++)
            doc.InsertPicture(doc.Blocks.Count - 1, 0, new string((char)('a' + i), 64)).Should().NotBeNull();

        doc.InsertPicture(doc.Blocks.Count - 1, 0, new string('f', 64)).Should().BeNull();

        var other = new WritingDocument();
        other.InsertPicture(0, 0, HashA);
        other.InsertPicture(other.Blocks.Count - 1, 0, HashA).Should().BeNull();
    }

    [Test]
    public void Backspace_after_a_picture_removes_it_and_joins_the_text()
    {
        var doc = WithText("before\nafter");
        doc.InsertPicture(0, 7, HashA);

        var (index, caret) = doc.BackspaceAtStart(2);

        Shape(doc).Should().Be("T:before\nafter");
        (index, caret).Should().Be((0, 6));
    }

    [Test]
    public void Backspace_merges_text_into_a_heading_above()
    {
        var doc = WithText("Title\nbody line\nmore");
        doc.ToggleHeading(0, 0);

        var (index, caret) = doc.BackspaceAtStart(1);

        Shape(doc).Should().Be("H:Titlebody line | T:more");
        (index, caret).Should().Be((0, 5));
    }

    [Test]
    public void Backspace_in_the_first_block_does_nothing()
    {
        var doc = WithText("abc");

        doc.BackspaceAtStart(0).Should().Be((0, 0));
        Shape(doc).Should().Be("T:abc");
    }

    [Test]
    public void Enter_after_a_heading_goes_to_the_text_below()
    {
        var doc = WithText("Title\nbody");
        doc.ToggleHeading(0, 0);

        doc.EnterAfterHeading(0).Should().Be((1, 0));

        var lone = new WritingDocument();
        lone.SetText(0, "x");
        lone.InsertPicture(0, 1, HashA);
        lone.ToggleHeading(0, 0);
        var (index, _) = lone.EnterAfterHeading(0);
        lone.Blocks[index].Kind.Should().Be(WritingBlockKind.Text);
    }

    [Test]
    public void Set_text_keeps_the_budget_and_one_line_headings()
    {
        var doc = WithText(new string('a', 9_990));
        doc.InsertPicture(0, 9_990, HashA);

        doc.SetText(2, new string('b', 50));

        doc.Blocks[2].Text.Should().HaveLength(10);
        doc.TextLength.Should().Be(10_000);

        var heading = WithText("x");
        heading.ToggleHeading(0, 0);
        heading.SetText(0, "a\nb" + new string('c', 80));
        heading.Blocks[0].Text.Should().HaveLength(60).And.StartWith("a b");
    }

    [Test]
    public void To_info_drops_empty_blocks_and_from_round_trips()
    {
        var doc = WithText("Title\nbody");
        doc.Title = "  My Piece  ";
        doc.ToggleHeading(0, 0);
        doc.InsertPicture(1, 4, HashA);

        var info = doc.ToInfo(CollegeSubjectCode.Lore);

        info.Title.Should().Be("My Piece");
        info.Blocks.Select(b => b.Kind).Should().Equal(CollegeBlockKind.Heading, CollegeBlockKind.Text, CollegeBlockKind.Picture);

        var back = WritingDocument.From(info);
        Shape(back).Should().Be("H:Title | T:body | P | T:");
        back.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Merging_a_heading_back_never_goes_over_the_budget()
    {
        var doc = new WritingDocument();
        doc.Blocks.Clear();
        doc.Blocks.Add(WritingBlock.OfText("a"));
        doc.Blocks.Add(new WritingBlock { Kind = WritingBlockKind.Heading, Text = "b" });
        doc.Blocks.Add(WritingBlock.OfText(new string('c', 9_998)));

        doc.ToggleHeading(1, 0);

        doc.TextLength.Should().BeLessThanOrEqualTo(10_000);
    }

    [Test]
    public void Removing_a_picture_never_goes_over_the_budget()
    {
        var doc = WithText(new string('a', 10_000));
        doc.InsertPicture(0, 5_000, HashA);

        doc.RemovePicture(1);

        doc.TextLength.Should().Be(10_000);
    }

    [Test]
    public void Backspace_from_a_heading_into_a_heading_leaves_no_empty_heading()
    {
        var doc = new WritingDocument();
        doc.Blocks.Clear();
        doc.Blocks.Add(new WritingBlock { Kind = WritingBlockKind.Heading, Text = "Ab" });
        doc.Blocks.Add(new WritingBlock { Kind = WritingBlockKind.Heading, Text = "cd" });
        doc.Blocks.Add(WritingBlock.OfText(string.Empty));

        var (index, caret) = doc.BackspaceAtStart(1);

        Shape(doc).Should().Be("H:Abcd | T:");
        (index, caret).Should().Be((0, 2));
    }

    [Test]
    public void Toggling_an_empty_heading_back_keeps_the_caret_in_range()
    {
        var doc = new WritingDocument();
        doc.Blocks.Clear();
        doc.Blocks.Add(WritingBlock.OfText("abc"));
        doc.Blocks.Add(new WritingBlock { Kind = WritingBlockKind.Heading, Text = string.Empty });
        doc.Blocks.Add(WritingBlock.OfText(string.Empty));

        var (index, caret) = doc.ToggleHeading(1, 0);

        caret.Should().BeInRange(0, doc.Blocks[index].Text.Length);
    }

    [Test]
    public void No_ops_do_not_mark_the_document_dirty()
    {
        var doc = WithText("x");
        doc.InsertPicture(0, 1, HashA);
        doc.MarkClean();

        doc.ToggleHeading(1, 0);
        doc.BackspaceAtStart(1);

        doc.IsDirty.Should().BeFalse();
    }

    [Test]
    public void A_picture_can_be_replaced_by_a_new_one()
    {
        var doc = WithText("before\nafter");
        doc.InsertPicture(0, 7, HashA);
        doc.MarkClean();

        doc.ReplacePicture(HashA, HashB).Should().BeTrue();

        Shape(doc).Should().Be("T:before | P | T:after");
        doc.Blocks.Single(b => b.Kind == WritingBlockKind.Picture).Hash.Should().Be(HashB);
        doc.IsDirty.Should().BeTrue();
    }

    [Test]
    public void Replacing_refuses_a_missing_or_duplicate_picture()
    {
        var doc = WithText("before\nafter");
        doc.InsertPicture(0, 7, HashA);
        doc.InsertPicture(2, 0, HashB);

        doc.ReplacePicture(HashA, HashB).Should().BeFalse();
        doc.ReplacePicture(new string('c', 64), new string('d', 64)).Should().BeFalse();
    }

    [Test]
    public void A_drawing_block_never_becomes_text()
    {
        var doc = WritingDocument.From(
            new CollegePieceInfo
            {
                Title = "T",
                Blocks = [new CollegeBlockInfo { Kind = CollegeBlockKind.Drawing }, new CollegeBlockInfo { Kind = CollegeBlockKind.Text, Text = "x" }]
            });

        Shape(doc).Should().Be("T:x");
    }

    private static WritingDocument Built(params WritingBlock[] blocks)
    {
        var doc = new WritingDocument();
        doc.Blocks.Clear();
        doc.Blocks.AddRange(blocks);

        return doc;
    }

    private static WritingBlock Heading(string text) => new() { Kind = WritingBlockKind.Heading, Text = text };

    [Test]
    public void Pasted_line_endings_and_control_characters_are_cleaned()
    {
        var doc = WithText("a\r\nb\rc\td\u0001e");

        doc.Blocks[0].Text.Should().Be("a\nb\ncde");
        doc.TextLength.Should().Be(7);
    }

    [Test]
    public void The_count_matches_the_server_join_of_text_around_an_empty_heading()
    {
        var doc = Built(WritingBlock.OfText("ab"), Heading(""), WritingBlock.OfText("cd"));

        doc.TextLength.Should().Be(5);
    }

    [Test]
    public void The_count_leaves_out_what_the_server_drops()
    {
        var doc = Built(WritingBlock.OfText("   "), Heading("  hi  "), WritingBlock.OfText("x"));

        doc.TextLength.Should().Be(3);
    }

    [Test]
    public void Typing_after_an_empty_heading_keeps_within_the_server_count()
    {
        var doc = Built(WritingBlock.OfText(new string('a', 9_997)), Heading(""), WritingBlock.OfText(string.Empty));

        doc.SetText(2, "bbbb");

        doc.Blocks[2].Text.Should().Be("bb");
        doc.TextLength.Should().Be(10_000);
    }

    [Test]
    public void Typing_into_a_full_piece_is_no_change()
    {
        var doc = WithText(new string('a', 10_000));
        doc.MarkClean();

        doc.SetText(0, new string('a', 10_000) + "b");

        doc.Blocks[0].Text.Should().HaveLength(10_000);
        doc.IsDirty.Should().BeFalse();
    }

    [Test]
    public void A_piece_at_the_block_limit_takes_no_more_headings_or_pictures()
    {
        var blocks = new List<WritingBlock>();

        for (var i = 0; i < 99; i++)
        {
            blocks.Add(Heading("h"));
            blocks.Add(WritingBlock.OfText("one\ntwo\nthree"));
        }

        blocks.Add(Heading("h"));
        blocks.Add(WritingBlock.OfText("one\ntwo\nthree"));
        var doc = Built([.. blocks]);
        var before = Shape(doc);

        doc.IsAtBlockLimit.Should().BeTrue();
        doc.ToggleHeading(1, 5).Should().Be((1, 5));
        doc.InsertPicture(1, 5, HashA).Should().BeNull();
        Shape(doc).Should().Be(before);
        doc.IsDirty.Should().BeFalse();
    }

    [Test]
    public void A_piece_below_the_block_limit_stays_within_it_after_a_heading()
    {
        var blocks = new List<WritingBlock>();

        for (var i = 0; i < 99; i++)
        {
            blocks.Add(Heading("h"));
            blocks.Add(WritingBlock.OfText("one\ntwo\nthree"));
        }

        var doc = Built([.. blocks]);

        doc.IsAtBlockLimit.Should().BeFalse();
        doc.ToggleHeading(1, 5);

        doc.ToInfo(CollegeSubjectCode.Lore).Blocks.Should().HaveCount(CollegeProtocol.MAX_BLOCKS);
        doc.IsAtBlockLimit.Should().BeTrue();
    }

    [Test]
    public void Joining_around_whitespace_never_goes_over_the_server_count()
    {
        var doc = Built(WritingBlock.OfText("   "), Heading("x"), WritingBlock.OfText(new string('a', 9_999)));

        doc.ToggleHeading(1, 0);

        doc.TextLength.Should().BeLessThanOrEqualTo(10_000);
    }
}
