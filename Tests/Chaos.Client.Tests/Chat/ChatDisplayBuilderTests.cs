using Chaos.Client.Chat;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class ChatDisplayBuilderTests
{
    private const string ZORP_ID = "profanity.zorp_001";

    private static FantasyDictionary ZorpDictionary()
        => FantasyDictionary.FromJson("""{"profanity.zorp_001": "dung"}""");

    private static List<ChatTag> ZorpAt(string body)
        => [new(body.IndexOf("zorp", StringComparison.Ordinal), 4, ZORP_ID, 0)];

    [Test]
    public async Task Censored_body_transforms_with_sender_prefix_reattached()
    {
        var body = "hello zorp";

        var result = ChatDisplayBuilder.Build("Bob: ", body, ZorpAt(body), ChatFilterMode.Censored, ZorpDictionary());

        result.DisplayText.Should()
              .Be("Bob: hello ****");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hide_returns_sender_prefixed_marker()
    {
        var body = "hello zorp";

        var result = ChatDisplayBuilder.Build("[Bob]: ", body, ZorpAt(body), ChatFilterMode.Hide, ZorpDictionary());

        result.DisplayText.Should()
              .Be("[Bob]: [Message hidden]");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unfiltered_passes_body_through_with_prefix()
    {
        var body = "hello zorp";

        var result = ChatDisplayBuilder.Build("Bob: ", body, ZorpAt(body), ChatFilterMode.Unfiltered, ZorpDictionary());

        result.DisplayText.Should()
              .Be("Bob: hello zorp");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Bad_span_shows_original_and_logs_once()
    {
        var logs = new List<string>();

        var result = ChatDisplayBuilder.Build(
            "Bob: ",
            "hello",
            [new ChatTag(50, 5, ZORP_ID, 0)],
            ChatFilterMode.Censored,
            ZorpDictionary(),
            logs.Add);

        result.DisplayText.Should()
              .Be("Bob: hello");

        logs.Should()
            .HaveCount(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Mixed_valid_and_bad_spans_transform_valid_and_log_once()
    {
        var body = "zorp ok";
        var logs = new List<string>();
        var tags = new List<ChatTag>
        {
            new(0, 4, ZORP_ID, 0),
            new(50, 5, ZORP_ID, 0),
            new(-3, 2, ZORP_ID, 0)
        };

        var result = ChatDisplayBuilder.Build("Bob: ", body, tags, ChatFilterMode.Censored, ZorpDictionary(), logs.Add);

        result.DisplayText.Should()
              .Be("Bob: **** ok");

        logs.Should()
            .HaveCount(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Missing_or_empty_tags_show_original()
    {
        ChatDisplayBuilder.Build("[Bob]: ", "hello", null, ChatFilterMode.Hide, ZorpDictionary())
                          .DisplayText.Should()
                          .Be("[Bob]: hello");

        ChatDisplayBuilder.Build("[Bob]: ", "hello", [], ChatFilterMode.Censored, ZorpDictionary())
                          .DisplayText.Should()
                          .Be("[Bob]: hello");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Original_text_and_tags_retained_on_result()
    {
        var body = "hello zorp";
        var tags = ZorpAt(body);

        var result = ChatDisplayBuilder.Build("Bob: ", body, tags, ChatFilterMode.Censored, ZorpDictionary());

        result.OriginalText.Should()
              .Be("Bob: hello zorp");

        result.Tags.Should()
              .BeSameAs(tags);

        await Task.CompletedTask;
    }

    [Test]
    public async Task BuildPublic_splits_name_prefix_for_say_and_shout()
    {
        var dictionary = ZorpDictionary();
        var tags = new List<ChatTag>
        {
            new(6, 4, ZORP_ID, 0)
        };

        ChatDisplayBuilder.BuildPublic("Bob: hello zorp", tags, ChatFilterMode.Censored, dictionary)
                          .DisplayText.Should()
                          .Be("Bob: hello ****");

        ChatDisplayBuilder.BuildPublic("Bob! hello zorp", tags, ChatFilterMode.Censored, dictionary)
                          .DisplayText.Should()
                          .Be("Bob! hello ****");

        ChatDisplayBuilder.BuildPublic("Bob: hello zorp", tags, ChatFilterMode.Unfiltered, dictionary)
                          .DisplayText.Should()
                          .Be("Bob: hello zorp");

        await Task.CompletedTask;
    }

    [Test]
    public async Task BuildWhisper_splits_bracket_prefix_for_direct_and_echo()
    {
        var dictionary = ZorpDictionary();
        var tags = new List<ChatTag>
        {
            new(6, 4, ZORP_ID, 0)
        };

        ChatDisplayBuilder.BuildWhisper("[Bob]: hello zorp", tags, ChatFilterMode.Censored, dictionary)
                          .DisplayText.Should()
                          .Be("[Bob]: hello ****");

        ChatDisplayBuilder.BuildWhisper("[Bob]> hello zorp", tags, ChatFilterMode.Censored, dictionary)
                          .DisplayText.Should()
                          .Be("[Bob]> hello ****");

        await Task.CompletedTask;
    }

    [Test]
    public async Task BuildGroupChat_keeps_channel_prefix()
    {
        var dictionary = ZorpDictionary();
        var tags = new List<ChatTag>
        {
            new(6, 4, ZORP_ID, 0)
        };

        ChatDisplayBuilder.BuildGroupChat("[!group] Bob: hello zorp", tags, ChatFilterMode.Censored, dictionary)
                          .DisplayText.Should()
                          .Be("[!group] Bob: hello ****");

        //the color byte after {= is a raw MessageColor value on the wire; 'a' stands in for it here
        ChatDisplayBuilder.BuildGroupChat("{=a[!group] Bob: hello zorp", tags, ChatFilterMode.Censored, dictionary)
                          .DisplayText.Should()
                          .Be("{=a[!group] Bob: hello ****");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Null_inputs_never_throw()
    {
        var result = ChatDisplayBuilder.Build(null, null, null, ChatFilterMode.Censored, null);

        result.DisplayText.Should()
              .BeEmpty();

        result.OriginalText.Should()
              .BeEmpty();

        await Task.CompletedTask;
    }
}
