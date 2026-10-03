#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.PumpkinCarving;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>Pumpkin Carving: the carving window and pumpkin looks (spec 2026-10-03-pumpkin-carving-design.md).</summary>
public sealed partial class WorldScreen
{
    //carving window — opened by the server's PumpkinCarvingDisplay Open when a player claims a pumpkin
    private PumpkinCarvingControl PumpkinWindow = null!;

    private void CreatePumpkinCarving()
    {
        PumpkinWindow = new PumpkinCarvingControl(Game.CreatureRenderer)
        {
            ZIndex = 2
        };

        Game.Connection.OnPumpkinCarvingDisplay += HandlePumpkinCarvingDisplay;
        Game.Connection.OnPumpkinLook += HandlePumpkinLook;
        PumpkinWindow.SaveRequested += (grid, closing) => Game.Connection.SendPumpkinCarvingSave(grid, closing);
    }

    private void HandlePumpkinCarvingDisplay(PumpkinCarvingDisplayArgs args)
    {
        if (args.Type == PumpkinCarvingDisplayType.Open)
            PumpkinWindow.Open(args.SecondsLeft, args.Grid ?? PumpkinGrid.Empty());
        else
            PumpkinWindow.OnServerClose();
    }

    private static void HandlePumpkinLook(PumpkinLookArgs args) => WorldState.ApplyPumpkinLook(args.EntityId, args.State, args.Grid);

    private void UnwirePumpkinCarving()
    {
        Game.Connection.OnPumpkinCarvingDisplay -= HandlePumpkinCarvingDisplay;
        Game.Connection.OnPumpkinLook -= HandlePumpkinLook;
    }
}
