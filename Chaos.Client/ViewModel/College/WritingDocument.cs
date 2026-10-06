using System.Text;
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

    /// <summary>
    ///     The characters the server counts against the 10,000: headings trimmed, blank blocks dropped, and text blocks
    ///     that end up next to each other joined with a line break.
    /// </summary>
    public int TextLength => Measure().Characters;

    /// <summary>The blocks the server keeps once it has dropped blank ones and joined neighbouring text.</summary>
    public int BlockCount => Measure().Blocks;

    /// <summary>
    ///     True when one more heading or picture could take the piece past the server's block limit (either can add two
    ///     blocks: itself and the text it splits off).
    /// </summary>
    public bool IsAtBlockLimit => BlockCount + 2 > CollegeProtocol.MAX_BLOCKS;

    public int PictureCount => Blocks.Count(b => b.Kind == WritingBlockKind.Picture);

    /// <summary>The server's cleaning of piece text: every line break becomes \n and other control characters go.</summary>
    public static string Clean(string text)
    {
        if (!text.Any(char.IsControl))
            return text;

        var builder = new StringBuilder(text.Length);

        foreach (var c in text.Replace("\r\n", "\n").Replace('\r', '\n'))
            if ((c == '\n') || !char.IsControl(c))
                builder.Append(c);

        return builder.ToString();
    }

    //the server's normalising, counted; block text is already clean, as SetText cleans it
    private (int Characters, int Blocks) Measure()
    {
        var characters = 0;
        var blocks = 0;
        var afterText = false;

        foreach (var block in Blocks)
            switch (block.Kind)
            {
                case WritingBlockKind.Picture:
                    blocks++;
                    afterText = false;

                    break;
                case WritingBlockKind.Heading:
                    var heading = block.Text.Trim().Length;

                    if (heading == 0)
                        break;

                    characters += heading;
                    blocks++;
                    afterText = false;

                    break;
                default:
                    if (string.IsNullOrWhiteSpace(block.Text))
                        break;

                    if (afterText)
                        characters++;
                    else
                        blocks++;

                    characters += block.Text.Length;
                    afterText = true;

                    break;
            }

        return (characters, blocks);
    }

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
        {
            if (block.Kind == CollegeBlockKind.Drawing)
                continue;

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
        }

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

    /// <summary>
    ///     Sets a block's text, cleaned as the server cleans it, keeping headings to one line of 60 and the whole piece
    ///     within the server's count of 10,000 characters.
    /// </summary>
    public void SetText(int index, string text)
    {
        var block = Blocks[index];

        if ((block.Kind == WritingBlockKind.Picture) || (text == block.Text))
            return;

        text = Clean(text);

        if (block.Kind == WritingBlockKind.Heading)
        {
            text = text.Replace('\n', ' ');

            if (text.Length > CollegeProtocol.MAX_HEADING_CHARS)
                text = text[..CollegeProtocol.MAX_HEADING_CHARS];
        }

        if (text == block.Text)
            return;

        var before = block.Text;
        block.Text = text;
        FitText(index);

        //typing past the budget is cut back to what was there, which is no change
        if (block.Text != before)
            IsDirty = true;
    }

    public (int Index, int Caret) ToggleHeading(int index, int caret)
    {
        var block = Blocks[index];

        if (block.Kind == WritingBlockKind.Picture)
            return (index, 0);

        if (block.Kind == WritingBlockKind.Heading)
        {
            IsDirty = true;
            block.Kind = WritingBlockKind.Text;

            return MergeTextAround(index, caret);
        }

        if (IsAtBlockLimit)
            return (index, caret);

        IsDirty = true;

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

    /// <summary>Puts a picture at the caret. Null when the piece already has 5 pictures or this one, or is at the block limit.</summary>
    public (int Index, int Caret)? InsertPicture(int index, int caret, string hash)
    {
        if ((PictureCount >= CollegeProtocol.MAX_PICTURES)
            || IsAtBlockLimit
            || Blocks.Any(b => (b.Kind == WritingBlockKind.Picture) && (b.Hash == hash)))
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
                FitText(index - 2);

                return (index - 2, Math.Min(caret, above.Text.Length));
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
        FitText(index - 1);

        return (index - 1, Math.Min(at, previous.Text.Length));
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

    /// <summary>Swaps a picture for an edited one in the same place. False when the old one is gone or the new one is already in the piece.</summary>
    public bool ReplacePicture(string oldHash, string newHash)
    {
        if (Blocks.Any(b => (b.Kind == WritingBlockKind.Picture) && (b.Hash == newHash))
            || Blocks.FirstOrDefault(b => (b.Kind == WritingBlockKind.Picture) && (b.Hash == oldHash)) is not { } block)
            return false;

        block.Hash = newHash;
        IsDirty = true;

        return true;
    }

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
        FitText(index);

        return (index, Math.Clamp(caret, 0, Blocks[index].Text.Length));
    }

    private string JoinParagraphs(string first, string second)
        => first.Length == 0 ? second
            : second.Length == 0 ? first
            : TextLength < CollegeProtocol.MAX_TEXT_CHARS ? first + "\n" + second
            : first + second;

    /// <summary>
    ///     Cuts the end of block <paramref name="index" /> until the piece is within the server's count. Each pass cuts the
    ///     overrun, and a joining line break can go as the block empties, so the loop settles in a pass or two.
    /// </summary>
    private void FitText(int index)
    {
        var block = Blocks[index];

        for (var over = TextLength - CollegeProtocol.MAX_TEXT_CHARS;
             (over > 0) && (block.Text.Length > 0);
             over = TextLength - CollegeProtocol.MAX_TEXT_CHARS)
            block.Text = block.Text[..Math.Max(0, block.Text.Length - over)];
    }

    private void EnsureTrailingText()
    {
        if ((Blocks.Count == 0) || (Blocks[^1].Kind != WritingBlockKind.Text))
            Blocks.Add(WritingBlock.OfText(string.Empty));
    }
}
