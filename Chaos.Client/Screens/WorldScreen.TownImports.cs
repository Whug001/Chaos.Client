#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.TownImports;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>Town imports: the mayor's import window and the admins' import window.</summary>
public sealed partial class WorldScreen
{
    //built on first use
    private TownImportAdminControl? TownImportAdmin;
    private TownImportBoardControl? TownImportBoard;

    private void WireTownImports()
    {
        Game.Connection.OnTownImportBoard += HandleTownImportBoard;
        Game.Connection.OnTownImportAdmin += HandleTownImportAdmin;
    }

    private void UnwireTownImports()
    {
        Game.Connection.OnTownImportBoard -= HandleTownImportBoard;
        Game.Connection.OnTownImportAdmin -= HandleTownImportAdmin;

        if (TownImportBoard is not null)
            TownImportBoard.PickRequested -= SendTownImportPick;

        if (TownImportAdmin is not null)
            TownImportAdmin.RequestSent -= SendTownImportAdminRequest;
    }

    /// <summary>The player's look now, for the import previews.</summary>
    private static AislingAppearance ViewerLook()
        => WorldState.GetPlayerEntity()?.Appearance ?? TownImportPreviewLook.Plain(Gender.Male);

    private void HandleTownImportAdmin(TownImportAdminArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (TownImportAdmin is null)
        {
            TownImportAdmin = new TownImportAdminControl(Game.AislingRenderer, ViewerLook)
            {
                ZIndex = 2
            };

            TownImportAdmin.RequestSent += SendTownImportAdminRequest;
            Root.AddChild(TownImportAdmin);
        }

        TownImportAdmin.Apply(args);
    }

    private void HandleTownImportBoard(TownImportBoardArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (TownImportBoard is null)
        {
            TownImportBoard = new TownImportBoardControl(Game.AislingRenderer, ViewerLook)
            {
                ZIndex = 2
            };

            TownImportBoard.PickRequested += SendTownImportPick;
            Root.AddChild(TownImportBoard);
        }

        if (args.Type == TownImportBoardType.Open)
            TownImportBoard.Open(args);
        else
            TownImportBoard.Refresh(args);
    }

    private void SendTownImportAdminRequest(TownImportAdminInteractionArgs args) => Game.Connection.SendTownImportAdminInteraction(args);

    private void SendTownImportPick(TownImportBoardInteractionArgs args) => Game.Connection.SendTownImportBoardInteraction(args);
}
