#region
using Chaos.Client.Collections;
using Chaos.Extensions.Common;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Authoritative chat and message state. Owns the chat message log and orange bar message history. Fires events when
///     new messages are added for UI reconciliation.
/// </summary>
public sealed class Chat
{
    private const int MAX_MESSAGES = 1000;
    private const int MAX_HISTORY = 1000;
    private const int MAX_WHISPER_NAMES = 5;

    private readonly CircularBuffer<ChatMessage> Messages = new(MAX_MESSAGES);
    private readonly CircularBuffer<OrangeBarMessage> OrangeBarHistory = new(MAX_HISTORY);

    //both HUD layouts build their own ChatInputControl, so anything the player expects to survive a
    //layout swap (or to be shared between the two boxes) has to live out here.
    private readonly List<string> WhisperSenders = [];
    private readonly List<string> WhisperTargets = [];

    /// <summary>
    ///     The players who most recently whispered you, newest first. The reply targets /r cycles through.
    /// </summary>
    public IReadOnlyList<string> RecentWhisperSenders => WhisperSenders;

    /// <summary>
    ///     The players you most recently whispered, newest first. The targets /w cycles through.
    /// </summary>
    public IReadOnlyList<string> RecentWhisperTargets => WhisperTargets;

    /// <summary>
    ///     The channel the chat input reopens in, set by typing a shortcut alone ("/g" then enter) and cleared by "/s".
    /// </summary>
    public ChatMode StickyChannel { get; set; } = ChatMode.Normal;

    /// <summary>
    ///     Adds a chat message (public, whisper, group, guild) with the specified color.
    /// </summary>
    public void AddMessage(string text, Color color)
    {
        var msg = new ChatMessage(text, color);
        Messages.Add(msg);
        MessageAdded?.Invoke(msg);
    }

    /// <summary>
    ///     Adds an orange bar message. Defaults to orange for system/server notifications; callers may pass a custom color
    ///     for whisper/group/guild messages so the Shift+F history and orange bar expand view preserve their chat color.
    /// </summary>
    public void AddOrangeBarMessage(string text, Color? color = null)
    {
        var msg = new OrangeBarMessage(text, color ?? Color.Orange);
        OrangeBarHistory.Add(msg);
        OrangeBarMessageAdded?.Invoke(msg);
    }

    /// <summary>
    ///     Records a player who whispered you, moving them to the front if already known.
    /// </summary>
    public void RecordWhisperFrom(string name) => RecordName(WhisperSenders, name);

    /// <summary>
    ///     Records a player you whispered, moving them to the front if already known.
    /// </summary>
    public void RecordWhisperTo(string name) => RecordName(WhisperTargets, name);

    private static void RecordName(List<string> names, string name)
    {
        names.RemoveAll(existing => existing.EqualsI(name));
        names.Insert(0, name);

        if (names.Count > MAX_WHISPER_NAMES)
            names.RemoveAt(names.Count - 1);
    }

    /// <summary>
    ///     Clears all chat messages, orange bar history, whisper names, and the sticky channel.
    /// </summary>
    public void Clear()
    {
        Messages.Clear();
        OrangeBarHistory.Clear();
        WhisperSenders.Clear();
        WhisperTargets.Clear();
        StickyChannel = ChatMode.Normal;
        Cleared?.Invoke();
    }

    /// <summary>
    ///     Fired when all messages are cleared.
    /// </summary>
    public event ClearedHandler? Cleared;

    /// <summary>
    ///     Returns the orange bar message history as a read-only list. Used by MessageHistoryPanel and OrangeBarControl for
    ///     display.
    /// </summary>
    public IReadOnlyList<OrangeBarMessage> GetOrangeBarHistory() => OrangeBarHistory;

    /// <summary>
    ///     Fired when a chat message is added (public, whisper, group, guild). Carries the message data.
    /// </summary>
    public event ChatMessageAddedHandler? MessageAdded;

    /// <summary>
    ///     Fired when an orange bar message is added (system messages). Carries the message text.
    /// </summary>
    public event OrangeBarMessageAddedHandler? OrangeBarMessageAdded;

    /// <summary>
    ///     A single chat message with text and display color.
    /// </summary>
    public readonly record struct ChatMessage(string Text, Color Color);

    /// <summary>
    ///     A single orange bar entry with text and display color. System/server notifications default to orange; whisper,
    ///     group, and guild messages carry their own chat color.
    /// </summary>
    public readonly record struct OrangeBarMessage(string Text, Color Color);
}