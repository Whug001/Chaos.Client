#if DEBUG
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Hud;
using Chaos.Client.Controls.World.Popups.CannonFair;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
#endif

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
#if DEBUG
    private CannonFairControl? CannonFairWindow;

    private void WireCannonFair() => ChatInputControl.CannonFairTestRequested += OpenCannonFairTest;

    private void UnwireCannonFair() => ChatInputControl.CannonFairTestRequested -= OpenCannonFairTest;

    private void OpenCannonFairTest()
    {
        if (Root is null)
            return;

        WorldHud.ChatInput.Unfocus();

        if (CannonFairWindow is null)
        {
            CannonFairWindow = new CannonFairControl(Game.AislingRenderer) { ZIndex = 2 };
            Root.AddChild(CannonFairWindow);
        }

        var look = ViewerLook();
        var botLook = TownImportPreviewLook.Plain(look.Gender == Gender.Male ? Gender.Female : Gender.Male);
        CannonFairWindow.Open(in look, in botLook);
    }
#endif
}
