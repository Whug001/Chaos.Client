using Chaos.Client.Chat;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;
using ChatModel = Chaos.Client.ViewModel.Chat;

namespace Chaos.Client.Tests;

public class ChatFilterReapplyTests
{
    private const string ZORP_ID = "profanity.zorp_001";

    private static FantasyDictionary ZorpDictionary()
        => FantasyDictionary.FromJson("""{"profanity.zorp_001": "dung"}""");

    private static List<ChatTag> ZorpAt(string body)
        => [new(body.IndexOf("zorp", StringComparison.Ordinal), 4, ZORP_ID, 0)];

    [Test]
    public async Task Rebuild_renders_the_kept_original_under_the_new_mode()
    {
        var original = "Bob: hello zorp";

        ChatDisplayBuilder.Rebuild(original, ZorpAt("hello zorp"), ChatFilterMode.Censored, ZorpDictionary())
                          .Should()
                          .Be("Bob: hello ****");
        ChatDisplayBuilder.Rebuild(original, ZorpAt("hello zorp"), ChatFilterMode.Hide, ZorpDictionary())
                          .Should()
                          .Be("Bob: [Message contains profanity and was removed.]");
        ChatDisplayBuilder.Rebuild(original, ZorpAt("hello zorp"), ChatFilterMode.Unfiltered, ZorpDictionary())
                          .Should()
                          .Be(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ReapplyFilter_rerenders_chat_and_orange_bar_and_fires_once()
    {
        var chat = new ChatModel();
        var body = "hello zorp";
        var stored = ChatDisplayBuilder.BuildPublic($"Bob: {body}", ZorpAt(body), ChatFilterMode.Censored, ZorpDictionary());
        chat.AddMessage(stored.DisplayText, Color.White, stored.OriginalText, stored.Tags);

        var orange = ChatDisplayBuilder.BuildOrangeBar($"[!world] Dose: {body}", ZorpAt(body), ChatFilterMode.Censored, ZorpDictionary());
        chat.AddOrangeBarMessage(orange.DisplayText, null, orange.OriginalText, orange.Tags);

        var fired = 0;
        chat.HistoryReRendered += () => fired++;

        chat.ReapplyFilter(ChatFilterMode.Hide, ZorpDictionary());

        chat.GetMessages().Should().ContainSingle().Which.Text.Should().Be("Bob: [Message contains profanity and was removed.]");
        chat.GetOrangeBarHistory().Should().ContainSingle().Which.Text.Should().Be("[!world] Dose: [Message contains profanity and was removed.]");
        fired.Should().Be(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ReapplyFilter_leaves_translated_lines_alone()
    {
        //a translation's tags index the translated body, not the kept original: rebuilding it from the
        //original would un-translate the line, so it keeps its display under the new mode.
        var chat = new ChatModel();
        chat.AddMessage("Haneul: hello zorp", Color.White, "Haneul: hello zorp", ZorpAt("hello zorp"), lineId: 5);
        chat.TryReplaceMessage(5, "[Kor] Haneul: hi", ZorpAt("hi"), "Haneul: hello zorp").Should().BeTrue();

        chat.ReapplyFilter(ChatFilterMode.Hide, ZorpDictionary());

        chat.FindMessage(5)!.Value.Text.Should().Be("[Kor] Haneul: hi");

        await Task.CompletedTask;
    }

    [Test]
    public async Task ReapplyFilter_with_nothing_to_change_fires_no_event()
    {
        var chat = new ChatModel();
        chat.AddMessage("You feel stronger: +1 Str!", Color.Orange);
        chat.AddOrangeBarMessage("You feel stronger: +1 Str!");

        var fired = 0;
        chat.HistoryReRendered += () => fired++;

        chat.ReapplyFilter(ChatFilterMode.Hide, ZorpDictionary());

        fired.Should().Be(0);
        chat.GetMessages().Should().ContainSingle().Which.Text.Should().Be("You feel stronger: +1 Str!");

        await Task.CompletedTask;
    }
}
