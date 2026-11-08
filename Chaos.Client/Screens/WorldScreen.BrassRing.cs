#if DEBUG
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Hud;
using Chaos.Client.Controls.World.Popups.BrassRing;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
#endif

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
#if DEBUG
    private BrassRingControl? BrassRingWindow;

    private void WireBrassRing() => ChatInputControl.BrassRingTestRequested += OpenBrassRingTest;

    private void UnwireBrassRing() => ChatInputControl.BrassRingTestRequested -= OpenBrassRingTest;

    private void OpenBrassRingTest()
    {
        if (Root is null)
            return;

        WorldHud.ChatInput.Unfocus();

        if (BrassRingWindow is null)
        {
            BrassRingWindow = new BrassRingControl(Game.AislingRenderer, Game.SoundSystem) { ZIndex = 2 };
            Root.AddChild(BrassRingWindow);
        }

        var look = ViewerLook();
        var dummyLook = TownImportPreviewLook.Plain(look.Gender == Gender.Male ? Gender.Female : Gender.Male);
        BrassRingWindow.Open(WorldState.GetPlayerEntity()?.Name ?? "You", in look, in dummyLook);
    }
#endif
}
