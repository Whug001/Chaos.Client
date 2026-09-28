using System.Text;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

/// <summary>
///     What the quest log window shows: the server's list of active quests, the selected quest (kept by key across
///     updates) and the GiveUp confirm. The server sends the whole list on open and whenever it changes.
/// </summary>
public sealed class QuestLogState
{
    public const float CONFIRM_SECONDS = 5f;

    private readonly List<QuestLogEntryInfo> EntryList = [];
    private float ConfirmSecondsLeft;

    /// <summary>The same list instance for the life of the state, so a row list can bind it once.</summary>
    public IReadOnlyList<QuestLogEntryInfo> Entries => EntryList;

    public string? SelectedKey { get; private set; }

    /// <summary>The quest whose GiveUp is armed, waiting for the second click.</summary>
    public string? ConfirmingKey { get; private set; }

    public QuestLogEntryInfo? Selected => EntryList.FirstOrDefault(entry => entry.Key == SelectedKey);

    public bool CanGiveUp => Selected?.CanGiveUp == true;

    /// <summary>The list, the selection or the confirm changed.</summary>
    public event Action? Changed;

    public void Apply(IReadOnlyList<QuestLogEntryInfo> entries)
    {
        EntryList.Clear();
        EntryList.AddRange(entries);

        if (Selected is null)
            SelectedKey = EntryList.Count > 0 ? EntryList[0].Key : null;

        if ((ConfirmingKey is not null) && EntryList.All(entry => entry.Key != ConfirmingKey))
            ClearConfirm();

        Changed?.Invoke();
    }

    public void Select(string key)
    {
        if (EntryList.All(entry => entry.Key != key))
            return;

        SelectedKey = key;
        ClearConfirm();
        Changed?.Invoke();
    }

    /// <summary>The GiveUp button. The first click arms the confirm; the second, within 5 s, returns the key to send.</summary>
    public string? PressGiveUp()
    {
        if (!CanGiveUp)
            return null;

        if (ConfirmingKey == SelectedKey)
        {
            var key = ConfirmingKey;
            ClearConfirm();
            Changed?.Invoke();

            return key;
        }

        ConfirmingKey = SelectedKey;
        ConfirmSecondsLeft = CONFIRM_SECONDS;
        Changed?.Invoke();

        return null;
    }

    public void CancelGiveUp()
    {
        if (ConfirmingKey is null)
            return;

        ClearConfirm();
        Changed?.Invoke();
    }

    /// <summary>Ticked by the world screen, in seconds. Ends an armed confirm after 5 s.</summary>
    public void Update(float seconds)
    {
        if (ConfirmingKey is null)
            return;

        ConfirmSecondsLeft -= seconds;

        if (ConfirmSecondsLeft <= 0f)
            CancelGiveUp();
    }

    public void Clear()
    {
        EntryList.Clear();
        SelectedKey = null;
        ClearConfirm();
        Changed?.Invoke();
    }

    /// <summary>The details pane's text, with {=x colour codes (no closing brace): gold title, grey giver, white text, lime when done.</summary>
    public string DetailsText()
    {
        if (Selected is not { } quest)
            return "{=iYou have no active quests.";

        if (ConfirmingKey == quest.Key)
            return $"{{=uClick GiveUp again to abandon {quest.Title}.";

        var text = new StringBuilder();
        text.Append("{=c").Append(quest.Title);

        if (!string.IsNullOrEmpty(quest.GivenBy))
            text.Append("\n{=i").Append(quest.GivenBy);

        text.Append("\n\n{=u").Append(quest.Text);

        if (quest.Progress.Count > 0)
        {
            text.Append('\n');

            foreach (var line in quest.Progress)
                text.Append("\n{=")
                    .Append(line.Have >= line.Need ? 'q' : 'u')
                    .Append(line.Label)
                    .Append(": ")
                    .Append(line.Have)
                    .Append(" / ")
                    .Append(line.Need);
        }

        return text.ToString();
    }

    private void ClearConfirm()
    {
        ConfirmingKey = null;
        ConfirmSecondsLeft = 0f;
    }
}
