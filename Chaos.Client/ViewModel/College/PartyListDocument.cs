using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

public sealed class PartyEntryDraft
{
    public string Text { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<string> Decoys { get; } = [];
}

/// <summary>
///     The party-list editor's model: a title and up to 50 entries (30 for Bluff), one of them selected for editing.
///     Bluff entries carry the real answer and up to 3 decoys. In Preview mode every edit is refused.
/// </summary>
public sealed class PartyListDocument
{
    private readonly List<PartyEntryDraft> Items = [];

    /// <summary>The server's id, or empty until the first save of a new list.</summary>
    public string Id { get; private set; } = string.Empty;

    public PartyListKind Kind { get; private set; } = PartyListKind.Quip;
    public string Title { get; private set; } = string.Empty;
    public bool IsPreview { get; private set; }
    public IReadOnlyList<PartyEntryDraft> Entries => Items;
    public int Selected { get; private set; } = -1;
    public PartyEntryDraft? Current => (Selected >= 0) && (Selected < Items.Count) ? Items[Selected] : null;
    public bool IsReadOnly => IsPreview;

    public int MaxEntries => Kind == PartyListKind.Bluff ? CollegeProtocol.MAX_BLUFF_ENTRIES : CollegeProtocol.MAX_PARTY_ENTRIES;

    public int MaxTextChars => Kind switch
    {
        PartyListKind.Quip => CollegeProtocol.QUIP_PROMPT_MAX_CHARS,
        PartyListKind.Bluff => CollegeProtocol.BLUFF_QUESTION_MAX_CHARS,
        _ => CollegeProtocol.SKETCH_PROMPT_MAX_CHARS
    };

    /// <summary>Goes up on every change, including selection, so the window knows when to refresh its boxes.</summary>
    public int Version { get; private set; }

    public bool IsDirty { get; private set; }

    /// <summary>Raised each time <see cref="Version" /> goes up.</summary>
    public event Action? Changed;

    public void New(PartyListKind kind, bool preview = false)
    {
        Id = string.Empty;
        Kind = kind;
        IsPreview = preview;
        Title = string.Empty;
        Items.Clear();
        Items.Add(new PartyEntryDraft());
        Selected = 0;
        IsDirty = false;
        Bump();
    }

    public void Load(string id, CollegePartyListDraftInfo info, bool preview = false)
    {
        Id = id;
        Kind = info.Kind;
        IsPreview = preview;
        Title = Cut(info.Title, CollegeProtocol.MAX_WORD_LIST_TITLE_CHARS);
        Items.Clear();

        foreach (var entry in info.Entries.Take(MaxEntries))
        {
            var draft = new PartyEntryDraft
            {
                Text = Cut(entry.Text, MaxTextChars),
                Answer = Cut(entry.Answer, CollegeProtocol.BLUFF_MAX_CHARS)
            };

            draft.Decoys.AddRange(entry.Decoys.Take(CollegeProtocol.BLUFF_MAX_DECOYS).Select(decoy => Cut(decoy, CollegeProtocol.BLUFF_MAX_CHARS)));
            Items.Add(draft);
        }

        if (Items.Count == 0)
            Items.Add(new PartyEntryDraft());

        Selected = 0;
        IsDirty = false;
        Bump();
    }

    public void SetId(string id) => Id = id;

    public void MarkClean() => IsDirty = false;

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
        text = Cut(text, MaxTextChars);

        if (IsReadOnly || Current is not { } entry || (entry.Text == text))
            return false;

        entry.Text = text;
        Touch();

        return true;
    }

    public bool SetAnswer(string answer)
    {
        answer = Cut(answer, CollegeProtocol.BLUFF_MAX_CHARS);

        if (IsReadOnly || Current is not { } entry || (entry.Answer == answer))
            return false;

        entry.Answer = answer;
        Touch();

        return true;
    }

    public bool AddDecoy()
    {
        if (IsReadOnly || Current is not { } entry || (entry.Decoys.Count >= CollegeProtocol.BLUFF_MAX_DECOYS))
            return false;

        entry.Decoys.Add(string.Empty);
        Touch();

        return true;
    }

    public bool SetDecoy(int index, string text)
    {
        text = Cut(text, CollegeProtocol.BLUFF_MAX_CHARS);

        if (IsReadOnly || Current is not { } entry || (index < 0) || (index >= entry.Decoys.Count) || (entry.Decoys[index] == text))
            return false;

        entry.Decoys[index] = text;
        Touch();

        return true;
    }

    public bool RemoveDecoy(int index)
    {
        if (IsReadOnly || Current is not { } entry || (index < 0) || (index >= entry.Decoys.Count))
            return false;

        entry.Decoys.RemoveAt(index);
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
        if (IsReadOnly || (Items.Count >= MaxEntries))
            return false;

        Items.Add(new PartyEntryDraft());
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

    /// <summary>Removes the selected entry. The list never ends up empty: the last one is replaced by a blank entry.</summary>
    public bool Remove()
    {
        if (IsReadOnly || (Current is null))
            return false;

        Items.RemoveAt(Selected);

        if (Items.Count == 0)
            Items.Add(new PartyEntryDraft());

        Selected = Math.Min(Selected, Items.Count - 1);
        Touch();

        return true;
    }

    public string Label(int index)
    {
        var text = Items[index].Text;

        return $"{index + 1}. {(string.IsNullOrWhiteSpace(text) ? "(empty)" : text)}";
    }

    public CollegePartyListDraftInfo ToInfo()
        => new()
        {
            Kind = Kind,
            Title = Title,
            Entries = Items.Select(
                    entry => new CollegePartyEntryInfo
                    {
                        Text = entry.Text,
                        Answer = entry.Answer,
                        Decoys = entry.Decoys.ToList()
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
