using Chaos.Client.Chat;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class UserOptionsChatFilterTests
{
    [Test]
    public void FreshOptions_DefaultToUnfilteredUnconfigured()
    {
        var options = new UserOptions();

        options.ChoiceValue(SettingKey.ChatFilterMode).Should().Be(0);
        options.ChatFilterMode.Should().Be(ChatFilterMode.Unfiltered);
        options.Value(SettingKey.HasConfiguredChatFilter).Should().BeFalse();
    }

    [Test]
    public void SyncPayloadApplies_ModeAndFlagAndFiresValueChanged()
    {
        //the same seam HandleUserOptions uses: choice index plus flag from the sync payload.
        var options = new UserOptions();
        var fired = new List<(SettingKey Key, bool Value)>();
        options.ValueChanged += (key, value) => fired.Add((key, value));

        var payload = new UserOptionsArgs
        {
            ShowBodyAnimations = true,
            ListenToHitSounds = true,
            PriorityAnimations = true,
            LockGear = false,
            WhisperSound = true,
            AllowExchange = true,
            HideEnemyHealthBars = false,
            AlwaysShowFriendlyNametags = false,
            DamageNumbersMyOutputOnly = false,
            ChatFilterMode = 2,
            HasConfiguredChatFilter = true
        };

        options.ApplyChoice(SettingKey.ChatFilterMode, payload.ChatFilterMode);
        options.Apply(SettingKey.HasConfiguredChatFilter, payload.HasConfiguredChatFilter);

        options.ChatFilterMode.Should().Be(ChatFilterMode.Censored);
        options.Value(SettingKey.HasConfiguredChatFilter).Should().BeTrue();
        fired.Should()
             .Contain((SettingKey.ChatFilterMode, true))
             .And.Contain((SettingKey.HasConfiguredChatFilter, true));
    }

    [Test]
    public void ApplyChoice_IgnoresOutOfRangeIndex()
    {
        var options = new UserOptions();
        var fired = new List<(SettingKey Key, bool Value)>();
        options.ValueChanged += (key, value) => fired.Add((key, value));

        options.ApplyChoice(SettingKey.ChatFilterMode, 1);

        options.ApplyChoice(SettingKey.ChatFilterMode, 99);

        options.ChatFilterMode.Should().Be(ChatFilterMode.Fantasy);
        fired.Should().HaveCount(1);
    }

    [Test]
    public void SelectChoice_RaisesUserChoiceSelectedWithoutTouchingCache()
    {
        //server-controlled choices echo back like toggles do: the cache moves on Apply, not on select.
        var options = new UserOptions();
        var selected = new List<(SettingKey Key, int Index)>();
        options.UserChoiceSelected += (key, index) => selected.Add((key, index));

        options.SelectChoice(SettingKey.ChatFilterMode, 1);

        selected.Should().ContainSingle().Which.Should().Be((SettingKey.ChatFilterMode, 1));
        options.ChatFilterMode.Should().Be(ChatFilterMode.Unfiltered);
    }

    [Test]
    public void ClearServerSettings_ResetsChatFilterDefaults()
    {
        var options = new UserOptions();
        options.ApplyChoice(SettingKey.ChatFilterMode, 3);
        options.Apply(SettingKey.HasConfiguredChatFilter, true);

        options.ClearServerSettings();

        options.ChatFilterMode.Should().Be(ChatFilterMode.Unfiltered);
        options.Value(SettingKey.HasConfiguredChatFilter).Should().BeFalse();
    }

    [Test]
    public void ModeChoices_MatchEnumOrder()
    {
        //the dropdown index IS the mode value: position 0 must read Unfiltered, 1 Fantasy, and so on.
        var def = SettingDefinitions.ByKey(SettingKey.ChatFilterMode);

        def.Choices.Should().HaveCount(4);

        for (var i = 0; i < def.Choices!.Count; i++)
            def.Choices[i].Should().Be(((ChatFilterMode)i).ToString());
    }

    [Test]
    public void ChatFilterKeys_AreServerOptionsWithDocumentedIds()
    {
        //Task 7 contract: ServerOption round-trip on UserOption 7 (mode) and 9 (flag).
        var mode = SettingDefinitions.ByKey(SettingKey.ChatFilterMode);
        var flag = SettingDefinitions.ByKey(SettingKey.HasConfiguredChatFilter);

        mode.Category.Should().Be(SettingCategory.ServerOption);
        mode.UserOption.Should().Be(UserOption.ChatFilterMode);
        ((byte)mode.UserOption!.Value).Should().Be(7);

        flag.Category.Should().Be(SettingCategory.ServerOption);
        flag.UserOption.Should().Be(UserOption.HasConfiguredChatFilter);
        ((byte)flag.UserOption!.Value).Should().Be(9);
    }
}
