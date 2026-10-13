using System.Text;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

/// <summary>One word being edited, with its optional hint.</summary>
public sealed class WordDraft
{
    public string Text { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
}

/// <summary>
///     The word-list editor's model: a title and up to 30 words (10 in Quick mode), one selected for editing. In Preview mode
///     every edit is refused.
/// </summary>
public sealed class WordListDocument
{
    private readonly List<WordDraft> Items = [];

    /// <summary>The server's id, or empty until the first save of a new list.</summary>
    public string Id { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;
    public WordListEditMode Mode { get; private set; } = WordListEditMode.Saved;
    public IReadOnlyList<WordDraft> Words => Items;
    public int Selected { get; private set; } = -1;
    public WordDraft? Current => (Selected >= 0) && (Selected < Items.Count) ? Items[Selected] : null;
    public bool IsReadOnly => Mode == WordListEditMode.Preview;

    public int MaxWords => Mode == WordListEditMode.Quick ? CollegeProtocol.MAX_QUICK_WORDS : CollegeProtocol.MAX_LIST_WORDS;

    /// <summary>Goes up on every change, including selection, so the window knows when to refresh its boxes.</summary>
    public int Version { get; private set; }

    public bool IsDirty { get; private set; }

    /// <summary>Raised each time <see cref="Version" /> goes up.</summary>
    public event Action? Changed;

    public void New(WordListEditMode mode)
    {
        Id = string.Empty;
        Title = string.Empty;
        Mode = mode;
        Items.Clear();
        Items.Add(new WordDraft());
        Selected = 0;
        IsDirty = false;
        Bump();
    }

    public void Load(string id, CollegeWordListDraftInfo info, WordListEditMode mode)
    {
        Id = id;
        Mode = mode;
        Title = Cut(info.Title, CollegeProtocol.MAX_WORD_LIST_TITLE_CHARS);
        Items.Clear();

        foreach (var word in info.Words.Take(MaxWords))
            Items.Add(
                new WordDraft
                {
                    Text = FilterWordInput(word.Text),
                    Hint = Cut(word.Hint, CollegeProtocol.MAX_HINT_CHARS)
                });

        if (Items.Count == 0)
            Items.Add(new WordDraft());

        Selected = 0;
        IsDirty = false;
        Bump();
    }

    public void SetId(string id) => Id = id;

    public void MarkClean() => IsDirty = false;

    /// <summary>Keeps only letters and single spaces, upper-cases, and cuts at 20 characters.</summary>
    public static string FilterWordInput(string text)
    {
        var builder = new StringBuilder();

        foreach (var c in text)
        {
            if (builder.Length >= CollegeProtocol.MAX_WORD_CHARS)
                break;

            if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'))
                builder.Append(char.ToUpperInvariant(c));
            else if ((c == ' ') && (builder.Length > 0) && (builder[^1] != ' '))
                builder.Append(' ');
        }

        return builder.ToString();
    }

    public bool SetTitle(string title)
    {
        title = Cut(title, CollegeProtocol.MAX_WORD_LIST_TITLE_CHARS);

        if (IsReadOnly || (title == Title))
            return false;

        Title = title;
        Touch();

        return true;
    }

    public bool SetText(string text)
    {
        text = FilterWordInput(text);

        if (IsReadOnly || Current is not { } word || (word.Text == text))
            return false;

        word.Text = text;
        Touch();

        return true;
    }

    public bool SetHint(string hint)
    {
        hint = Cut(hint, CollegeProtocol.MAX_HINT_CHARS);

        if (IsReadOnly || Current is not { } word || (word.Hint == hint))
            return false;

        word.Hint = hint;
        Touch();

        return true;
    }

    public void Select(int index)
    {
        if ((index < 0) || (index >= Items.Count) || (index == Selected))
            return;

        Selected = index;
        Bump();
    }

    public bool Add()
    {
        if (IsReadOnly || (Items.Count >= MaxWords))
            return false;

        Items.Add(new WordDraft());
        Selected = Items.Count - 1;
        Touch();

        return true;
    }

    public bool MoveUp()
    {
        if (IsReadOnly || (Selected <= 0))
            return false;

        (Items[Selected - 1], Items[Selected]) = (Items[Selected], Items[Selected - 1]);
        Selected--;
        Touch();

        return true;
    }

    public bool MoveDown()
    {
        if (IsReadOnly || (Selected < 0) || (Selected >= Items.Count - 1))
            return false;

        (Items[Selected + 1], Items[Selected]) = (Items[Selected], Items[Selected + 1]);
        Selected++;
        Touch();

        return true;
    }

    /// <summary>Removes the selected word. The list never ends up empty: the last one is replaced by a blank word.</summary>
    public bool Remove()
    {
        if (IsReadOnly || (Current is null))
            return false;

        Items.RemoveAt(Selected);

        if (Items.Count == 0)
            Items.Add(new WordDraft());

        Selected = Math.Min(Selected, Items.Count - 1);
        Touch();

        return true;
    }

    public string Label(int index)
    {
        var text = Items[index].Text;

        return $"{index + 1}. {(string.IsNullOrWhiteSpace(text) ? "(empty)" : text)}";
    }

    public CollegeWordListDraftInfo ToInfo()
        => new()
        {
            Title = Title,
            Words = Items.Select(
                             item => new CollegeWordInfo
                             {
                                 Text = item.Text,
                                 Hint = item.Hint
                             })
                         .ToList()
        };

    private void Touch()
    {
        IsDirty = true;
        Bump();
    }

    private void Bump()
    {
        Version++;
        Changed?.Invoke();
    }

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..max];
}
