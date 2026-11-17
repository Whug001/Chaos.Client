#region
using Chaos.Client.Controls.World.Popups.Learn;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     The learn-ability popup. The server sends the requirements dialog first and this packet right after it, so the
///     popup opens over that dialog, hides the dialog entirely, and answers through it. Any new dialog or menu, or the
///     dialog closing, takes the popup down without answering.
/// </summary>
public sealed partial class WorldScreen
{
    //built on first use
    private LearnAbilityControl? LearnAbility;

    private void WireLearnAbility()
    {
        Game.Connection.OnLearnAbilityDisplay += HandleLearnAbilityDisplay;
        NpcSession.OnClose += HideLearnAbility;
    }

    private void UnwireLearnAbility()
    {
        Game.Connection.OnLearnAbilityDisplay -= HandleLearnAbilityDisplay;
        NpcSession.OnClose -= HideLearnAbility;
    }

    private void HandleLearnAbilityDisplay(LearnAbilityDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        //no dialog to answer through: the requirements dialog never arrived or was already closed
        if (!NpcSession.Visible)
            return;

        if (LearnAbility is null)
        {
            LearnAbility = new LearnAbilityControl(NpcSession)
            {
                ZIndex = 2
            };

            Root.AddChild(LearnAbility);
        }

        LearnAbility.Show(args, WorldHud.ViewportBounds);
    }

    /// <summary>Takes the learn-ability popup down without answering, if it is up.</summary>
    private void HideLearnAbility() => LearnAbility?.Hide();
}
