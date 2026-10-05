using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

public enum WritingBlockKind
{
    Text,
    Heading,
    Picture
}

public sealed class WritingBlock
{
    public WritingBlockKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;

    public static WritingBlock OfText(string text) => new() { Kind = WritingBlockKind.Text, Text = text };
}

/// <summary>
///     The writing window's piece as editable blocks: text (with line breaks), one-line headings and pictures. Every editing
///     rule lives here; the window only shows blocks and forwards keys. The last block is always text, so there is always
///     somewhere to type.
/// </summary>
public sealed class WritingDocument
{
    public string Title
    {
        get;
        set
        {
            field = value.Length <= CollegeProtocol.MAX_TITLE_CHARS ? value : value[..CollegeProtocol.MAX_TITLE_CHARS];
            IsDirty = true;
        }
    } = string.Empty;

    public List<WritingBlock> Blocks { get; } = [WritingBlock.OfText(string.Empty)];
    public bool IsDirty { get; private set; }
    public int TextLength => Blocks.Where(b => b.Kind != WritingBlockKind.Picture).Sum(b => b.Text.Length);
    public int PictureCount => Blocks.Count(b => b.Kind == WritingBlockKind.Picture);

    public void MarkClean() => IsDirty = false;

    /// <summary>Counts the piece as changed, for one whose save was refused.</summary>
    public void MarkDirty() => IsDirty = true;

    public static WritingDocument From(CollegePieceInfo? piece)
    {
        var doc = new WritingDocument();

        if (piece is null)
            return doc;

        doc.Title = piece.Title;
        doc.Blocks.Clear();

        foreach (var block in piece.Blocks)
            doc.Blocks.Add(
                new WritingBlock
                {
                    Kind = block.Kind switch
                    {
                        CollegeBlockKind.Heading => WritingBlockKind.Heading,
                        CollegeBlockKind.Picture => WritingBlockKind.Picture,
                        _                        => WritingBlockKind.Text
                    },
                    Text = block.Text,
                    Hash = block.Hash
                });

        doc.EnsureTrailingText();
        doc.MarkClean();

        return doc;
    }

    public CollegePieceInfo ToInfo(CollegeSubjectCode subject)
        => new()
        {
            Subject = subject,
            Title = Title.Trim(),
            Blocks = Blocks.Where(b => (b.Kind == WritingBlockKind.Picture) || (b.Text.Trim().Length > 0))
                           .Select(
                               b => new CollegeBlockInfo
                               {
                                   Kind = b.Kind switch
                                   {
                                       WritingBlockKind.Heading => CollegeBlockKind.Heading,
                                       WritingBlockKind.Picture => CollegeBlockKind.Picture,
                                       _                        => CollegeBlockKind.Text
                                   },
                                   Text = b.Kind == WritingBlockKind.Picture ? string.Empty : b.Text,
                                   Hash = b.Hash
                               })
                           .ToList()
        };

    /// <summary>Sets a block's text, keeping headings to one line of 60 and the whole piece within 10,000 characters.</summary>
    public void SetText(int index, string text)
    {
        var block = Blocks[index];

        if (block.Kind == WritingBlockKind.Picture)
            return;

        if (block.Kind == WritingBlockKind.Heading)
            text = text.Replace("\r", string.Empty).Replace('\n', ' ');

        var limit = block.Kind == WritingBlockKind.Heading ? CollegeProtocol.MAX_HEADING_CHARS : int.MaxValue;
        var budget = CollegeProtocol.MAX_TEXT_CHARS - (TextLength - block.Text.Length);
        var max = Math.Max(0, Math.Min(limit, budget));
        var next = text.Length <= max ? text : text[..max];

        if (next == block.Text)
            return;

        block.Text = next;
        IsDirty = true;
    }

    public (int Index, int Caret) ToggleHeading(int index, int caret)
    {
        var block = Blocks[index];

        if (block.Kind == WritingBlockKind.Picture)
            return (index, 0);

        IsDirty = true;

        if (block.Kind == WritingBlockKind.Heading)
        {
            block.Kind = WritingBlockKind.Text;

            return MergeTextAround(index, caret);
        }

        var text = block.Text;
        caret = Math.Clamp(caret, 0, text.Length);
        var lineStart = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        var lineEnd = text.IndexOf('\n', caret);

        if (lineEnd < 0)
            lineEnd = text.Length;

        var before = lineStart > 0 ? text[..(lineStart - 1)] : null;
        var line = text[lineStart..lineEnd];
        var after = lineEnd < text.Length ? text[(lineEnd + 1)..] : null;

        if (line.Length > CollegeProtocol.MAX_HEADING_CHARS)
        {
            after = line[CollegeProtocol.MAX_HEADING_CHARS..] + (after is null ? string.Empty : "\n" + after);
            line = line[..CollegeProtocol.MAX_HEADING_CHARS];
        }

        var replacement = new List<WritingBlock>();

        if (before is not null)
            replacement.Add(WritingBlock.OfText(before));

        var headingIndex = index + replacement.Count;
        replacement.Add(new WritingBlock { Kind = WritingBlockKind.Heading, Text = line });

        if (after is not null)
            replacement.Add(WritingBlock.OfText(after));

        Blocks.RemoveAt(index);
        Blocks.InsertRange(index, replacement);
        EnsureTrailingText();

        return (headingIndex, Math.Min(caret - lineStart, line.Length));
    }

