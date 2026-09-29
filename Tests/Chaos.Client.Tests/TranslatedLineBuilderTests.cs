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
                                    ChatFilterMode.Unfiltered,
                                    FantasyDictionary.Empty)
                                .ChatLog
                                .Should()
                                .Be("[영어] [Bob]: 안녕");
}
