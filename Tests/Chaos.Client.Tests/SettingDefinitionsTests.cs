using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class SettingDefinitionsTests
{
    /// <summary>
    ///     <c>SettingDefinitions.ByKey</c> is looked up by every toggle, so a key without a definition is a crash on
    ///     click rather than a missing row.
    /// </summary>
    [Test]
    public void EveryKeyHasADefinition()
    {
        var defined = SettingDefinitions.All
                                        .Select(definition => definition.Key)
                                        .ToHashSet();

        Enum.GetValues<SettingKey>()
            .Should()
            .OnlyContain(key => defined.Contains(key));
    }

    [Test]
    public void NoKeyIsDefinedTwice()
        => SettingDefinitions.All
                             .Select(definition => definition.Key)
                             .Should()
                             .OnlyHaveUniqueItems();

    /// <summary>
    ///     A client-local toggle with no hooks would render as a checkbox that reads false and does nothing when
    ///     clicked.
    /// </summary>
    [Test]
    public void EveryClientLocalToggleCanBeReadAndWritten()
        => SettingDefinitions.All
                             .Where(
                                 definition => (definition.Category == SettingCategory.ClientLocal)
                                               && (definition.Choices is null)
                                               && (definition.GetSliderValue is null))
                             .Should()
                             .OnlyContain(definition => (definition.Get != null) && (definition.Set != null));

    /// <summary>A setting gated by another has to be gated by one that exists, or the row never enables.</summary>
    [Test]
    public void EveryGateNamesARealSetting()
    {
        var defined = SettingDefinitions.All
                                        .Select(definition => definition.Key)
                                        .ToHashSet();

        SettingDefinitions.All
                          .Where(definition => definition.GatedBy is not null)
                          .Should()
                          .OnlyContain(definition => defined.Contains(definition.GatedBy!.Value));
    }

    /// <summary>
    ///     The column defaults to a solid plate: it is a readout to be glanced at mid-fight, and solid is what
    ///     keeps it legible over map art. Transparency is the opt-in.
    /// </summary>
    [Test]
    public void GroupPanelsAreSolidUntilTheSettingIsTurnedOn()
    {
        var wasTransparent = ClientSettings.TransparentGroupPanels;

        try
        {
            ClientSettings.TransparentGroupPanels = false;

            var definition = SettingDefinitions.ByKey(SettingKey.TransparentGroupPanel);

            definition.Category.Should().Be(SettingCategory.ClientLocal);
            definition.Section.Should().Be(SettingSection.Display);

            //no point offering to restyle a column that is switched off
            definition.GatedBy.Should().Be(SettingKey.GroupPanel);

            definition.Get!()
                      .Should()
                      .BeFalse();

            definition.Set!(true);

            ClientSettings.TransparentGroupPanels.Should().BeTrue();

            definition.Get!()
                      .Should()
                      .BeTrue();
        } finally
        {
            ClientSettings.TransparentGroupPanels = wasTransparent;
        }
    }

    /// <summary>
    ///     Chat translation setting is in the Chat section right after chat filter with the correct three choices.
    /// </summary>
    [Test]
    public void ChatLanguageIsInChatSectionAfterChatFilter()
    {
        var chatDefinitions = SettingDefinitions.PanelRows(SettingSection.Chat).ToList();

        var filterModeIndex = chatDefinitions.FindIndex(d => d.Key == SettingKey.ChatFilterMode);
        filterModeIndex.Should().BeGreaterThanOrEqualTo(0, "chat filter mode should exist in Chat section");

        var chatLanguageIndex = chatDefinitions.FindIndex(d => d.Key == SettingKey.ChatLanguage);
        chatLanguageIndex.Should().Be(
            filterModeIndex + 1,
            "chat language should be right after chat filter (configured flag is hidden)");

        var chatLanguageDef = SettingDefinitions.ByKey(SettingKey.ChatLanguage);

        chatLanguageDef.Section.Should().Be(SettingSection.Chat);
        chatLanguageDef.Category.Should().Be(SettingCategory.ServerOption);
        chatLanguageDef.Span.Should().Be(SettingSpan.Full);
        chatLanguageDef.Choices.Should().HaveCount(3);
        chatLanguageDef.Choices.Should().Equal("Off", "English", "Korean (한국어)");
    }
}
