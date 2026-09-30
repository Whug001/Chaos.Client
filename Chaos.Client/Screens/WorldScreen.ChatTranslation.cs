#region
using Chaos.Client.Chat;
using Chaos.Client.Collections;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     Chat translation: remembers the original wire line behind each incoming chat line id, and when its translation
///     arrives swaps the chat log line and the speaker's bubble and re-shows a copy on the orange bar and message pane.
/// </summary>
public sealed partial class WorldScreen
{
    private const int MAX_PENDING_TRANSLATION_LINES = 256;

    private readonly Queue<uint> PendingTranslationOrder = new();
    private readonly Dictionary<uint, PendingTranslationLine> PendingTranslationLines = [];

    private void WireChatTranslation()
        => Game.Connection.OnChatTranslation += HandleChatTranslation;

    private void UnwireChatTranslation()
    {
        Game.Connection.OnChatTranslation -= HandleChatTranslation;
        PendingTranslationLines.Clear();
        PendingTranslationOrder.Clear();
    }

    private void RememberTranslatableLine(
        uint lineId,
        TranslatedLineKind kind,
        uint sourceId,
        bool isShout,
        Color color,
        string originalLine)
    {
        if (lineId == 0)
            return;

        if (PendingTranslationLines.TryAdd(lineId, new PendingTranslationLine(kind, sourceId, isShout, color, originalLine)))
            PendingTranslationOrder.Enqueue(lineId);
        else
            PendingTranslationLines[lineId] = new PendingTranslationLine(kind, sourceId, isShout, color, originalLine);

        while (PendingTranslationOrder.Count > MAX_PENDING_TRANSLATION_LINES)
            PendingTranslationLines.Remove(PendingTranslationOrder.Dequeue());
    }

    private void HandleChatTranslation(ChatTranslationArgs args)
    {
        if (!PendingTranslationLines.TryGetValue(args.LineId, out var pending))
            return;

        var (chatLog, bubble) = TranslatedLineBuilder.Build(
            pending.OriginalLine,
            args.Text,
            args.Tags,
            args.SourceLanguage,
            WorldState.UserOptions.ChatLanguage,
            WorldState.UserOptions.ChatFilterMode,
            FantasyDictionary.Default);

        //the hover text is the line as the reader saw it, read before the replace; a line that scrolled out of the log
        //has nothing to replace, but its bubble may still be up
        var current = WorldState.Chat.FindMessage(args.LineId);

        if (current is { } message)
            WorldState.Chat.TryReplaceMessage(args.LineId, chatLog, args.Tags, message.Text);

        if (pending.Kind is TranslatedLineKind.Public)
        {
            Overlays.TryReplaceChatBubble(args.LineId, bubble);
            Poker.TryReplaceChatBubble(args.LineId, bubble);
        }

        switch (pending.Kind)
        {
            case TranslatedLineKind.Whisper or TranslatedLineKind.Group or TranslatedLineKind.Guild:
                WorldState.Chat.AddOrangeBarMessage(chatLog, pending.Color);
                SystemMessagePane.AddMessage(chatLog, pending.Color);

                break;

            case TranslatedLineKind.WorldChannel:
                WorldState.Chat.AddOrangeBarMessage(chatLog);

                break;
        }
    }

    private enum TranslatedLineKind
    {
        Public,
        WorldChannel,
        Whisper,
        Group,
        Guild
    }

    private readonly record struct PendingTranslationLine(
        TranslatedLineKind Kind,
        uint SourceId,
        bool IsShout,
        Color Color,
        string OriginalLine);
}
