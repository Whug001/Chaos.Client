using Chaos.Client.Chat;
using Chaos.Client.Controls.Generic;
using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>
///     Task 7 gates: the first-run dialog appears exactly once (stored flag plus a session guard), every
///     mode row maps to its <see cref="ChatFilterMode" /> value, and both prefs live in the Chat section,
///     which F4 Settings draws as a single mode dropdown (the configured flag has no row).
/// </summary>
public class ChatFilterDialogTests
{
    [Test]
    public void UnconfiguredPlayer_SeesDialogOnce()
        => ChatFilterDialog.ShouldShowDialog(hasConfigured: false, shownThisSession: false).Should().BeTrue();

    [Test]
    public void ConfiguredPlayer_NeverSeesDialog()
        => ChatFilterDialog.ShouldShowDialog(hasConfigured: true, shownThisSession: false).Should().BeFalse();

    [Test]
    public void DialogSeenThisSession_NeverReshows()
    {
        ChatFilterDialog.ShouldShowDialog(hasConfigured: false, shownThisSession: true).Should().BeFalse();
        ChatFilterDialog.ShouldShowDialog(hasConfigured: true, shownThisSession: true).Should().BeFalse();
    }

    [Test]
    public void DialogPreselectsFantasy_LeavingStoredDefaultUnfiltered()
    {
        ChatFilterDialog.DefaultSelection.Should().Be(1);
        ((ChatFilterMode)ChatFilterDialog.DefaultSelection).Should().Be(ChatFilterMode.Fantasy);

        //the stored default stays Unfiltered until Continue sends through the Task 6 path.
        new UserOptions().ChatFilterMode.Should().Be(ChatFilterMode.Unfiltered);
    }

    [Test]
    public void EveryModeRow_MapsToItsChatFilterModeValue()
    {
        var choices = SettingDefinitions.ByKey(SettingKey.ChatFilterMode).Choices!;

        choices.Should().HaveCount(4);

        for (var i = 0; i < choices.Count; i++)
            choices[i].Should().Be(((ChatFilterMode)i).ToString());
    }

    [Test]
    public void ChatFilterPrefs_LiveInTheChatSection()
    {
        SettingDefinitions.ByKey(SettingKey.ChatFilterMode).Section.Should().Be(SettingSection.Chat);
        SettingDefinitions.ByKey(SettingKey.HasConfiguredChatFilter).Section.Should().Be(SettingSection.Chat);
    }

    [Test]
    public void SettingsPanel_DrawsTheChatSection()
        => SettingDefinitions.PanelSections.Should().Contain(SettingSection.Chat);

    [Test]
    public void SettingsPanel_ChatSectionIsOnlyTheModeDropdown()
    {
        var rows = SettingDefinitions.PanelRows(SettingSection.Chat).ToList();

        rows.Select(d => d.Key).Should().Equal(SettingKey.ChatFilterMode);
        rows[0].Choices.Should().NotBeNull();
    }

    [Test]
    public void SettingsPanel_DrawsEverySectionThatHasVisibleRows()
    {
        //a section missing from PanelSections would silently vanish from F4, as Chat once did.
        var withRows = Enum.GetValues<SettingSection>()
                           .Where(s => SettingDefinitions.PanelRows(s).Any());

        SettingDefinitions.PanelSections.Should().BeEquivalentTo(withRows);
    }
}
