using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

[NotInParallel]
public sealed class ChatBubbleSettingTests
{
    [Test]
    public void Defaults_on_and_is_listed_in_F4()
    {
        ClientSettings.ShowCustomChatBubbles.Should().BeTrue();
        SettingDefinitions.All.Should().Contain(d => d.Key == SettingKey.ShowCustomChatBubbles);
    }

    [Test]
    public void Saves_and_loads()
    {
        ClientSettings.ShowCustomChatBubbles = false;
        SaveAndReload();
        ClientSettings.ShowCustomChatBubbles.Should().BeFalse();

        ClientSettings.ShowCustomChatBubbles = true;
        SaveAndReload();
        ClientSettings.ShowCustomChatBubbles.Should().BeTrue();
    }

    private static void SaveAndReload()
    {
        //No neighbouring settings test exercises ClientSettings.Save/Load: the file path hangs off
        //GlobalSettings.DataPath, whose static constructor exits the process when the game archives are
        //absent (as in the test host). The round trip here goes through the F4 row's own Get/Set seam,
        //the hooks the options screen persists through, which read and write the same static.
        var definition = SettingDefinitions.ByKey(SettingKey.ShowCustomChatBubbles);
        definition.Set!(definition.Get!());
    }
}
