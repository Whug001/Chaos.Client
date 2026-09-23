using Chaos.Client.Chat;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class ChatSpanApplierTests
{
    private const string ZORP_ID = "profanity.zorp_001";

    private static FantasyDictionary ZorpDictionary()
        => FantasyDictionary.FromJson("""{"profanity.zorp_001": "dung"}""");

    [Test]
    public async Task Fantasy_substitutes_per_rule_id()
    {
        var body = "hello zorp world";
        var tags = new List<ChatTag>
        {
            new(body.IndexOf("zorp", StringComparison.Ordinal), 4, ZORP_ID, 0)
        };

        ChatSpanApplier.Apply(body, tags, ChatFilterMode.Fantasy, ZorpDictionary())
                       .Should()
                       .Be("hello dung world");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unknown_rule_id_masks_with_punctuation_kept()
    {
        var body = "well, zorp!";
        var tags = new List<ChatTag>
        {
            new(body.IndexOf("zorp", StringComparison.Ordinal), 4, "profanity.unknown_999", 0)
        };

        ChatSpanApplier.Apply(body, tags, ChatFilterMode.Fantasy, ZorpDictionary())
                       .Should()
                       .Be("well, ****!");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Case_copies_the_source()
    {
        var dictionary = ZorpDictionary();

        ChatSpanApplier.Apply(
                "SHIT",
                [new ChatTag(0, 4, ZORP_ID, 0)],
                ChatFilterMode.Fantasy,
                dictionary)
                       .Should()
                       .Be("DUNG");

        ChatSpanApplier.Apply(
                "Shit",
                [new ChatTag(0, 4, ZORP_ID, 0)],
                ChatFilterMode.Fantasy,
                dictionary)
                       .Should()
                       .Be("Dung");

        ChatSpanApplier.Apply(
                "shit",
                [new ChatTag(0, 4, ZORP_ID, 0)],
                ChatFilterMode.Fantasy,
                dictionary)
                       .Should()
                       .Be("dung");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Censored_writes_mask_per_span()
    {
        var body = "zorp and zorp";
        var tags = new List<ChatTag>
        {
            new(0, 4, ZORP_ID, 0),
            new(9, 4, ZORP_ID, 0)
        };

        ChatSpanApplier.Apply(body, tags, ChatFilterMode.Censored, ZorpDictionary())
                       .Should()
                       .Be("**** and ****");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unfiltered_returns_original_untouched()
    {
        var body = "hello zorp world";
        var tags = new List<ChatTag>
        {
            new(6, 4, ZORP_ID, 0)
        };

        ChatSpanApplier.Apply(body, tags, ChatFilterMode.Unfiltered, ZorpDictionary())
                       .Should()
                       .BeSameAs(body);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Empty_tag_list_returns_original_for_every_mode()
    {
        var body = "hello zorp world";
        var empty = Array.Empty<ChatTag>();
        var dictionary = ZorpDictionary();

        foreach (var mode in Enum.GetValues<ChatFilterMode>())
            ChatSpanApplier.Apply(body, empty, mode, dictionary)
                           .Should()
                           .BeSameAs(body);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Spans_apply_end_to_start()
    {
        //tags arrive in ascending order with different-length fantasy replacements; a forward
        //application would shift the second span into the inserted text, so only end-to-start wins
        var dictionary = FantasyDictionary.FromJson(
            """{"profanity.zorp_001": "DUNG BEETLE SOUP", "insult.quux_002": "x"}""");

        var body = "a zorp b quux c";
        var tags = new List<ChatTag>
        {
            new(2, 4, ZORP_ID, 0),
            new(9, 4, "insult.quux_002", 2)
        };

        ChatSpanApplier.Apply(body, tags, ChatFilterMode.Fantasy, dictionary)
                       .Should()
                       .Be("a DUNG BEETLE SOUP b x c");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Overlapping_spans_apply_inner_first()
    {
        var body = "abcdef";
        var tags = new List<ChatTag>
        {
            new(0, 5, ZORP_ID, 0),
            new(1, 4, ZORP_ID, 0)
        };

        //descending start: (1,4) masks first -> "a****f", then (0,5) -> "****f"
        ChatSpanApplier.Apply(body, tags, ChatFilterMode.Censored, ZorpDictionary())
                       .Should()
                       .Be("****f");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Punctuation_spaces_emotes_links_and_refs_pass_through()
    {
        var body = "hey 🙂 see https://example.com/x?a=1 zorp, ok? [item:42]";
        var tags = new List<ChatTag>
        {
            new(body.IndexOf("zorp", StringComparison.Ordinal), 4, ZORP_ID, 0)
        };

        ChatSpanApplier.Apply(body, tags, ChatFilterMode.Fantasy, ZorpDictionary())
                       .Should()
                       .Be("hey 🙂 see https://example.com/x?a=1 dung, ok? [item:42]");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hide_returns_marker_when_tags_present()
    {
        var body = "hello zorp world";
        var tags = new List<ChatTag>
        {
            new(6, 4, ZORP_ID, 0)
        };

        //the sender prefix is reattached by the caller (Task 5); the applier only swaps the body
        ChatSpanApplier.Apply(body, tags, ChatFilterMode.Hide, ZorpDictionary())
                       .Should()
                       .Be("[Message hidden]");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Duplicate_ids_in_file_mask()
    {
        var logs = new List<string>();
        var dictionary = FantasyDictionary.FromJson(
            """{"profanity.zorp_001": "dung", "profanity.zorp_001": "muck"}""",
            logs.Add);

        dictionary.TryGetSubstitution(ZORP_ID, out _)
                  .Should()
                  .BeFalse();

        logs.Should().NotBeEmpty();

        var body = "hello zorp world";
        var tags = new List<ChatTag>
        {
            new(6, 4, ZORP_ID, 0)
        };

        ChatSpanApplier.Apply(body, tags, ChatFilterMode.Fantasy, dictionary)
                       .Should()
                       .Be("hello **** world");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Malformed_file_keeps_chat_usable()
    {
        var logs = new List<string>();

        var act = () => FantasyDictionary.FromJson("{not json", logs.Add);

        act.Should().NotThrow();

        var dictionary = act();

        dictionary.Count.Should().Be(0);
        logs.Should().NotBeEmpty();

        //unknown-everything still renders a masked message instead of throwing or leaking text
        var tags = new List<ChatTag>
        {
            new(6, 4, ZORP_ID, 0)
        };

        ChatSpanApplier.Apply("hello zorp world", tags, ChatFilterMode.Fantasy, dictionary)
                       .Should()
                       .Be("hello **** world");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Default_dictionary_loads_the_seed_once()
    {
        var first = FantasyDictionary.Default;
        var second = FantasyDictionary.Default;

        first.Should().NotBeNull();
        ReferenceEquals(first, second).Should().BeTrue();

        first.Count.Should().BeGreaterThanOrEqualTo(13);
        first.TryGetSubstitution("profanity.oath_001", out var substitution)
             .Should()
             .BeTrue();
        substitution.Should().Be("dung");

        await Task.CompletedTask;
    }
}
