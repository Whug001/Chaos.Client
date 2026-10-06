using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

/// <summary>One question being edited. It always has four answer slots; blank slots after the last filled one are not sent.</summary>
public sealed class QuizQuestionDraft
{
    public string Text { get; set; } = string.Empty;
    public string[] Answers { get; } = ["", "", "", ""];

    /// <summary>The right answer's slot, or -1 while none is marked.</summary>
    public int Right { get; set; } = -1;
}

/// <summary>The quiz editor's model: a title and up to 20 questions, one of them selected for editing.</summary>
public sealed class QuizDocument
{
    private readonly List<QuizQuestionDraft> Items = [];

    /// <summary>The server's id, or empty until the first save of a new quiz.</summary>
    public string Id { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;
    public IReadOnlyList<QuizQuestionDraft> Questions => Items;
    public int Selected { get; private set; } = -1;
    public QuizQuestionDraft? Current => (Selected >= 0) && (Selected < Items.Count) ? Items[Selected] : null;

    /// <summary>Goes up on every change, including selection, so the window knows when to refresh its boxes.</summary>
    public int Version { get; private set; }

    public bool IsDirty { get; private set; }

    /// <summary>Raised each time <see cref="Version" /> goes up.</summary>
    public event Action? Changed;

    public void New()
    {
        Id = string.Empty;
        Title = string.Empty;
        Items.Clear();
        Items.Add(new QuizQuestionDraft());
        Selected = 0;
        IsDirty = false;
        Bump();
    }

    public void Load(string id, CollegeQuizDraftInfo info)
    {
        Id = id;
        Title = Cut(info.Title, CollegeProtocol.MAX_QUIZ_TITLE_CHARS);
        Items.Clear();

        foreach (var question in info.Questions.Take(CollegeProtocol.MAX_QUIZ_QUESTIONS))
        {
            var draft = new QuizQuestionDraft
            {
                Text = Cut(question.Text, CollegeProtocol.MAX_QUESTION_CHARS),
                Right = question.Right < CollegeProtocol.MAX_ANSWERS ? question.Right : -1
            };

            for (var i = 0; i < Math.Min(question.Answers.Count, CollegeProtocol.MAX_ANSWERS); i++)
                draft.Answers[i] = Cut(question.Answers[i], CollegeProtocol.MAX_ANSWER_CHARS);

            Items.Add(draft);
        }

        if (Items.Count == 0)
            Items.Add(new QuizQuestionDraft());

        Selected = 0;
        IsDirty = false;
        Bump();
    }

    public void SetId(string id) => Id = id;

    public void MarkClean() => IsDirty = false;

    public void SetTitle(string title)
    {
        title = Cut(title, CollegeProtocol.MAX_QUIZ_TITLE_CHARS);

        if (title == Title)
            return;

        Title = title;
        Touch();
    }

    public void SetText(string text)
    {
        text = Cut(text, CollegeProtocol.MAX_QUESTION_CHARS);

        if (Current is not { } question || (question.Text == text))
            return;

        question.Text = text;
        Touch();
    }

    public void SetAnswer(int slot, string text)
    {
        text = Cut(text, CollegeProtocol.MAX_ANSWER_CHARS);

        if (Current is not { } question || (slot < 0) || (slot >= CollegeProtocol.MAX_ANSWERS) || (question.Answers[slot] == text))
            return;

        question.Answers[slot] = text;
        Touch();
    }

    public void MarkRight(int slot)
    {
        if (Current is not { } question || (slot < 0) || (slot >= CollegeProtocol.MAX_ANSWERS) || (question.Right == slot))
            return;

        question.Right = slot;
        Touch();
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
        if (Items.Count >= CollegeProtocol.MAX_QUIZ_QUESTIONS)
            return false;

        Items.Add(new QuizQuestionDraft());
        Selected = Items.Count - 1;
        Touch();

        return true;
    }

    public bool MoveUp()
    {
        if (Selected <= 0)
            return false;

        (Items[Selected - 1], Items[Selected]) = (Items[Selected], Items[Selected - 1]);
        Selected--;
        Touch();

        return true;
    }

    public bool MoveDown()
    {
        if ((Selected < 0) || (Selected >= Items.Count - 1))
            return false;

        (Items[Selected + 1], Items[Selected]) = (Items[Selected], Items[Selected + 1]);
        Selected++;
        Touch();

        return true;
    }

    /// <summary>Removes the selected question. The quiz never ends up empty: the last one is replaced by a blank question.</summary>
    public bool Remove()
    {
        if (Current is null)
            return false;

        Items.RemoveAt(Selected);

        if (Items.Count == 0)
            Items.Add(new QuizQuestionDraft());

        Selected = Math.Min(Selected, Items.Count - 1);
        Touch();

        return true;
    }

    public string Label(int index)
    {
        var text = Items[index].Text;

        return $"{index + 1}. {(string.IsNullOrWhiteSpace(text) ? "(empty)" : text)}";
    }

    public CollegeQuizDraftInfo ToInfo()
        => new()
        {
            Title = Title,
            Questions = Items.Select(ToInfo).ToList()
        };

    private static CollegeQuizQuestionInfo ToInfo(QuizQuestionDraft draft)
    {
        var last = Array.FindLastIndex(draft.Answers, answer => !string.IsNullOrWhiteSpace(answer));

        return new CollegeQuizQuestionInfo
        {
            Text = draft.Text,
            Answers = draft.Answers.Take(last + 1).ToList(),
            Right = (draft.Right >= 0) && (draft.Right <= last) ? (byte)draft.Right : CollegeProtocol.NO_ANSWER
        };
    }

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