    /// <summary>Puts a picture at the caret. Null when the piece already has 5 pictures or this one.</summary>
    public (int Index, int Caret)? InsertPicture(int index, int caret, string hash)
    {
        if ((PictureCount >= CollegeProtocol.MAX_PICTURES) || Blocks.Any(b => (b.Kind == WritingBlockKind.Picture) && (b.Hash == hash)))
            return null;

        IsDirty = true;
        var picture = new WritingBlock { Kind = WritingBlockKind.Picture, Hash = hash };
        var block = Blocks[index];

        if (block.Kind != WritingBlockKind.Text)
        {
            Blocks.Insert(index + 1, picture);

            if ((index + 2 >= Blocks.Count) || (Blocks[index + 2].Kind != WritingBlockKind.Text))
                Blocks.Insert(index + 2, WritingBlock.OfText(string.Empty));

            return (index + 2, 0);
        }

        caret = Math.Clamp(caret, 0, block.Text.Length);
        var before = block.Text[..caret];
        var after = block.Text[caret..];

        if (before.EndsWith('\n'))
            before = before[..^1];

        if (after.StartsWith('\n'))
            after = after[1..];

        block.Text = before;
        Blocks.Insert(index + 1, picture);
        Blocks.Insert(index + 2, WritingBlock.OfText(after));

        return (index + 2, 0);
    }

    /// <summary>Backspace with the caret at a block's start: removes a picture above it, or joins the block to the one above.</summary>
    public (int Index, int Caret) BackspaceAtStart(int index)
    {
        if (index <= 0)
            return (0, 0);

        var previous = Blocks[index - 1];
        var current = Blocks[index];

        if ((previous.Kind != WritingBlockKind.Picture) && (current.Kind == WritingBlockKind.Picture))
            return (index, 0);

        IsDirty = true;

        if (previous.Kind == WritingBlockKind.Picture)
        {
            Blocks.RemoveAt(index - 1);

            if ((index - 2 >= 0) && (Blocks[index - 2].Kind == WritingBlockKind.Text) && (current.Kind == WritingBlockKind.Text))
            {
                var above = Blocks[index - 2];
                var caret = above.Text.Length;
                above.Text = JoinParagraphs(above.Text, current.Text);
                Blocks.RemoveAt(index - 1);
                EnsureTrailingText();

                return (index - 2, caret);
            }

            EnsureTrailingText();

            return (index - 1, 0);
        }

        var at = previous.Text.Length;

        if (previous.Kind == WritingBlockKind.Heading)
        {
            var newline = current.Text.IndexOf('\n');
            var firstLine = newline < 0 ? current.Text : current.Text[..newline];
            var rest = newline < 0 ? string.Empty : current.Text[(newline + 1)..];
            var room = CollegeProtocol.MAX_HEADING_CHARS - previous.Text.Length;
            var moved = firstLine.Length <= room ? firstLine : firstLine[..Math.Max(0, room)];
            previous.Text += moved;
            current.Text = firstLine[moved.Length..] + (rest.Length > 0 && firstLine.Length > moved.Length ? "\n" : string.Empty) + rest;

            if ((current.Text.Length == 0) && ((current.Kind != WritingBlockKind.Text) || (index != Blocks.Count - 1)))
                Blocks.RemoveAt(index);
        } else
        {
            previous.Text += current.Text;
            Blocks.RemoveAt(index);
        }

        EnsureTrailingText();

        return (index - 1, at);
    }

    /// <summary>Enter at the end of a heading: the caret moves to the text below it, which is created if missing.</summary>
    public (int Index, int Caret) EnterAfterHeading(int index)
    {
        if ((index + 1 < Blocks.Count) && (Blocks[index + 1].Kind == WritingBlockKind.Text))
            return (index + 1, 0);

        Blocks.Insert(index + 1, WritingBlock.OfText(string.Empty));
        IsDirty = true;

        return (index + 1, 0);
    }

    /// <summary>Removes a picture by its remove button, joining the text around it.</summary>
    public (int Index, int Caret) RemovePicture(int index)
        => (index + 1 < Blocks.Count) && (Blocks[index].Kind == WritingBlockKind.Picture) ? BackspaceAtStart(index + 1) : (index, 0);

    private (int Index, int Caret) MergeTextAround(int index, int caret)
    {
        if ((index > 0) && (Blocks[index - 1].Kind == WritingBlockKind.Text))
        {
            var previous = Blocks[index - 1];
            var joined = JoinParagraphs(previous.Text, Blocks[index].Text);
            caret += joined.Length - Blocks[index].Text.Length;
            previous.Text = joined;
            Blocks.RemoveAt(index);
            index--;
        }

        if ((index + 1 < Blocks.Count) && (Blocks[index + 1].Kind == WritingBlockKind.Text))
        {
            Blocks[index].Text = JoinParagraphs(Blocks[index].Text, Blocks[index + 1].Text);
            Blocks.RemoveAt(index + 1);
        }

        EnsureTrailingText();

        return (index, Math.Clamp(caret, 0, Blocks[index].Text.Length));
    }

    private string JoinParagraphs(string first, string second)
        => first.Length == 0 ? second
            : second.Length == 0 ? first
            : TextLength < CollegeProtocol.MAX_TEXT_CHARS ? first + "\n" + second
            : first + second;

    private void EnsureTrailingText()
    {
        if ((Blocks.Count == 0) || (Blocks[^1].Kind != WritingBlockKind.Text))
            Blocks.Add(WritingBlock.OfText(string.Empty));
    }
}
