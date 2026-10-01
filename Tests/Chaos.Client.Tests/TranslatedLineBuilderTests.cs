#region
using Chaos.Client.Chat;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TranslatedLineBuilderTests
{
    [Test]
    public void Public_KoreanSource()
    {
        var (chatLog, bubble) = TranslatedLineBuilder.Build(
            "Haneul: 안녕",
            "hi",
            [],
            ChatLanguage.Korean,
            ChatLanguage.English,
            ChatFilterMode.Unfiltered,
            FantasyDictionary.Empty);

        chatLog.Should()
               .Be("[Kor] Haneul: hi");

        bubble.Should()
              .Be("Haneul: hi");
    }

    [Test]
    public void Channel_TagGoesAfterColorCode()
    {
        var (chatLog, bubble) = TranslatedLineBuilder.Build(
            "{=c[!global] Bob: hello",
            "안녕",
            [],
            ChatLanguage.English,
            ChatLanguage.Korean,
            ChatFilterMode.Unfiltered,
            FantasyDictionary.Empty);

        chatLog.Should()
               .Be("{=c[영어] [!global] Bob: 안녕");

        bubble.Should()
              .Be("{=c[!global] Bob: 안녕");
    }

    [Test]
    public void Whisper_KeepsBracketPrefix()
        => TranslatedLineBuilder.Build(
                                    "[Bob]: hey",
                                    "안녕",
                                    [],
                                    ChatLanguage.English,
                                    ChatLanguage.Korean,
                                    ChatFilterMode.Unfiltered,
                                    FantasyDictionary.Empty)
                                .ChatLog
                                .Should()
                                .Be("[영어] [Bob]: 안녕");

    //formatter:off
    [Test]
    [Arguments(ChatLanguage.English, ChatLanguage.Korean, "[영어] ")]
    [Arguments(ChatLanguage.Korean, ChatLanguage.English, "[Kor] ")]
    //formatter:on
    public void Tag_NamesSourceLanguage_ForTheReader(ChatLanguage source, ChatLanguage reader, string expected)
        => TranslatedLineBuilder.SourceTag(source, reader).Should().Be(expected);
}
